using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Synchronization logic.
    /// </summary>
    public class DualPlayerController
    {
        #region Properties
        public CommonControlsPlayers View
        {
            get { return view; }
        }
        public bool Active
        {
            get { return active; }
        }
        #endregion

        #region Members
        private bool active;
        private bool synching;
        private bool dynamicSynching;
        private bool dualSaveInProgress;

        private CommonTimeline commonTimeline = new CommonTimeline();
        private long currentTime;   // current time in common timeline, in microseconds.
        private CommonControlsPlayers view = new CommonControlsPlayers();
        private List<PlayerScreen> players = new List<PlayerScreen>();
        private List<int> playerSlotIndices = new List<int>();
        private int layoutScreenCount;
        private int layoutColumns = 1;
        private int layoutRows = 1;
        private PlayerScreen referencePlayer;
        private Dictionary<PlayerScreen, Bitmap> lastSyncMergeImages = new Dictionary<PlayerScreen, Bitmap>();
        private List<PlayerScreen> syncMergeImageOrder = new List<PlayerScreen>();
        private int resyncOperations = 0;
        private int maxResyncOperations = 1;
        private HotkeyCommand[] hotkeys;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        #region Constructor
        public DualPlayerController()
        {
            view.Dock = DockStyle.Fill;

            view.PlayToggled += CCtrl_PlayToggled;
            view.GotoFirst += CCtrl_GotoFirst;
            view.GotoPrev += CCtrl_GotoPrev;
            view.GotoPrevKeyframe += CCtrl_GotoPrevKeyframe;
            view.GotoNext += CCtrl_GotoNext;
            view.GotoLast += CCtrl_GotoLast;
            view.GotoNextKeyframe += CCtrl_GotoNextKeyframe;
            view.GotoSync += CCtrl_GotoSync;
            view.AddKeyframe += CCtrl_AddKeyframe;
            view.SyncAsked += CCtrl_SyncAsked;
            view.MergeAsked += CCtrl_MergeAsked;
            view.PositionChanged += CCtrl_PositionChanged;
            view.DualSaveAsked += CCtrl_DualSaveAsked;
            view.DualSnapshotAsked += CCtrl_DualSnapshotAsked;

            hotkeys = HotkeySettingsManager.LoadHotkeys("DualPlayer");
        }
        #endregion

        #region Public methods
        public void ScreenListChanged(List<AbstractScreen> screenList)
        {
            int columns;
            int rows;
            ScreenLayoutSpec.GetDefaultGrid(screenList.Count, out columns, out rows);
            ScreenListChanged(screenList, columns, rows);
        }

        public void ScreenListChanged(List<AbstractScreen> screenList, int columns, int rows)
        {
            List<PlayerScreen> playerScreens = new List<PlayerScreen>();
            List<int> slotIndices = new List<int>();
            for (int i = 0; i < screenList.Count; i++)
            {
                PlayerScreen player = screenList[i] as PlayerScreen;
                if (player == null)
                    continue;

                playerScreens.Add(player);
                slotIndices.Add(i);
            }

            layoutScreenCount = screenList.Count;
            if (columns <= 0 || rows <= 0 || columns * rows != layoutScreenCount)
                ScreenLayoutSpec.GetDefaultGrid(layoutScreenCount, out columns, out rows);

            layoutColumns = columns;
            layoutRows = rows;
            playerSlotIndices = slotIndices;
            if (playerScreens.Count >= 2)
                Enter(playerScreens);
            else
                Exit();
        }
        public void SetReferencePlayer(PlayerScreen player)
        {
            if (player != null && players.Contains(player))
            {
                referencePlayer = player;
                UpdateSyncMergeOverlay(true);
            }
        }
        public void RefreshUICulture()
        {
            view.RefreshUICulture();

            if (synching == true)
                InitializeSync();
        }
        public void UpdateTrkFrame(long position)
        {
            view.UpdateCurrentPosition(position);
        }
        public void Pause()
        {
            if (!active || !synching)
                return;

            dynamicSynching = false;

            if (view.Playing)
                view.Pause();

            foreach (PlayerScreen player in players)
            {
                if (player.IsPlaying)
                    player.view.OnButtonPlay();
            }
        }
        public void StopMerge()
        {
            view.StopMerge();
        }
        public void SwapSync()
        {
            if (!active || players.Count != 2)
                return;

            players.Reverse();
            if (playerSlotIndices.Count == 2)
                playerSlotIndices.Reverse();

            if (synching)
                UpdateHairLines();
        }
        public void CommitLaunchSettings()
        {
            if (!active)
                return;

            if (players.Any(player => !player.Full))
                return;

            synching = true;
            foreach (PlayerScreen player in players)
                player.Synched = true;

            InitializeSync();
        }
        #endregion

        #region Players event handlers
        private void Player_PauseAsked(object sender, EventArgs e)
        {
            if (active && synching && view.Playing)
                Pause();
        }

        private void Player_PlayStarted(object sender, EventArgs e)
        {
            // If both players are playing, we must activate the dynamic synching, even if they were started independently.
            // This way they will continue playing on the next loop, this time in sync.
            if (!active || !synching)
                return;

            PlayerScreen player = sender as PlayerScreen;
            if (player == null)
                return;

            if (players.All(candidate => candidate.IsPlaying) && !view.Playing)
            {
                // Immediately force synchronization.
                // This may not fully register for automatically started players, see `resyncOperations`.
                commonTimeline.Initialize(players, referencePlayer);
                view.UpdateSyncPosition(commonTimeline.GetCommonTime(player, player.LocalTimeOriginPhysical));

                AlignPlayers(false);

                resyncOperations = 0;
                view.Play();
                dynamicSynching = true;
                EnsurePlayersPlaying();
            }
        }

        private void Player_SpeedChanged(object sender, EventArgs e)
        {
            if (!active || !synching)
                return;

            PlayerScreen player = sender as PlayerScreen;
            if (player == null || !PreferencesManager.PlayerPreferences.SyncLockSpeed)
                return;

            foreach (PlayerScreen otherPlayer in players.Where(candidate => candidate != player))
                otherPlayer.RealtimePercentage = player.RealtimePercentage;
        }

        private void Player_TimeOriginChanged(object sender, EventArgs e)
        {
            if (!active || !synching)
                return;

            PlayerScreen player = sender as PlayerScreen;
            if (player == null)
                return;

            // Reinit synchronization.
            commonTimeline.Initialize(players, referencePlayer);
            currentTime = commonTimeline.GetCommonTime(player, player.LocalTime);
            view.SetupTrkFrame(0, commonTimeline.LastTime, currentTime);
            view.UpdateSyncPosition(currentTime);
            UpdateHairLines();
        }

        private void Player_HighSpeedFactorChanged(object sender, EventArgs e)
        {
            if (!active || !synching)
                return;

            if (PreferencesManager.PlayerPreferences.SyncLockSpeed)
            {
                double percentage = players.Min(player => player.RealtimePercentage);
                foreach (PlayerScreen player in players)
                    player.RealtimePercentage = percentage;
            }

            // Synchronization must be reinitialized.
            commonTimeline.Initialize(players, this.referencePlayer);

            // TODO: Check if current time is still in bounds.
            PlayerScreen changedPlayer = sender as PlayerScreen ?? players.First();
            currentTime = Math.Min(currentTime, commonTimeline.GetCommonTime(changedPlayer, changedPlayer.LocalTime));

            view.SetupTrkFrame(0, commonTimeline.LastTime, currentTime);
            view.UpdateSyncPosition(currentTime);
        }

        private void Player_ImageChanged(object sender, EventArgs<Bitmap> e)
        {
            if (!active || !synching)
                return;

            PlayerScreen player = sender as PlayerScreen;
            if (player == null)
                return;

            if (dynamicSynching)
            {
                if (player.IsPlaying)
                {
                    //log.DebugFormat("Received image from [{0}] ({1}).", GetPlayerIndex(player), player.LocalTime / 1000);
                    currentTime = commonTimeline.GetCommonTime(player, player.LocalTime);

                    IEnumerable<PlayerScreen> otherPlayingPlayers = players.Where(candidate => candidate != player && candidate.IsPlaying);
                    if (otherPlayingPlayers.Any() && resyncOperations < maxResyncOperations)
                    {
                        //----------------------------------------------------------------------------------
                        // Test for desync.
                        // This is not the primary synchronization mechanism, videos should synchronize naturally from being started
                        // at the right time, based on their time origin and the fact that their playback rate relative to real time should match.
                        //
                        // However, for videos started automatically, we can't guarantee that one isn't ahead of the other.
                        // We allow ourselves one attempt at resync here.
                        // This kind of resync can easily misfire and cause judder so it's only used very sparingly.
                        //----------------------------------------------------------------------------------
                        PlayerScreen mostDivergentPlayer = otherPlayingPlayers
                            .OrderByDescending(candidate => Math.Abs(currentTime - commonTimeline.GetCommonTime(candidate, candidate.LocalTime)))
                            .First();
                        long otherTime = commonTimeline.GetCommonTime(mostDivergentPlayer, mostDivergentPlayer.LocalTime);
                        long divergence = Math.Abs(currentTime - otherTime);
                        long frameTime = Math.Max(player.LocalFrameTime, mostDivergentPlayer.LocalFrameTime);
                        if (divergence > frameTime)
                        {
                            resyncOperations++;

                            log.WarnFormat("Synchronization divergence: [{0}]@{1} vs [{2}]@{3}. Resynchronizing.",
                                GetPlayerIndex(player), currentTime / 1000, GetPlayerIndex(mostDivergentPlayer), otherTime / 1000);

                            AlignPlayers(true);
                        }
                    }

                    EnsurePlayersPlaying();
                }
                else if (players.All(candidate => !candidate.IsPlaying))
                {
                    // All players have completed a loop and are waiting.
                    currentTime = 0;
                    EnsurePlayersPlaying();
                }
            }

            UpdateHairLines();

            if (!view.Merging || e.Value == null)
                return;

            Bitmap previous;
            if (lastSyncMergeImages.TryGetValue(player, out previous) && previous != null && !object.ReferenceEquals(previous, e.Value))
                previous.Dispose();
            lastSyncMergeImages[player] = e.Value;
            syncMergeImageOrder.Remove(player);
            syncMergeImageOrder.Add(player);

            UpdateSyncMergeOverlay(!dualSaveInProgress);
        }
        private void UpdateSyncMergeOverlay(bool updateUI)
        {
            PlayerScreen target = referencePlayer != null && players.Contains(referencePlayer) ? referencePlayer : players.FirstOrDefault();
            if (target == null)
                return;

            List<Bitmap> otherImages = players
                .Where(candidate => candidate != target && lastSyncMergeImages.ContainsKey(candidate))
                .Select(candidate => lastSyncMergeImages[candidate])
                .Where(image => image != null)
                .ToList();
            Bitmap overlay = view.Merging ? ImageHelper.GetOverlayComposite(otherImages) : null;
            target.SetSyncMergeImage(overlay, updateUI);

            foreach (PlayerScreen other in players)
            {
                if (other != target)
                    other.SetSyncMergeImage(null, updateUI);
            }
        }
        #endregion

        #region View event handlers
        private void CCtrl_PlayToggled(object sender, EventArgs e)
        {
            if (synching)
            {
                AlignPlayers(false);

                dynamicSynching = view.Playing;
                if (dynamicSynching)
                {
                    resyncOperations = 0;
                    EnsurePlayersPlaying();
                }
            }

            // Propagate the stop call to screens.
            if (!view.Playing)
                Pause();
        }
        private void CCtrl_GotoFirst(object sender, EventArgs e)
        {
            Pause();

            if (synching)
            {
                currentTime = 0;
                GotoTime(currentTime, true);
                UpdateTrkFrame(currentTime);
            }
            else
            {
                foreach (PlayerScreen player in players)
                    player.view.buttonGotoFirst_Click(this, EventArgs.Empty);
            }
        }
        private void CCtrl_GotoPrev(object sender, EventArgs e)
        {
            Pause();

            if (synching)
            {
                if (currentTime > 0)
                {
                    currentTime -= commonTimeline.FrameTime;

                    GotoTime(currentTime, true);
                    UpdateTrkFrame(currentTime);
                }
            }
            else
            {
                foreach (PlayerScreen screen in players)
                    screen.view.buttonGotoPrevious_Click(this, EventArgs.Empty);
            }
        }
        private void CCtrl_GotoNext(object sender, EventArgs e)
        {
            Pause();

            if (synching)
            {
                if (currentTime < commonTimeline.LastTime)
                {
                    currentTime += commonTimeline.FrameTime;

                    GotoTime(currentTime, true);
                    UpdateTrkFrame(currentTime);
                }
            }
            else
            {
                foreach (PlayerScreen player in players)
                    player.view.buttonGotoNext_Click(this, EventArgs.Empty);
            }
        }
        private void CCtrl_GotoLast(object sender, EventArgs e)
        {
            Pause();

            if (synching)
            {
                currentTime = commonTimeline.LastTime;
                GotoTime(currentTime, true);
                UpdateTrkFrame(currentTime);
            }
            else
            {
                foreach (PlayerScreen player in players)
                    player.view.buttonGotoLast_Click(this, EventArgs.Empty);
            }
        }
        private void CCtrl_SyncAsked(object sender, EventArgs e)
        {
            if (!synching)
                return;

            SetSyncPoint(false);
            GotoTime(currentTime, true);
        }
        private void CCtrl_MergeAsked(object sender, EventArgs e)
        {
            if (!synching)
                return;

            log.Debug(String.Format("SyncMerge videos is now {0}", view.Merging.ToString()));

            // This will also do a full refresh, and trigger back Player_ImageChanged().
            foreach (PlayerScreen player in players)
                player.SyncMerge = view.Merging;
        }
        private void CCtrl_PositionChanged(object sender, TimeEventArgs e)
        {
            if (!synching)
                return;

            Pause();

            currentTime = e.Time;
            GotoTime(currentTime, true);
        }
        private void CCtrl_DualSaveAsked(object sender, EventArgs e)
        {
            if (!synching)
                return;

            Pause();

            dualSaveInProgress = true;

            DualVideoExporter exporter = new DualVideoExporter();
            List<PlayerScreen> exportPlayers;
            List<int> exportSlots;
            GetExportOrder(out exportPlayers, out exportSlots);
            exporter.Export(commonTimeline, exportPlayers, exportSlots, layoutScreenCount, layoutColumns, layoutRows, view.Merging);

            dualSaveInProgress = false;

            GotoTime(currentTime, true);
        }
        private void CCtrl_DualSnapshotAsked(object sender, EventArgs e)
        {
            if (!synching)
                return;

            Pause();
            List<PlayerScreen> exportPlayers;
            List<int> exportSlots;
            GetExportOrder(out exportPlayers, out exportSlots);
            DualSnapshoter.Save(exportPlayers, exportSlots, layoutScreenCount, layoutColumns, layoutRows, view.Merging);
        }

        private void CCtrl_GotoPrevKeyframe(object sender, EventArgs e)
        {
            Pause();

            if (!synching)
                return;

            foreach (PlayerScreen player in players)
                player.GotoPrevKeyframe();
        }
        private void CCtrl_GotoNextKeyframe(object sender, EventArgs e)
        {
            Pause();

            if (!synching)
                return;

            foreach (PlayerScreen player in players)
                player.GotoNextKeyframe();
        }
        private void CCtrl_AddKeyframe(object sender, EventArgs e)
        {
            Pause();

            if (!synching)
                return;

            foreach (PlayerScreen player in players)
                player.AddKeyframe();
        }
        private void CCtrl_GotoSync(object sender, EventArgs e)
        {
            Pause();

            if (!synching)
                return;

            PlayerScreen effectiveReference = referencePlayer ?? players.First();
            currentTime = commonTimeline.GetCommonTime(effectiveReference, effectiveReference.LocalTimeOriginPhysical);
            GotoTime(currentTime, true);
            UpdateTrkFrame(currentTime);
        }

        #endregion

        #region Entering/Exiting dual player management
        private void Enter(IList<PlayerScreen> playerScreens)
        {
            Exit();

            players.AddRange(playerScreens);
            if (referencePlayer == null || !players.Contains(referencePlayer))
                referencePlayer = players.FirstOrDefault();

            foreach (PlayerScreen player in players)
                AddEventHandlers(player);

            active = true;
        }
        private void Exit()
        {
            synching = false;
            dynamicSynching = false;
            foreach (Bitmap image in lastSyncMergeImages.Values)
            {
                if (image != null)
                    image.Dispose();
            }
            lastSyncMergeImages.Clear();
            syncMergeImageOrder.Clear();

            if (active)
            {
                foreach (PlayerScreen player in players)
                {
                    RemoveEventHandlers(player);
                    player.Synched = false;
                }

                players.Clear();
            }

            referencePlayer = null;
            active = false;
        }
        private void AddEventHandlers(PlayerScreen player)
        {
            player.PlayStarted += Player_PlayStarted;
            player.PauseAsked += Player_PauseAsked;
            player.SpeedChanged += Player_SpeedChanged;
            player.HighSpeedFactorChanged += Player_HighSpeedFactorChanged;
            player.TimeOriginChanged += Player_TimeOriginChanged;
            player.ImageChanged += Player_ImageChanged;
        }
        private void RemoveEventHandlers(PlayerScreen player)
        {
            player.PlayStarted -= Player_PlayStarted;
            player.PauseAsked -= Player_PauseAsked;
            player.SpeedChanged -= Player_SpeedChanged;
            player.HighSpeedFactorChanged -= Player_HighSpeedFactorChanged;
            player.TimeOriginChanged -= Player_TimeOriginChanged;
            player.ImageChanged -= Player_ImageChanged;
        }
        #endregion

        public void ExecuteDualCommand(HotkeyCommand playerCommand)
        {
            // A player has detected that a hotkey it received should actually be handled at the dual player level.
            // At that point there is still two options, either it's a true dual player command,
            // something normally bound to controls in the common controls,
            // or it's a multiplexed command, a command that should simply be forwarded to each player.

            HotkeyCommand dualCommand = hotkeys.FirstOrDefault(hk => hk != null && hk.KeyData == playerCommand.KeyData);
            if (dualCommand == null)
                return;

            DualPlayerCommands command = (DualPlayerCommands)dualCommand.CommandCode;

            switch(command)
            {
                case DualPlayerCommands.GotoPreviousKeyframe:
                case DualPlayerCommands.GotoNextKeyframe:
                case DualPlayerCommands.GotoSyncPoint:
                case DualPlayerCommands.AddKeyframe:
                    foreach (PlayerScreen player in players)
                        player.ExecuteScreenCommand(playerCommand.CommandCode);
                    break;

                default:
                    view.ExecuteDualCommand(dualCommand.CommandCode);
                    break;
            }
        }


        #region Synchronization
        public void ResetSync()
        {
            if (!active)
                return;

            synching = false;
            dynamicSynching = false;
            Pause();

            if (players.Any(player => !player.Full))
                return;

            synching = true;
            foreach (PlayerScreen player in players)
                player.Synched = true;

            if (PreferencesManager.PlayerPreferences.SyncLockSpeed)
            {
                double percentage = players.Min(player => player.RealtimePercentage);
                foreach (PlayerScreen player in players)
                    player.RealtimePercentage = percentage;
            }

            InitializeSync();

            foreach (PlayerScreen player in players)
                player.SyncMerge = false;
            StopMerge();

            GotoTime(currentTime, true);
        }

        private void InitializeSync()
        {
            commonTimeline.Initialize(players, this.referencePlayer);
            currentTime = 0;
            view.SetupTrkFrame(0, commonTimeline.LastTime, currentTime);
            PlayerScreen effectiveReference = this.referencePlayer ?? players.First();
            view.UpdateSyncPosition(commonTimeline.GetCommonTime(effectiveReference, effectiveReference.LocalTimeOriginPhysical));
            UpdateHairLines();

            log.Debug("Synchronization initialized.");
        }

        private void SetSyncPoint(bool intervalOnly)
        {
            log.DebugFormat("Resetting time origins for {0} players.", players.Count);
            foreach (PlayerScreen player in players)
                player.LocalTimeOriginPhysical = player.LocalTime;

            commonTimeline.Initialize(players, this.referencePlayer);
            PlayerScreen effectiveReference = this.referencePlayer ?? players.First();
            currentTime = commonTimeline.GetCommonTime(effectiveReference, effectiveReference.LocalTime);

            view.SetupTrkFrame(0, commonTimeline.LastTime, currentTime);
            view.UpdateSyncPosition(currentTime);
        }

        private void GotoTime(long commonTime, bool allowUIUpdate)
        {
            foreach (PlayerScreen player in players)
                GotoTime(player, commonTime, allowUIUpdate);

            UpdateHairLines();
        }

        private void GotoTime(PlayerScreen player, long commonTime, bool allowUIUpdate)
        {
            long localTime = commonTimeline.GetLocalTime(player, commonTime);
            localTime = Math.Max(0, localTime);

            if (player.LocalTime != localTime)
                player.GotoTime(localTime, allowUIUpdate);
        }

        private void UpdateHairLines()
        {
            if (players.Count == 0)
                return;

            List<long> playerTimes = players.Select(player => commonTimeline.GetCommonTime(player, player.LocalTime)).ToList();
            view.UpdateHairlines(playerTimes);
        }

        /// <summary>
        /// Force both players to align on a common time.
        /// Used if players may have moved independently from the common tracker.
        /// Should not be used while playback is active.
        /// </summary>
        private void AlignPlayers(bool catchup)
        {
            IEnumerable<long> playerTimes = players.Select(player => commonTimeline.GetCommonTime(player, player.LocalTime));

            if (catchup)
                currentTime = playerTimes.Max();
            else
                currentTime = playerTimes.Min();

            log.DebugFormat("Aligning players to {0}.", currentTime / 1000);
            GotoTime(currentTime, true);
        }

        private void EnsurePlayersPlaying()
        {
            foreach (PlayerScreen player in players)
            {
                if (!player.IsPlaying && !commonTimeline.IsOutOfBounds(player, currentTime))
                    player.EnsurePlaying();
            }
        }

        #endregion

        private int GetPlayerIndex(PlayerScreen player)
        {
            return players.IndexOf(player);
        }
        private void GetExportOrder(out List<PlayerScreen> exportPlayers, out List<int> exportSlots)
        {
            exportPlayers = new List<PlayerScreen>();
            exportSlots = new List<int>();
            if (referencePlayer != null && players.Contains(referencePlayer))
            {
                int index = players.IndexOf(referencePlayer);
                exportPlayers.Add(referencePlayer);
                exportSlots.Add(playerSlotIndices[index]);
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == referencePlayer)
                    continue;
                exportPlayers.Add(players[i]);
                exportSlots.Add(playerSlotIndices[i]);
            }
        }
    }
}
