using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Gammtek.IO;

namespace LegendaryExplorerCore.Packages
{
    /// <summary>
    /// Handles how different packages should open and be identified based on the first few bytes of the header
    /// </summary>
    public class GameIdentifier
    {
        public ushort UnrealVersion { get; init; }
        public ushort LicenseeVersion { get; init; }

        /// <summary>
        /// Human readable string representing the source of the game.
        /// </summary>
        public string Source { get; init; }

        /// <summary>
        /// Not sure how this could be handled as an enumeration...
        /// </summary>
        public MEGame GameID { get; init; }

        public GamePlatform Platform { get; init; }
        /// <summary>
        /// Expected endian for this package type
        /// </summary>
        public Endian Endian { get; init; } = Endian.Little;

        /// <summary>
        /// Signature to open package from stream
        /// </summary>
        /// <param name="inStream"></param>
        /// <param name="associatedFilePath"></param>
        /// <param name="useSharedPackageCache"></param>
        /// <param name="user"></param>
        /// <param name="quickLoad"></param>
        /// <returns></returns>
        public delegate IMEPackage OpenPackageFromStreamDelegate(Stream inStream, string associatedFilePath = null, bool quickload = false, Func<ExportEntry, bool> dataLoadPredicate = null);
            
        /// <summary>
        /// Delegate to invoke when opening a package
        /// </summary>
        public OpenPackageFromStreamDelegate OpenPackageFromStream { get; init; } = (s, associatedFilePath, onlyheader, dataLoadPredicate) => new MEPackage(s, associatedFilePath, onlyheader, dataLoadPredicate);
    }
}
