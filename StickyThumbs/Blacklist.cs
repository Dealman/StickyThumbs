using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace StickyThumbs
{
    public static class Blacklist
    {
        static HashSet<string> Processes { get; set; } = new HashSet<string>() { "SystemSettings", "ApplicationFrameHost" };

        static string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickyThumbs");
        static string FilePath = Path.Combine(FolderPath, "Blacklist.json");

        #region Public Methods
        public static bool Contains(string processName)
        {
            return Processes.Contains(processName);
        }

        public static List<string> GetBlacklist()
        {
            return Processes.ToList<string>();
        }

        public static void Add(string processName)
        {
            Processes.Add(processName);
            Save();
        }

        public static void Remove(string processName)
        {
            if (Processes.Contains(processName))
            {
                Processes.Remove(processName);
                Save();
            }
        }

        public static void Save()
        {
            Create();
        }

        public static void Load()
        {
            if (!Exists())
                Create();

            var fileContents = File.ReadAllText(FilePath);
            HashSet<string>? hashSet = JsonSerializer.Deserialize<HashSet<string>>(fileContents);
            if (hashSet is not null)
            {
                // Add defaults to HashSet if not present, only contains unique strings, so no risk of duplicates
                Processes.ToList<string>().ForEach(x => hashSet.Add(x));
                Processes = hashSet;
            }
        }
        #endregion

        #region Private Methods
        static bool Exists()
        {
            return File.Exists(FilePath);
        }

        static void Create()
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            var json = JsonSerializer.Serialize(Processes);
            File.WriteAllText(FilePath, json);
        }
        #endregion
    }
}
