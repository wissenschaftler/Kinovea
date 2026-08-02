#region License
/*
Copyright © Joan Charmant 2012.
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

namespace Kinovea.Services
{
    /// <summary>
    /// Holds the screen list and configuration to be used at launch.
    /// This is used in the context of command line and auto recovery.
    /// </summary>
    public static class LaunchSettingsManager
    {
        public static List<IScreenDescription> ScreenDescriptions { get; } = new List<IScreenDescription>();

        public static bool ShowExplorer { get; set; } = true;

        public static string Name { get; set; }

        /// <summary>
        /// Optional layout columns from workspace. 0 = unspecified.
        /// </summary>
        public static int LayoutColumns { get; set; }

        /// <summary>
        /// Optional layout rows from workspace. 0 = unspecified.
        /// </summary>
        public static int LayoutRows { get; set; }

        public static void ClearScreenDescriptions()
        {
            ScreenDescriptions.Clear();
            LayoutColumns = 0;
            LayoutRows = 0;
        }
        public static void AddScreenDescription(IScreenDescription screenDescription)
        {
            ScreenDescriptions.Add(screenDescription);
        }

        public static void SetLayout(int columns, int rows)
        {
            LayoutColumns = Math.Max(0, columns);
            LayoutRows = Math.Max(0, rows);
        }
    }
}
