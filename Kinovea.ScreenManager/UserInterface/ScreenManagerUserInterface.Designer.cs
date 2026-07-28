namespace Kinovea.ScreenManager
{
    partial class ScreenManagerUserInterface
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ScreenManagerUserInterface));
            this.pnlScreens = new System.Windows.Forms.Panel();
            this.splitScreensPanel = new System.Windows.Forms.SplitContainer();
            this.tableScreens = new System.Windows.Forms.TableLayoutPanel();
            this.pbLogo = new System.Windows.Forms.PictureBox();
            this.pnlScreens.SuspendLayout();
            this.splitScreensPanel.Panel1.SuspendLayout();
            this.splitScreensPanel.SuspendLayout();
            this.tableScreens.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbLogo)).BeginInit();
            this.SuspendLayout();
            // 
            // pnlScreens
            // 
            this.pnlScreens.Controls.Add(this.splitScreensPanel);
            this.pnlScreens.Location = new System.Drawing.Point(14, 34);
            this.pnlScreens.Margin = new System.Windows.Forms.Padding(1);
            this.pnlScreens.Name = "pnlScreens";
            this.pnlScreens.Size = new System.Drawing.Size(574, 367);
            this.pnlScreens.TabIndex = 2;
            this.pnlScreens.Visible = false;
            this.pnlScreens.Resize += new System.EventHandler(this.pnlScreens_Resize);
            // 
            // splitScreensPanel
            // 
            this.splitScreensPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitScreensPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitScreensPanel.IsSplitterFixed = true;
            this.splitScreensPanel.Location = new System.Drawing.Point(0, 0);
            this.splitScreensPanel.Margin = new System.Windows.Forms.Padding(3, 3, 0, 3);
            this.splitScreensPanel.Name = "splitScreensPanel";
            this.splitScreensPanel.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitScreensPanel.Panel1
            // 
            this.splitScreensPanel.Panel1.Controls.Add(this.tableScreens);
            // 
            // splitScreensPanel.Panel2
            // 
            this.splitScreensPanel.Panel2.BackColor = System.Drawing.Color.White;
            this.splitScreensPanel.Size = new System.Drawing.Size(574, 367);
            this.splitScreensPanel.SplitterDistance = 315;
            this.splitScreensPanel.TabIndex = 0;
            // 
            // tableScreens
            // 
            this.tableScreens.BackColor = System.Drawing.Color.White;
            this.tableScreens.ColumnCount = 1;
            this.tableScreens.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableScreens.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableScreens.Location = new System.Drawing.Point(0, 0);
            this.tableScreens.Margin = new System.Windows.Forms.Padding(0);
            this.tableScreens.Name = "tableScreens";
            this.tableScreens.RowCount = 1;
            this.tableScreens.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableScreens.Size = new System.Drawing.Size(572, 313);
            this.tableScreens.TabIndex = 0;
            // 
            // pbLogo
            // 
            this.pbLogo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.pbLogo.BackColor = System.Drawing.Color.Transparent;
            this.pbLogo.Image = ((System.Drawing.Image)(resources.GetObject("pbLogo.Image")));
            this.pbLogo.InitialImage = null;
            this.pbLogo.Location = new System.Drawing.Point(327, 422);
            this.pbLogo.Name = "pbLogo";
            this.pbLogo.Size = new System.Drawing.Size(362, 126);
            this.pbLogo.TabIndex = 1;
            this.pbLogo.TabStop = false;
            this.pbLogo.Visible = false;
            // 
            // ScreenManagerUserInterface
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.WhiteSmoke;
            this.Controls.Add(this.pnlScreens);
            this.Controls.Add(this.pbLogo);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "ScreenManagerUserInterface";
            this.Size = new System.Drawing.Size(720, 560);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.ScreenManagerUserInterface_DragDrop);
            this.DragOver += new System.Windows.Forms.DragEventHandler(this.DroppableArea_DragOver);
            this.DoubleClick += new System.EventHandler(this.ScreenManagerUserInterface_DoubleClick);
            this.pnlScreens.ResumeLayout(false);
            this.splitScreensPanel.Panel1.ResumeLayout(false);
            this.splitScreensPanel.ResumeLayout(false);
            this.tableScreens.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pbLogo)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pbLogo;
        private System.Windows.Forms.SplitContainer splitScreensPanel;
        private System.Windows.Forms.TableLayoutPanel tableScreens;
        private System.Windows.Forms.Panel pnlScreens;

    }
}
