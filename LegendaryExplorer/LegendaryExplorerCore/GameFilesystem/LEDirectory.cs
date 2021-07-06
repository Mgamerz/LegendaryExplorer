﻿#if WINDOWS
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
#endif

namespace LegendaryExplorerCore.GameFilesystem
{
    /// <summary>
    /// Contains information about the main LE directory as well as Launcher location. LE[X]Directory classes will use the LEDirectory default path to build their game paths.
    /// </summary>
    public class LEDirectory
    {
        /// <summary>
        /// Uses the registry to find the default game path for the Legendary Edition installation. On non-windows platforms, this method does nothing and simply returns false.
        /// </summary>
        /// <returns></returns>
        public static bool LookupDefaultPath()
        {
#if WINDOWS
            RegistryKey biowareKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\BioWare", false);
            RegistryKey leKey;
            if (biowareKey != null)
            {
                foreach (var key in biowareKey.GetSubKeyNames())
                {
                    if (key.EndsWith("Legendary Edition", StringComparison.InvariantCultureIgnoreCase))
                    {
                        leKey = biowareKey.OpenSubKey(key, false);
                        if (leKey == null) break;

                        string directory = (string)leKey.GetValue("Install Dir", null);
                        if (directory != null)
                        {
                            LegendaryExplorerCoreLibSettings.Instance.LEDirectory = directory;
                            return true;
                        }
                    }
                }
            }

            return false;
#else
            return false; // NOT IMPLEMENTED ON OTHER PLATFORMS
#endif
        }

        /// <summary>
        /// Only useful if executable is run with -NoHomeDir. Otherwise this folder won't exist and will be worthless.
        /// </summary>
        public static string BioWareDocumentsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare", @"Mass Effect Legendary Edition");

        public static string LauncherPath => GetLauncherPath();
        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new[] { "MassEffectLauncher.exe" });

        // This is here just for consistency with the other directory classes - some external tools use this
        public static string ExecutableFolder => GetExecutableDirectory();
        /// <summary>
        /// Proxy for GetLauncherPath()
        /// </summary>
        /// <param name="rootPathOverride"></param>
        /// <returns></returns>
        public static string GetExecutableDirectory(string rootPathOverride = null) => GetLauncherPath(rootPathOverride);

        public static string GetLauncherPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = LegendaryExplorerCoreLibSettings.Instance.LEDirectory;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "Game", "Launcher");
        }

        public static string LauncherExecutable => GetLauncherExecutable();
        public static string GetLauncherExecutable(string rootPathOverride = null)
        {
            var launcherPath = GetLauncherPath(rootPathOverride);
            if (launcherPath == null) return null;
            return Path.Combine(launcherPath, "MassEffectLauncher.exe");
        }

        public static readonly ReadOnlyCollection<string> VanillaLauncherDlls = Array.AsReadOnly(new[]
        {
            "bink2w64.dll"
        });
    }
}
