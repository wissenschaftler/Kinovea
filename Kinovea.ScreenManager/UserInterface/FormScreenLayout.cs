using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kinovea.ScreenManager.Languages;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    public sealed class FormScreenLayout : Form
    {
        private readonly NumericUpDown screenCount = new NumericUpDown();
        private readonly TableLayoutPanel slots = new TableLayoutPanel();
        private readonly List<ComboBox> typeSelectors = new List<ComboBox>();
        private readonly ComboBox arrangement = new ComboBox();
        private readonly Label arrangementLabel = new Label();
        private int builtSlotCount;

        public ScreenLayoutSpec LayoutSpec { get; private set; }

        public FormScreenLayout()
            : this(null)
        {
        }

        public FormScreenLayout(ScreenLayoutSpec current)
        {
            Text = ScreenManagerLang.ScreenLayout_Title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 380);
            AutoScroll = true;

            Label countLabel = new Label();
            countLabel.Text = ScreenManagerLang.ScreenLayout_Count;
            countLabel.AutoSize = true;
            countLabel.Location = new Point(12, 16);
            Controls.Add(countLabel);

            screenCount.Minimum = 1;
            screenCount.Maximum = 1000;
            screenCount.Value = current == null ? 1 : current.ScreenCount;
            screenCount.Location = new Point(240, 12);
            screenCount.Width = 90;
            screenCount.ValueChanged += ScreenCount_ValueChanged;
            Controls.Add(screenCount);

            arrangementLabel.Text = ScreenManagerLang.ScreenLayout_Arrangement;
            arrangementLabel.AutoSize = true;
            arrangementLabel.Location = new Point(12, 48);
            Controls.Add(arrangementLabel);

            arrangement.DropDownStyle = ComboBoxStyle.DropDownList;
            arrangement.Location = new Point(160, 44);
            arrangement.Width = 170;
            arrangement.Items.Add(ScreenManagerLang.ScreenLayout_Arrangement_2x2);
            arrangement.Items.Add(ScreenManagerLang.ScreenLayout_Arrangement_1x4);
            if (current != null && current.ScreenCount == 4 && current.Columns == 4)
                arrangement.SelectedIndex = 1;
            else
                arrangement.SelectedIndex = 0;
            Controls.Add(arrangement);

            slots.ColumnCount = 2;
            slots.RowCount = 0;
            slots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            slots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            slots.Location = new Point(12, 80);
            slots.Size = new Size(330, 240);
            slots.AutoSize = true;
            Controls.Add(slots);

            EnsureSlotCount((int)screenCount.Value, current);

            Button ok = new Button();
            ok.Text = ScreenManagerLang.Generic_Apply;
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(174, 332);
            ok.Click += Ok_Click;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = ScreenManagerLang.Generic_Cancel;
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(255, 332);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            UpdateSlotVisibility();
        }

        private void ScreenCount_ValueChanged(object sender, EventArgs e)
        {
            EnsureSlotCount((int)screenCount.Value, null);
            UpdateSlotVisibility();
        }

        private void EnsureSlotCount(int count, ScreenLayoutSpec current)
        {
            while (builtSlotCount < count)
            {
                int row = builtSlotCount;
                slots.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                Label label = new Label();
                label.Text = string.Format(ScreenManagerLang.ScreenLayout_Screen, row + 1);
                label.AutoSize = true;
                label.Anchor = AnchorStyles.Left;
                slots.Controls.Add(label, 0, row);

                ComboBox selector = new ComboBox();
                selector.DropDownStyle = ComboBoxStyle.DropDownList;
                selector.Items.Add(ScreenManagerLang.ScreenLayout_Playback);
                selector.Items.Add(ScreenManagerLang.ScreenLayout_Capture);
                selector.SelectedIndex = current != null && row < current.ScreenCount &&
                    current.ScreenTypes[row] == ScreenType.Capture ? 1 : 0;
                selector.Dock = DockStyle.Fill;
                typeSelectors.Add(selector);
                slots.Controls.Add(selector, 1, row);

                builtSlotCount++;
                slots.RowCount = builtSlotCount;
            }
        }

        private void UpdateSlotVisibility()
        {
            int count = (int)screenCount.Value;
            for (int i = 0; i < builtSlotCount; i++)
            {
                slots.GetControlFromPosition(0, i).Visible = i < count;
                slots.GetControlFromPosition(1, i).Visible = i < count;
            }

            bool showArrangement = count == 4;
            arrangementLabel.Visible = showArrangement;
            arrangement.Visible = showArrangement;
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            List<ScreenType> types = new List<ScreenType>();
            for (int i = 0; i < (int)screenCount.Value; i++)
                types.Add(typeSelectors[i].SelectedIndex == 1 ? ScreenType.Capture : ScreenType.Playback);

            int columns = 0;
            int rows = 0;
            if (types.Count == 4)
            {
                if (arrangement.SelectedIndex == 1)
                {
                    columns = 4;
                    rows = 1;
                }
                else
                {
                    columns = 2;
                    rows = 2;
                }
            }

            LayoutSpec = new ScreenLayoutSpec(types, columns, rows);
        }
    }
}
