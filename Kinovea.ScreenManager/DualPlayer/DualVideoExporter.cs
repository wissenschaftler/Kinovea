using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Kinovea.ScreenManager.Languages;
using System.IO;
using Kinovea.Video.FFMpeg;
using System.ComponentModel;
using Kinovea.Services;
using System.Drawing;
using Kinovea.Video;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Create and save a composite video with side by side synchronized images.
    /// If merge is active, only saves the left video.
    /// </summary>
    public class DualVideoExporter
    {
        private CommonTimeline commonTimeline;
        private IList<PlayerScreen> players;
        private IList<int> slotIndices;
        private int screenCount;
        private int columns;
        private int rows;
        private double fileFrameInterval;
        private string dualSaveFileName;
        private bool dualSaveCancelled;
        private bool merging;
        
        private VideoFileWriter videoFileWriter = new VideoFileWriter();
        private BackgroundWorker bgWorkerDualSave;
        private formProgressBar dualSaveProgressBar;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public void Export(CommonTimeline commonTimeline, PlayerScreen leftPlayer, PlayerScreen rightPlayer, bool merging)
        {
            Export(
                commonTimeline,
                new PlayerScreen[] { leftPlayer, rightPlayer },
                new int[] { 0, 1 },
                2,
                merging);
        }

        public void Export(CommonTimeline commonTimeline, IList<PlayerScreen> players, IList<int> slotIndices, int screenCount, bool merging)
        {
            int columns;
            int rows;
            ScreenLayoutSpec.GetDefaultGrid(screenCount, out columns, out rows);
            Export(commonTimeline, players, slotIndices, screenCount, columns, rows, merging);
        }

        public void Export(CommonTimeline commonTimeline, IList<PlayerScreen> players, IList<int> slotIndices, int screenCount, int columns, int rows, bool merging)
        {
            ValidateArguments(commonTimeline, players, slotIndices, screenCount);

            this.commonTimeline = commonTimeline;
            this.players = new List<PlayerScreen>(players);
            this.slotIndices = new List<int>(slotIndices);
            this.screenCount = screenCount;
            this.columns = columns;
            this.rows = rows;
            this.merging = merging;

            // During saving we move through the common timeline by a time unit based on framerate and high speed factor, but not based on user custom slow motion factor.
            // For the framerate saved in the file metadata we take user custom slow motion into account and not high speed factor.
            fileFrameInterval = 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null)
                    fileFrameInterval = Math.Max(fileFrameInterval, players[i].FrameInterval);
            }
            
            dualSaveFileName = GetFilename(players);
            if (string.IsNullOrEmpty(dualSaveFileName))
                return;

            dualSaveCancelled = false;
            
            // Instanciate and configure the bgWorker.
            bgWorkerDualSave = new BackgroundWorker();
            bgWorkerDualSave.WorkerReportsProgress = true;
            bgWorkerDualSave.WorkerSupportsCancellation = true;
            bgWorkerDualSave.DoWork += bgWorkerDualSave_DoWork;
            bgWorkerDualSave.ProgressChanged += bgWorkerDualSave_ProgressChanged;
            bgWorkerDualSave.RunWorkerCompleted += bgWorkerDualSave_RunWorkerCompleted;

            // Make sure none of the screen will try to update itself.
            // Otherwise it will cause access to the other screen image (in case of merge), which can cause a crash.
            
            SetDualSaveInProgress(true);
            try
            {
                dualSaveProgressBar = new formProgressBar(true);
                dualSaveProgressBar.Cancel = dualSave_CancelAsked;
                
                // The worker thread runs in the background while the UI thread is in the progress bar dialog.
                // We only continue after these two lines once the video has been saved or the saving cancelled.
                bgWorkerDualSave.RunWorkerAsync();
                dualSaveProgressBar.ShowDialog();

                if (dualSaveCancelled)
                    DeleteTemporaryFile(dualSaveFileName);
            }
            finally
            {
                SetDualSaveInProgress(false);
            }
        }

        private string GetFilename(IList<PlayerScreen> players)
        {
            using (SaveFileDialog dlgSave = new SaveFileDialog())
            {
                dlgSave.Title = ScreenManagerLang.CommandExportVideo_FriendlyName;
                dlgSave.RestoreDirectory = true;
                dlgSave.Filter = FilesystemHelper.SaveVideoFilter();
                dlgSave.FilterIndex = FilesystemHelper.GetFilterIndex(dlgSave.Filter, PreferencesManager.PlayerPreferences.VideoFormat);
                dlgSave.FileName = GetDefaultFilename(players);

                if (dlgSave.ShowDialog() != DialogResult.OK)
                    return null;

                return dlgSave.FileName;
            }
        }

        private void bgWorkerDualSave_DoWork(object sender, DoWorkEventArgs e)
        {
            // This is executed in Worker Thread space. (Do not call any UI methods)
            log.Debug("Saving side by side video.");

            int threadResult = 0;
            
            // Get first frame outside the loop to set up the saving context.
            long currentTime = 0;
            using (Bitmap composite = GetCompositeImage(currentTime))
            {
                log.DebugFormat("Composite size: {0}.", composite.Size);

                VideoInfo info = new VideoInfo
                {
                    ReferenceSize = composite.Size
                };

                string formatString = FilenameHelper.GetFormatString(dualSaveFileName);

                SaveResult result = videoFileWriter.OpenSavingContext(dualSaveFileName, info, formatString, fileFrameInterval);

                if (result != SaveResult.Success)
                {
                    e.Result = 2;
                    return;
                }

                videoFileWriter.SaveFrame(composite);
            }
            
            while (currentTime < commonTimeline.LastTime && !dualSaveCancelled)
            {
                currentTime += commonTimeline.FrameTime;

                if (bgWorkerDualSave.CancellationPending)
                {
                    threadResult = 1;
                    dualSaveCancelled = true;
                    break;
                }

                using (Bitmap composite = GetCompositeImage(currentTime))
                {
                    videoFileWriter.SaveFrame(composite);
                }

                int percent = (int)((double)currentTime * 100 / commonTimeline.LastTime);
                bgWorkerDualSave.ReportProgress(percent);
            }

            if (!dualSaveCancelled)
                threadResult = 0;
            
            e.Result = threadResult;
        }
        
        private void GotoTime(PlayerScreen player, long commonTime)
        {
            long localTime = commonTimeline.GetLocalTime(player, commonTime);
            localTime = Math.Max(0, localTime);
            player.GotoTime(localTime, false);
        }

        private Bitmap GetCompositeImage(long currentTime)
        {
            List<Bitmap> images = new List<Bitmap>();
            try
            {
                int imageCount = merging ? 1 : players.Count;
                for (int i = 0; i < imageCount; i++)
                {
                    PlayerScreen player = players[i];
                    if (player == null)
                    {
                        images.Add(null);
                        continue;
                    }

                    GotoTime(player, currentTime);
                    images.Add(player.GetFlushedImage());
                }

                IList<int> effectiveSlotIndices = merging ? new int[] { 0 } : slotIndices;
                int effectiveScreenCount = merging ? 1 : screenCount;
                int effectiveColumns = merging ? 1 : columns;
                int effectiveRows = merging ? 1 : rows;
                return ImageHelper.GetComposite(images, effectiveSlotIndices, effectiveScreenCount, effectiveColumns, effectiveRows, true);
            }
            finally
            {
                for (int i = 0; i < images.Count; i++)
                {
                    if (images[i] != null)
                        images[i].Dispose();
                }
            }
        }

        private void bgWorkerDualSave_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (bgWorkerDualSave.CancellationPending)
                return;

            dualSaveProgressBar.Update(Math.Min(e.ProgressPercentage, 100), 100, true);
        }

        private void bgWorkerDualSave_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                dualSaveProgressBar.Close();
                dualSaveProgressBar.Dispose();

                if (!dualSaveCancelled && (int)e.Result != 1 && videoFileWriter != null)
                    videoFileWriter.CloseSavingContext((int)e.Result == 0);
            }
            catch (Exception exception)
            {
                log.ErrorFormat("Error while completing dual save. {0}", exception);
            }

            NotificationCenter.RaiseRefreshFileExplorer(this, false);
        }

        private void dualSave_CancelAsked(object sender, EventArgs e)
        {
            // This will simply set BgWorker.CancellationPending to true, which we check periodically in the saving loop.
            // This will also end the bgWorker immediately, maybe before we check for the cancellation in the other thread. 
            
            videoFileWriter.CloseSavingContext(false);
            dualSaveCancelled = true;
            bgWorkerDualSave.CancelAsync();
        }

        private void DeleteTemporaryFile(string filename)
        {
            log.Debug("Dual video saving cancelled. Deleting file.");
            if (!File.Exists(filename))
                return;

            try
            {
                File.Delete(filename);
            }
            catch (Exception exp)
            {
                log.Error("Error while deleting file.");
                log.Error(exp.Message);
                log.Error(exp.StackTrace);
            }
        }

        private static string GetDefaultFilename(IList<PlayerScreen> players)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null)
                    names.Add(Path.GetFileNameWithoutExtension(players[i].FilePath));
            }

            return String.Join(" - ", names.ToArray());
        }

        private void SetDualSaveInProgress(bool value)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null)
                    players[i].DualSaveInProgress = value;
            }
        }

        private static void ValidateArguments(CommonTimeline commonTimeline, IList<PlayerScreen> players, IList<int> slotIndices, int screenCount)
        {
            if (commonTimeline == null)
                throw new ArgumentNullException("commonTimeline");
            if (players == null)
                throw new ArgumentNullException("players");
            if (slotIndices == null)
                throw new ArgumentNullException("slotIndices");
            if (players.Count == 0)
                throw new ArgumentException("At least one player is required.", "players");
            if (players.Count != slotIndices.Count)
                throw new ArgumentException("Players and slot indices must have the same count.");
            if (screenCount < 1 || screenCount > 4)
                throw new ArgumentOutOfRangeException("screenCount", "Screen count must be between 1 and 4.");

            bool[] usedSlots = new bool[screenCount];
            for (int i = 0; i < slotIndices.Count; i++)
            {
                int slotIndex = slotIndices[i];
                if (slotIndex < 0 || slotIndex >= screenCount)
                    throw new ArgumentOutOfRangeException("slotIndices", "A slot index is outside the layout.");
                if (usedSlots[slotIndex])
                    throw new ArgumentException("Slot indices must be unique.", "slotIndices");

                usedSlots[slotIndex] = true;
            }

            if (players[0] == null)
                throw new ArgumentException("The first player cannot be null.", "players");
        }
        
    }
}
