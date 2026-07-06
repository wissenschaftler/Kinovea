#region License
/*
Copyright ù Joan Charmant 2008.
jcharmant@gmail.com 
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2 
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.
*/
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Threading;
using System.Windows.Forms;

using Kinovea.Camera;
using Kinovea.ScreenManager.Languages;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    public partial class ScreenManagerUserInterface : UserControl
    {
        #region Events
        public event EventHandler<FileLoadAskedEventArgs> FileLoadAsked;
        public event EventHandler AutoLaunchAsked;
        #endregion
        
        #region Properties
        public bool CommonControlsVisible 
        {
            get { return !splitScreensPanel.Panel2Collapsed; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }
        public bool Closing { get; set; }
        #endregion

        #region Members
        private ThumbnailViewerContainer thumbnailViewerContainer = new ThumbnailViewerContainer();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion
        
        public ScreenManagerUserInterface()
        {
            log.Debug("Constructing ScreenManagerUserInterface.");
            InitializeComponent();

            BackColor = Color.White;
            Dock = DockStyle.Fill;
            
            InitializeThumbnailsContainer();

            thumbnailViewerContainer.BringToFront();
            pnlScreens.BringToFront();
            pnlScreens.Dock = DockStyle.Fill;
        }
        
        #region Public methods
        public void RefreshUICulture()
        {
            thumbnailViewerContainer.RefreshUICulture();
        }
        public void ShowCommonControls(bool show, Pair<Type, Type> types, CommonControlsPlayers cctrlsPlayers, CommonControlsCapture cctrlsCapture)
        {
            splitScreensPanel.Panel2Collapsed = !show;
            if (types == null)
                return;

            splitScreensPanel.Panel2.Controls.Clear();

            if (types.First == typeof(PlayerScreen))
                splitScreensPanel.Panel2.Controls.Add(cctrlsPlayers);
            else
                splitScreensPanel.Panel2.Controls.Add(cctrlsCapture);
        }
        public void ToggleCommonControls()
        {
            splitScreensPanel.Panel2Collapsed = !splitScreensPanel.Panel2Collapsed;
        }
        public void OrganizeScreens(List<AbstractScreen> screenList)
        {
            if(screenList.Count == 0)
            {
                pnlScreens.Visible = false;
                this.AllowDrop = true;
                ClearScreenColumns(true);

                if (!Closing)
                    thumbnailViewerContainer.Unhide();
            }
            else
            {
                pnlScreens.Visible = true;
                this.AllowDrop = false;
                
                thumbnailViewerContainer.HideContent();

                PrepareScreenColumns(screenList);
            }
        }
        #endregion

        private void InitializeThumbnailsContainer()
        {
            thumbnailViewerContainer.Dock = DockStyle.Fill;
            thumbnailViewerContainer.Visible = true;
            thumbnailViewerContainer.FileLoadAsked += (s,e) => {
                if(FileLoadAsked != null)
                    FileLoadAsked(this, e);
            };
            
            this.Controls.Add(thumbnailViewerContainer);
        }
        protected override void OnLoad(EventArgs e)
        {
            log.DebugFormat("In ScreenManager OnLoad");
            if (LaunchSettingsManager.ScreenDescriptions.Count > 0 && AutoLaunchAsked != null)
                AutoLaunchAsked(this, EventArgs.Empty);

        }
        private void pnlScreens_Resize(object sender, EventArgs e)
        {
            // Reposition Common Controls panel so it doesn't take more space than necessary.
            splitScreensPanel.SplitterDistance = pnlScreens.Height - 50;
        }
        private void ScreenManagerUserInterface_DoubleClick(object sender, EventArgs e)
        {
            NotificationCenter.RaiseLaunchOpenDialog(this);
        }

        #region Screen management
        private void PrepareScreenColumns(List<AbstractScreen> screenList)
        {
            ClearScreenColumns(false);

            tableScreens.ColumnCount = screenList.Count;
            tableScreens.ColumnStyles.Clear();

            for (int i = 0; i < screenList.Count; i++)
            {
                tableScreens.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / screenList.Count));

                Panel panel = CreateScreenPanel(i);
                UserControl screenUI = screenList[i].UI;
                panel.Controls.Add(screenUI);
                screenUI.Dock = DockStyle.Fill;
                tableScreens.Controls.Add(panel, i, 0);
            }
        }

        private Panel CreateScreenPanel(int index)
        {
            Panel panel = new Panel();
            panel.AllowDrop = true;
            panel.BackColor = Color.White;
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0);
            panel.Tag = index;
            panel.DragDrop += ScreenPanel_DragDrop;
            panel.DragOver += DroppableArea_DragOver;
            return panel;
        }

        private void ClearScreenColumns(bool disposeScreenControls)
        {
            List<Control> controls = new List<Control>();
            foreach (Control control in tableScreens.Controls)
                controls.Add(control);

            foreach (Control control in controls)
            {
                Panel panel = control as Panel;
                if (panel == null)
                    continue;

                if (disposeScreenControls)
                {
                    List<Control> children = new List<Control>();
                    foreach (Control child in panel.Controls)
                        children.Add(child);

                    foreach (Control child in children)
                        child.Dispose();
                }

                panel.Controls.Clear();
                panel.DragDrop -= ScreenPanel_DragDrop;
                panel.DragOver -= DroppableArea_DragOver;
                panel.Dispose();
            }

            tableScreens.Controls.Clear();
            tableScreens.ColumnStyles.Clear();
            tableScreens.ColumnCount = 1;
            tableScreens.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        }
        #endregion
        
        #region DragDrop
        private void DroppableArea_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.All;
        }
        private void ScreenManagerUserInterface_DragDrop(object sender, DragEventArgs e)
        {
            Drop(e, -1);
        }
        private void ScreenPanel_DragDrop(object sender, DragEventArgs e)
        {
            Control control = sender as Control;
            int target = control != null && control.Tag is int ? (int)control.Tag : -1;
            Drop(e, target);
        }
        private void Drop(DragEventArgs e, int target)
        {
            if(e.Data.GetDataPresent(typeof(CameraSummary)))
            {
                CameraSummary summary = (CameraSummary)e.Data.GetData(typeof(CameraSummary));
                if(summary != null)
                    CameraTypeManager.LoadCamera(summary, target);
            }
            else if(e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string filename = (string)e.Data.GetData(DataFormats.StringFormat);
                FileLoadAsked(this, new FileLoadAskedEventArgs(filename, target));
            }
            else if(e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Array fileArray = (Array)e.Data.GetData(DataFormats.FileDrop);
                if (fileArray != null)
                {
                   string filename = fileArray.GetValue(0).ToString();
                   FileLoadAsked(this, new FileLoadAskedEventArgs(filename, target));
                }
            }
        }
        #endregion
    }
}
