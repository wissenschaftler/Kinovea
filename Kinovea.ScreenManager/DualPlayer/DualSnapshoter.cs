using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using Kinovea.ScreenManager.Languages;
using System.IO;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    public static class DualSnapshoter
    {
        public static void Save(PlayerScreen leftPlayer, PlayerScreen rightPlayer, bool merging)
        {
            Save(
                new PlayerScreen[] { leftPlayer, rightPlayer },
                new int[] { 0, 1 },
                2,
                merging);
        }

        public static void Save(IList<PlayerScreen> players, IList<int> slotIndices, int screenCount, bool merging)
        {
            int columns;
            int rows;
            ScreenLayoutSpec.GetDefaultGrid(screenCount, out columns, out rows);
            Save(players, slotIndices, screenCount, columns, rows, merging);
        }

        public static void Save(IList<PlayerScreen> players, IList<int> slotIndices, int screenCount, int columns, int rows, bool merging)
        {
            ValidateArguments(players, slotIndices, screenCount);

            string filename = GetFilename(players);
            if (string.IsNullOrEmpty(filename))
                return;

            List<Bitmap> images = new List<Bitmap>();
            try
            {
                int imageCount = merging ? 1 : players.Count;
                for (int i = 0; i < imageCount; i++)
                    images.Add(players[i] == null ? null : players[i].GetFlushedImage());

                IList<int> effectiveSlotIndices = merging ? new int[] { 0 } : slotIndices;
                int effectiveScreenCount = merging ? 1 : screenCount;
                int effectiveColumns = merging ? 1 : columns;
                int effectiveRows = merging ? 1 : rows;
                using (Bitmap composite = ImageHelper.GetComposite(images, effectiveSlotIndices, effectiveScreenCount, effectiveColumns, effectiveRows, false))
                {
                    ImageHelper.Save(filename, composite);
                }
            }
            finally
            {
                for (int i = 0; i < images.Count; i++)
                {
                    if (images[i] != null)
                        images[i].Dispose();
                }
            }
            
            NotificationCenter.RaiseRefreshFileExplorer(null, false);
        }

        private static string GetFilename(IList<PlayerScreen> players)
        {
            using (SaveFileDialog dlgSave = new SaveFileDialog())
            {
                dlgSave.Title = ScreenManagerLang.Generic_SaveImage;
                dlgSave.RestoreDirectory = true;
                dlgSave.Filter = FilesystemHelper.SaveImageFilter();
                dlgSave.FilterIndex = FilesystemHelper.GetFilterIndex(dlgSave.Filter, PreferencesManager.PlayerPreferences.ImageFormat);
                dlgSave.FileName = GetDefaultFilename(players);

                if (dlgSave.ShowDialog() != DialogResult.OK)
                    return null;

                return dlgSave.FileName;
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

        private static void ValidateArguments(IList<PlayerScreen> players, IList<int> slotIndices, int screenCount)
        {
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
