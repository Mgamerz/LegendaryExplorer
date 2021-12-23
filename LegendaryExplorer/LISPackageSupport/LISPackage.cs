using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using static LegendaryExplorerCore.Unreal.UnrealFlags;
#if AZURE
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace LISPackageSupport
{
    public sealed class LISPackage : UnrealPackageFile, IMEPackage, IDisposable
    {
        public const ushort ME1UnrealVersion = 491;
        public const ushort ME1LicenseeVersion = 1008;

        /// <summary>
        /// Indicates what type of package file this is. 0 is normal, 1 is TESTPATCH patch package.
        /// </summary>
        public int PackageTypeId { get; }

        /// <summary>
        /// This is not useful for modding but we should not be changing the format of the package file.
        /// </summary>
        public List<string> AdditionalPackagesToCook = new List<string>();

        /// <summary>
        /// Passthrough to UnrealPackageFile's IsModified
        /// </summary>
        bool IMEPackage.IsModified
        {
            // Not sure why I can't use a private setter here.
            get => IsModified;
            set => IsModified = value;
        }

        public Endian Endian { get; }
        public MEGame Game { get; private set; } //can only be ME1, ME2, ME3, LE1, LE2, LE3. UDK is a separate class
        public GamePlatform Platform { get; private set; }

        public MELocalization Localization { get; } = MELocalization.None;

        public byte[] getHeader()
        {
            using var ms = MemoryManager.GetMemoryStream();
            //WriteHeader(ms, includeAdditionalPackageToCook: true);
            return ms.ToArray();
        }

        #region HeaderMisc
        private int Gen0ExportCount;
        private int Gen0NameCount;
        private int Gen0NetworkedObjectCount;
        private int ImportExportGuidsOffset;
        //private int ImportGuidsCount;
        //private int ExportGuidsCount;
        //private int ThumbnailTableOffset;
        private uint packageSource;
        private int unknown4;
        private int unknown6;
        #endregion

        private static bool _isBlankPackageCreatorRegistered;
        private static bool _isStreamLoaderRegistered;
        public static Func<string, MEGame, LISPackage> RegisterBlankPackageCreator()
        {
            if (_isBlankPackageCreatorRegistered)
            {
                throw new Exception(nameof(LISPackage) + " can only be initialized once");
            }

            _isBlankPackageCreatorRegistered = true;
            return (f, g) => new LISPackage(g, f);
        }

        public static Func<Stream, string, bool, Func<ExportEntry, bool>, LISPackage> RegisterStreamLoader()
        {
            if (_isStreamLoaderRegistered)
            {
                throw new Exception(nameof(LISPackage) + " streamloader can only be initialized once");
            }

            _isStreamLoaderRegistered = true;
            return (s, associatedFilePath, onlyheader, dataLoadPredicate) => new LISPackage(s, associatedFilePath, onlyheader, dataLoadPredicate);
        }

        /// <summary>
        /// Creates a new blank LISPackage object.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="filePath"></param>
        public LISPackage(MEGame game, string filePath = null) : base(filePath != null ? Path.GetFullPath(filePath) : null)
        {
            names = new List<string>();
            imports = new List<ImportEntry>();
            exports = new List<ExportEntry>();
            //new Package
            Game = game;
            Platform = GamePlatform.PC; //Platform must be set or saving code will throw exception (cannot save non-PC platforms)
            //reasonable defaults?
            Flags = EPackageFlags.Cooked | EPackageFlags.AllowDownload | EPackageFlags.DisallowLazyLoading | EPackageFlags.RequireImportsAlreadyLoaded;
            EntryLookupTable = new CaseInsensitiveDictionary<IEntry>();
        }

        /// <summary>
        /// Opens an ME package from the stream. If this file is from a disk, the filePath should be set to support saving and other lookups.
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="filePath"></param>
        /// <param name="onlyHeader">Only read header data. Do not load the tables or decompress</param>
        public LISPackage(Stream fs, string filePath = null, bool onlyHeader = false, Func<ExportEntry, bool> dataLoadPredicate = null) : base(filePath != null ? File.Exists(filePath) ? Path.GetFullPath(filePath) : filePath : null)
        {
            //MemoryStream fs = new MemoryStream(File.ReadAllBytes(filePath));
            //Debug.WriteLine($"Reading LISPackage from stream starting at position 0x{fs.Position:X8}");
            #region Header

            EndianReader packageReader = EndianReader.SetupForPackageReading(fs);
            packageReader.SkipInt32(); //skip magic as we have already read it
            Endian = packageReader.Endian;

            //Big endian means it will be console version and package header is slightly tweaked as some flags are always set

            // This is stored as integer by cooker as it is flipped by size word in big endian
            var versionLicenseePacked = packageReader.ReadUInt32();

            GamePlatform platformOverride = GamePlatform.Unknown; //Used to help differentiate beteween PS3 and Xenon ME3
            CompressionType fcCompressionType = CompressionType.None;

            if ((versionLicenseePacked is 0x00020000 or 0x00010000) && Endian == Endian.Little)
            {
                if (versionLicenseePacked == 0x20000)
                {
                    // It's WiiU LZMA or Xenon LZX
                    // To determine if it's LZMA we have to read the first block's compressed bytes and read first few bytes
                    // LZMA always starts with 0x5D and then is followed by a dictionary size of size word (32) (in ME it looks like 0x10000)

                    // This is done in the decompress fully compressed package method when we pass it None type
                    fs = CompressionHelper.DecompressFullyCompressedPackage(packageReader, ref fcCompressionType);
                    platformOverride = fcCompressionType == CompressionType.LZX ? GamePlatform.Xenon : GamePlatform.WiiU;
                }
                else if (versionLicenseePacked == 0x10000)
                {
                    //PS3, LZMA
                    fcCompressionType = CompressionType.LZMA; // Known already
                    fs = CompressionHelper.DecompressFullyCompressedPackage(packageReader, ref fcCompressionType);
                    platformOverride = GamePlatform.PS3;
                }
                // Fully compressed packages use little endian magic in them 
                // so we need to re-setup the endian reader
                // Why do they use different endians on the same processor platform?
                // Who the hell knows!
                packageReader = EndianReader.SetupForPackageReading(fs);
                packageReader.SkipInt32(); //skip magic as we have already read it
                Endian = packageReader.Endian;
                versionLicenseePacked = packageReader.ReadUInt32();
            }

            var unrealVersion = (ushort)(versionLicenseePacked & 0xFFFF);
            var licenseeVersion = (ushort)(versionLicenseePacked >> 16);
            bool platformNeedsResolved = false;
            var matchingPackageInfo = MEPackageHandler.GetPackageIdentifier(unrealVersion, licenseeVersion, Endian);
            if (matchingPackageInfo != null)
            {
                Game = matchingPackageInfo.GameID;
                if (matchingPackageInfo.Platform != GamePlatform.Unknown)
                {
                    Platform = matchingPackageInfo.Platform;
                }
                else
                {
                    // Needs resolved, which is done later in parsing.
                    if (platformOverride == GamePlatform.Unknown)
                    {
                        //Debug.WriteLine("Cannot differentiate PS3 vs Xenon ME3 files. Assuming PS3, this may be wrong assumption!");
                        platformNeedsResolved = true;
                        Platform = GamePlatform.PS3; //This is placeholder as Xenon and PS3 use same header format
                    }
                    else
                    {
                        Platform = platformOverride; // Used for fully compressed packages
                    }
                }
            }
            else
            {
                // We don't know what this package is for!)
                throw new Exception($"Package format not supported. Unreal version: {unrealVersion}, Licensee version: {licenseeVersion}");
            }


            /*
            switch (unrealVersion)
            {
                case ME1UnrealVersion when licenseeVersion == ME1LicenseeVersion:
                    Game = MEGame.ME1;
                    Platform = GamePlatform.PC;
                    break;
                case ME1XboxUnrealVersion when licenseeVersion == ME1XboxLicenseeVersion:
                    Game = MEGame.ME1;
                    Platform = GamePlatform.Xenon;
                    break;
                case ME1PS3UnrealVersion when licenseeVersion == ME1PS3LicenseeVersion:
                    Game = MEGame.ME1;
                    Platform = GamePlatform.PS3;
                    break;
                case ME2UnrealVersion when licenseeVersion == ME2LicenseeVersion && Endian == Endian.Little:
                case ME2DemoUnrealVersion when licenseeVersion == ME2LicenseeVersion:
                    Game = MEGame.ME2;
                    Platform = GamePlatform.PC;
                    break;
                case ME2UnrealVersion when licenseeVersion == ME2LicenseeVersion && Endian == Endian.Big:
                    Game = MEGame.ME2;
                    Platform = GamePlatform.Xenon;
                    break;
                case ME2PS3UnrealVersion when licenseeVersion == ME2PS3LicenseeVersion:
                    Game = MEGame.ME2;
                    Platform = GamePlatform.PS3;
                    break;
                case ME3WiiUUnrealVersion when licenseeVersion == ME3LicenseeVersion:
                    Game = MEGame.ME3;
                    Platform = GamePlatform.WiiU;
                    break;
                case ME3UnrealVersion when licenseeVersion == ME3LicenseeVersion:
                    Game = MEGame.ME3;
                    if (Endian == Endian.Little)
                    {
                        Platform = GamePlatform.PC;
                    }
                    else
                    {
                        // If the package is not compressed or fully compressed we cannot determine if this is PS3 or Xenon.
                        // PS3 and Xbox use same engine versions on the ME3 game (ME1/2 use same one but has slight differences for some reason)

                        // Code above determines platform if it's fully compressed, and code below determines platform based on compression type
                        // However if neither exist we don't have an easy way to differentiate files (such as files from SFAR)

                        // We attempt to resolve the platfrom later using SeekFreeShaderCache which is present
                        // in every single console file (vs PC's DLC only).
                        // Not 100% sure it's in every file. But hopefully it is.
                        if (platformOverride == GamePlatform.Unknown)
                        {
                            //Debug.WriteLine("Cannot differentiate PS3 vs Xenon ME3 files. Assuming PS3, this may be wrong assumption!");
                            platformNeedsResolved = true;
                            Platform = GamePlatform.PS3; //This is placeholder as Xenon and PS3 use same header format
                        }
                        else
                        {
                            Platform = platformOverride; // Used for fully compressed packages
                        }
                    }
                    break;
                case ME3UnrealVersion when licenseeVersion == ME3Xenon2011DemoLicenseeVersion:
                    Game = MEGame.ME3;
                    Platform = GamePlatform.Xenon;
                    break;
                case LE1UnrealVersion when licenseeVersion == LE1LicenseeVersion:
                    Game = MEGame.LE1;
                    Platform = GamePlatform.PC;
                    break;
                case LE2UnrealVersion when licenseeVersion == LE2LicenseeVersion:
                    Game = MEGame.LE2;
                    Platform = GamePlatform.PC;
                    break;
                case LE3UnrealVersion when licenseeVersion == LE3LicenseeVersion:
                    Game = MEGame.LE3;
                    Platform = GamePlatform.PC;
                    break;
                default:
                    throw new FormatException("Not a Mass Effect Package!");
            }*/
            FullHeaderSize = packageReader.ReadInt32();
            int foldernameStrLen = packageReader.ReadInt32();
            //always "None", so don't bother saving result
            if (foldernameStrLen > 0)
                fs.ReadStringLatin1Null(foldernameStrLen);
            else
                fs.ReadStringUnicodeNull(foldernameStrLen * -2);

            Flags = (EPackageFlags)packageReader.ReadUInt32();

            //Xenon Demo ME3 doesn't read this. Xenon ME3 Retail does
            //if (true) //Game is MEGame.ME3 or MEGame.LE3 && (Flags.Has(EPackageFlags.Cooked) || Platform != GamePlatform.PC) && licenseeVersion != ME3Xenon2011DemoLicenseeVersion)
            //{
            //    //Consoles are always cooked.
            //    PackageTypeId = packageReader.ReadInt32(); //0 = standard, 1 = patch ? Not entirely sure. patch_001 files with byte = 0 => game does not load
            //}

            NameCount = packageReader.ReadInt32();
            NameOffset = packageReader.ReadInt32();
            ExportCount = packageReader.ReadInt32();
            ExportOffset = packageReader.ReadInt32();
            ImportCount = packageReader.ReadInt32();
            ImportOffset = packageReader.ReadInt32();


            if (Game.IsLEGame() || Game != MEGame.ME1 || Platform != GamePlatform.Xenon)
            {
                // Seems this doesn't exist on ME1 Xbox
                DependencyTableOffset = packageReader.ReadInt32();
            }

            //if (Game.IsLEGame() || Game == MEGame.ME3 || Platform == GamePlatform.PS3)
            //{
            ImportExportGuidsOffset = packageReader.ReadInt32();
            var t1 = packageReader.ReadInt32(); //ImportGuidsCount always 0
            var t2 = packageReader.ReadInt32(); //ExportGuidsCount always 0
            var t3 = packageReader.ReadInt32(); //ThumbnailTableOffset always 0
            //}

            PackageGuid = packageReader.ReadGuid();

            uint generationsTableCount = packageReader.ReadUInt32();
            if (generationsTableCount > 0)
            {
                generationsTableCount--;
                Gen0ExportCount = packageReader.ReadInt32();
                Gen0NameCount = packageReader.ReadInt32();
                Gen0NetworkedObjectCount = packageReader.ReadInt32();
            }
            //should never be more than 1 generation, but just in case
            packageReader.Skip(generationsTableCount * 12);

            //if (Game != MEGame.LE1)
            //{
            packageReader.SkipInt32(); //engineVersion          Like unrealVersion and licenseeVersion, these 2 are determined by what game this is,
            packageReader.SkipInt32(); //cookedContentVersion   so we don't have to read them in

            /*
            if ((Game is MEGame.ME2 or MEGame.ME1) && Platform != GamePlatform.PS3) //PS3 on ME3 engine
            {
                packageReader.SkipInt32(); //always 0
                packageReader.SkipInt32(); //always 47699
                unknown4 = packageReader.ReadInt32();
                packageReader.SkipInt32(); //always 1 in ME1, always 1966080 in ME2
            }

            unknown6 = packageReader.ReadInt32(); // Build 
            packageReader.SkipInt32(); // Branch

            if (Game == MEGame.ME1 && Platform != GamePlatform.PS3)
            {
                packageReader.SkipInt32(); //always -1
            }*/
            //}
            //else
            //{
            //    packageReader.Position += 0x14; // Skip an unkonwn 14 bytes we will figure out later
            //}

            //COMPRESSION AND COMPRESSION CHUNKS
            var compressionFlagPosition = packageReader.Position;
            var compressionType = (CompressionType)packageReader.ReadInt32();
            if (platformNeedsResolved && compressionType != CompressionType.None)
            {
                Platform = compressionType == CompressionType.LZX ? GamePlatform.Xenon : GamePlatform.PS3;
                platformNeedsResolved = false;
            }

            //Debug.WriteLine($"Compression type {filePath}: {compressionType}");
            NumCompressedChunksAtLoad = packageReader.ReadInt32();

            //read package source
            var savedPos = packageReader.Position;
            packageReader.Skip(NumCompressedChunksAtLoad * 16); //skip chunk table so we can find package tag



            packageSource = packageReader.ReadUInt32(); //this needs to be read in so it can be properly written back out.

            if ((Game is MEGame.ME2 or MEGame.ME1) && Platform != GamePlatform.PS3)
            {
                packageReader.SkipInt32(); //always 0
            }

            //Doesn't need to be written out, so it doesn't need to be read in
            //keep this here in case one day we learn that this has a purpose
            //Narrator: On Jan 26, 2020 it turns out this was actually necessary to make it work
            //with ME3Tweaks Mixins as old code did not remove this section
            //Also we should strive to ensure closeness to the original source files as possible
            //because debugging things is a huge PITA if you start to remove stuff
            if (Game is MEGame.ME2 or MEGame.ME3 || Game.IsLEGame() || Platform == GamePlatform.PS3)
            {
                int additionalPackagesToCookCount = packageReader.ReadInt32();
                for (int i = 0; i < additionalPackagesToCookCount; i++)
                {
                    var packageStr = packageReader.ReadUnrealString();
                    AdditionalPackagesToCook.Add(packageStr);
                }
            }

            if (onlyHeader) return; // That's all we need to parse. 
            #endregion

            #region Decompression of package data

            //determine if tables are in order.
            //The < 500 is just to check that the tables are all at the start of the file. (will never not be the case for unedited files, but for modded ones, all things are possible)
            bool tablesInOrder = NameOffset < 500 && NameOffset < ImportOffset && ImportOffset < ExportOffset;

            packageReader.Position = savedPos; //restore position to chunk table
            Stream inStream = fs;
            if (IsCompressed && NumCompressedChunksAtLoad > 0)
            {
                inStream = CompressionHelper.DecompressPackage(packageReader, compressionFlagPosition, game: Game, platform: Platform,
                                                               canUseLazyDecompression: tablesInOrder && !platformNeedsResolved);
            }
            #endregion

            var endian = packageReader.Endian;
            packageReader = new EndianReader(inStream) { Endian = endian };
            //read namelist
            inStream.JumpTo(NameOffset);
            names = new List<string>(NameCount);
            for (int i = 0; i < NameCount; i++)
            {
                var name = packageReader.ReadUnrealString();
                names.Add(name);
                nameLookupTable[name] = i;
               // if (Game == MEGame.ME1 && Platform != GamePlatform.PS3)
                    inStream.Skip(8);
                //else if (Game == MEGame.ME2 && Platform != GamePlatform.PS3)
                //    inStream.Skip(4);
            }

            //read importTable
            inStream.JumpTo(ImportOffset);
            imports = new List<ImportEntry>(ImportCount);
            for (int i = 0; i < ImportCount; i++)
            {
                var imp = new ImportEntry(this, packageReader) { Index = i };
                if (MEPackageHandler.GlobalSharedCacheEnabled)
                    imp.PropertyChanged += importChanged; // If packages are not shared there is no point to attaching this
                imports.Add(imp);
            }

            //read exportTable
            inStream.JumpTo(ExportOffset);
            exports = new List<ExportEntry>(ExportCount);
            for (int i = 0; i < ExportCount; i++)
            {
                var e = new ExportEntry(this, packageReader, false) { Index = i };
                if (MEPackageHandler.GlobalSharedCacheEnabled)
                    e.PropertyChanged += exportChanged; // If packages are not shared there is no point to attaching this
                exports.Add(e);
                if (platformNeedsResolved && e.ClassName == "ShaderCache")
                {
                    // Read the first binary byte, it's a platform flag
                    // 0 = PC
                    // 1 = PS3
                    // 2 = Xenon
                    // 5 = WiiU / SM5 (LE)
                    // See ME3Explorer's EShaderPlatform enum in it's binary interpreter scans
                    var resetPos = packageReader.Position;
                    packageReader.Position = e.DataOffset + 0xC; // Skip 4 byte + "None"
                    var platform = packageReader.ReadByte();
                    if (platform == 1)
                    {
                        Platform = GamePlatform.PS3;
                        platformNeedsResolved = false;
                    }
                    else if (platform == 2)
                    {
                        Platform = GamePlatform.Xenon;
                        platformNeedsResolved = false;
                    }
                    else if (platform == 5)
                    {
                        // I think this won't ever occur
                        // as we have engine version diff
                        // But might as well just make sure
                        Platform = GamePlatform.WiiU;
                        platformNeedsResolved = false;
                    }
                    packageReader.Position = resetPos;
                }
            }

            foreach (ExportEntry export in dataLoadPredicate is null ? exports : exports.Where(dataLoadPredicate))
            {
                inStream.JumpTo(export.DataOffset);
                export.Data = packageReader.ReadBytes(export.DataSize);
            }

            packageReader.Dispose();

            if (filePath != null)
            {
                Localization = filePath.GetUnrealLocalization();
            }

            EntryLookupTable = new CaseInsensitiveDictionary<IEntry>(ExportCount + ImportCount);
            RebuildLookupTable(); // Builds the export/import lookup tables.
#if AZURE
            if (platformNeedsResolved)
            {
                Assert.Fail($"Package platform was not resolved! Package file: {FilePath}");
            }
#endif
        }



        public static Action<LISPackage, string, bool, bool, bool, bool, object> RegisterSaver() => saveByReconstructing;

        /// <summary>
        /// Saves the package to disk by reconstructing the package file
        /// </summary>
        /// <param name="LISPackage"></param>
        /// <param name="path"></param>
        /// <param name="isSaveAs"></param>
        /// <param name="compress"></param>
        private static void saveByReconstructing(LISPackage LISPackage, string path, bool isSaveAs, bool compress, bool includeAdditionalPackagesToCook, bool includeDependencyTable, object diskIOSyncLockObject = null)
        {
            //var sw = Stopwatch.StartNew();
            using var saveStream = compress
                ? saveCompressed(LISPackage, isSaveAs, includeAdditionalPackagesToCook, includeDependencyTable)
                : saveUncompressed(LISPackage, isSaveAs, includeAdditionalPackagesToCook, includeDependencyTable);

            // Lock writing with the sync object (if not null) to prevent disk concurrency issues
            // (the good old 'This file is in use by another process' message)
            if (diskIOSyncLockObject == null)
            {
                saveStream.WriteToFile(path ?? LISPackage.FilePath);
            }
            else
            {
                lock (diskIOSyncLockObject)
                {
                    saveStream.WriteToFile(path ?? LISPackage.FilePath);
                }
            }

            if (!isSaveAs)
            {
                LISPackage.AfterSave();
            }
            //var milliseconds = sw.ElapsedMilliseconds;
            //Debug.WriteLine($"Saved {Path.GetFileName(path)} in {milliseconds}");
            //sw.Stop();
        }

        /// <summary>
        /// Saves the package to stream. If this saving operation is not going to be committed to disk in the same place as the package was loaded from, you should mark this as a 'save as'.
        /// </summary>
        /// <param name="LISPackage"></param>
        /// <param name="includeAdditionalPackageToCook"></param>
        /// <param name="includeDependencyTable"></param>
        /// <returns></returns>
        private static MemoryStream saveUncompressed(LISPackage LISPackage, bool isSaveAs, bool includeAdditionalPackageToCook = true, bool includeDependencyTable = true)
        {
            // NOT SUPPORTED
            return new MemoryStream();
        }

        private static MemoryStream saveCompressed(LISPackage package, bool isSaveAs, bool includeAdditionalPackageToCook = true, bool includeDependencyTable = true)
        {
            // NOT SUPPORTED
            return new MemoryStream();
        }

        private static void WriteLegendaryExplorerCoreTag(MemoryStream ms)
        {
            ms.WriteInt32(1); //version. for if we want to append more data in the future 
            ms.WriteInt32(4); //size of preceding data. (just the version for now)
            ms.WriteStringASCII("LECL");
        }

        //Must not change export's DataSize!
        private static void UpdateOffsets(ExportEntry e, int oldDataOffset)
        {
            if (!e.IsDefaultObject)
            {
                if (e.Game == MEGame.ME1 && e.IsTexture())
                {
                    // For us to reliably have in-memory textures, the data offset of 'externally' stored textures
                    // needs to be updated to be accurate so that master and slave textures are in sync.
                    // So any texture mips stored as pccLZO needs their DataOffsets updated
                    var t2d = ObjectBinary.From<UTexture2D>(e);
                    var binStart = -1;
                    foreach (var mip in t2d.Mips.Where(x => x.IsCompressed && x.IsLocallyStored))
                    {
                        if (binStart == -1)
                        {
                            binStart = e.DataOffset + e.propsEnd();
                        }

                        // This is 
                        mip.DataOffset = binStart + mip.MipInfoOffsetFromBinStart + 0x10; // actual data offset is past storagetype, uncomp, comp, dataoffset
                    }

                    e.WriteBinary(t2d);
                }
                else
                {
                    switch (e.ClassName)
                    {
                        //case "WwiseBank":
                        case "WwiseStream" when e.GetProperty<NameProperty>("Filename") == null:
                        case "TextureMovie" when e.GetProperty<NameProperty>("TextureFileCacheName") == null:
                            e.WriteBinary(ObjectBinary.From(e));
                            break;
                        case "ShaderCache":
                            UpdateShaderCacheOffsets(e, oldDataOffset);
                            break;
                    }
                }
            }
        }

        public MemoryStream SaveToStream(bool compress = false, bool includeAdditionalPackagesToCook = true, bool includeDependencyTable = true)
        {
            return compress
                ? saveCompressed(this, true, includeAdditionalPackagesToCook, includeDependencyTable)
                : saveUncompressed(this, true, includeAdditionalPackagesToCook, includeDependencyTable);
        }

        //TODO: edit memory in-place somehow? This currently requires an allocation and a copy of the sizeable shadercache export, and must happen on save for nearly every LE file
        private static void UpdateShaderCacheOffsets(ExportEntry export, int oldDataOffset)
        {
            int newDataOffset = export.DataOffset;

            MEGame game = export.Game;
            var binData = new MemoryStream(export.Data, 0, export.DataSize, true, true);
            binData.Seek(export.propsEnd() + 1, SeekOrigin.Begin);

            int nameList1Count = binData.ReadInt32();
            binData.Seek(nameList1Count * 12, SeekOrigin.Current);

            if (game is MEGame.ME3 || game.IsLEGame())
            {
                int namelist2Count = binData.ReadInt32();//namelist2
                binData.Seek(namelist2Count * 12, SeekOrigin.Current);
            }

            if (game is MEGame.ME1)
            {
                int vertexFactoryMapCount = binData.ReadInt32();
                binData.Seek(vertexFactoryMapCount * 12, SeekOrigin.Current);
            }

            int shaderCount = binData.ReadInt32();
            for (int i = 0; i < shaderCount; i++)
            {
                binData.Seek(24, SeekOrigin.Current);
                int nextShaderOffset = binData.ReadInt32() - oldDataOffset;
                binData.Seek(-4, SeekOrigin.Current);
                binData.WriteInt32(nextShaderOffset + newDataOffset);
                binData.Seek(nextShaderOffset, SeekOrigin.Begin);
            }

            if (game is not MEGame.ME1)
            {
                int vertexFactoryMapCount = binData.ReadInt32();
                binData.Seek(vertexFactoryMapCount * 12, SeekOrigin.Current);
            }

            int materialShaderMapCount = binData.ReadInt32();
            for (int i = 0; i < materialShaderMapCount; i++)
            {
                binData.Seek(16, SeekOrigin.Current);

                int switchParamCount = binData.ReadInt32();
                binData.Seek(switchParamCount * 32, SeekOrigin.Current);

                int componentMaskParamCount = binData.ReadInt32();
                binData.Seek(componentMaskParamCount * 44, SeekOrigin.Current);

                if (game is MEGame.ME3 || game.IsLEGame())
                {
                    int normalParams = binData.ReadInt32();
                    binData.Seek(normalParams * 29, SeekOrigin.Current);

                    binData.Seek(8, SeekOrigin.Current);
                }

                int nextMaterialShaderMapOffset = binData.ReadInt32() - oldDataOffset;
                binData.Seek(-4, SeekOrigin.Current);
                binData.WriteInt32(nextMaterialShaderMapOffset + newDataOffset);
                binData.Seek(nextMaterialShaderMapOffset, SeekOrigin.Begin);
            }

            export.Data = binData.GetBuffer();
        }

        /// <summary>
        /// Sets the game for this LISPackage. DO NOT USE THIS UNLESS YOU ABSOLUTELY KNOW WHAT YOU ARE DOING
        /// </summary>
        /// <param name="newGame"></param>
        public void setGame(MEGame newGame)
        {
            Game = newGame;
        }

        /// <summary>
        /// Sets the platform for this LISPackage. DO NOT USE THIS UNLESS YOU ABSOLUTELY KNOW WHAT YOU ARE DOING.
        /// CHANGING THE PLATFORM TO ATTEMPT TO SAVE A CONSOLE FILE WILL NOT PRODUCE A USABLE CONSOLE FILE
        /// </summary>
        /// <param name="newPlatform"></param>
        internal void setPlatform(GamePlatform newPlatform)
        {
            Platform = newPlatform;
        }
    }
}