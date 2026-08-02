using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kinovea.Services;
using Kinovea.Video;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// Local file lookup for the per-screen search box.
    /// Phase 1: recent folders + shortcuts + common folders.
    /// Phase 2: user profile then fixed drives (broad search) if needed.
    /// Keyword search is intended to run on a background thread.
    /// </summary>
    public static class FileSearchHelper
    {
        public const int MaxResults = 50;
        public const int MaxSearchHistory = 15;
        public const int MaxRecentFolders = 50;

        private static readonly List<string> searchHistory = new List<string>();
        private static readonly object historyLock = new object();
        private static readonly string[] SkippedDirectoryNames = new[]
        {
            "$Recycle.Bin",
            "System Volume Information",
            "Windows",
            "Program Files",
            "Program Files (x86)",
            "ProgramData",
            "Recovery",
            "$WINDOWS.~BT"
        };

        public static IList<string> SearchHistory
        {
            get
            {
                lock (historyLock)
                    return searchHistory.ToList();
            }
        }

        public static void RememberSearch(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
                return;

            string trimmed = entry.Trim();
            lock (historyLock)
            {
                PreferencesManager.UpdateRecents(trimmed, searchHistory, MaxSearchHistory);
            }
        }

        public static IEnumerable<string> GetDropdownSuggestions()
        {
            List<string> items = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            lock (historyLock)
            {
                foreach (string entry in searchHistory)
                    AddUnique(items, seen, entry);
            }

            FileExplorerPreferences prefs = PreferencesManager.FileExplorerPreferences;
            if (prefs.RecentFiles != null)
            {
                foreach (string file in prefs.RecentFiles)
                    AddUnique(items, seen, file);
            }

            if (prefs.RecentFolders != null)
            {
                foreach (string folder in prefs.RecentFolders)
                    AddUnique(items, seen, folder);
            }

            return items;
        }

        /// <summary>
        /// Preferential roots: recent folders, shortcut folders, My Videos.
        /// </summary>
        public static List<string> GetPreferentialSearchRoots()
        {
            List<string> roots = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            FileExplorerPreferences prefs = PreferencesManager.FileExplorerPreferences;

            if (prefs.RecentFolders != null)
            {
                foreach (string folder in prefs.RecentFolders)
                {
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                        AddUnique(roots, seen, folder);
                }
            }

            if (prefs.ShortcutFolders != null)
            {
                foreach (ShortcutFolder shortcut in prefs.ShortcutFolders)
                {
                    if (shortcut != null && !string.IsNullOrEmpty(shortcut.Location) && Directory.Exists(shortcut.Location))
                        AddUnique(roots, seen, shortcut.Location);
                }
            }

            TryAddSpecialFolder(roots, seen, Environment.SpecialFolder.MyVideos);
            return roots;
        }

        public static List<string> GetBroadSearchRoots()
        {
            List<string> roots = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Prefer user-visible locations so the short phase-2 window is more useful.
            TryAddSpecialFolder(roots, seen, Environment.SpecialFolder.UserProfile);
            TryAddSpecialFolder(roots, seen, Environment.SpecialFolder.Desktop);
            TryAddSpecialFolder(roots, seen, Environment.SpecialFolder.MyDocuments);
            TryAddSpecialFolder(roots, seen, Environment.SpecialFolder.MyVideos);

            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady || drive.DriveType != System.IO.DriveType.Fixed)
                        continue;
                    AddUnique(roots, seen, drive.RootDirectory.FullName);
                }
            }
            catch
            {
            }

            return roots;
        }

        public static FileSearchResult Resolve(string input)
        {
            FileSearchResult result = new FileSearchResult();
            if (string.IsNullOrWhiteSpace(input))
                return result;

            string trimmed = input.Trim().Trim('"');

            try
            {
                if (LooksLikePath(trimmed))
                {
                    if (File.Exists(trimmed))
                    {
                        result.ExactPath = trimmed;
                        return result;
                    }

                    if (Directory.Exists(trimmed))
                    {
                        result.Query = trimmed;
                        result.Matches = SearchByKeywordInRoots("*", new List<string> { trimmed }, 2, 3);
                        return result;
                    }

                    result.PathNotFound = true;
                    result.Query = trimmed;
                    return result;
                }
            }
            catch
            {
                // Fall through to keyword search.
            }

            result.Query = trimmed;
            result.Matches = SearchByKeyword(trimmed);
            return result;
        }

        public static List<string> SearchByKeyword(string keyword)
        {
            List<string> matches = new List<string>();
            if (string.IsNullOrWhiteSpace(keyword))
                return matches;

            string needle = keyword.Trim();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Phase 1: preferential roots (intended to run off the UI thread).
            DateTime phase1Deadline = DateTime.UtcNow.AddSeconds(8);
            foreach (string root in GetPreferentialSearchRoots())
            {
                if (DateTime.UtcNow > phase1Deadline || matches.Count >= MaxResults)
                    break;
                SearchDirectory(root, needle, matches, seen, phase1Deadline, 0, 4);
            }

            if (matches.Count > 0)
                return matches;

            // Phase 2: broader scan on the user profile first, then fixed drive roots.
            DateTime phase2Deadline = DateTime.UtcNow.AddSeconds(60);
            foreach (string root in GetBroadSearchRoots())
            {
                if (DateTime.UtcNow > phase2Deadline || matches.Count >= MaxResults)
                    break;
                SearchDirectory(root, needle, matches, seen, phase2Deadline, 0, 5);
            }

            return matches;
        }

        private static List<string> SearchByKeywordInRoots(string keyword, List<string> roots, int seconds, int maxDepth)
        {
            List<string> matches = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
            string needle = keyword == "*" ? "" : keyword;

            foreach (string root in roots)
            {
                if (DateTime.UtcNow > deadline || matches.Count >= MaxResults)
                    break;
                SearchDirectory(root, needle, matches, seen, deadline, 0, maxDepth);
            }

            return matches;
        }

        private static void SearchDirectory(string directory, string keyword, List<string> matches, HashSet<string> seen, DateTime deadline, int depth, int maxDepth)
        {
            if (depth > maxDepth || DateTime.UtcNow > deadline || matches.Count >= MaxResults)
                return;

            try
            {
                foreach (string file in Directory.EnumerateFiles(directory))
                {
                    if (DateTime.UtcNow > deadline || matches.Count >= MaxResults)
                        return;

                    string extension = Path.GetExtension(file);
                    if (!VideoTypeManager.IsSupported(extension))
                        continue;

                    string name = Path.GetFileName(file);
                    if (keyword.Length > 0 && name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    AddUnique(matches, seen, file);
                }

                foreach (string subDir in Directory.EnumerateDirectories(directory))
                {
                    if (DateTime.UtcNow > deadline || matches.Count >= MaxResults)
                        return;

                    string name = Path.GetFileName(subDir);
                    if (ShouldSkipDirectory(name))
                        continue;

                    SearchDirectory(subDir, keyword, matches, seen, deadline, depth + 1, maxDepth);
                }
            }
            catch
            {
                // Skip inaccessible directories.
            }
        }

        private static bool ShouldSkipDirectory(string name)
        {
            if (string.IsNullOrEmpty(name))
                return true;
            if (name.StartsWith(".", StringComparison.Ordinal))
                return true;

            foreach (string skipped in SkippedDirectoryNames)
            {
                if (name.Equals(skipped, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool LooksLikePath(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            if (text.IndexOfAny(new[] { '\\', '/' }) >= 0)
                return true;

            if (text.Length >= 2 && char.IsLetter(text[0]) && text[1] == ':')
                return true;

            return Path.IsPathRooted(text);
        }

        private static void TryAddSpecialFolder(List<string> roots, HashSet<string> seen, Environment.SpecialFolder folder)
        {
            try
            {
                string path = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    AddUnique(roots, seen, path);
            }
            catch
            {
            }
        }

        private static void AddUnique(List<string> list, HashSet<string> seen, string value)
        {
            if (string.IsNullOrEmpty(value) || !seen.Add(value))
                return;
            list.Add(value);
        }
    }

    public class FileSearchResult
    {
        public string ExactPath { get; set; }
        public bool PathNotFound { get; set; }
        public string Query { get; set; }
        public List<string> Matches { get; set; } = new List<string>();
    }
}
