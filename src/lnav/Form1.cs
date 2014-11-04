namespace lnav
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public sealed partial class Form1 : Form
    {
        /// <summary> The full file path where the navigator is rooted </summary>
        readonly string _root;
        FileSystemWatcher _fileWatcher;
        CancellationTokenSource _searchCancel;

        // Position in file that a match was found
        Point? lastGrepPosition;

        // this is called on cmd when selecting a node
        const string FileLoadTarget = @"C:\Program Files (x86)\Vim\vim74\gvim.exe";
        const string FileLoadArgs = "--remote-tab-silent \"+call cursor({1},{2})\" \"{0}\""; // 0: filename, 1: row(1-based), 2: col(1-based)

        // How far down the tree should go before stopping
        const int MaximumDepth = 5;

        static readonly object Lock = new object();

        readonly RateLimit UpdateTree;

        public Form1()
        {
            InitializeComponent();
            _searchCancel = new CancellationTokenSource();

            _root = Directory.GetCurrentDirectory();
            if (_root.Substring(1).StartsWith(":\\Windows")) {
                Environment.Exit(1);
            }
            Text = _root;

            UpdateTree = new RateLimit(TimeSpan.FromSeconds(1), () => InvokeDelegate(() =>
            {
                lock (Lock)
                {
                    tree.BeginUpdate();
                    var state = tree.GetState();

                    ListDirectory(tree, _root);

                    tree.SetState(state);
                    tree.EndUpdate();
                }
            }));

            UpdateTree.Immediate();
            AddWatcher();
        }

        void InvokeDelegate(Action act)
        {
            if (InvokeRequired) { Invoke(act); } else { act(); }
        }

        void AddWatcher()
        {
            _fileWatcher = new FileSystemWatcher(_root) { 
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _fileWatcher.Created += RebuildTree;
            _fileWatcher.Deleted += RebuildTree;
        }

        void RebuildTree(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted || e.ChangeType == WatcherChangeTypes.Changed)
            {
                if (tree.Nodes.Find(e.Name, true).Length < 1) return; // change to a node we have hidden
            }
            UpdateTree.Trigger();
        }

        private void FormKeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;

            searchPreview.Insert(e.KeyChar);
            searchPreview.Focus();
            e.Handled = true;
        }

        private void FormKeyDown(object sender, KeyEventArgs e)
        {
            var focusTree = true;
            switch (e.KeyCode)
            {
                case Keys.Back:
                case Keys.Delete:
                    if (!searchPreview.Focused)
                    {
                        searchPreview.Backspace();
                    }
                    searchPreview.Focus();
                    e.Handled = false; // prevent keystrokes going to the tree
                    focusTree = false;
                    break;
                case Keys.Escape:
                    e.Handled = true;
                    searchPreview.Text = "";
                    _searchCancel.Cancel();
                    break;

                case Keys.Tab:
                    e.Handled = true;
                    if (e.Shift) // collapse all nodes
                    {
                        tree.CollapseAll();
                    }
                    else // Hunt for next match
                    {
                        ShowSearching();
                        HilightNextFileNameMatch(tree, searchPreview.Text);
                        DoneSearching();
                    }
                    break;

                case Keys.Return: // load the selected file
                    e.Handled = true;
                    lastGrepPosition = null;
                    TriggerOpenSelectedNode(makeNew: e.Shift);
                    break;

                case Keys.Left:
                case Keys.Right:
                case Keys.ShiftKey:
                    focusTree = false; // ok if it's already focused. Otherwise leave it
                    break;
            }
            if (focusTree) tree.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Tab || keyData == (Keys.Tab | Keys.Shift))
            {
                FormKeyDown(this, new KeyEventArgs(keyData));
                return true;
            }
            if (keyData == (Keys.Control | Keys.C))
            {
                if (tree.SelectedNode != null && !string.IsNullOrEmpty(tree.SelectedNode.FullPath))
                {
                    Clipboard.SetText(tree.SelectedNode.FullPath);
                }
                return true;
            }
            if (keyData == (Keys.Control | Keys.V))
            {
                searchPreview.Text = Clipboard.GetText();
                HilightNextFileNameMatch(tree, searchPreview.Text);
                return true;
            }
            if (keyData == (Keys.Control | Keys.G))
            {
                // check regex
                var pattern = searchPreview.Text;
                HilightNextRegexContentMatch(pattern);
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.G))
            {
                TriggerOpenSelectedNode(makeNew: false);
                return true;
            }
            var baseResult = base.ProcessCmdKey(ref msg, keyData);
            return baseResult;
        }

        void DoneSearching()
        {
            searchPreview.BackColor = Color.White;
            searchPreview.Refresh();
        }

        void ShowSearching()
        {
            searchPreview.BackColor = Color.CornflowerBlue;
            searchPreview.Refresh();
        }


        void TriggerOpenSelectedNode(bool makeNew)
        {
            if (tree.SelectedNode == null) { return; }

            var targetPath = tree.SelectedNode.FullPath;

            if (Directory.Exists(targetPath) && !makeNew) OpenDirectoryInExplorer(targetPath);
            else OpenFilePathInEditor(makeNew, targetPath);
        }

        void OpenDirectoryInExplorer(string targetPath)
        {
            Process.Start("explorer.exe", Path.Combine(_root, targetPath));
        }

        void OpenFilePathInEditor(bool makeNew, string targetPath)
        {
            if (makeNew) // edit new file: add search name to path
            {
                var t1 = Path.Combine(_root, targetPath);
                if (File.Exists(t1))
                {
                    t1 = Path.Combine(_root, Path.GetDirectoryName(targetPath) ?? "");
                }
                LoadFile(Path.Combine(t1, SearchFileName()), SearchPosition());
            }
            else // edit existing file.
            {
                var target = Path.Combine(_root, targetPath);
                if (File.Exists(target))
                {
                    LoadFile(target, SearchPosition());
                }
            }
        }

        /// <summary>
        /// Strip ":row:col" from a filename
        /// </summary>
        /// <returns></returns>
        string SearchFileName()
        {
            if (string.IsNullOrWhiteSpace(searchPreview.Text)) return "";
            return searchPreview.Text.Trim().Split(':')[0];
        }

        /// <summary>
        /// Read row and column of a search like "filename:row:col"
        /// </summary>
        Point SearchPosition()
        {
            if (lastGrepPosition != null) return lastGrepPosition.Value;
            if (string.IsNullOrWhiteSpace(searchPreview.Text)) return new Point(1, 1);
            var bits = searchPreview.Text.Trim().Split(':');
            switch (bits.Length)
            {
                case 3:
                    return new Point(PosOrDefault(bits[2]), PosOrDefault(bits[1]));
                case 2:
                    return new Point(1, PosOrDefault(bits[1]));
                default:
                    return new Point(1, 1);
            }
        }

        static int PosOrDefault(string p)
        {
            int r;
            if (int.TryParse(p, out r)) return r;
            return 1;
        }

        async void HilightNextRegexContentMatch(string pattern)
        {
            ShowSearching();
            if (Grep.IsValid(pattern))
            {
                var prevNode = tree.SelectedNode;
                var tn = await FindNextMatch(tree, n =>
                {
                    lastGrepPosition = Grep.FileContainsPattern(Path.Combine(_root, n.FullPath), pattern);
                    return lastGrepPosition != null;
                });
                tree.SelectedNode = tn ?? prevNode;
                DoneSearching();
            }
        }

        async void HilightNextFileNameMatch(TreeView treeView, string pattern)
        {
            var lcasePattern = pattern.ToLower().Replace("/", "\\");
            Func<TreeNode,bool> match;
            if (pattern.Contains("\\")) match = target => target.FullPath.ToLower().Contains(lcasePattern); // match paths and path fragments
            else match = target => target.Text.ToLower().Contains(lcasePattern); // match file and folder names, but not files based on folder names

            var newNode = await FindNextMatch(treeView, match);
            treeView.SelectedNode = newNode;
        }

        Task<TreeNode> FindNextMatch(TreeView treeView, Func<TreeNode,bool> match)
        {
            CancellationToken token;
            lock (Lock) {
                _searchCancel.Dispose();
                _searchCancel = new CancellationTokenSource();
                token = _searchCancel.Token;
            }
            var root = treeView.Nodes[0];

            var target = treeView.SelectedNode;
            var original = treeView.SelectedNode;

            return Task.Run(() =>
            {
                TreeNode newTarget = null;

                while (newTarget == null &&  (! token.IsCancellationRequested))
                {
                    newTarget = FindRecursive(target, original, match, token);
                    if (newTarget != null) { continue; }
                    Application.DoEvents();
                    target = WalkParentNext(target);
                    if (target != null) { continue; }
                    if (original != root)
                    {
                        // no matches from selected, start again at top
                        newTarget = FindRecursive(root, null, match, token);
                        if (newTarget == null) return null;
                    }
                    else
                    {
                        return null; // no matches
                    }
                }
                return newTarget;
            }, token);
        }

        static TreeNode FindRecursive(TreeNode target, TreeNode ignore, Func<TreeNode, bool> match, CancellationToken token)
        {
            if (target == null) return null;
            if (token.IsCancellationRequested) return null;
            
            if (target != ignore && match(target)) return target;

            foreach (TreeNode n in target.Nodes)
            {
                if (token.IsCancellationRequested) return null;
                var maybe = FindRecursive(n, ignore, match, token);
                if (maybe != null) return maybe;
            }
            if (target.NextNode != null)
            {
                var next = FindRecursive(target.NextNode, null, match, token);
                if (next != null) return next;
            }
            return null;
        }

        static TreeNode WalkParentNext(TreeNode target)
        {
            if (target == null) return null;
            do
            {
                target = target.Parent;
                if (target == null) return null;
            } while (target.NextNode == null);
            return target.NextNode;
        }

        void SearchBackspace()
        {
            if (searchPreview.Text.Length < 1) return;
            searchPreview.Text = searchPreview.Text.Substring(0, searchPreview.Text.Length - 1);
        }

        static void LoadFile(string fullPath, Point filePosition)
        {
            var wd = Path.GetDirectoryName(fullPath) ?? "\\";
            var fileName = Path.GetFileName(fullPath);
            Process.Start(new ProcessStartInfo {
                FileName = FileLoadTarget,
                Arguments = string.Format(FileLoadArgs, fileName, filePosition.Y, filePosition.X),
                WorkingDirectory = wd,
                UseShellExecute = true
            });
        }

        private static void ListDirectory(TreeView treeView, string path)
        {
            var rootDirectoryInfo = new DirectoryInfo(path);
            var nodes = CreateDirectoryNode(rootDirectoryInfo, 0);

            treeView.Nodes.Clear();
            foreach (TreeNode node in nodes.Nodes)
            {
                treeView.Nodes.Add(node);
            }
        }

        private static TreeNode CreateDirectoryNode(DirectoryInfo directoryInfo, int depth)
        {
            if (depth > MaximumDepth) return new TreeNode("...");

            var directoryNode = new TreeNode(directoryInfo.Name);

            try
            {
                var directories = DirectoryFilter(directoryInfo.GetDirectories());
                directoryNode.ForeColor = Color.SlateBlue;

                foreach (var directory in directories)
                    directoryNode.Nodes.Add(CreateDirectoryNode(directory, depth + 1));

                var files = StackFilter(directoryInfo.GetFiles().Select(f => f.Name).ToArray());
                foreach (var file in files)
                {
                    directoryNode.Nodes.Add(new TreeNode(file));
                }
            }
            catch (DirectoryNotFoundException) {
                // happens when a temp directory is created and deleted while we are refreshing.
                // Ignore
            }
            catch (UnauthorizedAccessException)
            {
                // add a dummy node?
                directoryNode.Text += " (Access Denied)";
                directoryNode.ForeColor = Color.Gray;
            }

            return directoryNode;
        }

        /// <summary>
        /// Exclude directories from recursion.
        /// </summary>
        static IEnumerable<DirectoryInfo> DirectoryFilter(IEnumerable<DirectoryInfo> getDirectories)
        {
            // TODO: match against `.gitignore` files
            return getDirectories.Where(d => 
                d.Name != "node_modules" 
                && d.Name != ".git" 
                && d.Name != ".idea"
                && d.Name != ".tscache");
        }

        /// <summary>
        /// Filter out generated files. Currently only supports ts->js->js.map
        /// </summary>
        static IEnumerable<string> StackFilter(string[] files)
        {
            var output = new List<string>();
            foreach (var file in files)
            {
                var add = true;
                if (file.EndsWith(".js.map", StringComparison.Ordinal))
                {
                    if (files.Contains(file.Replace(".js.map", ".js"))) add = false;
                }
                else if (file.EndsWith(".js", StringComparison.Ordinal))
                {
                    if (files.Contains(file.Replace(".js", ".ts"))) add = false;
                }
                else if (file.EndsWith(".d.ts", StringComparison.Ordinal))
                {
                    if (files.Contains(file.Replace(".d.ts", ".ts"))) add = false;
                }
                if (add) output.Add(file);
            }
            return output;
        }

        private void tree_DoubleClick(object sender, EventArgs e)
        {
            TriggerOpenSelectedNode(makeNew: false);
        }

        private void tree_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) // put relative path between selected and right-clicked into clipboard
            {
                var targ = tree.GetNodeAt(e.Location);
                var selected = tree.SelectedNode;
                if (targ == null || selected == null) return;

                var from = new FilePath(Path.Combine(_root, selected.FullPath));
                var to = new FilePath(Path.Combine(_root, targ.FullPath));

                var path = to.RelativeTo(from).ToEnvironmentalPath();

                Clipboard.SetText(path);
            }
        }
       
        readonly StringBuilder goToMessage = new StringBuilder();

        /// <summary>
        /// Receive 'go to' messages from command line invocation.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case Program.GoToMessage:
                    var val = m.LParam.ToInt32();
                    if (val == 0)
                    {
                        var finalMsg = goToMessage.ToString();
                        goToMessage.Clear();
                        AsyncSearch(finalMsg);
                    }
                    else
                    {
                        goToMessage.Append((char)val);
                    }
                    break;
                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        void AsyncSearch(string finalMsg)
        {
            // we might get a full path from outside, or a relative one, or a full file path.
            // as it's a bit of a mess, we should do something clever here. For now we will do
            // the stupid thing and just take the last path element:
            var searchTerm = Path.GetFileName(finalMsg);

            new Thread(() => InvokeDelegate(() =>
            {
                searchPreview.Text = searchTerm;
                HilightNextFileNameMatch(tree, searchTerm);

                // Try to become the front window -- may need to retry as helper may be switching focus about
                for (int i = 0; i < 10; i++)
                {
                    Win32.SetForegroundWindow(Handle);
                    Win32.BringWindowToTop(Handle);
                    Thread.Sleep(100); // give caller time to exit
                    if (Win32.GetForegroundWindow() == Handle) break;
                }
            })).Start();
        }

        private void tree_KeyPress(object sender, KeyPressEventArgs e)
        {
            // don't change selection until tab is pressed
            FormKeyPress(sender, e);    
        }
    }
}
