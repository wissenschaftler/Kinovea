using System;
using System.Collections.Generic;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    public sealed class ScreenLayoutSpec
    {
        public const int MaximumScreenCount = 4;

        private readonly List<ScreenType> screenTypes = new List<ScreenType>();

        public IList<ScreenType> ScreenTypes
        {
            get { return screenTypes.AsReadOnly(); }
        }

        public int ScreenCount
        {
            get { return screenTypes.Count; }
        }

        public int Columns { get; private set; }

        public int Rows { get; private set; }

        public ScreenLayoutSpec(IEnumerable<ScreenType> types)
            : this(types, 0, 0)
        {
        }

        public ScreenLayoutSpec(IEnumerable<ScreenType> types, int columns, int rows)
        {
            if (types == null)
                throw new ArgumentNullException("types");

            screenTypes.AddRange(types);
            if (screenTypes.Count < 1 || screenTypes.Count > MaximumScreenCount)
                throw new ArgumentOutOfRangeException("types");

            if (columns <= 0 || rows <= 0)
                GetDefaultGrid(screenTypes.Count, out columns, out rows);

            if (columns * rows != screenTypes.Count)
                throw new ArgumentException("Columns and rows must match the screen count.");

            Columns = columns;
            Rows = rows;
        }

        public static ScreenLayoutSpec Playback(int count)
        {
            return Playback(count, 0, 0);
        }

        public static ScreenLayoutSpec Playback(int count, int columns, int rows)
        {
            List<ScreenType> types = new List<ScreenType>();
            for (int i = 0; i < count; i++)
                types.Add(ScreenType.Playback);

            return new ScreenLayoutSpec(types, columns, rows);
        }

        public static ScreenLayoutSpec Capture(int count)
        {
            return Capture(count, 0, 0);
        }

        public static ScreenLayoutSpec Capture(int count, int columns, int rows)
        {
            List<ScreenType> types = new List<ScreenType>();
            for (int i = 0; i < count; i++)
                types.Add(ScreenType.Capture);

            return new ScreenLayoutSpec(types, columns, rows);
        }

        public static void GetDefaultGrid(int count, out int columns, out int rows)
        {
            if (count == 4)
            {
                columns = 2;
                rows = 2;
            }
            else
            {
                columns = Math.Max(1, count);
                rows = 1;
            }
        }
    }
}
