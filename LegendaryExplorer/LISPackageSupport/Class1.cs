using System;
using System.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;

namespace LISPackageSupport
{
    public class LISPackageSupport
    {
        /// <summary>
        /// Registers loaders for LiS (1) packages
        /// </summary>
        public static void AddSupport()
        {
            MEPackageHandler.GameIdentifiers.Add(new GameIdentifier() { UnrealVersion = 893, LicenseeVersion = 21, Platform = GamePlatform.PC, GameID = MEGame.Unknown, OpenPackageFromStream = LISPackageSupport.OpenPackage });
        }

        private static IMEPackage OpenPackage(Stream stream, string associatedfilepath, bool onlyHeader, Func<ExportEntry, bool> dataLoadPredicate)
        {
            return new LISPackage(stream, associatedfilepath, onlyHeader, dataLoadPredicate);
        }
    }
}
