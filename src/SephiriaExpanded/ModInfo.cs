using System;
using System.IO;

namespace SephiriaExpanded
{
    internal static class ModInfo
    {
        internal const string Name = "Sephiria Expanded";
        internal const string Version = "2.0";
        internal const string Author = "psyche";
        private const string AddOnsFolderName = "AddOns";

        internal static string Folder
        {
            get
            {
                try
                {
                    string location = typeof(ModInfo).Assembly.Location;
                    return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
                }
                catch
                {
                    return null;
                }
            }
        }

        internal static bool IsAddOnInstall
        {
            get
            {
                string folder = Folder;
                if (folder == null) return false;

                string parent = Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                return parent != null && string.Equals(Path.GetFileName(parent), AddOnsFolderName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
