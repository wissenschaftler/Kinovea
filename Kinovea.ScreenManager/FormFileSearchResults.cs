using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kinovea.ScreenManager.Languages;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Simple picker when a keyword search returns multiple video files.
    /// </summary>
    public class FormFileSearchResults : Form
    {
        public string SelectedPath { get; private set; }

        private readonly ListBox listBox = new ListBox();
        private readonly Button btnOk = new Button();
        private readonly Button btnCancel = new Button();

        public FormFileSearchResults(string query, IList<string> matches)
        {
            Text = string.IsNullOrEmpty(query)
                ? ScreenManagerLang.FileSearch_Title
                : string.Format(ScreenManagerLang.FileSearch_ResultsTitle, query);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 320);

            listBox.IntegralHeight = false;
            listBox.Dock = DockStyle.Fill;
            listBox.HorizontalScrollbar = true;
            foreach (string match in matches)
                listBox.Items.Add(match);
            if (listBox.Items.Count > 0)
                listBox.SelectedIndex = 0;
            listBox.DoubleClick += (s, e) => AcceptSelection();

            btnOk.Text = ScreenManagerLang.Generic_Open;
            btnOk.DialogResult = DialogResult.None;
            btnOk.Click += (s, e) => AcceptSelection();
            btnOk.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

            btnCancel.Text = ScreenManagerLang.Generic_Cancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

            Panel buttons = new Panel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 40;
            buttons.Padding = new Padding(8);
            btnCancel.Size = new Size(80, 24);
            btnOk.Size = new Size(80, 24);
            btnCancel.Location = new Point(ClientSize.Width - 96, 8);
            btnOk.Location = new Point(ClientSize.Width - 184, 8);
            buttons.Controls.Add(btnOk);
            buttons.Controls.Add(btnCancel);

            Controls.Add(listBox);
            Controls.Add(buttons);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void AcceptSelection()
        {
            if (listBox.SelectedItem == null)
                return;

            SelectedPath = listBox.SelectedItem.ToString();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
