﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.PlotDatabase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class PlotDBTests
    {
        [TestMethod]
        public void TestDBSerialization()
        {
            GlobalTest.Init();
            var le1File =
                new StreamReader(LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("PlotDatabases.zip", "le1.json")).ReadToEnd();
            var db1 = JsonConvert.DeserializeObject<PlotDatabaseTemp>(le1File);

            var le2File =
                new StreamReader(LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("PlotDatabases.zip", "le2.json")).ReadToEnd();
            var db2 = JsonConvert.DeserializeObject<PlotDatabaseTemp>(le2File);

            var le3File =
                new StreamReader(LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("PlotDatabases.zip", "le3.json")).ReadToEnd();
            var db3 = JsonConvert.DeserializeObject<PlotDatabaseTemp>(le3File);
        }
    }
}
