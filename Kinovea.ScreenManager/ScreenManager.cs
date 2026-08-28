#region License
/*
Copyright  Joan Charmant 2008.
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
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Linq;

using Kinovea.Camera;
using Kinovea.ScreenManager.Languages;
using Kinovea.Services;
using Kinovea.Video;
using Kinovea.Video.FFMpeg;

namespace Kinovea.ScreenManager
{
    public class ScreenManagerKernel : IKernel
    {
        #region Properties
        public UserControl UI
        {
            get { return view; }
        }
        public ScreenManagerUserInterface View
        {
            get { return view;}
        }
        public ResourceManager resManager
        {
            get { return new ResourceManager("Kinovea.ScreenManager.Languages.ScreenManagerLang", Assembly.GetExecutingAssembly()); }
        }
        public int ScreenCount
        {
            get { return screenList.Count;}
        }

        public int CaptureScreenCount
        {
            get { return captureScreens.Count(); }
        }
        #endregion

        public const int MaxScreens = ScreenLayoutSpec.MaximumScreenCount;

        #region Members
        private ScreenManagerUserInterface view;
        private DualPlayerController dualPlayer = new DualPlayerController();
        private DualCaptureController dualCapture = new DualCaptureController();
        private List<AbstractScreen> screenList = new List<AbstractScreen>();
        private IEnumerable<PlayerScreen> playerScreens;
        private IEnumerable<CaptureScreen> captureScreens;
        private AbstractScreen activeScreen = null;
        private bool canShowCommonControls;
        private int layoutColumns = 1;
        private int layoutRows = 1;
        private readonly List<LayoutSlotCacheEntry> layoutSlotCache = new List<LayoutSlotCacheEntry>();
        private bool screensSuspended;
        private SessionScreenSnapshot peakScreenSnapshot;
        private int peakScreenCount;
        private bool applyingLaunchScreenDescriptions;
        private readonly Stack<SessionScreenSnapshot.Slot> closedScreensStack = new Stack<SessionScreenSnapshot.Slot>();
        private const int MaxClosedScreensStack = 10;
        private int dualLaunchSettingsPendingCountdown;
        private List<string> camerasToDiscover = new List<string>();
        private AudioInputLevelMonitor audioInputLevelMonitor = new AudioInputLevelMonitor();
        
        // Video Filters
        private bool hasSvgFiles;
        private string svgPath;
        private FileSystemWatcher svgFilesWatcher = new FileSystemWatcher();
        private bool buildingSVGMenu;
        private List<ToolStripMenuItem> filterMenus = new List<ToolStripMenuItem>();
        
        #region Menus
        private ToolStripMenuItem mnuCloseFile = new ToolStripMenuItem();
        private ToolStripMenuItem mnuCloseFile2 = new ToolStripMenuItem();
        private ToolStripMenuItem mnuCloseFile3 = new ToolStripMenuItem();
        private ToolStripMenuItem mnuCloseFile4 = new ToolStripMenuItem();
        private ToolStripMenuItem mnuSave = new ToolStripMenuItem();
        private ToolStripMenuItem mnuSaveAs = new ToolStripMenuItem();
        private ToolStripMenuItem mnuExportVideo = new ToolStripMenuItem();
        private ToolStripMenuItem mnuExportSpreadsheet = new ToolStripMenuItem();
        private ToolStripMenuItem mnuExportODF = new ToolStripMenuItem();
        private ToolStripMenuItem mnuExportMSXML = new ToolStripMenuItem();
        private ToolStripMenuItem mnuExportXHTML = new ToolStripMenuItem();
        private ToolStripMenuItem mnuExportTEXT = new ToolStripMenuItem();
        private ToolStripMenuItem mnuLoadAnalysis = new ToolStripMenuItem();

        private ToolStripMenuItem mnuCutDrawing = new ToolStripMenuItem();
        private ToolStripMenuItem mnuCopyDrawing = new ToolStripMenuItem();
        private ToolStripMenuItem mnuPasteDrawing = new ToolStripMenuItem();
        
        private ToolStripMenuItem mnuOnePlayer = new ToolStripMenuItem();
        private ToolStripMenuItem mnuTwoPlayers = new ToolStripMenuItem();
        private ToolStripMenuItem mnuThreePlayers = new ToolStripMenuItem();
        private ToolStripMenuItem mnuFourPlayers = new ToolStripMenuItem();
        private ToolStripMenuItem mnuFourPlayersRow = new ToolStripMenuItem();
        private ToolStripMenuItem mnuInsertScreenRight = new ToolStripMenuItem();
        private ToolStripMenuItem mnuRestorePeakScreens = new ToolStripMenuItem();
        private ToolStripMenuItem mnuRestoreLastClosedScreen = new ToolStripMenuItem();
        private ToolStripMenuItem mnuOneCapture = new ToolStripMenuItem();
        private ToolStripMenuItem mnuTwoCaptures = new ToolStripMenuItem();
        private ToolStripMenuItem mnuTwoMixed = new ToolStripMenuItem();
        private ToolStripMenuItem mnuConfigureScreens = new ToolStripMenuItem();
        private ToolStripMenuItem mnuSwapScreens = new ToolStripMenuItem();
        private ToolStripMenuItem mnuToggleCommonCtrls = new ToolStripMenuItem();

        private ToolStripMenuItem mnuDeinterlace = new ToolStripMenuItem();

        private ToolStripMenuItem mnuDemosaic = new ToolStripMenuItem();
        private ToolStripMenuItem mnuDemosaicNone = new ToolStripMenuItem();
        private ToolStripMenuItem mnuDemosaicRGGB = new ToolStripMenuItem();
        private ToolStripMenuItem mnuDemosaicBGGR = new ToolStripMenuItem();
        private ToolStripMenuItem mnuDemosaicGRBG = new ToolStripMenuItem();
        private ToolStripMenuItem mnuDemosaicGBRG = new ToolStripMenuItem();

        private ToolStripMenuItem mnuFormat = new ToolStripMenuItem();
        private ToolStripMenuItem mnuFormatAuto = new ToolStripMenuItem();
        private ToolStripMenuItem mnuFormatForce43 = new ToolStripMenuItem();
        private ToolStripMenuItem mnuFormatForce169 = new ToolStripMenuItem();

        private ToolStripMenuItem mnuRotation = new ToolStripMenuItem();
        private ToolStripMenuItem mnuRotation0 = new ToolStripMenuItem();
        private ToolStripMenuItem mnuRotation90 = new ToolStripMenuItem();
        private ToolStripMenuItem mnuRotation180 = new ToolStripMenuItem();
        private ToolStripMenuItem mnuRotation270 = new ToolStripMenuItem();

        private ToolStripMenuItem mnuMirror = new ToolStripMenuItem();

        private ToolStripMenuItem mnuTimebase = new ToolStripMenuItem();

        private ToolStripMenuItem mnuSVGTools = new ToolStripMenuItem();
        private ToolStripMenuItem mnuImportImage = new ToolStripMenuItem();
        private ToolStripMenuItem mnuTestGrid = new ToolStripMenuItem();
        private ToolStripMenuItem mnuCoordinateAxis = new ToolStripMenuItem();
        private ToolStripMenuItem mnuCameraCalibration = new ToolStripMenuItem();
        private ToolStripMenuItem mnuTrajectoryAnalysis = new ToolStripMenuItem();
        private ToolStripMenuItem mnuScatterDiagram = new ToolStripMenuItem();
        private ToolStripMenuItem mnuAngularAnalysis = new ToolStripMenuItem();
        private ToolStripMenuItem mnuAngleAngleAnalysis = new ToolStripMenuItem();

        #endregion

        #region Toolbar
        private ToolStripButton toolHome = new ToolStripButton();
        private ToolStripButton toolSave = new ToolStripButton();
        private ToolStripButton toolOnePlayer = new ToolStripButton();
        private ToolStripButton toolTwoPlayers = new ToolStripButton();
        private ToolStripButton toolThreePlayers = new ToolStripButton();
        private ToolStripButton toolFourPlayers = new ToolStripButton();
        private ToolStripButton toolFourPlayersRow = new ToolStripButton();
        private ToolStripButton toolInsertScreenRight = new ToolStripButton();
        private ToolStripButton toolRestorePeakScreens = new ToolStripButton();
        private ToolStripButton toolRestoreLastClosedScreen = new ToolStripButton();
        private ToolStripButton toolOneCapture = new ToolStripButton();
        private ToolStripButton toolTwoCaptures = new ToolStripButton();
        private ToolStripButton toolTwoMixed = new ToolStripButton();
        #endregion
        
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        #region Constructor & initialization
        public ScreenManagerKernel()
        {
            log.Debug("Module Construction: ScreenManager.");

            view = new ScreenManagerUserInterface();
            view.FileLoadAsked += View_FileLoadAsked;
            view.ScreenSwapAsked += View_ScreenSwapAsked;
            view.AutoLaunchAsked += View_AutoLaunchAsked;
            AddCommonControlsEventHandlers();

            CameraTypeManager.CameraLoadAsked += CameraTypeManager_CameraLoadAsked;
            VideoTypeManager.VideoLoadAsked += VideoTypeManager_VideoLoadAsked;

            audioInputLevelMonitor.Enabled = PreferencesManager.CapturePreferences.CaptureAutomationConfiguration.EnableAudioTrigger;
            audioInputLevelMonitor.Threshold = PreferencesManager.CapturePreferences.CaptureAutomationConfiguration.AudioTriggerThreshold;
            audioInputLevelMonitor.ThresholdPassed += (s, e) => TriggerCapture();
            audioInputLevelMonitor.DeviceLost += (s, e) => AudioDeviceLost();

            InitializeVideoFilters();
            InitializeGuideWatcher();

            NotificationCenter.StopPlayback += (s, e) => DoStopPlaying();
            NotificationCenter.PreferencesOpened += NotificationCenter_PreferencesOpened;
            NotificationCenter.ExternalCommand += NotificationCenter_ExternalCommand;

            playerScreens = screenList.Where(s => s is PlayerScreen).Select(s => s as PlayerScreen);
            captureScreens = screenList.Where(s => s is CaptureScreen).Select(s => s as CaptureScreen);
        }

        private void InitializeVideoFilters()
        {
            //filterMenus.Add(CreateFilterMenu(new VideoFilterAutoLevels()));
            //filterMenus.Add(CreateFilterMenu(new VideoFilterContrast()));
            //filterMenus.Add(CreateFilterMenu(new VideoFilterSharpen()));
            //filterMenus.Add(CreateFilterMenu(new VideoFilterEdgesOnly()));
            filterMenus.Add(CreateFilterMenu(new VideoFilterMosaic()));
            filterMenus.Add(CreateFilterMenu(new VideoFilterReverse()));
            //filterMenus.Add(CreateFilterMenu(new VideoFilterSandbox()));
        }

        private ToolStripMenuItem CreateFilterMenu(AbstractVideoFilter _filter)
        {
            // TODO: test if we can directly use a copy of the argument in the closure.
            // would avoid passing through .Tag and multiple casts.
            ToolStripMenuItem menu = new ToolStripMenuItem(_filter.Name, _filter.Icon);
            menu.MergeAction = MergeAction.Append;
            menu.Tag = _filter;
            menu.Click += (s,e) => 
            {
                PlayerScreen screen = activeScreen as PlayerScreen;
                if(screen == null || !screen.IsCaching)
                    return;
                AbstractVideoFilter filter = (AbstractVideoFilter)((ToolStripMenuItem)s).Tag;
                filter.Activate(screen.FrameServer.VideoReader.WorkingZoneFrames, SetInteractiveEffect);
                screen.RefreshImage();
            };
            return menu;
        }

        private void InitializeGuideWatcher()
        {
            svgPath = Path.GetDirectoryName(Application.ExecutablePath) + "\\guides\\";
            svgFilesWatcher.Path = svgPath;
            svgFilesWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite;
            svgFilesWatcher.Filter = "*.svg";
            svgFilesWatcher.IncludeSubdirectories = true;
            svgFilesWatcher.EnableRaisingEvents = true;

            svgFilesWatcher.Changed += OnSVGFilesChanged;
            svgFilesWatcher.Created += OnSVGFilesChanged;
            svgFilesWatcher.Deleted += OnSVGFilesChanged;
            svgFilesWatcher.Renamed += OnSVGFilesChanged;
        }

        public void SetInteractiveEffect(InteractiveEffect _effect)
        {
            PlayerScreen player = activeScreen as PlayerScreen;
            if(player != null)
                player.SetInteractiveEffect(_effect);
        }
        
        public void RecoverCrash()
        {
            // Import recovered screens into launch settings.
            try
            {
                List<ScreenDescriptionPlayback> recoverables = RecoveryManager.GetRecoverables();
                if (recoverables != null && recoverables.Count > 0)
                {
                    FormCrashRecovery fcr = new FormCrashRecovery(recoverables);
                    FormsHelper.Locate(fcr);
                    if (fcr.ShowDialog() != DialogResult.OK)
                    {
                        log.DebugFormat("Recovery procedure cancelled. Deleting files.");
                        FilesystemHelper.DeleteOrphanFiles();
                    }
                }
            }
            catch (Exception)
            {
                log.Error("An error happened while running crash detection and recovery routine.");
                FilesystemHelper.DeleteOrphanFiles();
            }
        }

        public void LoadDefaultWorkspace()
        {
            if (LaunchSettingsManager.ScreenDescriptions.Count > 0)
                return;

            Workspace workspace = PreferencesManager.GeneralPreferences.Workspace;
            if (workspace.Screens == null || workspace.Screens.Count == 0)
                return;

            LaunchSettingsManager.SetLayout(workspace.Columns, workspace.Rows);
            foreach (var sd in workspace.Screens)
                LaunchSettingsManager.AddScreenDescription(sd);
        }

        /// <summary>
        /// Replace the current screens with the contents of a workspace file description.
        /// </summary>
        public bool LoadWorkspace(Workspace workspace)
        {
            if (workspace == null || workspace.Screens == null || workspace.Screens.Count == 0)
                return false;

            screensSuspended = false;

            while (screenList.Count > 0)
            {
                if (!ScreenRemover.RemoveScreen(this, 0))
                    break;
            }

            ClearLayoutSlotCache();

            LaunchSettingsManager.ClearScreenDescriptions();
            LaunchSettingsManager.SetLayout(workspace.Columns, workspace.Rows);
            foreach (IScreenDescription screen in workspace.Screens)
                LaunchSettingsManager.AddScreenDescription(screen);

            ApplyLaunchScreenDescriptions();
            return true;
        }
        #endregion

        #region IKernel Implementation
        public void BuildSubTree()
        {
            // No sub modules.
        }
        public void ExtendMenu(ToolStrip menu)
        {
            #region File
            ToolStripMenuItem mnuCatchFile = new ToolStripMenuItem();
            mnuCatchFile.MergeIndex = 0; // (File)
            mnuCatchFile.MergeAction = MergeAction.MatchOnly;

            // Load Analysis
            mnuLoadAnalysis.Image = Properties.Resources.file_kva2;
            mnuLoadAnalysis.Click += mnuLoadAnalysisOnClick;
            mnuLoadAnalysis.MergeIndex = 2;
            mnuLoadAnalysis.MergeAction = MergeAction.Insert;

            //----

            mnuSave.Image = Properties.Resources.filesave;
            mnuSave.Click += new EventHandler(mnuSaveOnClick);
            mnuSave.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S;
            mnuSave.MergeIndex = 5;
            mnuSave.MergeAction = MergeAction.Insert;

            mnuSaveAs.Image = Properties.Resources.filesave;
            mnuSaveAs.Click += new EventHandler(mnuSaveAsOnClick);
            mnuSaveAs.MergeIndex = 6;
            mnuSaveAs.MergeAction = MergeAction.Insert;

            mnuExportVideo.Image = Properties.Resources.film_save;
            mnuExportVideo.Click += new EventHandler(mnuExportVideoOnClick);
            mnuExportVideo.MergeIndex = 7;
            mnuExportVideo.MergeAction = MergeAction.Insert;

            mnuExportSpreadsheet.Image = Properties.Resources.table;
            mnuExportSpreadsheet.MergeIndex = 8;
            mnuExportSpreadsheet.MergeAction = MergeAction.Insert;
            mnuExportODF.Image = Properties.Resources.file_ods;
            mnuExportODF.Click += new EventHandler(mnuExportODF_OnClick);
            mnuExportMSXML.Image = Properties.Resources.file_xls;
            mnuExportMSXML.Click += new EventHandler(mnuExportMSXML_OnClick);
            mnuExportXHTML.Image = Properties.Resources.file_html;
            mnuExportXHTML.Click += new EventHandler(mnuExportXHTML_OnClick);
            mnuExportTEXT.Image = Properties.Resources.file_txt;
            mnuExportTEXT.Click += new EventHandler(mnuExportTEXT_OnClick);
            mnuExportSpreadsheet.DropDownItems.AddRange(new ToolStripItem[] { mnuExportODF, mnuExportMSXML, mnuExportXHTML, mnuExportTEXT });

            //------------------------

            mnuCloseFile.Image = Properties.Resources.film_close3;
            mnuCloseFile.Enabled = false;
            mnuCloseFile.Click += new EventHandler(mnuCloseFileOnClick);
            mnuCloseFile.MergeIndex = 10;
            mnuCloseFile.MergeAction = MergeAction.Insert;

            mnuCloseFile2.Image = Properties.Resources.film_close3;
            mnuCloseFile2.Enabled = false;
            mnuCloseFile2.Visible = false;
            mnuCloseFile2.Click += new EventHandler(mnuCloseFile2OnClick);
            mnuCloseFile2.MergeIndex = 11;
            mnuCloseFile2.MergeAction = MergeAction.Insert;

            mnuCloseFile3.Image = Properties.Resources.film_close3;
            mnuCloseFile3.Enabled = false;
            mnuCloseFile3.Visible = false;
            mnuCloseFile3.Click += new EventHandler(mnuCloseFile3OnClick);
            mnuCloseFile3.MergeIndex = 12;
            mnuCloseFile3.MergeAction = MergeAction.Insert;

            mnuCloseFile4.Image = Properties.Resources.film_close3;
            mnuCloseFile4.Enabled = false;
            mnuCloseFile4.Visible = false;
            mnuCloseFile4.Click += new EventHandler(mnuCloseFile4OnClick);
            mnuCloseFile4.MergeIndex = 13;
            mnuCloseFile4.MergeAction = MergeAction.Insert;

            //--------------------

            ToolStripItem[] subFile = new ToolStripItem[] {
                // Open file
                // Open replay observer,
                // Recent,
                mnuLoadAnalysis,
                // ----
                mnuSave,
                mnuSaveAs,
                mnuExportVideo,
                mnuExportSpreadsheet,
                //----
                mnuCloseFile,
                mnuCloseFile2,
                mnuCloseFile3,
                mnuCloseFile4,
                //----
                // Quit.
                };

            mnuCatchFile.DropDownItems.AddRange(subFile);
            #endregion

            #region Edit
            ToolStripMenuItem mnuCatchEdit = new ToolStripMenuItem();
            mnuCatchEdit.MergeIndex = 1; // (Edit)
            mnuCatchEdit.MergeAction = MergeAction.MatchOnly;

            mnuCutDrawing.Image = Properties.Drawings.cut;
            mnuCutDrawing.Click += new EventHandler(mnuCutDrawing_OnClick);
            mnuCutDrawing.MergeAction = MergeAction.Append;
            mnuCopyDrawing.Image = Properties.Drawings.copy;
            mnuCopyDrawing.Click += new EventHandler(mnuCopyDrawing_OnClick);
            mnuCopyDrawing.MergeAction = MergeAction.Append;
            mnuPasteDrawing.Image = Properties.Drawings.paste;
            mnuPasteDrawing.Click += new EventHandler(mnuPasteDrawing_OnClick);
            mnuPasteDrawing.MergeAction = MergeAction.Append;
            
            ToolStripItem[] subEdit = new ToolStripItem[] { new ToolStripSeparator(), mnuCutDrawing, mnuCopyDrawing, mnuPasteDrawing };
            mnuCatchEdit.DropDownItems.AddRange(subEdit);
            #endregion

            #region View
            ToolStripMenuItem mnuCatchScreens = new ToolStripMenuItem();
            mnuCatchScreens.MergeIndex = 2; // (Screens)
            mnuCatchScreens.MergeAction = MergeAction.MatchOnly;

            mnuOnePlayer.Image = Properties.Resources.television;
            mnuOnePlayer.Click += new EventHandler(mnuOnePlayerOnClick);
            mnuOnePlayer.MergeAction = MergeAction.Append;
            mnuTwoPlayers.Image = Properties.Resources.dualplayback;
            mnuTwoPlayers.Click += new EventHandler(mnuTwoPlayersOnClick);
            mnuTwoPlayers.MergeAction = MergeAction.Append;
            mnuThreePlayers.Image = Properties.Resources.dualplayback;
            mnuThreePlayers.Click += (s, e) => ApplyLayout(ScreenLayoutSpec.Playback(3));
            mnuThreePlayers.MergeAction = MergeAction.Append;
            mnuFourPlayers.Image = Properties.Resources.dualplayback;
            mnuFourPlayers.Click += (s, e) => ApplyLayout(ScreenLayoutSpec.Playback(4, 2, 2));
            mnuFourPlayers.MergeAction = MergeAction.Append;
            mnuFourPlayersRow.Image = Properties.Resources.dualplayback;
            mnuFourPlayersRow.Click += (s, e) => ApplyLayout(ScreenLayoutSpec.Playback(4, 4, 1));
            mnuFourPlayersRow.MergeAction = MergeAction.Append;
            mnuInsertScreenRight.Image = Properties.Drawings.plus_small;
            mnuInsertScreenRight.Click += (s, e) => InsertScreenToRightOfActive();
            mnuInsertScreenRight.MergeAction = MergeAction.Append;
            mnuRestorePeakScreens.Image = Properties.Resources.dualplayback;
            mnuRestorePeakScreens.Click += (s, e) => RestorePeakScreens();
            mnuRestorePeakScreens.MergeAction = MergeAction.Append;
            mnuRestoreLastClosedScreen.Image = Properties.Resources.film_close3;
            mnuRestoreLastClosedScreen.Click += (s, e) => RestoreLastClosedScreen();
            mnuRestoreLastClosedScreen.MergeAction = MergeAction.Append;
            mnuOneCapture.Image = Properties.Resources.camera_video;
            mnuOneCapture.Click += new EventHandler(mnuOneCaptureOnClick);
            mnuOneCapture.MergeAction = MergeAction.Append;
            mnuTwoCaptures.Image = Properties.Resources.dualcapture2;
            mnuTwoCaptures.Click += new EventHandler(mnuTwoCapturesOnClick);
            mnuTwoCaptures.MergeAction = MergeAction.Append;
            mnuTwoMixed.Image = Properties.Resources.dualmixed3;
            mnuTwoMixed.Click += new EventHandler(mnuTwoMixedOnClick);
            mnuTwoMixed.MergeAction = MergeAction.Append;
            mnuConfigureScreens.Image = Properties.Resources.common_controls;
            mnuConfigureScreens.Click += new EventHandler(mnuConfigureScreens_Click);
            mnuConfigureScreens.MergeAction = MergeAction.Append;
                        
            mnuSwapScreens.Image = Properties.Resources.flatswap3d;
            mnuSwapScreens.Enabled = false;
            mnuSwapScreens.Click += new EventHandler(mnuSwapScreensOnClick);
            mnuSwapScreens.MergeAction = MergeAction.Append;
            
            mnuToggleCommonCtrls.Image = Properties.Resources.common_controls;
            mnuToggleCommonCtrls.Enabled = false;
            mnuToggleCommonCtrls.ShortcutKeys = Keys.F5;
            mnuToggleCommonCtrls.Click += new EventHandler(mnuToggleCommonCtrlsOnClick);
            mnuToggleCommonCtrls.MergeAction = MergeAction.Append;
            
            ToolStripItem[] subScreens = new ToolStripItem[] { 		mnuOnePlayer,
                                                                    mnuTwoPlayers,
                                                                    mnuThreePlayers,
                                                                    mnuFourPlayers,
                                                                    mnuFourPlayersRow,
                                                                    mnuInsertScreenRight,
                                                                    mnuRestorePeakScreens,
                                                                    mnuRestoreLastClosedScreen,
                                                                    new ToolStripSeparator(),
                                                                    mnuOneCapture, 
                                                                    mnuTwoCaptures,
                                                                    new ToolStripSeparator(),
                                                                    mnuTwoMixed,
                                                                    mnuConfigureScreens,
                                                                    new ToolStripSeparator(), 
                                                                    mnuSwapScreens, 
                                                                    mnuToggleCommonCtrls };
            mnuCatchScreens.DropDownItems.AddRange(subScreens);
            #endregion

            #region Image
            ToolStripMenuItem mnuCatchImage = new ToolStripMenuItem();
            mnuCatchImage.MergeIndex = 3; // (Image)
            mnuCatchImage.MergeAction = MergeAction.MatchOnly;
            
            mnuDeinterlace.Image = Properties.Resources.deinterlace;
            mnuDeinterlace.Checked = false;
            mnuDeinterlace.ShortcutKeys = Keys.Control | Keys.D;
            mnuDeinterlace.Click += new EventHandler(mnuDeinterlaceOnClick);
            mnuDeinterlace.MergeAction = MergeAction.Append;

            mnuDemosaicNone.Click += mnuDemosaicNone_Click;
            mnuDemosaicRGGB.Click += mnuDemosaicRGGB_Click;
            mnuDemosaicBGGR.Click += mnuDemosaicBGGR_Click;
            mnuDemosaicGRBG.Click += mnuDemosaicGRBG_Click;
            mnuDemosaicGBRG.Click += mnuDemosaicGBRG_Click;
            mnuDemosaicRGGB.Image = Properties.Resources.rggb;
            mnuDemosaicBGGR.Image = Properties.Resources.bggr;
            mnuDemosaicGRBG.Image = Properties.Resources.grbg;
            mnuDemosaicGBRG.Image = Properties.Resources.gbrg;
            mnuDemosaic.Image = Properties.Resources.rggb;
            mnuDemosaic.MergeAction = MergeAction.Append;
            mnuDemosaic.DropDownItems.AddRange(new ToolStripItem[] { mnuDemosaicNone, new ToolStripSeparator(), mnuDemosaicRGGB, mnuDemosaicBGGR, mnuDemosaicGRBG, mnuDemosaicGBRG });
            
            mnuFormatAuto.Checked = true;
            mnuFormatAuto.Click += mnuFormatAutoOnClick;
            mnuFormatAuto.MergeAction = MergeAction.Append;
            mnuFormatForce43.Image = Properties.Resources.format43;
            mnuFormatForce43.Click += mnuFormatForce43OnClick;
            mnuFormatForce43.MergeAction = MergeAction.Append;
            mnuFormatForce169.Image = Properties.Resources.format169;
            mnuFormatForce169.Click += mnuFormatForce169OnClick;
            mnuFormatForce169.MergeAction = MergeAction.Append;
            mnuFormat.Image = Properties.Resources.shape_formats;
            mnuFormat.MergeAction = MergeAction.Append;
            mnuFormat.DropDownItems.AddRange(new ToolStripItem[] { mnuFormatAuto, new ToolStripSeparator(), mnuFormatForce43, mnuFormatForce169});

            mnuRotation0.Click += mnuRotation0_Click;
            mnuRotation90.Image = Properties.Resources.rotate90;
            mnuRotation90.Click += mnuRotation90_Click;
            mnuRotation180.Image = Properties.Resources.rotate180;
            mnuRotation180.Click += mnuRotation180_Click;
            mnuRotation270.Image = Properties.Resources.rotate270;
            mnuRotation270.Click += mnuRotation270_Click;
            mnuRotation.Image = Properties.Resources.imagerotate;
            mnuRotation.MergeAction = MergeAction.Append;
            mnuRotation.DropDownItems.AddRange(new ToolStripItem[] { mnuRotation0, mnuRotation90, mnuRotation270, mnuRotation180 });

            mnuMirror.Image = Properties.Resources.shape_mirror;
            mnuMirror.Checked = false;
            mnuMirror.ShortcutKeys = Keys.Control | Keys.M;
            mnuMirror.Click += new EventHandler(mnuMirrorOnClick);
            mnuMirror.MergeAction = MergeAction.Append;

            ConfigureVideoFilterMenus(null);

            mnuCatchImage.DropDownItems.Add(mnuFormat);
            mnuCatchImage.DropDownItems.Add(mnuRotation);
            mnuCatchImage.DropDownItems.Add(mnuMirror);
            mnuCatchImage.DropDownItems.Add(mnuDemosaic);
            mnuCatchImage.DropDownItems.Add(mnuDeinterlace);
            //mnuCatchImage.DropDownItems.Add(new ToolStripSeparator());
            
            // Temporary hack for including filters sub menus until a full plugin system is in place.
            // We just check on their type. Ultimately each plugin will have a category or a submenu property.
            //foreach(ToolStripMenuItem m in filterMenus)
            //{
            //    if (m.Tag is AdjustmentFilter)
            //        mnuCatchImage.DropDownItems.Add(m);
            //}
            
            #endregion

            #region Video
            ToolStripMenuItem mnuCatchVideo = new ToolStripMenuItem();
            mnuCatchVideo.MergeIndex = 4;
            mnuCatchVideo.MergeAction = MergeAction.MatchOnly;

            mnuTimebase.Image = Properties.Resources.camera_speed;
            mnuTimebase.Click += new EventHandler(mnuTimebase_OnClick);
            mnuTimebase.MergeAction = MergeAction.Append;
            
            mnuCatchVideo.DropDownItems.Add(mnuTimebase);
            mnuCatchVideo.DropDownItems.Add(new ToolStripSeparator());
            foreach(ToolStripMenuItem m in filterMenus)
            {
                mnuCatchVideo.DropDownItems.Add(m);
            }
            #endregion

            #region Tools
            ToolStripMenuItem mnuCatchTools = new ToolStripMenuItem();
            mnuCatchTools.MergeIndex = 5;
            mnuCatchTools.MergeAction = MergeAction.MatchOnly;

            BuildSvgMenu();

            mnuTestGrid.Image = Properties.Resources.grid2;
            mnuTestGrid.Click += mnuTestGrid_OnClick;
            mnuTestGrid.MergeAction = MergeAction.Append;

            mnuCoordinateAxis.Image = Properties.Resources.coordinate_axis;
            mnuCoordinateAxis.Click += mnuCoordinateAxis_OnClick;
            mnuCoordinateAxis.MergeAction = MergeAction.Append;

            mnuCameraCalibration.Image = Properties.Resources.checkerboard;
            mnuCameraCalibration.Click += mnuCameraCalibration_OnClick;
            mnuCameraCalibration.MergeAction = MergeAction.Append;

            mnuTrajectoryAnalysis.Image = Properties.Resources.function;
            mnuTrajectoryAnalysis.Click += mnuTrajectoryAnalysis_OnClick;
            mnuTrajectoryAnalysis.MergeAction = MergeAction.Append;

            mnuScatterDiagram.Image = Properties.Resources.function;
            mnuScatterDiagram.Click += mnuScatterDiagram_OnClick;
            mnuScatterDiagram.MergeAction = MergeAction.Append;

            mnuAngularAnalysis.Image = Properties.Resources.function;
            mnuAngularAnalysis.Click += mnuAngularAnalysis_OnClick;
            mnuAngularAnalysis.MergeAction = MergeAction.Append;

            mnuAngleAngleAnalysis.Image = Properties.Resources.function;
            mnuAngleAngleAnalysis.Click += mnuAngleAngleAnalysis_OnClick;
            mnuAngleAngleAnalysis.MergeAction = MergeAction.Append;

            mnuCatchTools.DropDownItems.AddRange(new ToolStripItem[] { 
                mnuSVGTools, 
                mnuTestGrid, 
                mnuCoordinateAxis, 
                mnuCameraCalibration, 
                new ToolStripSeparator(),
                mnuScatterDiagram,
                mnuTrajectoryAnalysis,
                mnuAngularAnalysis,
                mnuAngleAngleAnalysis
            });

            #endregion

            MenuStrip ThisMenu = new MenuStrip();
            ThisMenu.Items.AddRange(new ToolStripItem[] { mnuCatchFile, mnuCatchEdit, mnuCatchScreens, mnuCatchImage, mnuCatchVideo, mnuCatchTools });
            ThisMenu.AllowMerge = true;

            ToolStripManager.Merge(ThisMenu, menu);

            RefreshCultureMenu();
        }

        public void ExtendToolBar(ToolStrip toolbar)
        {
            // Save
            toolSave.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolSave.Image = Properties.Resources.filesave;
            toolSave.Click += new EventHandler(mnuSaveOnClick);
            
            // Workspace presets.
            
            toolHome.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolHome.Image = Properties.Resources.home3;
            toolHome.Click += new EventHandler(mnuHome_OnClick);
            
            toolOnePlayer.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolOnePlayer.Image = Properties.Resources.television;
            toolOnePlayer.Click += new EventHandler(mnuOnePlayerOnClick);
            
            toolTwoPlayers.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolTwoPlayers.Image = Properties.Resources.dualplayback;
            toolTwoPlayers.Click += new EventHandler(mnuTwoPlayersOnClick);

            toolThreePlayers.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolThreePlayers.Image = Properties.Resources.dualplayback;
            toolThreePlayers.Click += (s, e) => ApplyLayout(ScreenLayoutSpec.Playback(3));

            toolFourPlayers.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolFourPlayers.Image = Properties.Resources.dualplayback;
            toolFourPlayers.Click += (s, e) => ApplyLayout(ScreenLayoutSpec.Playback(4, 2, 2));

            toolFourPlayersRow.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolFourPlayersRow.Image = Properties.Resources.dualplayback;
            toolFourPlayersRow.Click += (s, e) => ApplyLayout(ScreenLayoutSpec.Playback(4, 4, 1));

            toolInsertScreenRight.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolInsertScreenRight.Image = Properties.Drawings.plus_small;
            toolInsertScreenRight.Click += (s, e) => InsertScreenToRightOfActive();

            toolRestorePeakScreens.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolRestorePeakScreens.Image = Properties.Resources.dualplayback;
            toolRestorePeakScreens.Click += (s, e) => RestorePeakScreens();

            toolRestoreLastClosedScreen.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolRestoreLastClosedScreen.Image = Properties.Resources.film_close3;
            toolRestoreLastClosedScreen.Click += (s, e) => RestoreLastClosedScreen();
            
            toolOneCapture.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolOneCapture.Image = Properties.Resources.camera_video;
            toolOneCapture.Click += new EventHandler(mnuOneCaptureOnClick);
            
            toolTwoCaptures.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolTwoCaptures.Image = Properties.Resources.dualcapture2;
            toolTwoCaptures.Click += new EventHandler(mnuTwoCapturesOnClick);
            
            toolTwoMixed.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolTwoMixed.Image = Properties.Resources.dualmixed3;
            toolTwoMixed.Click += new EventHandler(mnuTwoMixedOnClick);
            
            ToolStrip ts = new ToolStrip(new ToolStripItem[] { 
                                            toolSave,
                                            new ToolStripSeparator(),
                                            toolHome,
                                            new ToolStripSeparator(),
                                            toolOnePlayer,
                                            toolTwoPlayers,
                                            toolThreePlayers,
                                            toolFourPlayers,
                                            toolFourPlayersRow,
                                            toolInsertScreenRight,
                                            toolRestorePeakScreens,
                                            toolRestoreLastClosedScreen,
                                            new ToolStripSeparator(),
                                            toolOneCapture, 
                                            toolTwoCaptures, 
                                            new ToolStripSeparator(),
                                            toolTwoMixed });
            
            ToolStripManager.Merge(ts, toolbar);
            
        }
        public void ExtendStatusBar(ToolStrip statusbar)
        {
            // Nothing at this level.
            // No sub modules.
        }
        public void ExtendUI()
        {
            // No sub modules.
        }
        public void RefreshUICulture()
        {
            RefreshCultureMenu();
            OrganizeMenus();
            RefreshCultureToolbar();
            UpdateStatusBar();
            dualPlayer.RefreshUICulture();
            dualCapture.RefreshUICulture();
            view.RefreshUICulture();

            foreach (AbstractScreen screen in screenList)
                screen.RefreshUICulture();
        }

        /// <summary>
        /// Close the screen manager and its components.
        /// Returns true if the closing was cancelled. This happens when there are unsaved changes and the user cancelled.
        /// </summary>
        public bool CloseSubModules()
        {
            view.Closing = true;

            // Confirm dirty annotations once up front; then tear down without per-screen
            // CloseFile → OrganizeScreens (which made remaining screens relayout N times).
            for (int i = 0; i < screenList.Count; i++)
            {
                if (!(screenList[i] is PlayerScreen))
                    continue;

                if (!BeforeReplacingPlayerContent(i))
                {
                    view.Closing = false;
                    return true;
                }
            }

            while (screenList.Count > 0)
            {
                AbstractScreen screen = screenList[screenList.Count - 1];
                RemoveScreen(screen);
            }

            closedScreensStack.Clear();
            // One visual teardown (Closing short-circuits intermediate rebuilds).
            view.OrganizeScreens(screenList, layoutColumns, layoutRows, false);
            return false;
        }
        public void PreferencesUpdated()
        {
            foreach (AbstractScreen screen in screenList)
                screen.PreferencesUpdated();

            audioInputLevelMonitor.Enabled = PreferencesManager.CapturePreferences.CaptureAutomationConfiguration.EnableAudioTrigger;
            audioInputLevelMonitor.Threshold = PreferencesManager.CapturePreferences.CaptureAutomationConfiguration.AudioTriggerThreshold;

            // We may have changed the preferred audio input device.
            if (captureScreens.Count() > 0 && audioInputLevelMonitor.Enabled)
            {
                string id = PreferencesManager.CapturePreferences.CaptureAutomationConfiguration.AudioInputDevice;
                audioInputLevelMonitor.Start(id);
            }

            RefreshUICulture();
        }
        #endregion
        
        #region Event handlers for screens
        private void Screen_CloseAsked(object sender, EventArgs e)
        {
            AbstractScreen screen = sender as AbstractScreen;
            if (screen == null)
                return;

            // If the screen is in Drawtime filter (e.g: Mosaic), we just go back to normal play.
            if (screen is PlayerScreen && ((PlayerScreen)screen).InteractiveFiltering)
            {
                SetActiveScreen(screen);
                ((PlayerScreen)screen).DeactivateInteractiveEffect();
                return;
            }

            //screen.BeforeClose();

            int index = GetScreenIndex(screen);
            if (index == -1)
                return;

            CloseFile(index);

            AfterSharedBufferChange();
        }
        private void Screen_Activated(object sender, EventArgs e)
        {
            AbstractScreen screen = sender as AbstractScreen;
            SetActiveScreen(screen);
        }
        private void Screen_DualCommandReceived(object sender, EventArgs<HotkeyCommand> e)
        {
            // A screen has received a hotkey that must be handled at manager level.
            if (sender is PlayerScreen && dualPlayer.Active)
                dualPlayer.ExecuteDualCommand(e.Value);
            else if (sender is CaptureScreen && dualCapture.Active)
                dualCapture.ExecuteDualCommand(e.Value);
        }

        private void Player_OpenVideoAsked(object sender, EventArgs e)
        {
            string filename = FilePicker.OpenVideo();
            if (string.IsNullOrEmpty(filename))
                return;

            int index = GetScreenIndex(sender);
            if (index == -1)
                return;

            VideoTypeManager.LoadVideo(filename, index);
        }
        private void Player_VideoPathLoadAsked(object sender, EventArgs<string> e)
        {
            if (e == null || string.IsNullOrEmpty(e.Value))
                return;

            int index = GetScreenIndex(sender);
            if (index == -1)
                return;

            VideoTypeManager.LoadVideo(e.Value, index);
        }
        private void Player_OpenReplayWatcherAsked(object sender, EventArgs e)
        {
            string path = FilePicker.OpenReplayWatcher();
            if (string.IsNullOrEmpty(path))
                return;

            int index = GetScreenIndex(sender);
            if (index == -1)
                return;

            ScreenDescriptionPlayback screenDescription = new ScreenDescriptionPlayback();
            screenDescription.FullPath = path;
            screenDescription.IsReplayWatcher = true;
            screenDescription.Autoplay = true;
            screenDescription.Stretch = true;
            screenDescription.SpeedPercentage = PreferencesManager.PlayerPreferences.DefaultReplaySpeed;
            LoaderVideo.LoadVideoInScreen(this, path, index, screenDescription);
        }
        private void Player_OpenAnnotationsAsked(object sender, EventArgs e)
        {
            int index = GetScreenIndex(sender);
            if (index == -1)
                return;

            LoadAnalysis(index);
        }
        private void Player_SelectionChanged(object sender, EventArgs<bool> e)
        {
            // Soft sync: keep other screens on their current frames after load / working-zone init.
            ResetSync(true);

            dualLaunchSettingsPendingCountdown--;

            if (dualLaunchSettingsPendingCountdown == 0)
                dualPlayer.CommitLaunchSettings();
        }
        private void Player_ResetAsked(object sender, EventArgs e)
        {
            // A screen was reset (ex: a video was reloaded in place).
            // Soft rebind so other Full players are not seeked back to 0.
            ResetSync(true);
        }
        private void Capture_CameraDiscoveryComplete(object sender, EventArgs<string> e)
        {
            // A capture screen has just completed its camera discovery,
            // either by finding and loading the camera or by timeout.
            // Tick off that camera from the list and stop the whole discovery process if we are done.
            if (camerasToDiscover.Contains(e.Value))
                camerasToDiscover.Remove(e.Value);

            if (camerasToDiscover.Count == 0)
                CameraTypeManager.StopDiscoveringCameras();
        }
        #endregion

        #region Common controls event handlers
        private void AddCommonControlsEventHandlers()
        {
            dualPlayer.View.SwapAsked += CCtrl_SwapAsked;
            dualCapture.View.SwapAsked += CCtrl_SwapAsked;
        }
        private void CCtrl_SwapAsked(object sender, EventArgs e)
        {
            mnuSwapScreensOnClick(null, EventArgs.Empty);	
        }
        #endregion
        
        #region Public Methods
        public void SetActiveScreen(AbstractScreen screen)
        {
            if(screen == null)
                return;

            if (screenList.Count == 1 || screen == activeScreen)
            {
                activeScreen = screen;
                dualPlayer.SetReferencePlayer(screen as PlayerScreen);
                OrganizeMenus();
                return;
            }

            foreach (AbstractScreen s in screenList)
                s.DisplayAsActiveScreen(s == screen);
                
            activeScreen = screen;
            dualPlayer.SetReferencePlayer(screen as PlayerScreen);
            OrganizeMenus();
        }
        public void SetAllToInactive()
        {
            foreach (AbstractScreen screen in screenList)
                screen.DisplayAsActiveScreen(false);
        }
        public AbstractScreen GetScreenAt(int index)
        {
            return (index >= 0 && index < screenList.Count) ? screenList[index] : null;
        }

        public bool CanAddScreen()
        {
            return true;
        }

        public bool IsApplyingLaunchScreenDescriptions
        {
            get { return applyingLaunchScreenDescriptions; }
        }

        private int GetScreenIndex(object sender)
        {
            AbstractScreen screen = sender as AbstractScreen;
            return screen == null ? -1 : screenList.IndexOf(screen);
        }
        
        public void RemoveFirstEmpty()
        {
            foreach (AbstractScreen screen in screenList)
            {
                if (screen.Full)
                    continue;

                RemoveScreen(screen);
                break;
            }
            
            AfterRemoveScreen();
        }
        public void RemoveScreen(AbstractScreen screen)
        {
            RemoveScreenEventHandlers(screen);
            
            screen.BeforeClose();
            screenList.Remove(screen);
            screen.AfterClose();
            
            AfterRemoveScreen();
        }
        private void AfterRemoveScreen()
        {
            if (screenList.Count > 0)
                SetActiveScreen(screenList[0]);
            else
                activeScreen = null;

            foreach (PlayerScreen p in playerScreens)
                p.Synched = false;
        }
        
        public void SwapScreens()
        {
            SwapScreens(0, 1);
        }
        public void SwapScreens(int firstIndex, int secondIndex)
        {
            if (firstIndex < 0 || secondIndex < 0 ||
                firstIndex >= screenList.Count || secondIndex >= screenList.Count ||
                firstIndex == secondIndex)
                return;

            AbstractScreen temp = screenList[firstIndex];
            screenList[firstIndex] = screenList[secondIndex];
            screenList[secondIndex] = temp;
        }

        private bool RemoveScreensFromEnd(int count)
        {
            while (screenList.Count > count)
            {
                if (!ScreenRemover.RemoveScreen(this, screenList.Count - 1))
                    return false;
            }

            AfterSharedBufferChange();
            return true;
        }

        public void OrganizeScreens()
        {
            if (view.Closing)
                return;

            SyncLayoutGridToScreenCount();
            view.OrganizeScreens(screenList, layoutColumns, layoutRows, screensSuspended);
            UpdateStatusBar();

            for (int i = 0; i < screenList.Count; i++)
            {
                screenList[i].Identify(i);
                PlayerScreen player = screenList[i] as PlayerScreen;
                if (player != null)
                    player.ApplyControlsLayout(i, layoutColumns, layoutRows);
            }

            if (captureScreens.Count() > 0 && audioInputLevelMonitor.Enabled)
            {
                string id = PreferencesManager.CapturePreferences.CaptureAutomationConfiguration.AudioInputDevice;
                audioInputLevelMonitor.Start(id);
            }
            else
            {
                audioInputLevelMonitor.Stop();
            }
        }

        public void UpdateStatusBar()
        {
            //------------------------------------------------------------------
            // Function called on RefreshUiCulture, CommandShowScreen...
            // and calling upper module (supervisor).
            //------------------------------------------------------------------

            String StatusString = "";

            StatusString = string.Join(" | ", screenList.Select(s => s.Status));

            NotificationCenter.RaiseStatusUpdated(this, StatusString);
        }
        public void OrganizeCommonControls()
        {
            SyncLayoutGridToScreenCount();
            dualPlayer.ScreenListChanged(screenList, layoutColumns, layoutRows);
            dualPlayer.SetReferencePlayer(activeScreen as PlayerScreen);
            dualCapture.ScreenListChanged(screenList);

            bool showPlayers = playerScreens.Count() >= 2;
            bool showCapture = captureScreens.Count() >= 2;
            view.ShowCommonControls(showPlayers, dualPlayer.View, showCapture, dualCapture.View);
            canShowCommonControls = showPlayers || showCapture;

            bool canSwap = screenList.Count == 2;
            dualPlayer.View.SetSwapEnabled(canSwap);
            dualCapture.View.SetSwapEnabled(canSwap);
        }
        public void AfterSharedBufferChange()
        {
            // The screen list has changed and involve capture screens.
            // Update their shared state to trigger a memory buffer reset.
            int captureScreenCount = CaptureScreenCount;
            foreach (CaptureScreen screen in captureScreens)
                screen.SetShared(captureScreenCount);
        }
        public void FullScreen(bool fullScreen)
        {
            foreach (AbstractScreen screen in screenList)
                screen.FullScreen(fullScreen);
        }
        public static void AlertInvalidFileName()
        {
            string msgTitle = ScreenManagerLang.Error_Capture_InvalidFile_Title;
            string msgText = ScreenManagerLang.Error_Capture_InvalidFile_Text.Replace("\\n", "\n");
                
            MessageBox.Show(msgText, msgTitle, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
        public static void AlertDirectoryNotCreated()
        {
            string msgTitle = ScreenManagerLang.Error_Capture_DirectoryNotCreated_Title;
            string msgText = ScreenManagerLang.Error_Capture_DirectoryNotCreated_Text.Replace("\\n", "\n");

            MessageBox.Show(msgText, msgTitle, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
        
        public Workspace ExtractWorkspace()
        {
            Workspace workspace = new Workspace();
            workspace.Columns = layoutColumns;
            workspace.Rows = layoutRows;
            foreach (var screen in screenList)
                workspace.Screens.Add(screen.GetScreenDescription());

            return workspace;
        }
        #endregion
        
        #region Menu organization
        public void OrganizeMenus()
        {
            DoOrganizeMenu();
        }
        private void BuildSvgMenu()
        {
            mnuSVGTools.Image = Properties.Resources.images;
            mnuSVGTools.MergeAction = MergeAction.Append;
            mnuImportImage.Image = Properties.Resources.image;
            mnuImportImage.Click += new EventHandler(mnuImportImage_OnClick);
            mnuImportImage.MergeAction = MergeAction.Append;
            AddImportImageMenu(mnuSVGTools);
            
            AddSvgSubMenus(svgPath, mnuSVGTools);
        }
        private void AddImportImageMenu(ToolStripMenuItem menu)
        {
            menu.DropDownItems.Add(mnuImportImage);
            menu.DropDownItems.Add(new ToolStripSeparator());
        }
        private void AddSvgSubMenus(string dir, ToolStripMenuItem menu)
        {
            // This is a recursive function that browses a directory and its sub directories,
            // each directory is made into a menu tree, each svg file is added as a menu leaf.
            if (!Directory.Exists(dir))
                return;
            
            buildingSVGMenu = true;

            // Loop sub directories.
            string[] subDirs = Directory.GetDirectories (dir);
            foreach (string subDir in subDirs)
            {
                // Create a menu
                ToolStripMenuItem mnuSubDir = new ToolStripMenuItem();
                mnuSubDir.Text = Path.GetFileName(subDir);
                mnuSubDir.Image = Properties.Resources.folder;
                mnuSubDir.MergeAction = MergeAction.Append;
                    
                // Build sub tree.
                AddSvgSubMenus(subDir, mnuSubDir);
                    
                // Add to parent if non-empty.
                if(mnuSubDir.HasDropDownItems)
                    menu.DropDownItems.Add(mnuSubDir);
            }

            // Then loop files within the sub directory.
            foreach (string file in Directory.GetFiles(dir))
            {
                if (!Path.GetExtension(file).ToLower().Equals(".svg"))
                    continue;
                
                hasSvgFiles = true;
                        
                // Create a menu. 
                ToolStripMenuItem mnuSVGDrawing = new ToolStripMenuItem();
                mnuSVGDrawing.Text = Path.GetFileNameWithoutExtension(file);
                mnuSVGDrawing.Tag = file;
                mnuSVGDrawing.Image = Properties.Resources.vector;
                mnuSVGDrawing.Click += new EventHandler(mnuSVGDrawing_OnClick);
                mnuSVGDrawing.MergeAction = MergeAction.Append;
                        
                // Add to parent.
                menu.DropDownItems.Add(mnuSVGDrawing);
            }
                    
            buildingSVGMenu = false;
        }
        private void DoOrganizeMenu()
        {
            // Enable / disable menus depending on state of active screen
            // and global screen configuration.
            
            #region Menus depending only on the state of the active screen
            bool activeScreenIsEmpty = false;
            if (activeScreen != null && screenList.Count > 0)
            {
                if(!activeScreen.Full)
                {
                    activeScreenIsEmpty = true;
                }
                else if (activeScreen is PlayerScreen)
                {
                    PlayerScreen player = activeScreen as PlayerScreen;
                    
                    // 1. Video is loaded : save-able and analysis is loadable.
                    
                    // File
                    mnuSave.Enabled = true;
                    mnuSaveAs.Enabled = true;
                    mnuExportVideo.Enabled = true;
                    toolSave.Enabled = true;
                    mnuExportSpreadsheet.Enabled = player.FrameServer.Metadata.HasData;
                    mnuExportODF.Enabled = player.FrameServer.Metadata.HasData;
                    mnuExportMSXML.Enabled = player.FrameServer.Metadata.HasData;
                    mnuExportXHTML.Enabled = player.FrameServer.Metadata.HasData;
                    mnuExportTEXT.Enabled = player.FrameServer.Metadata.HasTrack;
                    mnuLoadAnalysis.Enabled = true;
                    
                    // Edit
                    HistoryMenuManager.SwitchContext(player.HistoryStack);
                    ConfigureClipboardMenus(player);

                    // Image
                    mnuDeinterlace.Enabled = player.FrameServer.VideoReader.CanChangeDeinterlacing;
                    mnuMirror.Enabled = true;
                    mnuDeinterlace.Checked = player.Deinterlaced;
                    mnuMirror.Checked = player.Mirrored;
                    if (!player.IsSingleFrame)
                    {
                        ConfigureImageFormatMenus(player);
                        ConfigureImageRotationMenus(player);
                        ConfigureImageDemosaicingMenus(player);
                    }
                    else
                    {
                        ConfigureImageFormatMenus(null);
                        ConfigureImageRotationMenus(null);
                        ConfigureImageDemosaicingMenus(null);
                    }

                    // Video
                    mnuTimebase.Enabled = true;
                    ConfigureVideoFilterMenus(player);

                    // Tools
                    mnuSVGTools.Enabled = hasSvgFiles;
                    mnuTestGrid.Enabled = false;
                    mnuCoordinateAxis.Enabled = true;
                    mnuCoordinateAxis.Checked = player.FrameServer.Metadata.DrawingCoordinateSystem.Visible;
                    mnuCameraCalibration.Enabled = true;
                    mnuTrajectoryAnalysis.Enabled = true;
                    mnuScatterDiagram.Enabled = true;
                    mnuAngularAnalysis.Enabled = true;
                    mnuAngleAngleAnalysis.Enabled = true;
                    
                }
                else if(activeScreen is CaptureScreen)
                {
                    CaptureScreen captureScreen = activeScreen as CaptureScreen;   
                    
                    // File
                    mnuSave.Enabled = false;
                    mnuSaveAs.Enabled = false;
                    mnuExportVideo.Enabled = false;
                    toolSave.Enabled = false;
                    mnuExportSpreadsheet.Enabled = false;
                    mnuExportODF.Enabled = false;
                    mnuExportMSXML.Enabled = false;
                    mnuExportXHTML.Enabled = false;
                    mnuExportTEXT.Enabled = false;
                    mnuLoadAnalysis.Enabled = true;

                    // Edit
                    HistoryMenuManager.SwitchContext(captureScreen.HistoryStack);
                    ConfigureClipboardMenus(activeScreen);

                    // Image
                    mnuDeinterlace.Enabled = false;
                    mnuMirror.Enabled = true;
                    mnuDeinterlace.Checked = false;
                    mnuMirror.Checked = captureScreen.Mirrored;
                    ConfigureImageFormatMenus(captureScreen);
                    ConfigureImageRotationMenus(captureScreen);
                    ConfigureImageDemosaicingMenus(captureScreen);

                    // Video
                    mnuTimebase.Enabled = false;
                    ConfigureVideoFilterMenus(null);

                    // Tools
                    mnuSVGTools.Enabled = false;
                    mnuTestGrid.Enabled = true;
                    mnuTestGrid.Checked = captureScreen.TestGridVisible;
                    mnuCoordinateAxis.Enabled = false;
                    mnuCameraCalibration.Enabled = false;
                    mnuTrajectoryAnalysis.Enabled = false;
                    mnuScatterDiagram.Enabled = false;
                    mnuAngularAnalysis.Enabled = false;
                    mnuAngleAngleAnalysis.Enabled = false;
                }
                else
                {
                    // KO ?
                    activeScreenIsEmpty = true;
                }
            }
            else
            {
                // No active screen. ( = no screens)
                activeScreenIsEmpty = true;
            }

            if (activeScreenIsEmpty)
            {
                // File
                mnuSave.Enabled = false;
                mnuSaveAs.Enabled = false;
                mnuExportVideo.Enabled = false;
                toolSave.Enabled = false;
                mnuLoadAnalysis.Enabled = false;
                mnuExportSpreadsheet.Enabled = false;
                mnuExportODF.Enabled = false;
                mnuExportMSXML.Enabled = false;
                mnuExportXHTML.Enabled = false;
                mnuExportTEXT.Enabled = false;

                // Edit
                HistoryMenuManager.SwitchContext(null);
                ConfigureClipboardMenus(null);

                // Image
                mnuDeinterlace.Enabled = false;
                mnuMirror.Enabled = false;
                mnuDeinterlace.Checked = false;
                mnuMirror.Checked = false;
                ConfigureImageFormatMenus(null);
                ConfigureImageRotationMenus(null);
                ConfigureImageDemosaicingMenus(null);
                
                // Video
                mnuTimebase.Enabled = false;
                ConfigureVideoFilterMenus(null);

                // Tools
                mnuSVGTools.Enabled = false;
                mnuTestGrid.Enabled = false;
                mnuTestGrid.Checked = false;
                mnuCoordinateAxis.Enabled = false;
                mnuCoordinateAxis.Checked = false;
                mnuCameraCalibration.Enabled = false;
                mnuTrajectoryAnalysis.Enabled = false;
                mnuScatterDiagram.Enabled = false;
                mnuAngularAnalysis.Enabled = false;
                mnuAngleAngleAnalysis.Enabled = false;
            }
            #endregion

            #region Menus depending on the specifc screen configuration
            // File
            ToolStripMenuItem[] closeFileMenus = new ToolStripMenuItem[] { mnuCloseFile, mnuCloseFile2, mnuCloseFile3, mnuCloseFile4 };
            foreach (ToolStripMenuItem menu in closeFileMenus)
            {
                menu.Visible = false;
                menu.Enabled = false;
            }

            string strClosingText = ScreenManagerLang.Generic_Close;
            
            mnuSwapScreens.Enabled = screenList.Count == 2;
            mnuToggleCommonCtrls.Enabled = canShowCommonControls;

            bool canInsertScreen = screenList.Count > 0;
            mnuInsertScreenRight.Enabled = canInsertScreen;
            toolInsertScreenRight.Enabled = canInsertScreen;

            bool canRestorePeak = peakScreenSnapshot != null &&
                peakScreenSnapshot.Slots.Count > screenList.Count;
            mnuRestorePeakScreens.Enabled = canRestorePeak;
            toolRestorePeakScreens.Enabled = canRestorePeak;

            bool canRestoreClosed = closedScreensStack.Count > 0;
            mnuRestoreLastClosedScreen.Enabled = canRestoreClosed;
            toolRestoreLastClosedScreen.Enabled = canRestoreClosed;

            bool allScreensAreEmpty = screenList.Count == 0 || screenList.All(screen => !screen.Full);
            int closeMenuIndex = 0;
            for (int i = 0; i < screenList.Count && closeMenuIndex < closeFileMenus.Length; i++)
            {
                PlayerScreen player = screenList[i] as PlayerScreen;
                if (player == null || !player.Full)
                    continue;

                ToolStripMenuItem menu = closeFileMenus[closeMenuIndex];
                menu.Text = strClosingText + " - " + player.FileName;
                menu.Tag = i;
                menu.Enabled = true;
                menu.Visible = true;
                closeMenuIndex++;
            }

            if (allScreensAreEmpty || closeMenuIndex == 0)
            {
                // No screens at all, or all screens empty => 1 menu visible but disabled.

                mnuCloseFile.Text = strClosingText;
                mnuCloseFile.Visible = true;
                mnuCloseFile.Enabled = false;
            }
            #endregion
        }
        private void ConfigureVideoFilterMenus(PlayerScreen player)
        {
            bool hasVideo = player != null && player.Full;
            foreach(ToolStripMenuItem menu in filterMenus)
            {
                AbstractVideoFilter filter = menu.Tag as AbstractVideoFilter;
                if(filter == null)
                    continue;
                
                menu.Visible = filter.Experimental ? Software.Experimental : true;
                menu.Enabled = hasVideo ? player.IsCaching : false;
            }
        }
        private void ConfigureImageFormatMenus(AbstractScreen screen)
        {
            // Set the enable and check prop of the image formats menu according of current screen state.
            bool canChangeAspectRatio = screen != null && screen.Full && screen is PlayerScreen && ((PlayerScreen)screen).FrameServer.VideoReader.CanChangeAspectRatio;
            mnuFormat.Enabled = canChangeAspectRatio;
            mnuFormatAuto.Enabled = canChangeAspectRatio;
            mnuFormatForce43.Enabled = canChangeAspectRatio;
            mnuFormatForce169.Enabled = canChangeAspectRatio;

            if (!canChangeAspectRatio)
                return;

            if (!canChangeAspectRatio)
            
            // Reset all checks before setting the right one.
            mnuFormatAuto.Checked = screen.AspectRatio == ImageAspectRatio.Auto;
            mnuFormatForce43.Checked = screen.AspectRatio == ImageAspectRatio.Force43;
            mnuFormatForce169.Checked = screen.AspectRatio == ImageAspectRatio.Force169;
        }
        private void ConfigureImageDemosaicingMenus(AbstractScreen screen)
        {
            bool canChangeDemosaicing = screen != null && screen.Full && screen is PlayerScreen && ((PlayerScreen)screen).FrameServer.VideoReader.CanChangeDemosaicing;
            mnuDemosaic.Enabled = canChangeDemosaicing;
            mnuDemosaicNone.Enabled = canChangeDemosaicing;
            mnuDemosaicRGGB.Enabled = canChangeDemosaicing;
            mnuDemosaicBGGR.Enabled = canChangeDemosaicing;
            mnuDemosaicGRBG.Enabled = canChangeDemosaicing;
            mnuDemosaicGBRG.Enabled = canChangeDemosaicing;

            if (!canChangeDemosaicing)
                return;
            
            mnuDemosaicNone.Checked = screen.Demosaicing == Demosaicing.None;
            mnuDemosaicRGGB.Checked = screen.Demosaicing == Demosaicing.RGGB;
            mnuDemosaicBGGR.Checked = screen.Demosaicing == Demosaicing.BGGR;
            mnuDemosaicGRBG.Checked = screen.Demosaicing == Demosaicing.GRBG;
            mnuDemosaicGBRG.Checked = screen.Demosaicing == Demosaicing.GBRG;
        }
        private void ConfigureImageRotationMenus(AbstractScreen screen)
        {
            bool screenIsFull = screen != null && screen.Full;
            bool playerCanChangeRotation = screenIsFull && screen is PlayerScreen && ((PlayerScreen)screen).FrameServer.VideoReader.CanChangeImageRotation;
            bool canChangeImageRotation = screenIsFull && (screen is CaptureScreen || playerCanChangeRotation);
            mnuRotation.Enabled = canChangeImageRotation;
            mnuRotation0.Enabled = canChangeImageRotation;
            mnuRotation90.Enabled = canChangeImageRotation;
            mnuRotation180.Enabled = canChangeImageRotation;
            mnuRotation270.Enabled = canChangeImageRotation;

            if (!canChangeImageRotation)
                return;

            mnuRotation0.Checked = screen.ImageRotation == ImageRotation.Rotate0;
            mnuRotation90.Checked = screen.ImageRotation == ImageRotation.Rotate90;
            mnuRotation180.Checked = screen.ImageRotation == ImageRotation.Rotate180;
            mnuRotation270.Checked = screen.ImageRotation == ImageRotation.Rotate270;
        }
        private void OnSVGFilesChanged(object source, FileSystemEventArgs e)
        {
            // We are in the file watcher thread. NO direct UI Calls from here.
            log.Debug(String.Format("Action recorded in the guides directory: {0}", e.ChangeType));
            if(!buildingSVGMenu)
            {
                buildingSVGMenu = true;
                // Use "view" object just to merge back into the UI thread.
                view.BeginInvoke((MethodInvoker) delegate {DoSVGFilesChanged();});
            }
        }
        public void DoSVGFilesChanged()
        {
            mnuSVGTools.DropDownItems.Clear();
            AddImportImageMenu(mnuSVGTools);
            AddSvgSubMenus(svgPath, mnuSVGTools);
        }
        private void ConfigureClipboardMenus(AbstractScreen screen)
        {
            if (screen is PlayerScreen)
            {
                PlayerScreen player = screen as PlayerScreen;
                bool canCutOrCopy = player.FrameServer.Metadata.HitDrawing != null && player.FrameServer.Metadata.HitDrawing.IsCopyPasteable;
                mnuCutDrawing.Enabled = canCutOrCopy;
                mnuCopyDrawing.Enabled = canCutOrCopy;
                if (!canCutOrCopy)
                {
                    mnuCutDrawing.Text = ScreenManagerLang.mnuCutDrawing;
                    mnuCopyDrawing.Text = ScreenManagerLang.mnuCopyDrawing;
                }
                else
                {
                    mnuCutDrawing.Text = string.Format("{0} ({1})", ScreenManagerLang.mnuCutDrawing, player.FrameServer.Metadata.HitDrawing.Name);
                    mnuCopyDrawing.Text = string.Format("{0} ({1})", ScreenManagerLang.mnuCopyDrawing, player.FrameServer.Metadata.HitDrawing.Name);
                }
                
                mnuPasteDrawing.Enabled = DrawingClipboard.HasContent;
                if (DrawingClipboard.HasContent)
                {
                    mnuPasteDrawing.Text = string.Format("{0} ({1})", ScreenManagerLang.mnuPasteDrawing, DrawingClipboard.Name);
                }
            }
            else
            {
                mnuCutDrawing.Enabled = false;
                mnuCopyDrawing.Enabled = false;
                mnuPasteDrawing.Enabled = false;
            }
        }
        #endregion

        #region Culture
        private void RefreshCultureToolbar()
        {
            toolSave.ToolTipText = ScreenManagerLang.Generic_SaveKVA;
            toolHome.ToolTipText = ScreenManagerLang.mnuHome;
            toolOnePlayer.ToolTipText = ScreenManagerLang.mnuOnePlayer;
            toolTwoPlayers.ToolTipText = ScreenManagerLang.mnuTwoPlayers;
            toolThreePlayers.ToolTipText = ScreenManagerLang.mnuThreePlayers;
            toolFourPlayers.ToolTipText = ScreenManagerLang.mnuFourPlayers;
            toolFourPlayersRow.ToolTipText = ScreenManagerLang.mnuFourPlayersRow;
            toolInsertScreenRight.ToolTipText = ScreenManagerLang.mnuInsertScreenRight;
            toolRestorePeakScreens.ToolTipText = ScreenManagerLang.mnuRestorePeakScreens;
            toolRestoreLastClosedScreen.ToolTipText = ScreenManagerLang.mnuRestoreLastClosedScreen;
            toolOneCapture.ToolTipText = ScreenManagerLang.mnuOneCapture;
            toolTwoCaptures.ToolTipText = ScreenManagerLang.mnuTwoCaptures;
            toolTwoMixed.ToolTipText = ScreenManagerLang.mnuTwoMixed;	
        }
        private void RefreshCultureMenu()
        {
            // File
            mnuCloseFile.Text = ScreenManagerLang.Generic_Close;
            mnuCloseFile2.Text = ScreenManagerLang.Generic_Close;
            mnuCloseFile3.Text = ScreenManagerLang.Generic_Close;
            mnuCloseFile4.Text = ScreenManagerLang.Generic_Close;
            mnuSave.Text = ScreenManagerLang.Generic_SaveKVA;
            mnuSaveAs.Text = ScreenManagerLang.Generic_SaveKVAAs;
            mnuExportVideo.Text = ScreenManagerLang.Generic_ExportVideo;
            mnuExportSpreadsheet.Text = ScreenManagerLang.mnuExportSpreadsheet;
            mnuExportODF.Text = "LibreOffice (.odf)";
            mnuExportMSXML.Text = "Microsoft Excel (.xml)";
            mnuExportXHTML.Text = "Web (.html)";
            mnuExportTEXT.Text = "Gnuplot (.txt)";
            mnuLoadAnalysis.Text = ScreenManagerLang.mnuLoadAnalysis;

            // Edit
            mnuCutDrawing.Text = ScreenManagerLang.mnuCutDrawing;
            mnuCopyDrawing.Text = ScreenManagerLang.mnuCopyDrawing;
            mnuPasteDrawing.Text = ScreenManagerLang.mnuPasteDrawing;
            mnuCutDrawing.ShortcutKeys = HotkeySettingsManager.GetMenuShortcut("PlayerScreen", (int)PlayerScreenCommands.CutDrawing);
            mnuCopyDrawing.ShortcutKeys = HotkeySettingsManager.GetMenuShortcut("PlayerScreen", (int)PlayerScreenCommands.CopyDrawing);
            mnuPasteDrawing.ShortcutKeys = HotkeySettingsManager.GetMenuShortcut("PlayerScreen", (int)PlayerScreenCommands.PasteDrawing);
            
            // View
            mnuOnePlayer.Text = ScreenManagerLang.mnuOnePlayer;
            mnuTwoPlayers.Text = ScreenManagerLang.mnuTwoPlayers;
            mnuThreePlayers.Text = ScreenManagerLang.mnuThreePlayers;
            mnuFourPlayers.Text = ScreenManagerLang.mnuFourPlayers;
            mnuFourPlayersRow.Text = ScreenManagerLang.mnuFourPlayersRow;
            mnuInsertScreenRight.Text = ScreenManagerLang.mnuInsertScreenRight;
            mnuRestorePeakScreens.Text = ScreenManagerLang.mnuRestorePeakScreens;
            mnuRestoreLastClosedScreen.Text = ScreenManagerLang.mnuRestoreLastClosedScreen;
            mnuOneCapture.Text = ScreenManagerLang.mnuOneCapture;
            mnuTwoCaptures.Text = ScreenManagerLang.mnuTwoCaptures;
            mnuTwoMixed.Text = ScreenManagerLang.mnuTwoMixed;
            mnuConfigureScreens.Text = ScreenManagerLang.mnuConfigureScreens;
            mnuSwapScreens.Text = ScreenManagerLang.mnuSwapScreens;
            mnuToggleCommonCtrls.Text = ScreenManagerLang.mnuToggleCommonCtrls;
            
            // Image
            mnuDeinterlace.Text = ScreenManagerLang.mnuDeinterlace;
            mnuFormatAuto.Text = ScreenManagerLang.mnuFormatAuto;
            mnuFormatForce43.Text = ScreenManagerLang.mnuFormatForce43;
            mnuFormatForce169.Text = ScreenManagerLang.mnuFormatForce169;
            mnuFormat.Text = ScreenManagerLang.mnuFormat;
            mnuDemosaicNone.Text = "None";
            mnuDemosaicRGGB.Text = "RGGB";
            mnuDemosaicBGGR.Text = "BGGR";
            mnuDemosaicGRBG.Text = "GRBG";
            mnuDemosaicGBRG.Text = "GBRG";
            mnuDemosaic.Text = "Demosaicing";
            mnuRotation0.Text = ScreenManagerLang.mnuRotation0;
            mnuRotation90.Text = ScreenManagerLang.mnuRotation90;
            mnuRotation180.Text = ScreenManagerLang.mnuRotation180;
            mnuRotation270.Text = ScreenManagerLang.mnuRotation270;
            mnuRotation.Text = ScreenManagerLang.mnuRotation;
            mnuMirror.Text = ScreenManagerLang.mnuMirror;
            RefreshCultureMenuFilters();

            // Video
            mnuTimebase.Text = ScreenManagerLang.mnuTimebase;

            // Tools
            mnuSVGTools.Text = ScreenManagerLang.mnuSVGTools;
            mnuImportImage.Text = ScreenManagerLang.mnuImportImage;
            mnuTestGrid.Text = ScreenManagerLang.DrawingName_TestGrid;
            mnuCoordinateAxis.Text = ScreenManagerLang.mnuCoordinateSystem;
            mnuCameraCalibration.Text = ScreenManagerLang.dlgCameraCalibration_Title + "...";
            mnuScatterDiagram.Text = ScreenManagerLang.DataAnalysis_ScatterDiagram + "...";
            mnuTrajectoryAnalysis.Text = ScreenManagerLang.DataAnalysis_LinearKinematics + "...";
            mnuAngularAnalysis.Text = ScreenManagerLang.DataAnalysis_AngularKinematics + "...";
            mnuAngleAngleAnalysis.Text = ScreenManagerLang.DataAnalysis_AngleAngleDiagrams + "...";
        }
            
        private void RefreshCultureMenuFilters()
        {
            foreach(ToolStripMenuItem menu in filterMenus)
            {
                AbstractVideoFilter filter = menu.Tag as AbstractVideoFilter;
                if(filter != null)
                    menu.Text = filter.Name;
            }
        }
                
        #endregion
             
        #region Menus events handlers

        #region File
        private void mnuCloseFileOnClick(object sender, EventArgs e)
        {
            CloseFileFromMenu(sender, 0);
        }
        private void mnuCloseFile2OnClick(object sender, EventArgs e)
        {
            CloseFileFromMenu(sender, 1);
        }
        private void mnuCloseFile3OnClick(object sender, EventArgs e)
        {
            CloseFileFromMenu(sender, 2);
        }
        private void mnuCloseFile4OnClick(object sender, EventArgs e)
        {
            CloseFileFromMenu(sender, 3);
        }
        private void CloseFileFromMenu(object sender, int defaultIndex)
        {
            ToolStripMenuItem menu = sender as ToolStripMenuItem;
            int screenIndex = menu != null && menu.Tag is int ? (int)menu.Tag : defaultIndex;
            CloseFile(screenIndex);
        }
        private void CloseFile(int screenIndex)
        {
            AbstractScreen screen = GetScreenAt(screenIndex);
            SessionScreenSnapshot.Slot closedSlot = null;
            if (screen != null)
            {
                string cacheDirectory = Path.Combine(Software.TempDirectory, "session-closed");
                try
                {
                    if (!Directory.Exists(cacheDirectory))
                        Directory.CreateDirectory(cacheDirectory);
                }
                catch
                {
                }

                closedSlot = CreateSessionSlot(screenIndex, screen, cacheDirectory);
            }

            ShiftLayoutSlotCacheAfterClose(screenIndex);
            if (!ScreenRemover.RemoveScreen(this, screenIndex))
            {
                SessionScreenSnapshot.DeleteAnnotationFile(closedSlot);
                return;
            }

            if (closedSlot != null)
            {
                closedScreensStack.Push(closedSlot);
                if (closedScreensStack.Count > MaxClosedScreensStack)
                {
                    List<SessionScreenSnapshot.Slot> newest = closedScreensStack.Take(MaxClosedScreensStack).ToList();
                    closedScreensStack.Clear();
                    for (int i = newest.Count - 1; i >= 0; i--)
                        closedScreensStack.Push(newest[i]);
                }
            }

            AfterSharedBufferChange();
            OrganizeScreens();
            OrganizeCommonControls();
            OrganizeMenus();
        }
        private void mnuSaveOnClick(object sender, EventArgs e)
        {
            PlayerScreen player = activeScreen as PlayerScreen;
            if (player == null)
                return;

            DoStopPlaying();
            player.Save();
        }

        private void mnuSaveAsOnClick(object sender, EventArgs e)
        {
            PlayerScreen player = activeScreen as PlayerScreen;
            if (player == null)
                return;

            DoStopPlaying();
            player.SaveAs();
        }

        private void mnuExportVideoOnClick(object sender, EventArgs e)
        {
            PlayerScreen player = activeScreen as PlayerScreen;
            if (player == null)
                return;

            DoStopPlaying();
            player.ExportVideo();
        }

        private void mnuLoadAnalysisOnClick(object sender, EventArgs e)
        {
            if (activeScreen != null)
            {
                int index = GetScreenIndex(activeScreen);
                if (index == -1)
                    return;

                LoadAnalysis(index);
            }
        }
        private void LoadAnalysis(int targetScreen)
        {
            AbstractScreen screen = GetScreenAt(targetScreen);
            if (screen == null)
                return;

            if (screen is PlayerScreen)
                DoStopPlaying();
             
            string filename = FilePicker.OpenAnnotations();
            if (filename == null)
                return;

            screen.LoadKVA(filename);
        }
        private void mnuExportODF_OnClick(object sender, EventArgs e)
        {
            ExportSpreadsheet(MetadataExportFormat.ODF);
        }
        private void mnuExportMSXML_OnClick(object sender, EventArgs e)
        {
            ExportSpreadsheet(MetadataExportFormat.MSXML);	
        }
        private void mnuExportXHTML_OnClick(object sender, EventArgs e)
        {
            ExportSpreadsheet(MetadataExportFormat.XHTML);
        }
        private void mnuExportTEXT_OnClick(object sender, EventArgs e)
        {
            ExportSpreadsheet(MetadataExportFormat.TrajectoryText);
        }
        private void ExportSpreadsheet(MetadataExportFormat format)
        {
            PlayerScreen player = activeScreen as PlayerScreen;
            if (player == null || !player.FrameServer.Metadata.HasData)
                return;
            
            DoStopPlaying();    

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = ScreenManagerLang.dlgExportSpreadsheet_Title;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.Filter = "LibreOffice (*.ods)|*.ods|Microsoft Excel (*.xml)|*.xml|Web (*.html)|*.html|Gnuplot (*.txt)|*.txt";
            saveFileDialog.FilterIndex = ((int)format) + 1;
                        
            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(player.FrameServer.Metadata.VideoPath);

            if (saveFileDialog.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(saveFileDialog.FileName))
                return;

            MetadataExporter.Export(player.FrameServer.Metadata, saveFileDialog.FileName, format);
        }
        #endregion

        #region Edit
        private void mnuCutDrawing_OnClick(object sender, EventArgs e)
        {
            if (activeScreen is PlayerScreen)
            {
                PlayerScreen player = activeScreen as PlayerScreen;
                player.ExecuteScreenCommand((int)PlayerScreenCommands.CutDrawing);
            }
            else if (activeScreen is CaptureScreen)
            {
                //CaptureScreen captureScreen = activeScreen as CaptureScreen;
                //captureScreen.ExecuteScreenCommand((int)CaptureScreenCommands.CutDrawing);
            }
        }
        private void mnuCopyDrawing_OnClick(object sender, EventArgs e)
        {
            if (activeScreen is PlayerScreen)
            {
                PlayerScreen player = activeScreen as PlayerScreen;
                player.ExecuteScreenCommand((int)PlayerScreenCommands.CopyDrawing);
            }
        }
        private void mnuPasteDrawing_OnClick(object sender, EventArgs e)
        {
            if (activeScreen is PlayerScreen)
            {
                PlayerScreen player = activeScreen as PlayerScreen;
                player.ExecuteScreenCommand((int)PlayerScreenCommands.PasteInPlaceDrawing);
            }
        }
        #endregion

        #region View
        private void mnuHome_OnClick(object sender, EventArgs e)
        {
            if (screensSuspended)
            {
                LeaveBrowserMode();
                return;
            }

            if (screenList.Count <= 0)
                return;

            EnterBrowserMode();
        }

        private void EnterBrowserMode()
        {
            DoStopPlaying();
            screensSuspended = true;
            OrganizeScreens();
            OrganizeCommonControls();
            OrganizeMenus();
        }

        private void LeaveBrowserMode()
        {
            if (!screensSuspended)
                return;

            screensSuspended = false;
            OrganizeScreens();
            OrganizeCommonControls();
            OrganizeMenus();
        }

        /// <summary>
        /// When opening a file from the Home browser while screens are suspended,
        /// discard the suspended session and start fresh.
        /// </summary>
        private bool DiscardSuspendedScreensForNewFile()
        {
            if (!screensSuspended)
                return true;

            screensSuspended = false;
            while (screenList.Count > 0)
            {
                if (!ScreenRemover.RemoveScreen(this, 0))
                {
                    // User cancelled a dirty save — stay in browser with remaining screens suspended.
                    screensSuspended = screenList.Count > 0;
                    OrganizeScreens();
                    OrganizeCommonControls();
                    OrganizeMenus();
                    return false;
                }
            }

            ClearLayoutSlotCache();
            return true;
        }
        private void mnuOnePlayerOnClick(object sender, EventArgs e)
        {
            ApplyLayout(ScreenLayoutSpec.Playback(1));
        }
        private void mnuTwoPlayersOnClick(object sender, EventArgs e)
        {
            ApplyLayout(ScreenLayoutSpec.Playback(2));
        }
        private void mnuOneCaptureOnClick(object sender, EventArgs e)
        {
            ApplyLayout(ScreenLayoutSpec.Capture(1));
        }
        private void mnuTwoCapturesOnClick(object sender, EventArgs e)
        {
            ApplyLayout(ScreenLayoutSpec.Capture(2));
        }
        private void mnuTwoMixedOnClick(object sender, EventArgs e)
        {
            ApplyLayout(new ScreenLayoutSpec(new ScreenType[] { ScreenType.Capture, ScreenType.Playback }));
        }
        private void mnuSwapScreensOnClick(object sender, EventArgs e)
        {
            if (screenList.Count != 2)
                return;

            // Do not call OrganizeCommonControls here: ScreenListChanged would Exit/Enter
            // and clear an active sync group. SwapSync keeps sync and updates hairlines/slots.
            SwapScreens();
            OrganizeScreens();
            OrganizeMenus();
            UpdateStatusBar();
            
            dualPlayer.SwapSync();
        }
        private void mnuToggleCommonCtrlsOnClick(object sender, EventArgs e)
        {
            view.ToggleCommonControls();

            // Reset synchronization. 
            // This will allow the shortcuts to only be routed to the active screen if the dual controls aren't visible.
            ResetSync();
        }
        #endregion

        #region Image
        private void mnuDeinterlaceOnClick(object sender, EventArgs e)
        {
            PlayerScreen player = activeScreen as PlayerScreen;
            if(player != null)
            {
                mnuDeinterlace.Checked = !mnuDeinterlace.Checked;
                player.Deinterlaced = mnuDeinterlace.Checked;	
            }
        }
        private void mnuFormatAutoOnClick(object sender, EventArgs e)
        {
            ChangeAspectRatio(ImageAspectRatio.Auto);
        }
        private void mnuFormatForce43OnClick(object sender, EventArgs e)
        {
            ChangeAspectRatio(ImageAspectRatio.Force43);
        }
        private void mnuFormatForce169OnClick(object sender, EventArgs e)
        {
            ChangeAspectRatio(ImageAspectRatio.Force169);
        }      
        private void ChangeAspectRatio(ImageAspectRatio aspect)
        {
            if(activeScreen == null)
                return;
        
            if(activeScreen.AspectRatio != aspect)
                activeScreen.AspectRatio = aspect;
            
            mnuFormatForce43.Checked = aspect == ImageAspectRatio.Force43;
            mnuFormatForce169.Checked = aspect == ImageAspectRatio.Force169;
            mnuFormatAuto.Checked = aspect == ImageAspectRatio.Auto;
        }
        private void mnuDemosaicNone_Click(object sender, EventArgs e)
        {
            ChangeDemosaic(Demosaicing.None);
        }
        private void mnuDemosaicRGGB_Click(object sender, EventArgs e)
        {
            ChangeDemosaic(Demosaicing.RGGB);
        }
        private void mnuDemosaicBGGR_Click(object sender, EventArgs e)
        {
            ChangeDemosaic(Demosaicing.BGGR);
        }
        private void mnuDemosaicGRBG_Click(object sender, EventArgs e)
        {
            ChangeDemosaic(Demosaicing.GRBG);
        }
        private void mnuDemosaicGBRG_Click(object sender, EventArgs e)
        {
            ChangeDemosaic(Demosaicing.GBRG);
        }
        private void ChangeDemosaic(Demosaicing demosaic)
        {
            if (activeScreen == null)
                return;

            if (activeScreen.Demosaicing != demosaic)
                activeScreen.Demosaicing = demosaic;
            
            mnuDemosaicNone.Checked = activeScreen.Demosaicing == Demosaicing.None;
            mnuDemosaicRGGB.Checked = activeScreen.Demosaicing == Demosaicing.RGGB;
            mnuDemosaicBGGR.Checked = activeScreen.Demosaicing == Demosaicing.BGGR;
            mnuDemosaicGRBG.Checked = activeScreen.Demosaicing == Demosaicing.GRBG;
            mnuDemosaicGBRG.Checked = activeScreen.Demosaicing == Demosaicing.GBRG;
        }
        private void mnuRotation0_Click(object sender, EventArgs e)
        {
            ChangeImageRotation(ImageRotation.Rotate0);
        }
        private void mnuRotation90_Click(object sender, EventArgs e)
        {
            ChangeImageRotation(ImageRotation.Rotate90);
        }
        private void mnuRotation180_Click(object sender, EventArgs e)
        {
            ChangeImageRotation(ImageRotation.Rotate180);
        }
        private void mnuRotation270_Click(object sender, EventArgs e)
        {
            ChangeImageRotation(ImageRotation.Rotate270);
        }
        private void ChangeImageRotation(ImageRotation rot)
        {
            if (activeScreen == null)
                return;

            if (activeScreen.ImageRotation != rot)
                activeScreen.ImageRotation = rot;

            mnuRotation0.Checked = rot == ImageRotation.Rotate0;
            mnuRotation90.Checked = rot == ImageRotation.Rotate90;
            mnuRotation180.Checked = rot == ImageRotation.Rotate180;
            mnuRotation270.Checked = rot == ImageRotation.Rotate270;
        }
        private void mnuMirrorOnClick(object sender, EventArgs e)
        {
            if (activeScreen == null)
                return;

            mnuMirror.Checked = !mnuMirror.Checked;
            activeScreen.Mirrored = mnuMirror.Checked;
        }
        private void mnuImportImage_OnClick(object sender, EventArgs e)
        {
            if(activeScreen == null || !activeScreen.CapabilityDrawings)
                return;
            
            // Display file open dialog and launch the drawing.
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = ScreenManagerLang.dlgImportReference_Title;
            openFileDialog.Filter = FilesystemHelper.OpenImageFilter(ScreenManagerLang.FileFilter_AllSupported);
            openFileDialog.FilterIndex = 1;

            if (openFileDialog.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(openFileDialog.FileName))
            {
                bool svg = Path.GetExtension(openFileDialog.FileName).ToLower() == ".svg";
                LoadDrawing(openFileDialog.FileName, svg);
            }
        }
        private void mnuSVGDrawing_OnClick(object sender, EventArgs e)
        {
            // One of the dynamically added SVG tools menu has been clicked.
            // Add a drawing of the right type to the active screen.
            ToolStripMenuItem menu = sender as ToolStripMenuItem;
            if(menu != null)
            {
                string svgFile = menu.Tag as string;
                LoadDrawing(svgFile, true);
            }
        }
        private void LoadDrawing(string path, bool isSVG)
        {
            if(path != null && path.Length > 0 && activeScreen != null && activeScreen.CapabilityDrawings)
            {
                activeScreen.AddImageDrawing(path, isSVG);
            }	
        }
        private void mnuCoordinateAxis_OnClick(object sender, EventArgs e)
        {
            PlayerScreen ps = activeScreen as PlayerScreen;
            if (ps == null)
                return;

            mnuCoordinateAxis.Checked = !mnuCoordinateAxis.Checked;
            ps.FrameServer.Metadata.DrawingCoordinateSystem.Visible = mnuCoordinateAxis.Checked;
            ps.RefreshImage();
        }

        private void mnuTestGrid_OnClick(object sender, EventArgs e)
        {
            CaptureScreen cs = activeScreen as CaptureScreen;
            if (cs == null)
                return;

            mnuTestGrid.Checked = !mnuTestGrid.Checked;
            cs.TestGridVisible = mnuTestGrid.Checked;
        }

        private void mnuCameraCalibration_OnClick(object sender, EventArgs e)
        {
            PlayerScreen ps = activeScreen as PlayerScreen;
            if (ps == null)
                return;

            ps.ShowCameraCalibration();
        }

        private void mnuTrajectoryAnalysis_OnClick(object sender, EventArgs e)
        {
            PlayerScreen ps = activeScreen as PlayerScreen;
            if (ps == null)
                return;

            ps.ShowTrajectoryAnalysis();
        }
        private void mnuScatterDiagram_OnClick(object sender, EventArgs e)
        {
            PlayerScreen ps = activeScreen as PlayerScreen;
            if (ps == null)
                return;

            ps.ShowScatterDiagram();
        }
        private void mnuAngularAnalysis_OnClick(object sender, EventArgs e)
        {
            PlayerScreen ps = activeScreen as PlayerScreen;
            if (ps == null)
                return;

            ps.ShowAngularAnalysis();
        }
        private void mnuAngleAngleAnalysis_OnClick(object sender, EventArgs e)
        {
            PlayerScreen ps = activeScreen as PlayerScreen;
            if (ps == null)
                return;

            ps.ShowAngleAngleAnalysis();
        }
        #endregion

        #region Motion
        private void mnuTimebase_OnClick(object sender, EventArgs e)
        {
            PlayerScreen ps = activeScreen as PlayerScreen;
            if (ps != null)
                ps.ConfigureTimebase();
        }
        #endregion
        #endregion

        #region Services
        private void VideoTypeManager_VideoLoadAsked(object sender, VideoLoadAskedEventArgs e)
        {
            if (!DiscardSuspendedScreensForNewFile())
                return;

            DoLoadMovieInScreen(e.Path, e.Target);
        }
        
        private void DoLoadMovieInScreen(string path, int targetScreen)
        {
            if (FilesystemHelper.IsReplayWatcher(path))
            {
                ScreenDescriptionPlayback screenDescription = new ScreenDescriptionPlayback();
                screenDescription.FullPath = path;
                screenDescription.IsReplayWatcher = true;
                screenDescription.Autoplay = true;
                screenDescription.Stretch = true;
                screenDescription.SpeedPercentage = PreferencesManager.PlayerPreferences.DefaultReplaySpeed;
                LoaderVideo.LoadVideoInScreen(this, path, screenDescription);
            }
            else
            {
                if (!File.Exists(path))
                    return;

                if (MetadataSerializer.IsMetadataFile(path) && targetScreen >= 0)
                {
                    // Special case of loading a KVA file on top of a loaded video.
                    AbstractScreen screen = GetScreenAt(targetScreen);
                    if (screen == null || !screen.Full)
                        return;

                    screen.LoadKVA(path);
                    screen.RefreshImage();
                }
                else
                {
                    LoaderVideo.LoadVideoInScreen(this, path, targetScreen);
                }
            }
        }
        
        private void DoLoadCameraInScreen(CameraSummary summary, int targetScreen)
        {
            if(summary == null)
                return;

            LoaderCamera.LoadCameraInScreen(this, summary, targetScreen);
        }
        
        private void DoStopPlaying()
        {
            foreach (PlayerScreen player in playerScreens)
                player.StopPlaying();

            dualPlayer.Pause();
        }

        private void View_FileLoadAsked(object source, FileLoadAskedEventArgs e)
        {
            if (!DiscardSuspendedScreensForNewFile())
                return;

            DoLoadMovieInScreen(e.Source, e.Target);
        }
        private void View_ScreenSwapAsked(object source, EventArgs<Pair<int, int>> e)
        {
            if (e == null || e.Value == null)
                return;

            // Restrict this interaction to 3+ screens and only when merge is off.
            if (screenList.Count < 3 || dualPlayer.View.Merging)
                return;

            int sourceIndex = e.Value.First;
            int targetIndex = e.Value.Second;
            if (sourceIndex < 0 || targetIndex < 0 ||
                sourceIndex >= screenList.Count || targetIndex >= screenList.Count ||
                sourceIndex == targetIndex)
                return;

            SwapScreens(sourceIndex, targetIndex);
            OrganizeScreens();
            OrganizeCommonControls();
            OrganizeMenus();
            UpdateStatusBar();
            ResetSync();
        }

        private void CameraTypeManager_CameraLoadAsked(object source, CameraLoadAskedEventArgs e)
        {
            if (!DiscardSuspendedScreensForNewFile())
                return;

            DoLoadCameraInScreen(e.Source, e.Target);
        }

        private void View_AutoLaunchAsked(object source, EventArgs e)
        {
            ApplyLaunchScreenDescriptions();
        }

        private void ApplyLaunchScreenDescriptions()
        {
            int reloaded = 0;

            int count = LaunchSettingsManager.ScreenDescriptions.Count;

            // Start by collecting the list of cameras to be found. 
            // We will keep the camera discovery system active until we have found all of them or time out.
            camerasToDiscover.Clear();
            foreach (IScreenDescription screenDescription in LaunchSettingsManager.ScreenDescriptions)
            {
                if (screenDescription is ScreenDescriptionCapture)
                    camerasToDiscover.Add(((ScreenDescriptionCapture)screenDescription).CameraName);
            }

            if (camerasToDiscover.Count == 0)
                CameraTypeManager.StopDiscoveringCameras();

            applyingLaunchScreenDescriptions = true;
            try
            {
                foreach (IScreenDescription screenDescription in LaunchSettingsManager.ScreenDescriptions)
                {
                    if (screenDescription is ScreenDescriptionCapture)
                    {
                        int targetScreen = reloaded;
                        AddCaptureScreen();
                        ScreenDescriptionCapture sdc = screenDescription as ScreenDescriptionCapture;
                        CameraSummary summary = new CameraSummary(sdc.CameraName);

                        LoaderCamera.LoadCameraInScreen(this, summary, targetScreen, sdc);
                        reloaded++;
                    }
                    else if (screenDescription is ScreenDescriptionPlayback)
                    {
                        int targetScreen = reloaded;
                        AddPlayerScreen();
                        ScreenDescriptionPlayback sdp = screenDescription as ScreenDescriptionPlayback;
                        LoaderVideo.LoadVideoInScreen(this, sdp.FullPath, targetScreen, sdp);
                        reloaded++;
                    }
                }
            }
            finally
            {
                applyingLaunchScreenDescriptions = false;
            }

            // Only player screens raise SelectionChanged used to commit dual sync.
            dualLaunchSettingsPendingCountdown = LaunchSettingsManager.ScreenDescriptions
                .OfType<ScreenDescriptionPlayback>().Count();

            if (reloaded > 0)
            {
                ApplyLaunchLayoutGrid(reloaded);
                OrganizeScreens();
                OrganizeCommonControls();
                OrganizeMenus();
                MaybeUpdatePeakSnapshot();
            }
        }

        private void ApplyLaunchLayoutGrid(int screenCount)
        {
            int columns = LaunchSettingsManager.LayoutColumns;
            int rows = LaunchSettingsManager.LayoutRows;
            if (columns > 0 && rows > 0 && columns * rows == screenCount)
            {
                layoutColumns = columns;
                layoutRows = rows;
            }
            else
            {
                ScreenLayoutSpec.GetDefaultGrid(screenCount, out layoutColumns, out layoutRows);
            }
        }

        private void TriggerCapture()
        {
            foreach (CaptureScreen screen in captureScreens)
                screen.TriggerCapture();
        }

        private void AudioDeviceLost()
        {
            foreach (CaptureScreen screen in captureScreens)
                screen.AudioDeviceLost();
        }

        private void NotificationCenter_PreferencesOpened(object source, EventArgs e)
        {
            audioInputLevelMonitor.Enabled = false;
        }

        private void NotificationCenter_ExternalCommand(object source, ExternalCommandEventArgs e)
        {
            // Parses the payload of the external command string and send it to the correct handler.
            // The payload is in the form "<Handler>.<Command>", for example "CaptureScreen.ToggleRecording".

            string[] tokens = e.Name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 2)
            {
                log.ErrorFormat("Malformed external command. \"{0\"}", e.Name);
                return;
            }

            switch (tokens[0])
            {
                case "CaptureScreen":
                    {
                        CaptureScreenCommands command;
                        bool parsed = Enum.TryParse(tokens[1], out command);
                        if (!parsed)
                        {
                            log.ErrorFormat("Unsupported capture screen command \"{0}\".", tokens[1]);
                            return;
                        }

                        foreach (CaptureScreen screen in captureScreens)
                            screen.ExecuteScreenCommand((int)command);

                        break;
                    }
                case "PlayerScreen":
                    {
                        PlayerScreenCommands command;
                        bool parsed = Enum.TryParse(tokens[1], out command);
                        if (!parsed)
                        {
                            log.ErrorFormat("Unsupported player screen command \"{0}\".", tokens[1]);
                            return;
                        }

                        foreach (PlayerScreen screen in playerScreens)
                            screen.ExecuteScreenCommand((int)command);

                        break;
                    }
                default:
                    log.ErrorFormat("Unsupported handler in external command: \"{0}\"", tokens[0]);
                    break;
            }
        }
        #endregion

        #region Screen organization
        private void mnuConfigureScreens_Click(object sender, EventArgs e)
        {
            using (FormScreenLayout form = new FormScreenLayout(GetCurrentLayoutSpec()))
            {
                if (form.ShowDialog(view) == DialogResult.OK)
                    ApplyLayout(form.LayoutSpec);
            }
        }

        private ScreenLayoutSpec GetCurrentLayoutSpec()
        {
            List<ScreenType> types = new List<ScreenType>();
            foreach (AbstractScreen screen in screenList)
                types.Add(screen is CaptureScreen ? ScreenType.Capture : ScreenType.Playback);

            if (types.Count == 0)
                return ScreenLayoutSpec.Playback(1);

            SyncLayoutGridToScreenCount();
            return new ScreenLayoutSpec(types, layoutColumns, layoutRows);
        }

        public bool ApplyLayout(ScreenLayoutSpec spec)
        {
            if (spec == null)
                return false;

            if (screensSuspended)
                LeaveBrowserMode();

            // Shrink from the end: cache videos before closing so they can be restored later.
            for (int i = screenList.Count - 1; i >= spec.ScreenCount; i--)
            {
                CacheLayoutSlot(i);
                RemoveScreenSilently(i);
            }

            List<int> slotsToRestore = new List<int>();

            for (int i = 0; i < spec.ScreenCount; i++)
            {
                Type expectedType = spec.ScreenTypes[i] == ScreenType.Capture ? typeof(CaptureScreen) : typeof(PlayerScreen);
                AbstractScreen current = GetScreenAt(i);
                if (current != null && current.GetType() == expectedType)
                    continue;

                if (current != null)
                {
                    CacheLayoutSlot(i);
                    RemoveScreenSilently(i);
                }

                AbstractScreen replacement = spec.ScreenTypes[i] == ScreenType.Capture ?
                    (AbstractScreen)new CaptureScreen() : new PlayerScreen();
                replacement.RefreshUICulture();
                AddScreenAt(replacement, i);

                if (CanRestoreLayoutSlot(i, expectedType))
                    slotsToRestore.Add(i);
            }

            layoutColumns = spec.Columns;
            layoutRows = spec.Rows;

            AfterSharedBufferChange();
            OrganizeScreens();
            OrganizeCommonControls();
            OrganizeMenus();

            if (slotsToRestore.Count > 0)
            {
                dualLaunchSettingsPendingCountdown = slotsToRestore.Count;
                foreach (int slot in slotsToRestore)
                    RestoreLayoutSlot(slot);
            }

            if (canShowCommonControls)
                ResetSync();

            MaybeUpdatePeakSnapshot();
            return true;
        }

        /// <summary>
        /// Insert an empty screen immediately to the right of the active screen.
        /// Existing screen content is preserved. Layout becomes a single row with equal column widths.
        /// </summary>
        public bool InsertScreenToRightOfActive()
        {
            if (screenList.Count == 0)
                return false;

            if (screensSuspended)
                LeaveBrowserMode();

            int activeIndex = activeScreen != null ? screenList.IndexOf(activeScreen) : -1;
            if (activeIndex < 0)
                activeIndex = 0;

            int insertIndex = activeIndex + 1;
            AbstractScreen active = screenList[activeIndex];
            AbstractScreen previousActive = activeScreen;

            AbstractScreen inserted = active is CaptureScreen ?
                (AbstractScreen)new CaptureScreen() : new PlayerScreen();
            inserted.RefreshUICulture();

            ShiftLayoutSlotCacheAfterInsert(insertIndex);
            AddScreenAt(inserted, insertIndex);

            // Keep a single row so "to the right" is visual and all column widths stay equal.
            layoutColumns = screenList.Count;
            layoutRows = 1;

            AfterSharedBufferChange();
            OrganizeScreens();
            OrganizeCommonControls();
            OrganizeMenus();

            if (previousActive != null && screenList.Contains(previousActive))
                SetActiveScreen(previousActive);
            else if (screenList.Count > 0)
                SetActiveScreen(screenList[Math.Min(activeIndex, screenList.Count - 1)]);

            if (canShowCommonControls)
                ResetSync();

            MaybeUpdatePeakSnapshot();
            return true;
        }

        private void MaybeUpdatePeakSnapshot()
        {
            int count = screenList.Count;
            if (count == 0)
                return;

            // Below the recorded peak: keep the peak snapshot so "restore max screens" still works.
            // At or above peak: capture current layout/content (so videos opened after inserting
            // a screen are included, not only the empty state at first peak).
            if (count < peakScreenCount)
                return;

            peakScreenCount = count;
            if (peakScreenSnapshot != null)
                peakScreenSnapshot.ClearAnnotationFiles();

            peakScreenSnapshot = CaptureSessionSnapshot("session-peak");
            OrganizeMenus();
        }

        /// <summary>
        /// Refresh the peak-session snapshot when the current layout is at the session max screen count.
        /// Call after loading content into a screen so restore reflects the latest files, not only the insert moment.
        /// </summary>
        public void RefreshPeakSnapshotIfAtPeak()
        {
            MaybeUpdatePeakSnapshot();
        }

        private SessionScreenSnapshot CaptureSessionSnapshot(string cacheSubFolder)
        {
            SessionScreenSnapshot snapshot = new SessionScreenSnapshot();
            SyncLayoutGridToScreenCount();
            snapshot.Columns = layoutColumns;
            snapshot.Rows = layoutRows;

            string cacheDirectory = Path.Combine(Software.TempDirectory, cacheSubFolder);
            try
            {
                if (!Directory.Exists(cacheDirectory))
                    Directory.CreateDirectory(cacheDirectory);
            }
            catch (Exception e)
            {
                log.ErrorFormat("Failed to create session cache directory {0}.", cacheDirectory);
                log.Error(e.ToString());
            }

            for (int i = 0; i < screenList.Count; i++)
            {
                AbstractScreen screen = screenList[i];
                SessionScreenSnapshot.Slot slot = CreateSessionSlot(i, screen, cacheDirectory);
                if (slot != null)
                    snapshot.Slots.Add(slot);
            }

            return snapshot;
        }

        private SessionScreenSnapshot.Slot CreateSessionSlot(int index, AbstractScreen screen, string cacheDirectory)
        {
            if (screen == null)
                return null;

            IScreenDescription description = screen.GetScreenDescription();
            ScreenDescriptionPlayback playback = description as ScreenDescriptionPlayback;
            if (playback != null)
            {
                playback.Autoplay = false;
                if (string.IsNullOrEmpty(playback.FullPath) && !playback.IsReplayWatcher && !screen.Full)
                {
                    // Keep empty playback placeholders in peak snapshots.
                }
            }

            string annotationsPath = null;
            PlayerScreen player = screen as PlayerScreen;
            if (player != null && player.FrameServer != null && player.FrameServer.Metadata != null && player.Full)
            {
                try
                {
                    annotationsPath = Path.Combine(cacheDirectory, Guid.NewGuid().ToString() + ".kva");
                    MetadataSerializer serializer = new MetadataSerializer();
                    serializer.SaveToFile(player.FrameServer.Metadata, annotationsPath);
                }
                catch (Exception e)
                {
                    log.ErrorFormat("Failed to cache annotations for session slot {0}.", index);
                    log.Error(e.ToString());
                    annotationsPath = null;
                }
            }

            return new SessionScreenSnapshot.Slot
            {
                Index = index,
                ScreenType = screen is CaptureScreen ? ScreenType.Capture : ScreenType.Playback,
                Description = description,
                AnnotationsPath = annotationsPath
            };
        }

        private void RestorePeakScreens()
        {
            if (peakScreenSnapshot == null || peakScreenSnapshot.Slots.Count == 0)
                return;
            if (screenList.Count >= peakScreenSnapshot.Slots.Count)
                return;

            if (screensSuspended)
                LeaveBrowserMode();

            ApplySessionSnapshot(peakScreenSnapshot, keepPeak: true);
        }

        private void RestoreLastClosedScreen()
        {
            if (closedScreensStack.Count == 0)
                return;

            if (screensSuspended)
                LeaveBrowserMode();

            SessionScreenSnapshot.Slot slot = closedScreensStack.Pop();
            int insertIndex = Math.Min(Math.Max(0, slot.Index), screenList.Count);

            AbstractScreen inserted = slot.ScreenType == ScreenType.Capture ?
                (AbstractScreen)new CaptureScreen() : new PlayerScreen();
            inserted.RefreshUICulture();
            ShiftLayoutSlotCacheAfterInsert(insertIndex);
            AddScreenAt(inserted, insertIndex);

            layoutColumns = screenList.Count;
            layoutRows = 1;

            AfterSharedBufferChange();
            OrganizeScreens();
            OrganizeCommonControls();
            OrganizeMenus();

            RestoreSessionSlot(insertIndex, slot);
            SessionScreenSnapshot.DeleteAnnotationFile(slot);
            MaybeUpdatePeakSnapshot();
            OrganizeMenus();
        }

        private void ApplySessionSnapshot(SessionScreenSnapshot snapshot, bool keepPeak)
        {
            if (snapshot == null || snapshot.Slots.Count == 0)
                return;

            while (screenList.Count > 0)
            {
                if (!ScreenRemover.RemoveScreen(this, 0))
                    return;
            }

            ClearLayoutSlotCache();

            for (int i = 0; i < snapshot.Slots.Count; i++)
            {
                SessionScreenSnapshot.Slot slot = snapshot.Slots[i];
                AbstractScreen screen = slot.ScreenType == ScreenType.Capture ?
                    (AbstractScreen)new CaptureScreen() : new PlayerScreen();
                screen.RefreshUICulture();
                AddScreenAt(screen, i);
            }

            if (snapshot.Columns > 0 && snapshot.Rows > 0 && snapshot.Columns * snapshot.Rows == snapshot.Slots.Count)
            {
                layoutColumns = snapshot.Columns;
                layoutRows = snapshot.Rows;
            }
            else
            {
                ScreenLayoutSpec.GetDefaultGrid(snapshot.Slots.Count, out layoutColumns, out layoutRows);
            }

            AfterSharedBufferChange();
            OrganizeScreens();
            OrganizeCommonControls();
            OrganizeMenus();

            dualLaunchSettingsPendingCountdown = snapshot.Slots.Count(s => s.ScreenType == ScreenType.Playback && s.Description is ScreenDescriptionPlayback);
            for (int i = 0; i < snapshot.Slots.Count; i++)
                RestoreSessionSlot(i, snapshot.Slots[i]);

            if (!keepPeak)
            {
                snapshot.ClearAnnotationFiles();
            }

            MaybeUpdatePeakSnapshot();
            if (canShowCommonControls)
                ResetSync();
        }

        private void RestoreSessionSlot(int index, SessionScreenSnapshot.Slot slot)
        {
            if (slot == null)
                return;

            AbstractScreen screen = GetScreenAt(index);
            if (screen == null)
                return;

            ScreenDescriptionPlayback playback = slot.Description as ScreenDescriptionPlayback;
            if (playback != null && screen is PlayerScreen)
            {
                if (!string.IsNullOrEmpty(playback.FullPath) || playback.IsReplayWatcher)
                {
                    LoaderVideo.LoadVideoInScreen(this, playback.FullPath, index, playback);
                    PlayerScreen player = GetScreenAt(index) as PlayerScreen;
                    if (player != null && player.Full &&
                        !string.IsNullOrEmpty(slot.AnnotationsPath) && File.Exists(slot.AnnotationsPath))
                    {
                        player.LoadKVA(slot.AnnotationsPath);
                    }
                }
            }
        }

        private void EnsureLayoutSlotCacheSize(int size)
        {
            while (layoutSlotCache.Count < size)
                layoutSlotCache.Add(null);
        }

        private void CacheLayoutSlot(int index)
        {
            if (index < 0)
                return;

            ClearLayoutSlotEntry(index);

            AbstractScreen screen = GetScreenAt(index);
            if (screen == null || !screen.Full)
                return;

            IScreenDescription description = screen.GetScreenDescription();
            ScreenDescriptionPlayback playback = description as ScreenDescriptionPlayback;
            if (playback != null)
            {
                playback.Autoplay = false;
                if (string.IsNullOrEmpty(playback.FullPath) && !playback.IsReplayWatcher)
                    return;
            }

            string annotationsPath = null;
            PlayerScreen player = screen as PlayerScreen;
            if (player != null && player.FrameServer != null && player.FrameServer.Metadata != null)
            {
                try
                {
                    string cacheDirectory = Path.Combine(Software.TempDirectory, "layout-slots");
                    if (!Directory.Exists(cacheDirectory))
                        Directory.CreateDirectory(cacheDirectory);

                    annotationsPath = Path.Combine(cacheDirectory, Guid.NewGuid().ToString() + ".kva");
                    MetadataSerializer serializer = new MetadataSerializer();
                    serializer.SaveToFile(player.FrameServer.Metadata, annotationsPath);
                }
                catch (Exception e)
                {
                    log.ErrorFormat("Failed to cache annotations for layout slot {0}.", index);
                    log.Error(e.ToString());
                    annotationsPath = null;
                }
            }

            EnsureLayoutSlotCacheSize(index + 1);
            layoutSlotCache[index] = new LayoutSlotCacheEntry(description, annotationsPath);
        }

        private bool CanRestoreLayoutSlot(int index, Type expectedType)
        {
            if (index < 0 || index >= layoutSlotCache.Count || layoutSlotCache[index] == null)
                return false;

            if (expectedType == typeof(PlayerScreen))
            {
                ScreenDescriptionPlayback playback = layoutSlotCache[index].Description as ScreenDescriptionPlayback;
                return playback != null && (!string.IsNullOrEmpty(playback.FullPath) || playback.IsReplayWatcher);
            }

            return false;
        }

        private void RestoreLayoutSlot(int index)
        {
            AbstractScreen screen = GetScreenAt(index);
            if (screen == null || screen.Full)
                return;

            LayoutSlotCacheEntry entry = layoutSlotCache[index];
            if (entry == null)
                return;

            ScreenDescriptionPlayback playback = entry.Description as ScreenDescriptionPlayback;
            if (playback == null || !(screen is PlayerScreen))
                return;

            LoaderVideo.LoadVideoInScreen(this, playback.FullPath, index, playback);

            PlayerScreen player = GetScreenAt(index) as PlayerScreen;
            if (player != null && player.Full &&
                !string.IsNullOrEmpty(entry.AnnotationsPath) && File.Exists(entry.AnnotationsPath))
            {
                player.LoadKVA(entry.AnnotationsPath);
            }
        }

        private void RemoveScreenSilently(int index)
        {
            AbstractScreen screen = GetScreenAt(index);
            if (screen == null)
                return;

            // Layout switches cache the content for later restore, so skip the dirty confirmation dialog.
            RemoveScreen(screen);
        }

        private void ShiftLayoutSlotCacheAfterClose(int removedIndex)
        {
            if (removedIndex < 0 || removedIndex >= layoutSlotCache.Count)
                return;

            ClearLayoutSlotEntry(removedIndex);
            layoutSlotCache.RemoveAt(removedIndex);
        }

        private void ShiftLayoutSlotCacheAfterInsert(int insertIndex)
        {
            if (insertIndex < 0)
                return;

            EnsureLayoutSlotCacheSize(insertIndex);
            layoutSlotCache.Insert(insertIndex, null);
        }

        private void ClearLayoutSlotCache()
        {
            for (int i = 0; i < layoutSlotCache.Count; i++)
                ClearLayoutSlotEntry(i);
            layoutSlotCache.Clear();
        }

        private void ClearLayoutSlotEntry(int index)
        {
            if (index < 0)
                return;

            EnsureLayoutSlotCacheSize(index + 1);

            LayoutSlotCacheEntry entry = layoutSlotCache[index];
            if (entry != null && !string.IsNullOrEmpty(entry.AnnotationsPath))
            {
                try
                {
                    if (File.Exists(entry.AnnotationsPath))
                        File.Delete(entry.AnnotationsPath);
                }
                catch (Exception e)
                {
                    log.ErrorFormat("Failed to delete cached annotations for layout slot {0}.", index);
                    log.Error(e.ToString());
                }
            }

            layoutSlotCache[index] = null;
        }

        private sealed class LayoutSlotCacheEntry
        {
            public IScreenDescription Description { get; private set; }
            public string AnnotationsPath { get; private set; }

            public LayoutSlotCacheEntry(IScreenDescription description, string annotationsPath)
            {
                Description = description;
                AnnotationsPath = annotationsPath;
            }
        }

        private void SyncLayoutGridToScreenCount()
        {
            int count = screenList.Count;
            if (count == 0)
            {
                layoutColumns = 1;
                layoutRows = 1;
                return;
            }

            if (layoutColumns * layoutRows != count)
                ScreenLayoutSpec.GetDefaultGrid(count, out layoutColumns, out layoutRows);
        }

        private void AddScreenAt(AbstractScreen screen, int index)
        {
            if (!CanAddScreen())
                return;

            // We are about to insert a screen, signal it to existing capture screens for buffer memory management.
            int captureScreenCount = CaptureScreenCount + (screen is CaptureScreen ? 1 : 0);
            foreach (CaptureScreen captureScreen in captureScreens)
                captureScreen.SetShared(captureScreenCount);

            AddScreenEventHandlers(screen);
            screenList.Insert(Math.Min(index, screenList.Count), screen);
        }

        /// <summary>
        /// Disable synchronization or reset it to the screens' time origins.
        /// This should be called any time the screen list change, working zones change, dual controls visiblity changes.
        /// </summary>
        private void ResetSync()
        {
            ResetSync(false);
        }

        private void ResetSync(bool preservePositions)
        {
            foreach (PlayerScreen p in playerScreens)
                p.Synched = false;

            if (view.CommonControlsVisible)
                dualPlayer.ResetSync(preservePositions);
        }
        public void AddPlayerScreen()
        {
            if (!CanAddScreen())
                return;

            PlayerScreen screen = new PlayerScreen();
            screen.RefreshUICulture();
            AddScreen(screen);
        }
        public void AddCaptureScreen()
        {
            if (!CanAddScreen())
                return;
            
            CaptureScreen screen = new CaptureScreen();
            screen.SetShared(CaptureScreenCount + 1);

            screen.RefreshUICulture();
            AddScreen(screen);
        }

        /// <summary>
        /// Find the most appropriate screen to load into.
        /// Must be of the same type, and empty if possible.
        /// </summary>
        public int FindTargetScreen(Type type)
        {
            for (int i = 0; i < screenList.Count; i++)
            {
                AbstractScreen screen = screenList[i];
                if (screen.GetType() == type && !screen.Full)
                    return i;
            }

            // If no empty screen was found, overload, but start from the last screen.
            for (int i = screenList.Count - 1; i >= 0; i--)
            {
                if (screenList[i].GetType() == type)
                    return i;
            }

            // We do not replace capture screens with videos or vice-versa.
            return -1;
        }

        /// <summary>
        /// Asks the user for confirmation on replacing the current content.
        /// Check if we are overloading on a non-empty screen and propose to save data.
        /// </summary>
        /// <returns>true if the loading can go on</returns>
        public bool BeforeReplacingPlayerContent(int targetScreen)
        {
            PlayerScreen player = GetScreenAt(targetScreen) as PlayerScreen;
            if (player == null || !player.FrameServer.Metadata.IsDirty)
                return true;

            DialogResult save = ShowConfirmDirtyDialog();
            if (save == DialogResult.No)
            {
                // No: don't save.
                return true;
            }
            else if (save == DialogResult.Cancel)
            {
                // Cancel: stop loading the new video.
                return false;
            }
            else
            {
                // Yes: save, then load the new video.
                DoStopPlaying();
                player.Save();
                return true;
            }
        }
        private DialogResult ShowConfirmDirtyDialog()
        {
            string caption = ScreenManagerLang.InfoBox_MetadataIsDirty_Title;
            string text = ScreenManagerLang.InfoBox_MetadataIsDirty_Text.Replace("\\n", "\n");
            return MessageBox.Show(text, caption, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
        }
        
        private void AddScreen(AbstractScreen screen)
        {
            if (!CanAddScreen())
                return;

            // We are about to add a new screen, signal it to existing capture screens for buffer memory management.
            int captureScreenCount = CaptureScreenCount + (screen is CaptureScreen ? 1 : 0);
            foreach (CaptureScreen captureScreen in captureScreens)
                captureScreen.SetShared(captureScreenCount);

            AddScreenEventHandlers(screen);
            screenList.Add(screen);
        }
        private void AddScreenEventHandlers(AbstractScreen screen)
        {
            screen.CloseAsked += Screen_CloseAsked;
            screen.Activated += Screen_Activated;
            screen.DualCommandReceived += Screen_DualCommandReceived;

            if (screen is PlayerScreen)
                AddPlayerScreenEventHandlers(screen as PlayerScreen);
            else if (screen is CaptureScreen)
                AddCaptureScreenEventHandlers(screen as CaptureScreen);
        }
        private void AddPlayerScreenEventHandlers(PlayerScreen screen)
        {
            screen.OpenVideoAsked += Player_OpenVideoAsked;
            screen.VideoPathLoadAsked += Player_VideoPathLoadAsked;
            screen.OpenReplayWatcherAsked += Player_OpenReplayWatcherAsked;
            screen.OpenAnnotationsAsked += Player_OpenAnnotationsAsked;
            screen.SelectionChanged += Player_SelectionChanged;
            screen.ResetAsked += Player_ResetAsked;
        }
        private void AddCaptureScreenEventHandlers(CaptureScreen screen)
        {
            screen.CameraDiscoveryComplete += Capture_CameraDiscoveryComplete;
        }
        private void RemoveScreenEventHandlers(AbstractScreen screen)
        {
            screen.CloseAsked -= Screen_CloseAsked;
            screen.Activated -= Screen_Activated;
            screen.DualCommandReceived -= Screen_DualCommandReceived;

            if (screen is PlayerScreen)
                RemovePlayerScreenEventHandlers(screen as PlayerScreen);
            else if (screen is CaptureScreen)
                RemoveCaptureScreenEventHandlers(screen as CaptureScreen);

        }
        private void RemovePlayerScreenEventHandlers(PlayerScreen screen)
        {
            screen.OpenVideoAsked -= Player_OpenVideoAsked;
            screen.VideoPathLoadAsked -= Player_VideoPathLoadAsked;
            screen.OpenReplayWatcherAsked -= Player_OpenReplayWatcherAsked;
            screen.OpenAnnotationsAsked -= Player_OpenAnnotationsAsked;
            screen.SelectionChanged -= Player_SelectionChanged;
            screen.ResetAsked -= Player_ResetAsked;
        }

        private void RemoveCaptureScreenEventHandlers(CaptureScreen screen)
        {
            screen.CameraDiscoveryComplete -= Capture_CameraDiscoveryComplete;
        }

        #endregion
    }
}
