using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    public class CommonTimeline
    {
        public long LastTime
        {
            get { return commonLastTime; }
        }

        public long FrameTime
        {
            get { return frameTime; }
        }

        Dictionary<Guid, PlayerSyncInfo> syncInfos = new Dictionary<Guid, PlayerSyncInfo>();
        private long commonLastTime;
        private long frameTime;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        /// <summary>
        /// Initialize synchro using players current time origins.
        /// </summary>
        public void Initialize(PlayerScreen leftPlayer, PlayerScreen rightPlayer)
        {
            Initialize(new PlayerScreen[] { leftPlayer, rightPlayer });
        }

        public void Initialize(IList<PlayerScreen> players)
        {
            Initialize(players, players != null && players.Count > 0 ? players[0] : null);
        }

        public void Initialize(IList<PlayerScreen> players, PlayerScreen referencePlayer)
        {
            syncInfos.Clear();
            commonLastTime = 0;
            frameTime = 0;
            if (players == null || players.Count == 0)
                return;

            PlayerScreen reference = referencePlayer != null && players.Contains(referencePlayer) ? referencePlayer : players[0];
            long referenceDuration = Math.Max(1, reference.LocalLastTime - reference.LocalTimeOriginPhysical);
            long latestNormalizedOrigin = long.MinValue;

            foreach (PlayerScreen player in players)
            {
                PlayerSyncInfo info = new PlayerSyncInfo();
                info.SyncTime = player.LocalTimeOriginPhysical;
                info.LastTime = player.LocalLastTime;
                info.Scale = 1.0;

                if (PreferencesManager.PlayerPreferences.SyncByMotion)
                {
                    long duration = Math.Max(1, info.LastTime - info.SyncTime);
                    info.Scale = (double)duration / referenceDuration;
                }

                syncInfos[player.Id] = info;
                latestNormalizedOrigin = Math.Max(latestNormalizedOrigin, (long)(info.SyncTime / info.Scale));
            }

            foreach (PlayerScreen player in players)
            {
                PlayerSyncInfo info = syncInfos[player.Id];
                long normalizedOrigin = (long)(info.SyncTime / info.Scale);
                info.Offset = latestNormalizedOrigin - normalizedOrigin;

                long playerFrameTime = (long)(player.LocalFrameTime * info.Scale);
                frameTime = frameTime == 0 ? playerFrameTime : Math.Min(frameTime, playerFrameTime);
                commonLastTime = Math.Max(commonLastTime, GetCommonTime(player, info.LastTime));
            }
        }
        
        /// <summary>
        /// Converts a common time into a local time for a specific player.
        /// </summary>
        public long GetLocalTime(PlayerScreen player, long commonTime)
        {
            if (!syncInfos.ContainsKey(player.Id))
                return 0;

            return ((long)(commonTime * syncInfos[player.Id].Scale)) - syncInfos[player.Id].Offset;
        }

        /// <summary>
        /// Converts a local time in a player into a common time.
        /// </summary>
        public long GetCommonTime(PlayerScreen player, long localTime)
        {
            if (!syncInfos.ContainsKey(player.Id))
                return 0;

            return syncInfos[player.Id].Offset + ((long)(localTime / syncInfos[player.Id].Scale));
        }

        /// <summary>
        /// Test whether a given common time is outside the range of the player.
        /// </summary>
        public bool IsOutOfBounds(PlayerScreen player, long commonTime)
        {
            if (!syncInfos.ContainsKey(player.Id))
                return true;

            long localTime = GetLocalTime(player, commonTime);
            return localTime < 0 || localTime > syncInfos[player.Id].LastTime;
        }
    }
}
