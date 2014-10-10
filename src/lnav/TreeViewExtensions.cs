namespace lnav
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Forms;

    public class TreeViewState {
        public List<string> Expansions { get; set; }
        public string SelectedFullPath { get; set; }
    }

    public static class TreeViewExtensions
    {
        public static TreeViewState GetState(this TreeView tree)
        {
            return new TreeViewState
            {
                Expansions = tree.Nodes.Descendants()
                            .Where(n => n.IsExpanded)
                            .Select(n => n.FullPath)
                            .ToList(),
                SelectedFullPath = GetNodePath(tree.SelectedNode)
            };
        }

        static string GetNodePath(TreeNode n) { return n == null ? null : n.FullPath; }

        public static void SetState(this TreeView tree, TreeViewState savedState)
        {
            var expandedSet = new HashSet<string>(savedState.Expansions);
            foreach (var node in tree.Nodes.Descendants())
            {
                if (expandedSet.Contains(node.FullPath)) node.Expand();
                if (node.FullPath == savedState.SelectedFullPath)
                {
                    tree.SelectedNode = node;
                }
            }
        }

        public static IEnumerable<TreeNode> Descendants(this TreeNodeCollection c)
        {
            foreach (var node in c.OfType<TreeNode>())
            {
                yield return node;

                foreach (var child in node.Nodes.Descendants())
                {
                    yield return child;
                }
            }
        }
    }
}