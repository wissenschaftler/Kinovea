using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Resizable popup list for the per-screen file search box.
    /// Shows full paths with horizontal and vertical scrollbars.
    /// </summary>
    public class FileSearchDropDown : ToolStripDropDown
    {
        public event EventHandler<EventArgs<string>> ItemChosen;

        private static Size rememberedSize = new Size(420, 280);

        private readonly ListBox listBox = new ListBox();
        private readonly Panel host = new Panel();
        private readonly Label grip = new Label();
        private bool resizing;
        private Point resizeStart;
        private Size sizeStart;

        public FileSearchDropDown()
        {
            AutoClose = true;
            AutoSize = false;
            Padding = Padding.Empty;
            Margin = Padding.Empty;

            host.Size = rememberedSize;
            host.MinimumSize = new Size(240, 120);
            host.BackColor = SystemColors.Window;

            listBox.BorderStyle = BorderStyle.None;
            listBox.IntegralHeight = false;
            listBox.HorizontalScrollbar = true;
            listBox.Dock = DockStyle.Fill;
            listBox.Font = SystemFonts.MessageBoxFont;
            listBox.Click += (s, e) => AcceptSelection();
            listBox.KeyDown += listBox_KeyDown;

            grip.Text = "◢";
            grip.TextAlign = ContentAlignment.MiddleCenter;
            grip.Dock = DockStyle.Bottom;
            grip.Height = 14;
            grip.Cursor = Cursors.SizeNWSE;
            grip.BackColor = SystemColors.Control;
            grip.MouseDown += grip_MouseDown;
            grip.MouseMove += grip_MouseMove;
            grip.MouseUp += grip_MouseUp;

            host.Controls.Add(listBox);
            host.Controls.Add(grip);

            ToolStripControlHost controlHost = new ToolStripControlHost(host);
            controlHost.AutoSize = false;
            controlHost.Margin = Padding.Empty;
            controlHost.Padding = Padding.Empty;
            controlHost.Size = host.Size;
            Items.Add(controlHost);
        }

        public void ShowSuggestions(Control anchor, IEnumerable<string> suggestions, string filterText)
        {
            listBox.BeginUpdate();
            listBox.Items.Clear();
            string filter = filterText == null ? "" : filterText.Trim();
            foreach (string suggestion in suggestions)
            {
                if (string.IsNullOrEmpty(suggestion))
                    continue;
                if (filter.Length > 0 &&
                    suggestion.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                listBox.Items.Add(suggestion);
            }
            listBox.EndUpdate();

            if (listBox.Items.Count > 0)
                listBox.SelectedIndex = 0;

            host.Size = rememberedSize;
            ((ToolStripControlHost)Items[0]).Size = host.Size;
            Size = new Size(host.Width + 2, host.Height + 2);

            Show(anchor, new Point(0, anchor.Height));
            listBox.Focus();
        }

        private void AcceptSelection()
        {
            if (listBox.SelectedItem == null)
                return;

            string value = listBox.SelectedItem.ToString();
            rememberedSize = host.Size;
            Close();
            if (ItemChosen != null)
                ItemChosen(this, new EventArgs<string>(value));
        }

        private void listBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                AcceptSelection();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void grip_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            resizing = true;
            resizeStart = Cursor.Position;
            sizeStart = host.Size;
        }

        private void grip_MouseMove(object sender, MouseEventArgs e)
        {
            if (!resizing)
                return;

            int width = Math.Max(host.MinimumSize.Width, sizeStart.Width + (Cursor.Position.X - resizeStart.X));
            int height = Math.Max(host.MinimumSize.Height, sizeStart.Height + (Cursor.Position.Y - resizeStart.Y));
            host.Size = new Size(width, height);
            ((ToolStripControlHost)Items[0]).Size = host.Size;
            Size = new Size(host.Width + 2, host.Height + 2);
        }

        private void grip_MouseUp(object sender, MouseEventArgs e)
        {
            if (!resizing)
                return;
            resizing = false;
            rememberedSize = host.Size;
        }
    }
}
