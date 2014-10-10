namespace lnav
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;

    public sealed partial class Form1 : Form
    {
        readonly string _root;
        FileSystemWatcher _fileWatcher;

        // this is called on cmd when selecting a node
        const string LoadTarget = @"C:\Program Files (x86)\Vim\vim74\gvim.exe";
        const string LoadArgs = "--remote-tab-silent \"{0}\"";

        // How far down the tree should go before stopping
        const int MaximumDepth = 5;

        static readonly object Lock = new object();

        readonly RateLimit UpdateTree;

        public Form1()
        {
            InitializeComponent();

            _root = Directory.GetCurrentDirectory();
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
            UpdateTree.Trigger();
        }

        private void FormKeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;

            searchPreview.Text += e.KeyChar;
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
                Clipboard.SetText(tree.SelectedNode.FullPath);
            }
            if (keyData == (Keys.Control | Keys.V))
            {
                searchPreview.Text = Clipboard.GetText();
                HilightNextMatch(tree, searchPreview.Text);
            }
            var baseResult = base.ProcessCmdKey(ref msg, keyData);
            return baseResult;
        }

        private void FormKeyDown(object sender, KeyEventArgs e)
        {
            tree.Focus();
            switch (e.KeyCode)
            {
                case Keys.Back:
                case Keys.Delete:
                    SearchBackspace();
                    break;
                case Keys.Escape:
                    searchPreview.Text = "";
                    break;

                case Keys.Tab:
                    if (e.Shift) // collapse all nodes
                    {
                        tree.CollapseAll();
                    }
                    else // Hunt for next match
                    {
                        HilightNextMatch(tree, searchPreview.Text);
                    }
                    break;

                case Keys.Return: // load the selected file
                    TriggerOpenSelectedNode(makeNew: e.Shift);
                    break;

            }
        }

        void TriggerOpenSelectedNode(bool makeNew)
        {
            if (tree.SelectedNode != null)
            {
                if (makeNew) // edit new file: add search name to path
                {
                    var t1 = Path.Combine(_root, tree.SelectedNode.FullPath);
                    if (File.Exists(t1))
                    {
                        t1 = Path.Combine(_root, Path.GetDirectoryName(tree.SelectedNode.FullPath) ?? "");
                    }
                    LoadFile(Path.Combine(t1, searchPreview.Text.Trim()));
                }
                else // edit existing file.
                {
                    var target = Path.Combine(_root, tree.SelectedNode.FullPath);
                    if (File.Exists(target))
                    {
                        LoadFile(target);
                    }
                }
            }
        }

        static void HilightNextMatch(TreeView treeView, string pattern)
        {
            var root = treeView.Nodes[0];

            var target = treeView.SelectedNode;
            var original = treeView.SelectedNode;
            TreeNode newTarget = null;

            while (newTarget == null)
            {
                newTarget = FindRecursive(target, original, pattern.ToLower());
                if (newTarget != null) { continue; }
                Application.DoEvents();
                target = WalkParentNext(target);
                if (target != null) { continue; }
                if (original != root)
                {
                    // no matches from selected, start again at top
                    newTarget = FindRecursive(root, null, pattern.ToLower());
                    if (newTarget == null) return;
                }
                else
                {
                    return; // no matches
                }
            }

            treeView.SelectedNode = newTarget;
        }

        static TreeNode FindRecursive(TreeNode target, TreeNode ignore, string pattern)
        {
            if (target == null) return null;
            if (target != ignore && target.Text.ToLower().Contains(pattern)) return target;
            foreach (TreeNode n in target.Nodes) {
                var maybe = FindRecursive(n, ignore, pattern);
                if (maybe != null) return maybe;
            }
            if (target.NextNode != null)
            {
                var next = FindRecursive(target.NextNode, null, pattern);
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

        static void LoadFile(string fullPath)
        {
            var wd = Path.GetDirectoryName(fullPath) ?? "\\";
            var fileName = Path.GetFileName(fullPath);
            Process.Start(new ProcessStartInfo {
                FileName = LoadTarget,
                Arguments = string.Format(LoadArgs, fileName),
                WorkingDirectory = wd,
                UseShellExecute = true
            });
        }

        private static void ListDirectory(TreeView treeView, string path)
        {
            treeView.Nodes.Clear();
            var rootDirectoryInfo = new DirectoryInfo(path);
            var nodes = CreateDirectoryNode(rootDirectoryInfo, 0);

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

                foreach (var directory in directories)
                    directoryNode.Nodes.Add(CreateDirectoryNode(directory, depth + 1));

                var files = StackFilter(directoryInfo.GetFiles().Select(f => f.Name).ToArray());
                foreach (var file in files) directoryNode.Nodes.Add(new TreeNode(file));
            }
            catch (DirectoryNotFoundException)
            {
                // happens when a temp directory is created and deleted while we are refreshing.
                // Ignore
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
    }
}
