﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace LegendaryExplorerCore.GameFilesystem
{
    public static class LE3Directory
    {
        public static string BioGamePath => GetBioGamePath();
        public static string GetBioGamePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "BIOGame");
        }

        public static string DLCPath => GetDLCPath();
        public static string GetDLCPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetBioGamePath(rootPathOverride), "DLC");
        }

        public static string CookedPCPath => GetCookedPCPath();
        public static string GetCookedPCPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetBioGamePath(rootPathOverride), CookedName);
        }

        public static string ExecutableFolder => GetExecutableDirectory();
        public static string GetExecutableDirectory(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "Binaries", "Win64");
        }

        public static string ExecutablePath => GetExecutablePath();
        public static string GetExecutablePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "MassEffect3.exe");
        }

        public static string ASIPath => GetASIPath();
        public static string GetASIPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return null; //Path.Combine(GetExecutableDirectory(rootPathOverride), "asi"); //TODO: Implement in LEX once asis work
        }

        public static string TextureModMarkerPath => GetTextureModMarkerPath();
        public static string GetTextureModMarkerPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return null; //Path.Combine(GetCookedPCPath(rootPathOverride), "adv_combat_tutorial_xbox_D_Int.afc");
        }

        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new [] { "MassEffect3.exe" });

        public static ReadOnlyCollection<string> VanillaDlls = Array.AsReadOnly(new []
        {
            "amd_ags_x64.dll",
            "bink2w64.dll",
            "dbdata.dll",
            "oo2core_8_win64.dll",
            "PhysXCooking64.dll",
            "PhysXCore64.dll"
        });

        public static string BioWareDocumentsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare", @"Mass Effect 3");
        public static string LODConfigFile => Path.Combine(BioWareDocumentsPath, @"BIOGame", @"Config", @"GamerSettings.ini");
        public static string CookedName => "CookedPCConsole";


        private static string _DefaultGamePath;
        public static string DefaultGamePath
        {
            get
            {
                if (string.IsNullOrEmpty(_DefaultGamePath))
                    return null;
                return Path.GetFullPath(_DefaultGamePath); //normalize
            }
            set
            {
                if (value != null)
                {
                    if (value.Contains("BioGame", StringComparison.OrdinalIgnoreCase))
                        value = value.Substring(0, value.LastIndexOf("BioGame", StringComparison.OrdinalIgnoreCase));
                }
                _DefaultGamePath = value;
            }
        }
        
        // Is this useful?
        public static string TocFile => DefaultGamePath != null ? Path.Combine(DefaultGamePath, @"BIOGame\PCConsoleTOC.bin") : null;



        static LE3Directory()
        {
            ReloadDefaultGamePath(false);
        }

        public static void ReloadDefaultGamePath(bool forceUseRegistry = false)
        {
            if (!forceUseRegistry && !string.IsNullOrEmpty(LegendaryExplorerCorLibSettings.Instance?.LEDirectory))
            {
                DefaultGamePath = Path.Join(LegendaryExplorerCorLibSettings.Instance.LEDirectory, "ME3");
            }
            else
            {
#if WINDOWS
                // TODO: Implement in LEX. Could extract a method as this will be the exact same as LE1 & LE2
                //string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                //string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                //string subkey = @"BioWare\Mass Effect 3";

                //string keyName = hkey32 + subkey;
                //string test = (string)Registry.GetValue(keyName, "Install Dir", null);
                //if (test != null)
                //{
                //    DefaultGamePath = test;
                //    LegendaryExplorerCorLibSettings.Instance.ME3Directory = DefaultGamePath;
                //    return;
                //}

                //keyName = hkey64 + subkey;
                //DefaultGamePath = (string)Registry.GetValue(keyName, "Install Dir", null);
                //if (DefaultGamePath != null)
                //{
                //    DefaultGamePath += Path.DirectorySeparatorChar;
                //    LegendaryExplorerCorLibSettings.Instance.ME3Directory = DefaultGamePath;
                //}
#endif
            }
        }

        public static readonly CaseInsensitiveDictionary<string> OfficialDLCNames = new CaseInsensitiveDictionary<string>
        {
            ["DLC_HEN_PR"] = "From Ashes",
            ["DLC_OnlinePassHidCE"] = "Collectors Edition Content",
            ["DLC_CON_END"] = "Extended Cut",
            ["DLC_CON_GUN01"] = "Firefight Pack",
            ["DLC_EXP_Pack001"] = "Leviathan",
            ["DLC_UPD_Patch01"] = "Multiplayer Balance Changes Cache 1",
            ["DLC_CON_GUN02"] = "Groundside Resistance Pack",
            ["DLC_EXP_Pack002"] = "Omega",
            ["DLC_CON_APP01"] = "Alternate Appearance Pack 1",
            ["DLC_UPD_Patch02"] = "Multiplayer Balance Changes Cache 2",
            ["DLC_EXP_Pack003_Base"] = "Citadel - Part I",
            ["DLC_EXP_Pack003"] = "Citadel - Part II",
            ["DLC_CON_DH1"] = "Genesis 2",
            ["DLC_CON_PRO1"] = "Unknown",
            ["DLC_CON_PRO2"] = "Unknown",
            ["DLC_CON_PRO3"] = "Unknown",
            ["DLC_CON_PRO4"] = "Unknown",
            ["DLC_CON_PRO5"] = "Unknown",
            ["DLC_CON_PRO6"] = "Unknown",
            ["DLC_METR_Patch01"] = "Day 1 Patch?"
        };

        public static readonly ReadOnlyCollection<string> OfficialDLC = Array.AsReadOnly(new[]
        {
            "DLC_HEN_PR",
            "DLC_OnlinePassHidCE",
            "DLC_CON_END",
            "DLC_CON_GUN01",
            "DLC_EXP_Pack001",
            "DLC_UPD_Patch01",
            "DLC_CON_GUN02",
            "DLC_EXP_Pack002",
            "DLC_CON_APP01",
            "DLC_UPD_Patch02",
            "DLC_EXP_Pack003_Base",
            "DLC_EXP_Pack003",
            "DLC_CON_DH1",
            "DLC_CON_PRO1",
            "DLC_CON_PRO2",
            "DLC_CON_PRO3",
            "DLC_CON_PRO4",
            "DLC_CON_PRO5",
            "DLC_CON_PRO6",
            "DLC_METR_Patch01"
        });
    }
}