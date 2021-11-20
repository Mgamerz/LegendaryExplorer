﻿using System.IO;
using System.Collections.Generic;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.TLK
{
    public static class ME2TalkFiles
    {
        public static List<TalkFile> tlkList = new();

        public static void LoadTlkData(string fileName)
        {
            if (File.Exists(fileName))
            {
                var tlk = new TalkFile();
                tlk.LoadTlkData(fileName);
                tlkList.Add(tlk);
            }
        }

        public static string FindDataById(int strRefID, bool withFileName = false)
        {
            string s = "No Data";
            foreach (TalkFile tlk in tlkList)
            {
                s = tlk.FindDataById(strRefID, withFileName);
                if (s != "No Data")
                {
                    return s;
                }
            }
            return s;
        }

        public static void ClearLoadedTlks()
        {
            tlkList.Clear();
        }
    }
}
