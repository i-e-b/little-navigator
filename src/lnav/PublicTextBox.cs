namespace lnav
{
    using System.Globalization;
    using System.Windows.Forms;

    /// <summary>
    /// A TextBox control that exposes editing methods
    /// </summary>
    public class PublicTextBox:TextBox
    {
        int LastSelectionStart = 0;
        int LastSelectionLength = 0;

        // Insert a character in the current location
        public void Insert(char keyChar)
        {
            RestoreSelection();
            if (SelectionLength > 0)
            {
                Text = Text.Remove(SelectionStart, SelectionLength);
            }
            Text = Text.Insert(SelectionStart, keyChar.ToString(CultureInfo.InvariantCulture));
            SelectionStart = LastSelectionStart + 1;
            SelectionLength = 0;
            SaveSelection();
        }

        // Backspace at the current location
        public void Backspace()
        {
            RestoreSelection();
            if (SelectionLength > 0)
            {
                Text = Text.Remove(SelectionStart, SelectionLength);
            }
            else if (SelectionStart > 0)
            {
                Text = Text.Remove(SelectionStart - 1, 1);
                SelectionStart = LastSelectionStart - 1;
            }
            SelectionLength = 0;
            SaveSelection();
        }

        void RestoreSelection()
        {
            if (SelectionLength <= 0) SelectionLength = LastSelectionLength;
            if (SelectionStart <= 0) SelectionStart = LastSelectionStart;

            LastSelectionStart = SelectionStart;
            LastSelectionLength = SelectionLength;
        }

        void SaveSelection()
        {
            LastSelectionStart = SelectionStart;
            LastSelectionLength = SelectionLength;
        }
    }
}