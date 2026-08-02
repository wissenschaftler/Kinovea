using System;
using System.Collections.Generic;
using System.IO;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Snapshot of a multi-screen layout used for peak-session restore,
    /// plus single-slot entries for undo-close.
    /// </summary>
    public class SessionScreenSnapshot
    {
        public int Columns { get; set; }
        public int Rows { get; set; }
        public List<Slot> Slots { get; private set; } = new List<Slot>();

        public class Slot
        {
            public int Index { get; set; }
            public ScreenType ScreenType { get; set; }
            public IScreenDescription Description { get; set; }
            public string AnnotationsPath { get; set; }
        }

        public void ClearAnnotationFiles()
        {
            foreach (Slot slot in Slots)
                DeleteAnnotationFile(slot);
        }

        public static void DeleteAnnotationFile(Slot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.AnnotationsPath))
                return;

            try
            {
                if (File.Exists(slot.AnnotationsPath))
                    File.Delete(slot.AnnotationsPath);
            }
            catch
            {
            }

            slot.AnnotationsPath = null;
        }
    }
}
