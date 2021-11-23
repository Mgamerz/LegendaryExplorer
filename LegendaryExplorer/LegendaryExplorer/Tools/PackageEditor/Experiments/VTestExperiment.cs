﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using DocumentFormat.OpenXml.EMMA;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.Tools.Sequence_Editor.Experiments;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using Microsoft.Win32;
using Newtonsoft.Json;
using Model = LegendaryExplorerCore.Unreal.BinaryConverters.Model;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    public class VTestExperiment
    {

        public class VTestOptions
        {
            #region Configurable options
            /// <summary>
            /// List of levels to port. DO NOT INCLUDE BIOA_.
            /// </summary>
            public string[] vTestLevels = new[]
            {
                // Comment/uncomment these to select which files to run on
                "PRC2",
                "PRC2AA"
            };

            /// <summary>
            /// If lightmaps and shadowmaps should be stripped and dynamic lighting turned on
            /// </summary>
            public bool useDynamicLighting = true;

            /// <summary>
            /// Strips shadow maps off. If using dynamic lighting, shadow maps are always stripped
            /// </summary>
            public bool stripShadowMaps = false;

            /// <summary>
            /// If light and shadowmaps for meshes ported from ME1 (not using LE1 donor) should be ported instead of stripped. This may not look good but may be possible to adjust.
            /// </summary>
            public bool allowTryingPortedMeshLightMap = true;

            /// <summary>
            /// If terrains should have their lightmaps ported over (if they exist)
            /// </summary>
            public bool portTerrainLightmaps = true;

            /// <summary>
            /// If level models should be ported.
            /// </summary>
            public bool portModels = false;

            /// <summary>
            /// If the audio localizations should be ported
            /// </summary>
            public bool portAudioLocalizations = true;

            /// <summary>
            /// If a level's list of StreamableTextureInstance's should be copied over.
            /// </summary>
            public bool installTexturesInstanceMap = true;

            /// <summary>
            /// If a level's list of textures to force streaming should be copied over.
            /// </summary>
            public bool installForceTextureStreaming = false;

            /// <summary>
            /// The intensity scalar for the CCLava lightmap
            /// </summary>
            public float LavaLightmapScalar = 0.15f;

            /// <summary>
            /// If debug features should be enabled in the build
            /// </summary>
            public bool debugBuild = false;

            /// <summary>
            /// If static lighting should be converted to non-static lighting. Only works if debugBuild is true
            /// </summary>
            public bool debugConvertStaticLightingToNonStatic = false;

            /// <summary>
            /// If each actor porting should also import into a new asset package that can speed up build 
            /// </summary>
            public bool debugBuildAssetCachePackage = false;



            /// <summary>
            /// The cache that is passed through to sub operations. You can change the
            /// CacheMaxSize to tune memory usage vs performance.
            /// </summary>
            public PackageCache cache = new PackageCache() { CacheMaxSize = 20 };
            #endregion

            #region Autoset options - Do not change these
            public PackageEditorWindow packageEditorWindow;
            public IMEPackage vTestHelperPackage;
            public ObjectInstanceDB objectDB;
            public IMEPackage assetCachePackage;
            internal int assetCacheIndex;
            #endregion

        }


        #region Vars
        /// <summary>
        /// List of things to port when porting a level with VTest
        /// </summary>
        private static string[] ClassesToVTestPort = new[]
        {
            "InterpActor",
            "BioInert",
            "BioUsable",
            "BioPawn",
            "SkeletalMeshActor",
            "PostProcessVolume",
            "BioMapNote",
            "Note",
            "BioTrigger",
            "BioSunActor",
            "BlockingVolume",
            "BioDoor",
            "StaticMeshCollectionActor",
            "StaticLightCollectionActor",
            "ReverbVolume",
            "BioAudioVolume",
            "AmbientSound",
            "BioLedgeMeshActor",
            "BioStage",
            "HeightFog",
            "PrefabInstance",
            "CameraActor",
            //"Terrain", // Do not port in - we will specifically port this with a special donor system
            //"Model", // Do not port in - we will specifically port the level model in to prevent donor system 

            // Pass 2
            "StaticMeshActor",
            "TriggerVolume",
            "BioSquadCombat",
            "PhysicsVolume",
            "BioWp_ActionStation",
            "BioLookAtTarget",
            "BioUsable",
            "BioContainer",

            // Pass 3
            //"Brush", // R A G E
            "PathNode",
            "BioCoverVolume",
            "BioTriggerVolume",
            "BioWp_AssaultPoint",
            "BioSquadPlayer",
            "BioUseable",
            "BioSquadSitAndShoot",
            "CoverLink",
            "BioWaypointSet",
            "BioPathPoint",
            "Emitter"

        };

        /// <summary>
        /// Classes to port only for master level files
        /// </summary>
        private static string[] ClassesToVTestPortMasterOnly = new[]
        {
            "PlayerStart",
            "BioTriggerStream"
        };

        private static string[] DebugBuildClassesToVTestPort = new[]
        {
            "PointLight",
            "SpotLight",

        };

        // Files we know are referenced by name but do not exist
        private static string[] VTest_NonExistentBTSFiles =
        {
            "bioa_prc2_ccahern_l",
            "bioa_prc2_cccave01",
            "bioa_prc2_cccave02",
            "bioa_prc2_cccave03",
            "bioa_prc2_cccave04",
            "bioa_prc2_cccrate01",
            "bioa_prc2_cccrate02",
            "bioa_prc2_cclobby01",
            "bioa_prc2_cclobby02",
            "bioa_prc2_ccmid01",
            "bioa_prc2_ccmid02",
            "bioa_prc2_ccmid03",
            "bioa_prc2_ccmid04",
            "bioa_prc2_ccscoreboard",
            "bioa_prc2_ccsim01",
            "bioa_prc2_ccsim02",
            "bioa_prc2_ccsim03",
            "bioa_prc2_ccsim04",
            "bioa_prc2_ccspace02",
            "bioa_prc2_ccspace03",
            "bioa_prc2_ccthai01",
            "bioa_prc2_ccthai02",
            "bioa_prc2_ccthai03",
            "bioa_prc2_ccthai04",
            "bioa_prc2_ccthai05",
            "bioa_prc2_ccthai06",
        };

        // This is list of materials to run a conversion to a MaterialInstanceConstant
        // List is not long cause not a lot of materials support this...
        private static string[] vtest_DonorMaterials = new[]
        {
            "BIOA_MAR10_T.UNC_HORIZON_MAT_Dup",
        };

        #endregion

        #region Kinda Hacky Vars
        /// <summary>
        /// List of all actor classes that were encountered during the last VTest session. Resets at the start of every VTest.
        /// </summary>
        internal static List<string> actorTypesNotPorted;

        #endregion

        #region Main porting methods

        private static string GetAssetCachePath()
        {
            return Path.Combine(PAEMPaths.VTest_DonorsDir, "Z_CrossgenV_AssetCache.pcc");
        }

        /// <summary>
        /// Runs the main VTest
        /// </summary>
        /// <param name="pe"></param>
        /// <param name="installAndBootGame"></param>
        public static async void VTest(PackageEditorWindow pe, bool? installAndBootGame = null)
        {
            // Prep
            EntryImporter.NonDonorItems.Clear();
            actorTypesNotPorted = new List<string>();

            if (installAndBootGame == null)
            {
                var result = MessageBox.Show(pe, "Install VTest and run the game when VTest completes? PAEMPaths must be set.", "Auto install and boot", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                installAndBootGame = result == MessageBoxResult.Yes;
            }

            // This object is passed through to all the methods so we don't have to constantly update the signatures
            var vTestOptions = new VTestOptions()
            {
                packageEditorWindow = pe,
            };

            pe.SetBusy("Performing VTest");
            await Task.Run(() =>
            {
                RunVTest(vTestOptions);
            }).ContinueWithOnUIThread(result =>
            {
                if (result.Exception != null)
                    throw result.Exception;
                pe.EndBusy();
                if (installAndBootGame != null && installAndBootGame.Value)
                {
                    var mmPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\ME3Tweaks", "ExecutableLocation", null);
                    if (mmPath != null && File.Exists(mmPath))
                    {

                        var moddesc = Path.Combine(Directory.GetParent(PAEMPaths.VTest_DonorsDir).FullName, "moddesc.ini");
                        if (File.Exists(moddesc))
                        {
                            ProcessStartInfo psi = new ProcessStartInfo(mmPath, $"--installmod \"{moddesc}\" --bootgame LE1");
                            Process.Start(psi);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Internal single-thread VTest session
        /// </summary>
        /// <param name="vTestOptions"></param>
        private static void RunVTest(VTestOptions vTestOptions)
        {
            Debug.WriteLine("Beginning VTest");
            Debug.WriteLine($"Cache GUID: {vTestOptions.cache.guid}");
            string dbPath = AppDirectories.GetObjectDatabasePath(MEGame.LE1);
            //string matPath = AppDirectories.GetMaterialGuidMapPath(MEGame.ME1);
            //Dictionary<Guid, string> me1MaterialMap = null;
            vTestOptions.packageEditorWindow.BusyText = "Loading databases";

            if (File.Exists(dbPath))
            {
                vTestOptions.objectDB = ObjectInstanceDB.DeserializeDB(File.ReadAllText(dbPath));
                vTestOptions.objectDB.BuildLookupTable(); // Lookup table is required as we are going to compile things
                vTestOptions.packageEditorWindow.BusyText = "Inventorying donors";

                // Add extra donors and VTestHelper package
                foreach (var file in Directory.GetFiles(PAEMPaths.VTest_DonorsDir))
                {
                    if (file.RepresentsPackageFilePath())
                    {
                        if (Path.GetFileNameWithoutExtension(file) == "VTestHelper")
                        {
                            // Load the VTestHelper, don't index it
                            vTestOptions.vTestHelperPackage = MEPackageHandler.OpenMEPackage(file, forceLoadFromDisk: true); // Do not put into cache
                            
                            // Inventory the classes from vtest helper to ensure they can be created without having to be in the 
                            // code for LEC
                            foreach (var e in vTestOptions.vTestHelperPackage.Exports.Where(x => x.IsClass && x.InheritsFrom("SequenceObject")))
                            {
                                var classInfo = GlobalUnrealObjectInfo.generateClassInfo(e);
                                var defaults = vTestOptions.vTestHelperPackage.GetUExport(ObjectBinary.From<UClass>(e).Defaults);
                                Debug.WriteLine($@"Inventorying {e.InstancedFullPath}");
                                GlobalUnrealObjectInfo.GenerateSequenceObjectInfoForClassDefaults(defaults);
                                GlobalUnrealObjectInfo.InstallCustomClassInfo(e.ObjectName, classInfo, e.Game);
                            }
                        }
                        else
                        {
                            // Inventory
                            using var p = MEPackageHandler.OpenMEPackage(file);
                            PackageEditorExperimentsM.IndexFileForObjDB(vTestOptions.objectDB, MEGame.LE1, p);
                        }

                    }
                }
            }
            else
            {
                return;
            }

            if (!vTestOptions.debugBuildAssetCachePackage && File.Exists(GetAssetCachePath()))
            {
                // Make the asset package resident so it won't be dropped
                var resident = vTestOptions.cache.GetCachedPackage(GetAssetCachePath(), true);
                vTestOptions.cache.AddResidentPackage(resident);
            }

            vTestOptions.packageEditorWindow.BusyText = "Clearing mod folder";
            // Clear out dest dir
            foreach (var f in Directory.GetFiles(PAEMPaths.VTest_FinalDestDir))
            {
                File.Delete(f);
            }

            // Copy in precomputed files
            vTestOptions.packageEditorWindow.BusyText = "Copying precomputed files";
            foreach (var f in Directory.GetFiles(PAEMPaths.VTest_PrecomputedDir))
            {
                File.Copy(f, Path.Combine(PAEMPaths.VTest_FinalDestDir, Path.GetFileName(f)));
            }

            // If we are building an asset cache package we initialize it here
            if (vTestOptions.debugBuildAssetCachePackage)
            {
                var assetCachePath = GetAssetCachePath();
                CreateEmptyLevel(assetCachePath, MEGame.LE1);
                vTestOptions.assetCachePackage = MEPackageHandler.OpenMEPackage(assetCachePath, forceLoadFromDisk: true);
            }

            vTestOptions.packageEditorWindow.BusyText = "Running VTest";

            // VTest File Loop ---------------------------------------
            foreach (var vTestLevel in vTestOptions.vTestLevels)
            {
                var levelFiles = Directory.GetFiles(Path.Combine(PAEMPaths.VTest_SourceDir, vTestLevel)).ToList();
                foreach (var f in levelFiles)
                {
                    if (f.Contains("_LOC_", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Check if we should port this in this session.
                        if (!f.Contains("_LOC_INT", StringComparison.InvariantCultureIgnoreCase) && !vTestOptions.portAudioLocalizations)
                        {
                            continue; // Do not port this non-int file.
                        }
                    }

                    PortFile(f, vTestLevel, vTestOptions);
                }
            }

            vTestOptions.cache.ReleasePackages(true); // Dump everything out of memory

            // TLKS ARE DONE POST ONLY
            PostUpdateTLKs(vTestOptions);


            Debug.WriteLine("Non donated items: ");
            foreach (var nonDonorItems in EntryImporter.NonDonorItems.OrderBy(x => x))
            {
                Debug.WriteLine(nonDonorItems);
            }

            Debug.WriteLine("Actor classes that were not ported:");
            foreach (var ac in actorTypesNotPorted)
            {
                Debug.WriteLine(ac);
            }

            // VTest post QA
            vTestOptions.packageEditorWindow.BusyText = "Performing checks";

            // Perform checks on all files
            foreach (var f in Directory.GetFiles(PAEMPaths.VTest_FinalDestDir))
            {
                if (f.RepresentsPackageFilePath())
                {
                    using var p = MEPackageHandler.OpenMEPackage(f);
                    VTest_CheckFile(p, vTestOptions);
                }
            }

            // If we are building an asset cache package we initialize it here
            if (vTestOptions.debugBuildAssetCachePackage)
            {
                vTestOptions.packageEditorWindow.BusyText = "Saving Asset Cache";

                // Remove 'TheWorld' so it's just assets
                EntryPruner.TrashEntries(vTestOptions.assetCachePackage, vTestOptions.assetCachePackage.Exports.Where(x => x.InstancedFullPath.StartsWith("TheWorld")).ToList());

                vTestOptions.assetCachePackage.Save();
            }
        }

        private static void PortFile(string levelFileName, string masterMapName, VTestOptions vTestOptions)
        {
            if (levelFileName.Contains("_LOC_", StringComparison.InvariantCultureIgnoreCase))
            {
                //if (levelFileName.Contains("bioa_prc2_ccsim05_dsg_LOC_FR", StringComparison.InvariantCultureIgnoreCase))
                PortLOCFile(levelFileName, vTestOptions);
            }
            else
            {
                var levelName = Path.GetFileNameWithoutExtension(levelFileName);
                //if (levelName.CaseInsensitiveEquals("BIOA_PRC2_CCMID04_LAY"))
                    PortVTestLevel(masterMapName, levelName, vTestOptions, levelName is "BIOA_PRC2" or "BIOA_PRC2AA", true);
            }
        }

        private static void VTestCheckTextures(IMEPackage mePackage, VTestOptions vTestOptions)
        {
            var maxLodInfo = TextureLODInfo.LEMaxLodSizes(mePackage.Game);
            foreach (var exp in mePackage.Exports.Where(x => x.IsTexture()))
            {
                var texinfo = ObjectBinary.From<UTexture2D>(exp);
                if (texinfo.Mips.Any(x => x.StorageType == StorageTypes.empty))
                {
                    // Check LOD bias if this will render in game
                    // Adjust the internal lod bias.
                    var props = exp.GetProperties();
                    var texGroup = props.GetProp<EnumProperty>(@"LODGroup");
                    if (texGroup != null && maxLodInfo.TryGetValue(texGroup.Value.Instanced, out var maxDimension))
                    {
                        // cubemaps will have null texture group. we don't want to update these
                        if (texinfo.Mips[0].SizeX > maxDimension || texinfo.Mips[0].SizeY > maxDimension)
                        {
                            Debug.WriteLine($@"FOUND UNUSABLE EMPTY MIP: {exp.InstancedFullPath} IN {Path.GetFileNameWithoutExtension(mePackage.FilePath)}");
                        }
                    }
                }

                if (exp.Parent != null && exp.Parent.ClassName != "TextureCube" && texinfo.Mips.Count(x => x.IsLocallyStored) > 6)
                {
                    Debug.WriteLine($"Externally storable texture: {exp.InstancedFullPath}");
                }
            }
        }

        private static void VTestCheckImports(IMEPackage p, VTestOptions vTestOptions)
        {
            foreach (var import in p.Imports)
            {
                if (import.IsAKnownNativeClass())
                    continue; //skip
                var resolvedExp = EntryImporter.ResolveImport(import, null, vTestOptions.cache, clipRootLevelPackage: false);
                if (resolvedExp == null)
                {
                    // Look in DB for objects that have same suffix
                    // This is going to be VERY slow

                    var instancedNameSuffix = "." + import.ObjectName.Instanced;
                    string similar = "";
                    foreach (var name in vTestOptions.objectDB.NameTable)
                    {
                        if (name.EndsWith(instancedNameSuffix, StringComparison.InvariantCultureIgnoreCase))
                        {
                            similar += ", " + name;
                        }
                    }

                    Debug.WriteLine($"Import not resolved in {Path.GetFileName(p.FilePath)}: {import.InstancedFullPath}, may be these ones instead: {similar}");
                }
            }
        }

        /// <summary>
        /// Ports a level file for VTest. Saves package at the end.
        /// </summary>
        /// <param name="mapName">Overarching map name</param>
        /// <param name="sourceName">Full map file name</param>
        /// <param name="finalDestDir"></param>
        /// <param name="sourceDir"></param>
        /// <param name="db"></param>
        /// <param name="pe"></param>
        /// <param name="syncBioWorldInfo"></param>
        /// <param name="portMainSequence"></param>
        private static void PortVTestLevel(string mapName, string sourceName, VTestOptions vTestOptions, bool syncBioWorldInfo = false, bool portMainSequence = false)
        {
            vTestOptions.cache.ReleasePackages(x => Path.GetFileNameWithoutExtension(x) != "SFXGame" && Path.GetFileNameWithoutExtension(x) != "Engine"); //Reduce memory overhead
            var outputFile = $@"{PAEMPaths.VTest_FinalDestDir}\{sourceName.ToUpper()}.pcc";
            CreateEmptyLevel(outputFile, MEGame.LE1);

            using var le1File = MEPackageHandler.OpenMEPackage(outputFile, forceLoadFromDisk: true);
            using var me1File = MEPackageHandler.OpenMEPackage($@"{PAEMPaths.VTest_SourceDir}\{mapName}\{sourceName}.SFM", forceLoadFromDisk: true);

            var levelName = Path.GetFileNameWithoutExtension(le1File.FilePath);

            vTestOptions.packageEditorWindow.BusyText = $"Preparing\n{levelName}";

            // BIOC_BASE -> SFXGame
            var bcBaseIdx = me1File.findName("BIOC_Base");
            me1File.replaceName(bcBaseIdx, "SFXGame");

            // BIOG_StrategicAI -> SFXStrategicAI
            var bgsaiBaseIdx = me1File.findName("BIOG_StrategicAI");
            if (bgsaiBaseIdx >= 0)
                me1File.replaceName(bgsaiBaseIdx, "SFXStrategicAI");

            CorrectFileForLEXMapFileDefaults(me1File, le1File, vTestOptions);
            PrePortingCorrections(me1File, vTestOptions);

            //if (levelName == "BIOA_PRC2_CCMAIN_CONV")
            //{
            //    me1File.Save(@"C:\Users\Mgame\Desktop\conv.sfm");
            //}

            // Once we are confident in porting we will just take the actor list from PersistentLevel
            // For now just port these
            var itemsToPort = new List<ExportEntry>();

            var me1PL = me1File.FindExport(@"TheWorld.PersistentLevel");
            var me1PersistentLevel = ObjectBinary.From<Level>(me1PL);

            itemsToPort.AddRange(me1PersistentLevel.Actors.Where(x => x.value != 0) // Skip blanks
                .Select(x => me1File.GetUExport(x.value))
                .Where(x => ClassesToVTestPort.Contains(x.ClassName) || (syncBioWorldInfo && ClassesToVTestPortMasterOnly.Contains(x.ClassName))));

            if (vTestOptions.debugBuild && vTestOptions.debugConvertStaticLightingToNonStatic)
            {
                // Lights are baked into the files but they are not part of the actors list. We have to manually find these
                var lights = me1File.Exports.Where(x => x.Parent == me1PL && x.IsA("Light") && x.ClassName != "StaticLightCollectionActor").ToList();
                foreach (var light in lights)
                {
                    // Lights lose their settings when coalesced into a SLCA
                    light.ObjectFlags = 0; // Clear
                    light.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional | UnrealFlags.EObjectFlags.LoadForClient | UnrealFlags.EObjectFlags.LoadForServer | UnrealFlags.EObjectFlags.LoadForEdit | UnrealFlags.EObjectFlags.HasStack;
                }
                itemsToPort.AddRange(lights);
            }

            // WIP: Find which classes we have yet to port
            // BioWorldInfo is not ported except on the level master. Might need to see if there's things
            // like scene desaturation in it worth porting.
            foreach (var v in me1PersistentLevel.Actors)
            {
                var entry = v.value != 0 ? v.GetEntry(me1File) : null;
                if (entry != null && !actorTypesNotPorted.Contains(entry.ClassName) && !ClassesToVTestPort.Contains(entry.ClassName) && !ClassesToVTestPortMasterOnly.Contains(entry.ClassName) && entry.ClassName != "BioWorldInfo")
                {
                    actorTypesNotPorted.Add(entry.ClassName);
                }
            }

            // End WIP

            VTestFilePorting(me1File, le1File, itemsToPort, vTestOptions);

            RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
            {
                Cache = vTestOptions.cache,
                IsCrossGame = true,
                ImportExportDependencies = true,
                TargetGameDonorDB = vTestOptions.objectDB
            };

            // Replace BioWorldInfo if requested
            if (syncBioWorldInfo)
            {
                var me1BWI = me1File.Exports.FirstOrDefault(x => x.ClassName == "BioWorldInfo");
                if (me1BWI != null)
                {
                    me1BWI.indexValue = 1;
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular, me1BWI, le1File, le1File.FindExport(@"TheWorld.PersistentLevel.BioWorldInfo_0"), true, rop, out _);
                }
            }

            // Replace Main_Sequence if requested

            if (portMainSequence)
            {
                vTestOptions.packageEditorWindow.BusyText = $"Porting sequencing on\n{levelName}";
                var dest = le1File.FindExport(@"TheWorld.PersistentLevel.Main_Sequence");
                var source = me1File.FindExport(@"TheWorld.PersistentLevel.Main_Sequence");
                if (source != null && dest != null)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular, source, le1File, dest, true, rop, out _);
                }
                else
                {
                    Debug.WriteLine($"No sequence to port in {sourceName}");
                }
            }


            var le1PL = le1File.FindExport("TheWorld.PersistentLevel");
            var le1PersistentLevel = ObjectBinary.From<Level>(le1PL);

            // Port over ModelComponents
            if (/*vTestOptions.portModels && */ShouldPortModel(sourceName.ToUpper()))
            {
                var me1ModelUIndex = ObjectBinary.From<Level>(me1PL).Model;
                List<UIndex> modelComponents = new List<UIndex>();
                foreach (var mc in me1File.Exports.Where(x => x.ClassName == "ModelComponent"))
                {
                    var mcb = ObjectBinary.From<ModelComponent>(mc);
                    if (mcb.Model == me1ModelUIndex)
                    {
                        IEntry modelComp = le1File.FindExport(mc.InstancedFullPath);
                        if (modelComp == null)
                        {
                            rop.CrossPackageMap.Clear();
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, mc, le1File, le1PL, true, rop, out modelComp);
                        }

                        modelComponents.Add(modelComp.UIndex);
                    }
                }
                le1PersistentLevel.ModelComponents = modelComponents.ToArray();
            }

            // Port over StreamableTextures
            if (vTestOptions.installTexturesInstanceMap)
            {
                foreach (var textureInstance in me1PersistentLevel.TextureToInstancesMap)
                {
                    //le1PersistentLevel.ForceStreamTextures
                    var me1Tex = me1File.GetEntry(textureInstance.Key);
                    var le1Tex = le1File.FindEntry(me1Tex.InstancedFullPath);
                    if (le1Tex != null)
                    {
                        le1PersistentLevel.TextureToInstancesMap[le1Tex.UIndex] = textureInstance.Value;
                    }
                }
            }

            // Port over ForceStreamTextures
            if (vTestOptions.installForceTextureStreaming)
            {
                foreach (var fst in me1PersistentLevel.ForceStreamTextures)
                {
                    //le1PersistentLevel.ForceStreamTextures
                    var me1Tex = me1File.GetEntry(fst.Key);
                    var le1Tex = le1File.FindEntry(me1Tex.InstancedFullPath);
                    if (le1Tex != null)
                    {
                        le1PersistentLevel.ForceStreamTextures[le1Tex.UIndex] = fst.Value;
                    }
                }
            }

            // FINALIZE PERSISTENTLEVEL
            le1PL.WriteBinary(le1PersistentLevel);

            if (vTestOptions.useDynamicLighting)
            {
                vTestOptions.packageEditorWindow.BusyText = $"Generating Dynamic Lighting on\n{levelName}";
                CreateDynamicLighting(le1File, vTestOptions, true);
            }

            // This must come after dynamic lighting as we correct a few dynamic lightings
            PostPortingCorrections(me1File, le1File, vTestOptions);

            if (vTestOptions.debugBuild)
            {
                vTestOptions.packageEditorWindow.BusyText = $"Enabling debug features on\n{levelName}";
                VTest_EnableDebugOptionsOnPackage(le1File, vTestOptions);
            }

            //if (le1File.Exports.Any(x => x.IsA("PathNode")))
            //{
            //    Debugger.Break();
            //}
            vTestOptions.packageEditorWindow.BusyText = $"Saving package\n{levelName}";
            le1File.Save();

            // Save the extra localization versions too for GE, FE, IE
            switch (levelName)
            {
                case "BIOA_PRC2_CCSIM05_DSG_LOC_FR": // French English VO
                    le1File.Save(Path.Combine(PAEMPaths.VTest_FinalDestDir, "BIOA_PRC2_CCSIM05_DSG_LOC_FE"));
                    break;
                case "BIOA_PRC2_CCSIM05_DSG_LOC_DE": // German English VO
                    le1File.Save(Path.Combine(PAEMPaths.VTest_FinalDestDir, "BIOA_PRC2_CCSIM05_DSG_LOC_GE"));
                    break;
                case "BIOA_PRC2_CCSIM05_DSG_LOC_IT": // Italian English VO
                    le1File.Save(Path.Combine(PAEMPaths.VTest_FinalDestDir, "BIOA_PRC2_CCSIM05_DSG_LOC_IE"));
                    break;

                case "BIOA_PRC2_CCSCOREBOARD_DSG_LOC_FR": // French English VO
                    le1File.Save(Path.Combine(PAEMPaths.VTest_FinalDestDir, "BIOA_PRC2_CCSCOREBOARD_DSG_LOC_FE"));
                    break;
                case "BIOA_PRC2_CCSCOREBOARD_DSG_LOC_DE": // German English VO
                    le1File.Save(Path.Combine(PAEMPaths.VTest_FinalDestDir, "BIOA_PRC2_CCSCOREBOARD_DSG_LOC_GE"));
                    break;
                case "BIOA_PRC2_CCSCOREBOARD_DSG_LOC_IT": // Italian English VO
                    le1File.Save(Path.Combine(PAEMPaths.VTest_FinalDestDir, "BIOA_PRC2_CCSCOREBOARD_DSG_LOC_IE"));
                    break;
            }

            vTestOptions.packageEditorWindow.BusyText = $"RCP CHECK on\n{levelName}";

            Debug.WriteLine($"RCP CHECK FOR {Path.GetFileNameWithoutExtension(le1File.FilePath)} -------------------------");
            ReferenceCheckPackage rcp = new ReferenceCheckPackage();
            EntryChecker.CheckReferences(rcp, le1File, EntryChecker.NonLocalizedStringConverter);

            foreach (var err in rcp.GetBlockingErrors())
            {
                Debug.WriteLine($"RCP: [ERROR] {err.Entry.InstancedFullPath} {err.Message}");
            }

            foreach (var err in rcp.GetSignificantIssues())
            {
                Debug.WriteLine($"RCP: [WARN] {err.Entry.InstancedFullPath} {err.Message}");
            }
        }

        private static bool ShouldPortModel(string uppercaseBaseName)
        {
            switch (uppercaseBaseName)
            {
                case "BIOA_PRC2_CCLOBBY02_LAY": // Fixes hole above vidinos
                case "BIOA_PRC2_CCSIM": // Fixes holes in roof of ccsim room
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Updates the level's Model, Polys, as they likely have a name collision
        /// </summary>
        /// <param name="me1File"></param>
        /// <param name="le1File"></param>
        /// <param name="vTestOptions"></param>
        private static void CorrectFileForLEXMapFileDefaults(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            RelinkerOptionsPackage rop = new RelinkerOptionsPackage() { Cache = vTestOptions.cache, TargetGameDonorDB = vTestOptions.objectDB }; // We do not set game db here as we will not be donating anything.

            // Port in the level's Model in the main file
            var me1PL = me1File.FindExport("TheWorld.PersistentLevel");
            var me1PersistentLevel = ObjectBinary.From<Level>(me1PL);

            var le1PL = le1File.FindExport("TheWorld.PersistentLevel");
            var le1PersistentLevel = ObjectBinary.From<Level>(le1PL);

            if (me1PersistentLevel.Model != 0)
            {
                // Ensure model names match
                var me1ModelExp = me1File.GetUExport(me1PersistentLevel.Model);
                var le1ModelExp = le1File.GetUExport(le1PersistentLevel.Model);
                le1ModelExp.indexValue = me1ModelExp.indexValue;

                // Binaries
                var me1Model = ObjectBinary.From<Model>(me1ModelExp);
                var le1Model = ObjectBinary.From<Model>(le1ModelExp);

                // Ensure polys names match
                var me1Polys = me1File.GetUExport(me1Model.Polys);
                var le1Polys = le1File.GetUExport(le1Model.Polys);
                le1Polys.indexValue = me1Polys.indexValue;

                // Copy over the Model data.
                if (/*vTestOptions.portModels*/ShouldPortModel(Path.GetFileNameWithoutExtension(le1File.FilePath).ToUpper()))
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, me1ModelExp, le1File, le1ModelExp, true, rop, out _);
                }
            }
        }

        private static void FixPlanters(IMEPackage le1File, VTestOptions vTestOptions)
        {
            // Planters need a mesh copied in since they got split into two pieces
            var fPath = Path.GetFileNameWithoutExtension(le1File.FilePath);
            switch (fPath)
            {
                case "BIOA_PRC2AA":
                    {
                        // PLANTER HIGH
                        using var planterSource = MEPackageHandler.OpenMEPackage(Path.Combine(MEDirectories.GetCookedPath(MEGame.LE1), "BIOA_ICE20_03_DSG.pcc"), forceLoadFromDisk: true);
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, planterSource.FindExport("TheWorld.PersistentLevel.InterpActor_67"), le1File, le1File.FindEntry("TheWorld.PersistentLevel"), true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var leavesSMA);
                        PathEdUtils.SetLocation(leavesSMA as ExportEntry, -35797.312f, 10758.975f, 6777.0386f);
                    }
                    break;
                case "BIOA_PRC2AA_00_LAY":
                    {
                        // PLANTER HIGH (DOOR)
                        using var planterSource = MEPackageHandler.OpenMEPackage(Path.Combine(MEDirectories.GetCookedPath(MEGame.LE1), "BIOA_ICE20_03_DSG.pcc"), forceLoadFromDisk: true);
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, planterSource.FindExport("TheWorld.PersistentLevel.InterpActor_67"), le1File, le1File.FindEntry("TheWorld.PersistentLevel"), true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var leavesHighSMA);
                        PathEdUtils.SetLocation(leavesHighSMA as ExportEntry, -35043.76f, 10664f, 6792.9917f);

                        // PLANTER MEDIUM (NEARBED)
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, planterSource.FindExport("TheWorld.PersistentLevel.InterpActor_23"), le1File, le1File.FindEntry("TheWorld.PersistentLevel"), true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var leavesMedSMA);
                        PathEdUtils.SetLocation(leavesMedSMA as ExportEntry, -35470.273f, 11690.752f, 6687.2974f + (112f * 0.6f)); // 112f is the offset

                        // PLANTER MEDIUM (TABLE)
                        var leavesMed2SMA = EntryCloner.CloneTree(leavesMedSMA);
                        PathEdUtils.SetLocation(leavesMed2SMA as ExportEntry, -34559.5f, 11378.695f, 6687.457f + (112f * 0.6f)); // 112f is the offset

                    }
                    break;
            }
        }

        private static StructProperty ConvertCoverSlot(StructProperty me1CoverSlotProps, IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // How to convert a coverslot

            // 1. Draw some circles
            var csProps = GlobalUnrealObjectInfo.getDefaultStructValue(MEGame.LE1, "CoverSlot", true, vTestOptions.cache);

            // Populate Actions in 684 before we enumerate things
            var actions = csProps.GetProp<ArrayProperty<EnumProperty>>("Actions");
            actions.Clear();
            if (me1CoverSlotProps.GetProp<BoolProperty>("bLeanLeft").Value) actions.Add(new EnumProperty("CA_LeanLeft", "ECoverAction", MEGame.LE1));
            if (me1CoverSlotProps.GetProp<BoolProperty>("bLeanRight").Value) actions.Add(new EnumProperty("CA_LeanRight", "ECoverAction", MEGame.LE1));
            if (me1CoverSlotProps.GetProp<BoolProperty>("bCanPopUp").Value) actions.Add(new EnumProperty("CA_PopUp", "ECoverAction", MEGame.LE1));
            // Might be more but no clue.


            // 2. Draw the rest of the fucking owl
            foreach (var me1Prop in me1CoverSlotProps.Properties.ToList())
            {
                switch (me1Prop)
                {
                    case IntProperty:
                    case FloatProperty:
                    case BoolProperty:
                    case EnumProperty:
                        if (TryUpdateProp(me1Prop, csProps))
                        {
                            me1CoverSlotProps.Properties.Remove(me1Prop);
                        }

                        break;
                    case ObjectProperty op:
                        if (op.Value == 0)
                            me1CoverSlotProps.Properties.Remove(me1Prop); // This doesn't have a value
                        break;
                    case StructProperty sp:
                        {

                            if (sp.Name == "LocationOffset" || sp.Name == "RotationOffset")
                            {
                                // These can be directly moved.
                                if (!sp.IsImmutable)
                                    Debugger.Break();
                                TryUpdateProp(me1Prop, csProps);
                                me1CoverSlotProps.Properties.Remove(sp);
                            }

                            if (sp.Name == "MantleTarget")
                            {
                                ConvertCoverReference(sp, csProps.GetProp<StructProperty>("MantleTarget").Properties, me1File, le1File, vTestOptions);
                                me1CoverSlotProps.Properties.Remove(sp);
                            }

                            break;
                        }
                    case ArrayProperty<StructProperty> asp:
                        {
                            switch (asp.Name)
                            {
                                case "DangerLinks":
                                    {
                                        var le1DLProp = csProps.GetProp<ArrayProperty<StructProperty>>("DangerLinks");
                                        foreach (var dl in asp)
                                        {
                                            var dlProps = GlobalUnrealObjectInfo.getDefaultStructValue(MEGame.LE1, "DangerLink", true, vTestOptions.cache);
                                            ConvertDangerLink(dl, dlProps, me1File, le1File, vTestOptions);
                                            le1DLProp.Add(new StructProperty("DangerLink", dlProps, isImmutable: true));
                                        }

                                        break;
                                    }
                                case "ExposedFireLinks":
                                    {
                                        var le1DLProp = csProps.GetProp<ArrayProperty<StructProperty>>("ExposedFireLinks");
                                        if (le1DLProp.Count > 0)
                                            Debugger.Break(); // This should be empty to start with...
                                        int linkNum = 0;
                                        foreach (var dl in asp)
                                        {
                                            // CoverReference -> ExposedLink (ExposedScale). No way to compute this at all... Guess just random ¯\_(ツ)_/¯
                                            var dlProps = GlobalUnrealObjectInfo.getDefaultStructValue(MEGame.LE1, "ExposedLink", true, vTestOptions.cache);
                                            ConvertExposedLink(dl, dlProps, me1File, le1File, vTestOptions);
                                            le1DLProp.Add(new StructProperty("ExposedLink", dlProps, isImmutable: true));
                                            //Debug.WriteLine($"Converted EFL {linkNum} of {asp.Count}");
                                            linkNum++;
                                        }

                                        break;
                                    }
                                case "FireLinks":
                                case "ForcedFireLinks":
                                    {
                                        var le1DLProp = csProps.GetProp<ArrayProperty<StructProperty>>(asp.Name);
                                        foreach (var dl in asp)
                                        {
                                            // FireLink -> FireLink. This struct changed a lot
                                            var dlProps = GlobalUnrealObjectInfo.getDefaultStructValue(MEGame.LE1, "FireLink", true, vTestOptions.cache);
                                            ConvertFireLink(dl, dlProps, me1File, le1File, vTestOptions);
                                            le1DLProp.Add(new StructProperty("FireLink", dlProps, isImmutable: true));
                                        }

                                        break;
                                    }
                                case "OverlapClaims":
                                case "TurnTarget":
                                    {
                                        var le1DLProp = csProps.GetProp<ArrayProperty<StructProperty>>(asp.Name);
                                        foreach (var me1CovRef in asp)
                                        {
                                            // FireLink -> FireLink. This struct changed a lot
                                            var le1CovRefProps = GlobalUnrealObjectInfo.getDefaultStructValue(MEGame.LE1, me1CovRef.StructType, true, vTestOptions.cache);
                                            ConvertCoverReference(me1CovRef, le1CovRefProps, me1File, le1File, vTestOptions);
                                            le1DLProp.Add(new StructProperty(me1CovRef.StructType, le1CovRefProps, isImmutable: true));
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                }
            }

            if (me1CoverSlotProps.Properties.Count > 0)
            {
                // uncomment to debug these
                //Debug.WriteLine("The following properties were not translated:");
                foreach (var mp in me1CoverSlotProps.Properties)
                {
                    //Debug.WriteLine(mp.Name );
                }
            }


            return new StructProperty("CoverSlot", csProps, isImmutable: true);

            bool TryUpdateProp(Property p, PropertyCollection destCollection)
            {
                if (destCollection.ContainsNamedProp(p.Name))
                {
                    destCollection.AddOrReplaceProp(p);
                    return true;
                }
                Debug.WriteLine($"Target doesn't have property named {p.Name}");
                return false;
            }
        }

        private static void ConvertFireLink(StructProperty me1FL, PropertyCollection le1FL, IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            le1FL.GetProp<BoolProperty>("bFallbackLink").Value = me1FL.GetProp<BoolProperty>("bFallbackLink")?.Value ?? false;
            var mta = me1FL.GetProp<StructProperty>("TargetLink");
            var slotIdx = me1FL.GetProp<IntProperty>("TargetSlotIdx");
            var lta = le1FL.GetProp<StructProperty>("TargetActor").Properties;
            ConvertNavRefToCoverRef(mta, lta, slotIdx, me1File, le1File, vTestOptions);

            // Items MUST BE DONE ON A SECOND PASS ONCE ALL THE COVERSLOTS HAVE BEEN GENERATED
        }

        private static void GenerateFireLinkItemsForFile(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // This appears to be map of SourceCoverType and the action on it -> Destination Cover Type and Action
            // E.g. This FirELinkItem is for Me popping up, shooting at a coverlink that has someone doing action Default MidLevel (hiding)
            // Will require reading the destination CoverSlots so this will actually probably need to be done on a second pass...

            foreach (var le1Cl in le1File.Exports.Where(x => x.ClassName == "CoverLink"))
            {
                var me1cl = me1File.FindExport(le1Cl.InstancedFullPath);

                var me1Props = me1cl.GetProperties();
                var le1Props = le1Cl.GetProperties();

                var me1Slots = me1Props.GetProp<ArrayProperty<StructProperty>>("Slots");
                var le1Slots = le1Props.GetProp<ArrayProperty<StructProperty>>("Slots");

                for (int i = 0; i < me1Slots.Count; i++)
                {
                    var me1Slot = me1Slots[i];
                    var le1Slot = le1Slots[i];
                    GenerateFireLinkItemsForSlot(me1Slot, le1Slot, me1File, le1File, vTestOptions);
                }

                le1Cl.WriteProperties(le1Props);
            }
        }

        private static void GenerateFireLinkItemsForSlot(StructProperty me1Slot, StructProperty le1Slot, IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // GET THE RAGU MARIO
            // WE'RE A MAKIN SPAGHETTI

            var me1FireLinks = me1Slot.GetProp<ArrayProperty<StructProperty>>("FireLinks");
            var le1FireLinks = le1Slot.GetProp<ArrayProperty<StructProperty>>("FireLinks");
            for (int i = 0; i < me1FireLinks.Count; i++)
            {
                var le1FL = le1FireLinks[i];
                var le1Items = le1FL.GetProp<ArrayProperty<StructProperty>>("Items"); // We will populate this list

                var targetActor = le1FL.GetProp<StructProperty>("TargetActor");
                var destCoverVal = targetActor.GetProp<ObjectProperty>("Actor");
                if (destCoverVal == null)
                    Debugger.Break(); // it's cross level, what a nightmare

                var destSlotIdx = targetActor.GetProp<IntProperty>("SlotIdx");
                var destCover = destCoverVal.ResolveToEntry(le1File) as ExportEntry;
                var destSlot = destCover.GetProperty<ArrayProperty<StructProperty>>("Slots")[destSlotIdx];

                var destType = destSlot.GetProp<EnumProperty>("CoverType").Value; //DestType
                List<string> destActions = new List<string>();
                if (destSlot.GetProp<BoolProperty>("bLeanLeft")) destActions.Add("CA_LeanLeft");
                if (destSlot.GetProp<BoolProperty>("bLeanRight")) destActions.Add("CA_LeanRight");
                if (destSlot.GetProp<BoolProperty>("bCanPopUp")) destActions.Add("CA_PopUp");
                destActions.Add("CA_Default"); // This doesn't seem reliable but idk what else to do

                var srcType = me1FireLinks[i].GetProp<EnumProperty>("CoverType").Value;

                int generated = 0;
                var srcActions = me1FireLinks[i].GetProp<ArrayProperty<EnumProperty>>("CoverActions");
                foreach (var srcAction in srcActions)
                {
                    // This now has enough info for SrcType, SrcAction, destType in the dest

                    // UNKNOWN HOW THE DEST ACTION IS DETERMINED, IT DOESN'T APPEAR RELIABLE. See above
                    foreach (var destAction in destActions)
                    {
                        PropertyCollection fliProps = new PropertyCollection();
                        fliProps.Add(new EnumProperty(srcType, "ECoverType", MEGame.LE1, "SrcType"));
                        fliProps.Add(new EnumProperty(srcAction.Value, "ECoverAction", MEGame.LE1, "SrcAction"));
                        fliProps.Add(new EnumProperty(destType, "ECoverType", MEGame.LE1, "DestType"));
                        fliProps.Add(new EnumProperty(destAction, "ECoverAction", MEGame.LE1, "DestAction"));
                        le1Items.Add(new StructProperty("FireLinkItem", fliProps, isImmutable: true));
                        generated++;
                        //Debug.WriteLine($"Generated FLI {generated}. DAC: {destActions.Count}, SAC: {srcActions.Count}");
                    }
                }
            }

        }

        private static void ConvertNavRefToCoverRef(StructProperty mta, PropertyCollection lta, IntProperty slotIdx, IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // ME1: NavReference (w/ External SlotIdx)
            // LE1: CoverReference
            // We don't bother changing the Direction.

            lta.GetProp<IntProperty>("SlotIdx").Value = slotIdx;
            lta.GetProp<StructProperty>("Guid").Properties = mta.GetProp<StructProperty>("Guid").Properties;

            var nav = mta.GetProp<ObjectProperty>("Nav");
            lta.GetProp<ObjectProperty>("Actor").Value = le1File.FindExport(me1File.GetUExport(nav.Value).InstancedFullPath).UIndex;

        }

        private static void ConvertExposedLink(StructProperty me1ELStruct, PropertyCollection le1ELStructProps, IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // ME1: NavReference
            // LE1: DangerLink
            // We don't bother changing the DangerCost.
            var le1ExposedTargetActor = le1ELStructProps.GetProp<StructProperty>("TargetActor");
            ConvertCoverReference(me1ELStruct, le1ExposedTargetActor.Properties, me1File, le1File, vTestOptions);

            // The ExposedScale is the amount of exposure to other links. Higher exposure is better... I think?
            // This is computed during map cook so ... yeah ... ... ...
            le1ELStructProps.GetProp<ByteProperty>("ExposedScale").Value = 128; // No idea what to put here
        }

        private static void ConvertDangerLink(StructProperty me1DLStruct, PropertyCollection le1DLStruct, IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // ME1: NavReference
            // LE1: DangerLink
            // We don't bother changing the DangerCost.
            var le1ARStruct = le1DLStruct.GetProp<StructProperty>("DangerNav");
            ConvertNavRefToActorRef(me1DLStruct, le1ARStruct.Properties, me1File, le1File, vTestOptions);
        }

        private static void ConvertNavRefToActorRef(StructProperty me1NRStruct, PropertyCollection le1ARStruct, IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            le1ARStruct.GetProp<StructProperty>("Guid").Properties = me1NRStruct.GetProp<StructProperty>("Guid").Properties;
            var nav = me1NRStruct.GetProp<ObjectProperty>("Nav");
            if (nav.Value != 0)
            {
                // All navigation points should have been imported by VTest... Soo......
                // Hopefully this works
                le1ARStruct.GetProp<ObjectProperty>("Actor").Value = le1File.FindExport(me1File.GetUExport(nav.Value).InstancedFullPath).UIndex;
                //Debugger.Break();
            }
        }

        /// <summary>
        /// Converts CoverReference (491) -> CoverReference (684)
        /// </summary>
        /// <param name="me1Prop"></param>
        /// <param name="le1Props"></param>
        /// <param name="targetPropName"></param>
        /// <param name="me1File"></param>
        /// <param name="le1File"></param>
        /// <param name="vTestOptions"></param>
        private static void ConvertCoverReference(StructProperty me1Prop, PropertyCollection le1Props, IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            le1Props.GetProp<IntProperty>("SlotIdx").Value = me1Prop.GetProp<IntProperty>("SlotIdx").Value;
            le1Props.GetProp<IntProperty>("Direction").Value = me1Prop.GetProp<IntProperty>("Direction").Value;
            le1Props.GetProp<StructProperty>("Guid").Properties = me1Prop.GetProp<StructProperty>("Guid").Properties;
            var nav = me1Prop.GetProp<ObjectProperty>("Nav");
            if (nav.Value != 0)
            {
                // All navigation points should have been imported by VTest... Soo......
                // Hopefully this works
                le1Props.GetProp<ObjectProperty>("Actor").Value = le1File.FindExport(me1File.GetUExport(nav.Value).InstancedFullPath).UIndex;
                //Debugger.Break();
            }
            // Default is 0 so don't have to do anything
        }

        private static void VTest_EnableDebugOptionsOnPackage(IMEPackage le1File, VTestOptions vTestOptions)
        {
            // This is no longer necessary with m_aObjLog Enabler mod
            //SequenceEditorExperimentsM.ConvertSeqAct_Log_objComments(le1File, vTestOptions.cache);
        }

        /// <summary>
        /// Ports a list of actors between levels with VTest
        /// </summary>
        /// <param name="sourcePackage"></param>
        /// <param name="destPackage"></param>
        /// <param name="itemsToPort"></param>
        /// <param name="db"></param>
        /// <param name="pe"></param>
        private static void VTestFilePorting(IMEPackage sourcePackage, IMEPackage destPackage, IEnumerable<ExportEntry> itemsToPort, VTestOptions vTestOptions)
        {
            // PRECORRECTION - CORRECTIONS TO THE SOURCE FILE BEFORE PORTING
            var levelName = Path.GetFileNameWithoutExtension(destPackage.FilePath);

            // PORTING ACTORS
            var le1PL = destPackage.FindExport("TheWorld.PersistentLevel");
            foreach (var e in itemsToPort)
            {
                vTestOptions.packageEditorWindow.BusyText = $"Porting {e.ObjectName.Instanced} on\n{levelName}";
                RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
                {
                    Cache = vTestOptions.cache,
                    ImportExportDependencies = true,
                    IsCrossGame = true,
                    TargetGameDonorDB = vTestOptions.objectDB
                };
                var report = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, e, destPackage,
                    le1PL, true, rop, out _);

                if (vTestOptions.debugBuildAssetCachePackage)
                {
                    rop.CrossPackageMap.Clear();
                    var originalIndexValue = e.indexValue;
                    var assetPL = vTestOptions.assetCachePackage.FindExport("TheWorld.PersistentLevel");
                    e.indexValue = vTestOptions.assetCacheIndex++; // We do this to ensure no collisions so the cache is built
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, e, vTestOptions.assetCachePackage,
                        assetPL, true, rop, out _);
                    e.indexValue = originalIndexValue;
                }
            }
        }

        /// <summary>
        /// Ports a LOC file by porting the ObjectReferencer within it
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="db"></param>
        /// <param name="pc"></param>
        /// <param name="pe"></param>
        private static void PortLOCFile(string sourceFile, VTestOptions vTestOptions)
        {
            var packName = Path.GetFileNameWithoutExtension(sourceFile);
            vTestOptions.packageEditorWindow.BusyText = $"Porting {packName}";

            var destPackagePath = Path.Combine(PAEMPaths.VTest_FinalDestDir, $"{packName.ToUpper()}.pcc");
            MEPackageHandler.CreateAndSavePackage(destPackagePath, MEGame.LE1);
            using var package = MEPackageHandler.OpenMEPackage(destPackagePath);
            using var sourcePackage = MEPackageHandler.OpenMEPackage(sourceFile);

            PrePortingCorrections(sourcePackage, vTestOptions);

            var bcBaseIdx = sourcePackage.findName("BIOC_Base");
            if (bcBaseIdx >= 0)
            {
                sourcePackage.replaceName(bcBaseIdx, "SFXGame");
            }

            foreach (var e in sourcePackage.Exports.Where(x => x.ClassName == "ObjectReferencer"))
            {
                // Correct the object referencer to remove objects that shouldn't be be ported.
                var ro = e.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedObjects");
                foreach (var refObj in ro.ToList())
                {
                    if (refObj.ResolveToEntry(sourcePackage) is ExportEntry resolved)
                    {
                        bool removed = false; // for if we check more than just archetypes

                        // Prune items with bad archetypes
                        var archetypeObjectName = resolved.Archetype?.ObjectName.Name ?? null;
                        switch (archetypeObjectName) // Not instanced as these are typically sub items and have instance numbers
                        {
                            case "DistributionLPFMaxRadius":
                            case "DistributionLPFMinRadius":
                                ro.Remove(refObj);
                                removed = true;
                                break;
                        }

                        if (removed)
                            continue;
                    }
                }
                e.WriteProperty(ro);
                RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
                {
                    IsCrossGame = true,
                    ImportExportDependencies = true,
                    Cache = vTestOptions.cache,
                    TargetGameDonorDB = vTestOptions.objectDB
                };

                var report = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, e, package, null, true, rop, out _);
                if (report.Any())
                {
                    //Debugger.Break();
                }
            }

            //CorrectSequences(package, vTestOptions);
            PostPortingCorrections(sourcePackage, package, vTestOptions);

            vTestOptions.packageEditorWindow.BusyText = $"Saving {packName}";
            package.Save();
        }
        #endregion

        #region Utility methods
        public static void CreateEmptyLevel(string outpath, MEGame game)
        {
            var emptyLevelName = $"{game}EmptyLevel";
            File.Copy(Path.Combine(AppDirectories.ExecFolder, $"{emptyLevelName}.pcc"), outpath, true);
            using var Pcc = MEPackageHandler.OpenMEPackage(outpath);
            for (int i = 0; i < Pcc.Names.Count; i++)
            {
                string name = Pcc.Names[i];
                if (name.Equals(emptyLevelName))
                {
                    var newName = name.Replace(emptyLevelName, Path.GetFileNameWithoutExtension(outpath));
                    Pcc.replaceName(i, newName);
                }
            }

            var packguid = Guid.NewGuid();
            var package = Pcc.GetUExport(game switch
            {
                MEGame.LE1 => 4,
                MEGame.LE3 => 6,
                MEGame.ME2 => 7,
                _ => 1
            });
            package.PackageGUID = packguid;
            Pcc.PackageGuid = packguid;
            Pcc.Save();
        }

        private static StructProperty MakeLinearColorStruct(string propertyName, float r, float g, float b, float a)
        {
            PropertyCollection p = new PropertyCollection();
            p.AddOrReplaceProp(new FloatProperty(r, "R"));
            p.AddOrReplaceProp(new FloatProperty(g, "G"));
            p.AddOrReplaceProp(new FloatProperty(b, "B"));
            p.AddOrReplaceProp(new FloatProperty(a, "A"));
            return new StructProperty("LinearColor", p, propertyName, true);
        }
        #endregion

        #region Correction methods
        public static void PrePortingCorrections(IMEPackage sourcePackage, VTestOptions vTestOptions)
        {
            // FILE SPECIFIC
            var sourcePackageName = Path.GetFileNameWithoutExtension(sourcePackage.FilePath).ToUpper();
            if (sourcePackageName == "BIOA_PRC2_CCSIM03_LAY")
            {
                // garage door
                sourcePackage.FindExport("TheWorld.PersistentLevel.BioDoor_1.SkeletalMeshComponent_1").RemoveProperty("Materials"); // The materials changed in LE so using the original set is wrong. Remove this property to prevent porting donors for it
            }

            // Strip static mesh light maps since they don't work crossgen. Strip them from
            // the source so they don't port
            foreach (var exp in sourcePackage.Exports.ToList())
            {
                PruneUnusedProperties(exp);
                #region Remove Light and Shadow Maps
                if (exp.ClassName == "StaticMeshComponent")
                {
                    //if Non-Collection Static add LE Bool
                    if (exp.Parent.ClassName == "StaticMeshActor")
                    {
                        var props = exp.GetProperties();
                        props.AddOrReplaceProp(new BoolProperty(true, "bIsOwnerAStaticMeshActor"));
                        exp.WriteProperties(props);
                    }

                    if (vTestOptions == null || vTestOptions.useDynamicLighting || vTestOptions.stripShadowMaps)
                    {
                        if (vTestOptions != null && vTestOptions.allowTryingPortedMeshLightMap && !sourcePackageName.StartsWith("BIOA_PRC2AA")) // BIOA_PRC2AA doesn't seem to work with lightmaps
                        {
                            var sm = exp.GetProperty<ObjectProperty>("StaticMesh"); // name might need changed?
                            if (sm != null)
                            {
                                if (sourcePackage.TryGetEntry(sm.Value, out var smEntry))
                                {
                                    if (ShouldPortLightmaps(smEntry))
                                    {
                                        continue; // Do not port
                                    }
                                }
                            }
                        }

                        var b = ObjectBinary.From<StaticMeshComponent>(exp);
                        foreach (var lod in b.LODData)
                        {
                            // Clear light and shadowmaps
                            if (vTestOptions == null || vTestOptions.stripShadowMaps || vTestOptions.useDynamicLighting)
                            {
                                lod.ShadowMaps = new UIndex[0];
                            }

                            if (vTestOptions == null || vTestOptions.useDynamicLighting)
                            {
                                lod.LightMap = new LightMap() { LightMapType = ELightMapType.LMT_None };
                            }
                        }

                        exp.WriteBinary(b);
                    }
                }
                #endregion
                //// These are precomputed and stored in VTestHelper.pcc 
                //else if (exp.ClassName == "Terrain")
                //{
                //    exp.RemoveProperty("TerrainComponents"); // Don't port the components; we will port them ourselves in post
                //}
                else if (exp.ClassName == "BioTriggerStream")
                {
                    PreCorrectBioTriggerStream(exp);
                }
                else if (exp.ClassName == "BioWorldInfo")
                {
                    // Remove streaminglevels that don't do anything
                    //PreCorrectBioWorldInfoStreamingLevels(exp);
                }
                else if (exp.ClassName == "MaterialInstanceConstant")
                {
                    PreCorrectMaterialInstanceConstant(exp);
                }
                else if (exp.ClassName == "ModelComponent")
                {
                    if (vTestOptions.useDynamicLighting)
                    {
                        var mcb = ObjectBinary.From<ModelComponent>(exp);
                        foreach (var elem in mcb.Elements)
                        {
                            elem.ShadowMaps = new UIndex[0]; // We want no shadowmaps
                            elem.LightMap = new LightMap() { LightMapType = ELightMapType.LMT_None }; // Strip the lightmaps
                        }

                        exp.WriteBinary(mcb);
                    }
                }
                else if (exp.ClassName == "Sequence" && exp.GetProperty<StrProperty>("ObjName")?.Value == "PRC2_KillTriggerVolume")
                {
                    // Done before porting to prevent Trash from appearing in target
                    PreCorrectKillTriggerVolume(exp, vTestOptions);
                }

                // KNOWN BAD NAMES
                if (exp.ClassName == "Texture2D")
                {
                    if (exp.InstancedFullPath == "BIOA_JUG80_T.JUG80_SAIL")
                    {
                        // Rename to match crossgen
                        exp.ObjectName = "JUG80_SAIL_CROSSGENFIX";
                    }
                    else if (exp.InstancedFullPath == "BIOA_ICE60_T.checker")
                    {
                        // Rename to match crossgen
                        exp.ObjectName = "BIOA_ICE60_T.checker_CROSSGENFIX";
                    }
                }
            }
        }

        private static bool ShouldPortLightmaps(IEntry smEntry)
        {
            if (PossibleLightMapUsers.Contains(smEntry.InstancedFullPath))
            {
                return true; // We will try to use the original lightmaps for this
            }

            // Thai specific stuff as this map really needs some help
            // TEST ONLY
            // Install lightmap for the cover boxes
            if (ME1OptimizationAssets.Contains(smEntry.InstancedFullPath))
            {
                // Allow this as we will replace with ME1 mesh as it's lightmaps and lower tris
                Debug.WriteLine($"Not using dynamic lighting for THAI optimization, mesh {smEntry.InstancedFullPath}");
                return true;
            }

            return false;
        }

        // Terrible performance, but i don't care
        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }

        private static Dictionary<Guid, Guid> expressionGuidMap = new()
        {
            // INT, RA, RU
            { new Guid(StringToByteArray("AD2F8F9FB837D8499EF1FC9799289A3E")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Alpha_Map -> Texture (Scoreboard)
            { new Guid(StringToByteArray("32144A9CDE189141BC421589B7EF3C0A")), new Guid(StringToByteArray("A1A3A72858C9DC45A10D3E9967BE4EE8")) }, // Character_Color -> ColorSelected (Scoreboard)
            { new Guid(StringToByteArray("E1EC0FC0E38D07439505E7C1EBB17F6D")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Alpha_Map -> Texture (Scoreboard Pulse)

            // FR
            { new Guid(StringToByteArray("89A949CF1307894385007C3F7E294B31")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Alpha_Map -> Texture (Scoreboard)
            { new Guid(StringToByteArray("37CFE5EE4EBB19448C1145BC9CE552DF")), new Guid(StringToByteArray("A1A3A72858C9DC45A10D3E9967BE4EE8")) }, // Character_Color -> ColorSelected (Scoreboard)
            { new Guid(StringToByteArray("4C7796E7E334BC418E3D223A3A0C57D4")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Alpha_Map -> Texture (Scoreboard Pulse)

            // IT
            { new Guid(StringToByteArray("05E20F645EEC884DA4CEEFD9E7A0CF90")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Alpha_Map -> Texture (Scoreboard)
            { new Guid(StringToByteArray("4F149E20719FF740B3B2AC822ECA5DFA")), new Guid(StringToByteArray("A1A3A72858C9DC45A10D3E9967BE4EE8")) }, // Character_Color -> ColorSelected (Scoreboard)
            { new Guid(StringToByteArray("C60CD51DDE59A640BC776D3248FC5BBB")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Character_Color -> ColorSelected (Scoreboard)

            // DE
            { new Guid(StringToByteArray("B70FAC0E0EFB33448ABE57CB3D786FC8")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Alpha_Map -> Texture (Scoreboard)
            { new Guid(StringToByteArray("392F5A5B38808A4B90E4AD2421875C16")), new Guid(StringToByteArray("A1A3A72858C9DC45A10D3E9967BE4EE8")) }, // Character_Color -> ColorSelected (Scoreboard)
            { new Guid(StringToByteArray("61EFE5604E71624D8B8A934EB297BBD8")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Alpha_Map -> Texture (Scoreboard Pulse)

            // what is this used for?
            //{ new Guid(StringToByteArray("E1EC0FC0E38D07439505E7C1EBB17F6D")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Alpha_Map -> Texture (Scoreboard Pulse)

        };

        private static Dictionary<string, string> parameterNameMap = new()
        {
            { "Alpha_Map", "Texture" }, // PRC2 Scoreboard Materials
            { "Character_Color", "ColorSelected" } // PRC2 Scoreboard Materials
        };

        private static void PreCorrectMaterialInstanceConstant(ExportEntry exp)
        {
            // Some parameters need updated to match new materials

            // VECTORS
            var vectorParams = exp.GetProperty<ArrayProperty<StructProperty>>("VectorParameterValues");
            if (vectorParams != null)
            {
                foreach (var vp in vectorParams)
                {
                    var parameterName = vp.GetProp<NameProperty>("ParameterName").Value.Name;
                    var expressionGuid = CommonStructs.GetGuid(vp.GetProp<StructProperty>("ExpressionGUID"));
                    if (expressionGuidMap.TryGetValue(expressionGuid, out var newGuid) && parameterNameMap.TryGetValue(parameterName, out var newParameterName))
                    {
                        Debug.WriteLine($"Updating VP MIC {exp.InstancedFullPath}");
                        vp.GetProp<NameProperty>("ParameterName").Value = newParameterName;
                        vp.Properties.AddOrReplaceProp(CommonStructs.GuidProp(newGuid, "ExpressionGUID"));
                    }
                }
                exp.WriteProperty(vectorParams);
            }

            // TEXTURES
            var textureParams = exp.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues");
            if (textureParams != null)
            {
                foreach (var tp in textureParams)
                {
                    var parameterName = tp.GetProp<NameProperty>("ParameterName").Value.Name;
                    var expressionGuid = CommonStructs.GetGuid(tp.GetProp<StructProperty>("ExpressionGUID"));
                    if (expressionGuidMap.TryGetValue(expressionGuid, out var newGuid) && parameterNameMap.TryGetValue(parameterName, out var newParameterName))
                    {
                        Debug.WriteLine($"Updating TP MIC {exp.InstancedFullPath}");
                        tp.GetProp<NameProperty>("ParameterName").Value = newParameterName;
                        tp.Properties.AddOrReplaceProp(CommonStructs.GuidProp(newGuid, "ExpressionGUID"));
                    }
                }
                exp.WriteProperty(textureParams);
            }
        }

        /// <summary>
        /// PRECORRECTED as we do not want to have trash exports in target
        /// </summary>
        /// <param name="killTriggerSeq"></param>
        /// <param name="vTestOptions"></param>
        public static void PreCorrectKillTriggerVolume(ExportEntry killTriggerSeq, VTestOptions vTestOptions)
        {
            var sequenceObjects = KismetHelper.GetSequenceObjects(killTriggerSeq).OfType<ExportEntry>().ToList();
            var cursor = sequenceObjects.FirstOrDefault(x => x.ClassName == "SeqVar_Object");

            var compareObject = SequenceObjectCreator.CreateSequenceObject(killTriggerSeq.FileRef, "SeqCond_CompareObject", vTestOptions.cache);
            var playerObj = SequenceObjectCreator.CreateSequenceObject(killTriggerSeq.FileRef, "SeqVar_Player", vTestOptions.cache);

            KismetHelper.CreateVariableLink(compareObject, "A", cursor);
            KismetHelper.CreateVariableLink(compareObject, "B", playerObj);

            var takeDamage = SequenceObjectCreator.CreateSequenceObject(killTriggerSeq.FileRef, "BioSeqAct_CauseDamage", vTestOptions.cache);
            takeDamage.WriteProperty(new FloatProperty(50, "m_fDamageAmountAsPercentOfMaxHealth"));

            KismetHelper.AddObjectsToSequence(killTriggerSeq, false, takeDamage, compareObject, playerObj);

            KismetHelper.CreateVariableLink(takeDamage, "Target", cursor); // Hook up target to damage

            var doAction = sequenceObjects.FirstOrDefault(x => x.ClassName == "BioSeqAct_DoActionInVolume");
            var log = sequenceObjects.FirstOrDefault(x => x.ClassName == "SeqAct_Log");
            KismetHelper.RemoveOutputLinks(doAction);


            KismetHelper.CreateOutputLink(doAction, "Next", compareObject, 0); // Connect DoAction Next to CompareObject
            KismetHelper.CreateOutputLink(compareObject, "A == B", takeDamage); // TEST ONLY - TOUCHING PLAYER
            //KismetHelper.CreateOutputLink(compareObject, "A == B", doAction, 1); // Connect CompareObj to DoAction (The touching pawn is Player, skip damage)
            KismetHelper.CreateOutputLink(compareObject, "A != B", takeDamage); // Connect CompareObj to DoAction (The touching pawn is Player, skip damage)

            KismetHelper.CreateOutputLink(takeDamage, "Out", doAction, 1); // Connect DoAction Next to Damage In
            KismetHelper.CreateOutputLink(takeDamage, "Out", log); // Connect takedamage to log

            // TRASH AND REMOVE FROM SEQUENCE
            var destroy = sequenceObjects.FirstOrDefault(x => x.ClassName == "SeqAct_Destroy");
            KismetHelper.RemoveAllLinks(destroy);

            //remove from sequence
            var seqObjs = SeqTools.GetParentSequence(destroy).GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            seqObjs.Remove(new ObjectProperty(destroy));
            SeqTools.GetParentSequence(destroy).WriteProperty(seqObjs);

            //Trash
            EntryPruner.TrashEntryAndDescendants(destroy);

            // BLOCKING VOLUMES WHERE KILL VOLUMES ARE FOR PLAYER AND HENCH
            /*var donorBv = vTestOptions.vTestHelperPackage.FindExport("PRC2.BlockingVolume_32000");
            donorBv.indexValue = FindNextNameIndex(killTriggerSeq.FileRef, "TheWorld.PersistentLevel.BlockingVolume");
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, donorBv, killTriggerSeq.FileRef, killTriggerSeq.FileRef.FindExport("TheWorld.PersistentLevel"), true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var newBvEntry);
            donorBv.indexValue = 32001; // reset

            var newBV = newBvEntry as ExportEntry;
            newBV.WriteProperty(new NameProperty("CROSSGEN_KillVolumeCollision", "Tag"));

            var affectedActors = new ArrayProperty<NameProperty>("lstAffectedActors");
            affectedActors.Add(new NameProperty("player"));
            affectedActors.Add(new NameProperty("hench_humanmale"));
            affectedActors.Add(new NameProperty("hench_humanmale"));
            affectedActors.Add(new NameProperty("hench_asari"));
            affectedActors.Add(new NameProperty("hench_krogan"));
            affectedActors.Add(new NameProperty("hench_quarian"));
            affectedActors.Add(new NameProperty("hench_turian"));
            newBV.WriteProperty(affectedActors);
            newBV.WriteProperty(new BoolProperty(false, "bInclusionaryList"));
            newBV.WriteProperty(new BoolProperty(false, "bBlockCamera"));

            var killVolume = (SeqTools.GetVariableLinksOfNode(SeqTools.GetParentSequence(killTriggerSeq))[0].LinkedNodes[0] as ExportEntry).GetProperty<ObjectProperty>("ObjValue").ResolveToEntry(killTriggerSeq.FileRef) as ExportEntry;
            var tempClone = EntryCloner.CloneTree(killVolume);

            foreach (var exp in killVolume.FileRef.Exports.Where(x => x.idxLink == tempClone.UIndex))
            {
                exp.idxLink = newBV.UIndex; // attach to parent
                if (exp.ClassName == "Model")
                {
                    newBV.WriteProperty(new ObjectProperty(exp, "Brush"));
                }
                else if (exp.ClassName == "BrushComponent")
                {
                    exp.Archetype = killTriggerSeq.FileRef.FindImport("Engine.Default__BlockingVolume.BrushComponent0");
                    newBV.WriteProperty(new ObjectProperty(exp, "BrushComponent"));
                    newBV.WriteProperty(new ObjectProperty(exp, "CollisionComponent"));
                }
            }
            EntryPruner.TrashEntryAndDescendants(tempClone); // delete the clone

            PathEdUtils.SetLocation(newBV, PathEdUtils.GetLocation(killVolume));
            var drawScale3d = killVolume.GetProperty<StructProperty>("DrawScale3D");
            if (drawScale3d != null)
            {
                newBV.WriteProperty(drawScale3d);
            }
            //var drawScale = killVolume.GetProperty<FloatProperty>("DrawScale");
            //if (drawScale != null)
            //{
            //    newBV.WriteProperty(drawScale);
            //}
            newBV.WriteProperty(new FloatProperty(1.03f, "DrawScale")); // Make just a bit bigger

            //todo: set scale

            // add to level
            var level = killTriggerSeq.FileRef.FindExport("TheWorld.PersistentLevel");
            var levelBin = ObjectBinary.From<Level>(level);
            levelBin.Actors.Add(newBV);
            level.WriteBinary(levelBin);
            */

            // Porting in object added BIOC_Base import
            // BIOC_BASE -> SFXGame
            var bcBaseIdx = killTriggerSeq.FileRef.findName("BIOC_Base");
            killTriggerSeq.FileRef.replaceName(bcBaseIdx, "SFXGame");
        }

        private static int FindNextNameIndex(IMEPackage package, string ifp)
        {
            int i = 1;
            while (true)
            {
                if (package.FindEntry($"{ifp}_{(i - 1)}") == null)
                {
                    return i;
                }
                i++;
            }
        }

        public static void ConvertME1TerrainComponent(ExportEntry exp)
        {
            // Strip Lightmap
            var b = ObjectBinary.From<TerrainComponent>(exp);
            b.LightMap = new LightMap() { LightMapType = ELightMapType.LMT_None };
            // Convert the tesselation... something... idk something that makes it multiply
            // by what appears to be 16x16 (256)
            var props = exp.GetProperties();
            //var sizeX = props.GetProp<IntProperty>("SectionSizeX");
            //var sizeY = props.GetProp<IntProperty>("SectionSizeY");
            //var trueSizeX = props.GetProp<IntProperty>("TrueSectionSizeX");
            //var trueSizeY = props.GetProp<IntProperty>("TrueSectionSizeY");

            //var factorSize = sizeX * sizeY; // idk
            //for (int i = 0; i < trueSizeY; i++)
            //{
            //    for (int j = 0; j < trueSizeX; j++)
            //    {
            //        // uh... idk?
            //        var collisionIdx = (i * trueSizeY) + j;
            //        var vtx = b.CollisionVertices[collisionIdx];
            //        b.CollisionVertices[collisionIdx] = new Vector3(vtx.X * factorSize, vtx.Y * factorSize, vtx.Z);
            //        Debug.WriteLine(collisionIdx + " " + b.CollisionVertices[collisionIdx].ToString());
            //    }
            //}

            // Correct collision vertices as they've changed from local to world in LE
            float scaleX = 256; // Default DrawScale3D for terrain is 256
            float scaleY = 256;
            float scaleZ = 256;

            float basex = 0;
            float basey = 0;
            float basez = 0;

            var ds3d = (exp.Parent as ExportEntry).GetProperty<StructProperty>("DrawScale3D");
            if (ds3d != null)
            {
                scaleX = ds3d.GetProp<FloatProperty>("X").Value;
                scaleY = ds3d.GetProp<FloatProperty>("Y").Value;
                scaleZ = ds3d.GetProp<FloatProperty>("Z").Value;
            }

            var ds = (exp.Parent as ExportEntry).GetProperty<FloatProperty>("DrawScale");
            if (ds != null)
            {
                scaleX *= ds.Value;
                scaleY *= ds.Value;
                scaleZ *= ds.Value;
            }

            var loc = (exp.Parent as ExportEntry).GetProperty<StructProperty>("Location");
            if (loc != null)
            {
                basex = loc.GetProp<FloatProperty>("X").Value;
                basey = loc.GetProp<FloatProperty>("Y").Value;
                basez = loc.GetProp<FloatProperty>("Z").Value;
            }

            // COLLISION VERTICES
            for (int i = 0; i < b.CollisionVertices.Length; i++)
            {
                var cv = b.CollisionVertices[i];
                Vector3 newV = new Vector3();

                newV.X = basex - (cv.X * scaleX);
                newV.Y = basey - (cv.Y * scaleY);
                newV.Z = basez + (cv.Z * scaleZ); // Is this right?
                b.CollisionVertices[i] = newV;
            }

            // Bounding Volume Tree
            Vector3 dif = new Vector3(86806.1f, -70072.58f, -6896.561f);
            for (int i = 0; i < b.BVTree.Length; i++)
            {
                var box = b.BVTree[i].BoundingVolume;
                box.Min = new Vector3 { X = basex - (box.Min.X * scaleX), Y = basey /*- (box.Min.Y * scaleY)*/, Z = basez + (box.Min.Z * scaleZ) };
                box.Max = new Vector3 { X = basex /*+ (box.Max.X * scaleX)*/, Y = basey + (box.Max.Y * scaleY), Z = basez + (box.Max.Z * scaleZ) };
            }

            exp.WriteBinary(b);

            // Make dynamic lighting
            //var props = exp.GetProperties();
            //props.RemoveNamedProperty("BlockRigidBody"); // make collidable?
            props.RemoveNamedProperty("ShadowMaps");
            props.AddOrReplaceProp(new BoolProperty(false, "bForceDirectLightMap"));
            props.AddOrReplaceProp(new BoolProperty(true, "bCastDynamicShadow"));
            props.AddOrReplaceProp(new BoolProperty(true, "bAcceptDynamicLights"));

            var lightingChannels = props.GetProp<StructProperty>("LightingChannels") ??
                                   new StructProperty("LightingChannelContainer", false,
                                       new BoolProperty(true, "bIsInitialized"))
                                   {
                                       Name = "LightingChannels"
                                   };
            lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Static"));
            lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Dynamic"));
            lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "CompositeDynamic"));
            props.AddOrReplaceProp(lightingChannels);

            exp.WriteProperties(props);
        }

        private static void PruneUnusedProperties(ExportEntry exp)
        {
            // Lots of components are not used or don't exist and can't be imported in LE1
            // Get rid of them here
            PropertyCollection props = exp.GetProperties();

            // Might be better to enumerate all object properties and trim out ones that reference
            // known non-existent things
            if (exp.IsA("LightComponent"))
            {
                props.RemoveNamedProperty("PreviewInnerCone");
                props.RemoveNamedProperty("PreviewOuterCone");
                props.RemoveNamedProperty("PreviewLightRadius");
            }

            if (exp.IsA("NavigationPoint"))
            {
                props.RemoveNamedProperty("GoodSprite");
                props.RemoveNamedProperty("BadSprite");
            }

            if (exp.IsA("BioArtPlaceable"))
            {
                props.RemoveNamedProperty("CoverMesh"); // Property exists but is never set
            }

            if (exp.IsA("SoundNodeAttenuation"))
            {
                props.RemoveNamedProperty("LPFMinRadius");
                props.RemoveNamedProperty("LPFMaxRadius");
            }

            if (exp.IsA("BioAPCoverMeshComponent"))
            {
                exp.Archetype = null; // Remove the archetype. This is on BioDoor's and does nothing in practice, in ME1 there is nothing to copy from the archetype
            }

            if (exp.IsA("BioSquadCombat"))
            {
                props.RemoveNamedProperty("m_oSprite");
            }

            if (exp.IsA("CameraActor"))
            {
                props.RemoveNamedProperty("MeshComp"); // some actors have a camera mesh that was probably used to better visualize in-editor
            }

            exp.WriteProperties(props);
        }

        private static void PreCorrectBioWorldInfoStreamingLevels(ExportEntry exp)
        {
            // Older games (ME1 at least) can reference levels that don't exist. This didn't breka game. Later games this does break
            // has a bunch of level references that don't exist

            //if (triggerStream.ObjectName.Instanced == "BioTriggerStream_0")
            //    Debugger.Break();
            var streamingLevels = exp.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels");
            if (streamingLevels != null)
            {
                for (int i = streamingLevels.Count - 1; i >= 0; i--)
                {
                    var lsk = streamingLevels[i].ResolveToEntry(exp.FileRef) as ExportEntry;
                    var packageName = lsk.GetProperty<NameProperty>("PackageName");
                    if (VTest_NonExistentBTSFiles.Contains(packageName.Value.Instanced.ToLower()))
                    {
                        // Do not port this
                        Debug.WriteLine($@"Removed non-existent LSK package: {packageName.Value.Instanced} in {Path.GetFileNameWithoutExtension(exp.FileRef.FilePath)}");
                        streamingLevels.RemoveAt(i);
                    }
                    else
                    {
                        Debug.WriteLine($@"LSK package exists: {packageName.Value.Instanced} in {Path.GetFileNameWithoutExtension(exp.FileRef.FilePath)}");
                    }
                }

                exp.WriteProperty(streamingLevels);
            }
        }

        private static void PostCorrectMaterialsToInstanceConstants(IMEPackage me1Package, IMEPackage le1Package, VTestOptions vTestOptions)
        {
            // Oh lordy this is gonna suck

            // Donor materials need tweaks to behave like the originals
            // So we make a new MaterialInstanceConstant, copy in the relevant(?) values,
            // and then repoint all incoming references to the Material to use this MaterialInstanceConstant instead.
            // This is going to be slow and ugly code
            // Technically this could be done in the relinker but I don't want to stuff
            // something this ugly in there
            foreach (var le1Material in le1Package.Exports.Where(x => vtest_DonorMaterials.Contains(x.InstancedFullPath)).ToList())
            {
                Debug.WriteLine($"Correcting material inputs for donor material: {le1Material.InstancedFullPath}");
                var donorinputs = new List<string>();
                var expressions = le1Material.GetProperty<ArrayProperty<ObjectProperty>>("Expressions");
                foreach (var express in expressions.Select(x => x.ResolveToEntry(le1Package) as ExportEntry))
                {
                    if (express.ClassName == "MaterialExpressionVectorParameter")
                    {
                        donorinputs.Add(express.GetProperty<NameProperty>("ParameterName").Value.Name);
                    }
                }

                Debug.WriteLine(@"Donor has the following inputs:");
                foreach (var di in donorinputs)
                {
                    Debug.WriteLine(di);
                }

                var me1Material = me1Package.FindExport(le1Material.InstancedFullPath);

                var sourceMatInst = vTestOptions.vTestHelperPackage.Exports.First(x => x.ClassName == "MaterialInstanceConstant"); // cause it can change names here
                sourceMatInst.ObjectName = $"{le1Material.ObjectName}_MatInst";
                RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
                {
                    Cache = vTestOptions.cache,
                    ImportExportDependencies = true,
                    IsCrossGame = true,
                    TargetGameDonorDB = vTestOptions.objectDB
                };
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceMatInst, le1Package, le1Material.Parent, true, rop, out var le1MatInstEntry);

                var le1MatInst = le1MatInstEntry as ExportEntry;
                var le1MatInstProps = le1MatInst.GetProperties();

                le1MatInstProps.AddOrReplaceProp(new ObjectProperty(le1Material, "Parent")); // Update the parent

                // VECTOR EXPRESSIONS
                var vectorExpressions = new ArrayProperty<StructProperty>("VectorParameterValues");
                foreach (var v in me1Material.GetProperty<ArrayProperty<ObjectProperty>>("Expressions").Select(x => x.ResolveToEntry(me1Package) as ExportEntry))
                {
                    if (v.ClassName == "MaterialExpressionVectorParameter")
                    {
                        var exprInput = v.GetProperty<NameProperty>("ParameterName").Value.Name;
                        if (donorinputs.Contains(exprInput))
                        {
                            var vpv = v.GetProperty<StructProperty>("DefaultValue");
                            PropertyCollection pc = new PropertyCollection();
                            pc.AddOrReplaceProp(MakeLinearColorStruct("ParameterValue", vpv.GetProp<FloatProperty>("R"), vpv.GetProp<FloatProperty>("G"), vpv.GetProp<FloatProperty>("B"), vpv.GetProp<FloatProperty>("A")));
                            pc.AddOrReplaceProp(new FGuid(Guid.Empty).ToStructProperty("ExpressionGUID"));
                            pc.AddOrReplaceProp(new NameProperty(exprInput, "ParameterName"));
                            vectorExpressions.Add(new StructProperty("VectorParameterValue", pc));
                            donorinputs.Remove(exprInput);
                        }
                    }
                    else
                    {
                        //Debugger.Break();
                    }
                }

                if (vectorExpressions.Any())
                {
                    le1MatInstProps.AddOrReplaceProp(vectorExpressions);
                }

                // SCALAR EXPRESSIONS
                var me1MatInfo = ObjectBinary.From<Material>(me1Material);
                var scalarExpressions = new ArrayProperty<StructProperty>("ScalarParameterValues");
                foreach (var v in me1MatInfo.SM3MaterialResource.UniformPixelScalarExpressions)
                {
                    if (v is MaterialUniformExpressionScalarParameter spv)
                    {
                        PropertyCollection pc = new PropertyCollection();
                        pc.AddOrReplaceProp(new FGuid(Guid.Empty).ToStructProperty("ExpressionGUID"));
                        pc.AddOrReplaceProp(new NameProperty(spv.ParameterName, "ParameterName"));
                        pc.AddOrReplaceProp(new FloatProperty(spv.DefaultValue, "ParameterValue"));
                        scalarExpressions.Add(new StructProperty("ScalarParameterValue", pc));
                    }
                }

                if (scalarExpressions.Any())
                {
                    le1MatInstProps.AddOrReplaceProp(scalarExpressions);
                }

                le1MatInst.WriteProperties(le1MatInstProps);

                // Find things that reference this material and repoint them
                var entriesToUpdate = le1Material.GetEntriesThatReferenceThisOne();
                foreach (var entry in entriesToUpdate.Keys)
                {
                    if (entry == le1MatInst)
                        continue;
                    le1MatInst.GetProperties();
                    var relinkDict = new ListenableDictionary<IEntry, IEntry>();
                    relinkDict[le1Material] = le1MatInst; // This is a ridiculous hack

                    rop = new RelinkerOptionsPackage()
                    {
                        CrossPackageMap = relinkDict,
                        Cache = vTestOptions.cache,
                        ImportExportDependencies = false // This is same-package so there's nothing to import.
                    };

                    Relinker.Relink(entry as ExportEntry, entry as ExportEntry, rop);
                    le1MatInst.GetProperties();
                }
            }
        }

        private static void PreCorrectBioTriggerStream(ExportEntry triggerStream)
        {
            // Older games (ME1 at least) can reference levels that don't exist. This didn't break game. Later games this does break. Maybe. IDK.

            //if (triggerStream.ObjectName.Instanced == "BioTriggerStream_0")
            //    Debugger.Break();
            // triggerStream.RemoveProperty("m_oAreaMapOverride"); // Remove this when stuff is NOT borked up
            //
            // return;
            var streamingStates = triggerStream.GetProperty<ArrayProperty<StructProperty>>("StreamingStates");
            if (streamingStates != null)
            {
                foreach (var ss in streamingStates)
                {
                    var inChunkName = ss.GetProp<NameProperty>("InChunkName").Value.Name.ToLower();

                    if (inChunkName != "none" && VTest_NonExistentBTSFiles.Contains(inChunkName))
                        Debugger.Break(); // Hmm....

                    var visibleChunks = ss.GetProp<ArrayProperty<NameProperty>>("VisibleChunkNames");
                    for (int i = visibleChunks.Count - 1; i >= 0; i--)
                    {
                        if (VTest_NonExistentBTSFiles.Contains(visibleChunks[i].Value.Name.ToLower()))
                        {
                            Debug.WriteLine($"PreCorrect: VS Remove BTS level {visibleChunks[i].Value}");
                            //visibleChunks.RemoveAt(i);
                        }
                    }

                    var loadChunks = ss.GetProp<ArrayProperty<NameProperty>>("LoadChunkNames");
                    for (int i = loadChunks.Count - 1; i >= 0; i--)
                    {
                        if (VTest_NonExistentBTSFiles.Contains(loadChunks[i].Value.Name.ToLower()))
                        {
                            Debug.WriteLine($"PreCorrect: LC Remove BTS level {loadChunks[i].Value}");
                            //loadChunks.RemoveAt(i);
                        }
                    }
                }

                triggerStream.WriteProperty(streamingStates);
            }
            else
            {
                //yDebug.WriteLine($"{triggerStream.InstancedFullPath} in {triggerStream} has NO StreamingStates!!");
            }
        }

        private static void CorrectTerrainMaterialsAndSlopes(ExportEntry mTerrain, ExportEntry lTerrain, bool prc2aa, VTestOptions vTestOptions)
        {
            var le1File = lTerrain.FileRef;
            var me1File = mTerrain.FileRef;

            var mLayers = mTerrain.GetProperty<ArrayProperty<StructProperty>>("Layers");
            var lLayers = lTerrain.GetProperty<ArrayProperty<StructProperty>>("Layers");


            foreach (var lLayer in lLayers)
            {
                // Find matching mLayer
                var lSetup = lLayer.GetProp<ObjectProperty>("Setup").ResolveToEntry(le1File) as ExportEntry;
                ExportEntry mSetup = null;
                foreach (var mSetupStruct in mLayers)
                {
                    var mSetupTmp = mSetupStruct.GetProp<ObjectProperty>("Setup").ResolveToEntry(me1File) as ExportEntry;
                    if (mSetupTmp != null && mSetupTmp.InstancedFullPath == lSetup.InstancedFullPath)
                    {
                        mSetup = mSetupTmp;
                        break;
                    }
                }

                if (mSetup == null)
                    continue; // Don't update this

                var mMaterials = mSetup.GetProperty<ArrayProperty<StructProperty>>("Materials");
                var lMaterials = lSetup.GetProperty<ArrayProperty<StructProperty>>("Materials");

                if (prc2aa && mMaterials.Count == lMaterials.Count)
                {
                    // ONLY PRC2AA WILL RUN THIS CODE
                    for (int i = 0; i < mMaterials.Count; i++)
                    {
                        var mMat = mMaterials[i];
                        var lMat = lMaterials[i];

                        foreach (var prop in mMat.Properties)
                        {
                            if (prop is ObjectProperty)
                                continue; // Do not change

                            lMat.Properties.AddOrReplaceProp(prop);
                        }
                    }
                }

                // CORRECTIONS
                var setupName = mSetup.ObjectName.Name;
                switch (setupName)
                {
                    case "UNC20_TLSetup_lessDispl": // PRC2AA_00_LAY
                        {
                            // Memory Unique
                            var cgv = le1File.FindExport("CROSSGENV");
                            if (cgv == null)
                            {
                                cgv = ExportCreator.CreatePackageExport(lTerrain.FileRef, "CROSSGENV", null);
                                cgv.indexValue = 0;
                            }

                            lSetup.Parent = cgv;
                            lSetup.ObjectName = "CROSSGENV_PRC2AA_TerrainLayerSetup";

                            // Slope Changes
                            lMaterials[0].GetProp<StructProperty>("MinSlope").GetProp<FloatProperty>("Base").Value = 2; // 90 degrees never
                            lMaterials[1].GetProp<StructProperty>("MinSlope").GetProp<FloatProperty>("Base").Value = 0; // 0 degrees always
                        }

                        break;
                    case "lav60_RIVER01_terrain_setup": // PRC2_CCLAVA
                        {
                            // Memory Unique
                            var cgv = le1File.FindExport("CROSSGENV");
                            if (cgv == null)
                            {
                                cgv = ExportCreator.CreatePackageExport(lTerrain.FileRef, "CROSSGENV", null);
                                cgv.indexValue = 0;
                            }

                            lSetup.Parent = cgv;
                            lSetup.ObjectName = "CROSSGENV_PRC2_CCLAVA_TerrainLayerSetup";

                            // No idea why this fixes it tbh but it does
                            lMaterials[0].GetProp<FloatProperty>("Alpha").Value = 0;
                            lMaterials[1].GetProp<FloatProperty>("Alpha").Value = 0;
                        }
                        break;
                }


                lSetup.WriteProperty(lMaterials);

            }
        }

        private static string[] cclavaTextureStreamingMaterials = new[]
        {
            // Terrain materials
            "BIOA_LAV60_T.LAV60_GrassF02",
            "BIOA_LAV60_T.LAV60_Rock02",
            "BIOA_LAV60_T.lav60_riveredge01",
            "BIOA_LAV60_T.LAV60_Road",
            "BIOA_LAV60_T.lav60_rockcover_new_road"
        };

        private static void PostPortingCorrections(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // Corrections to run AFTER porting is done
            var levelName = Path.GetFileNameWithoutExtension(le1File.FilePath);

            vTestOptions.packageEditorWindow.BusyText = $"PPC (CoverSlots) on\n{levelName}";
            ReinstateCoverSlots(me1File, le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (FireLinks) on\n{levelName}";
            GenerateFireLinkItemsForFile(me1File, le1File, vTestOptions); // This must run after cover slots are reinstated. This might need to run after all files are generated if there are cross levels
            vTestOptions.packageEditorWindow.BusyText = $"PPC (Textures) on\n{levelName}";
            CorrectTextures(le1File);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (PrefabSequences) on\n{levelName}";
            CorrectPrefabSequenceClass(le1File);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (Sequences) on\n{levelName}";
            CorrectSequences(le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (Pathfinding) on\n{levelName}";
            CorrectPathfindingNetwork(me1File, le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (MaterialInstanceConstants) on\n{levelName}";
            PostCorrectMaterialsToInstanceConstants(me1File, le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (VFX) on\n{levelName}";
            CorrectVFX(me1File, le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (Pink Visor) on\n{levelName}";
            FixPinkVisorMaterial(le1File);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (Unlock Ahern Mission) on\n{levelName}";
            DebugUnlockAhernMission(le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (2DAs) on\n{levelName}";
            CorrectGethEquipment2DAs(le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (Audio Lengths) on\n{levelName}";
            FixAudioLengths(le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (Planters) on\n{levelName}";
            FixPlanters(le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (Lighting) on\n{levelName}";
            FixLighting(le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (Ahern Conversation) on\n{levelName}";
            FixAhernConversation(le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (Optimization) on\n{levelName}";
            PortME1OptimizationAssets(me1File, le1File, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"PPC (LEVEL SPECIFIC) on\n{levelName}";


            var fName = Path.GetFileNameWithoutExtension(le1File.FilePath);
            // Port in the collision-corrected terrain
            if (fName.CaseInsensitiveEquals("BIOA_PRC2_CCLava"))
            {
                PortInCorrectedTerrain(me1File, le1File, "CCLava.Terrain_1", "BIOA_LAV60_00_LAY.pcc", vTestOptions);
                CorrectTerrainSetup(me1File, le1File, vTestOptions);

                // Correct the DirectionalMaxComponent scalar
                var terrainComponents = le1File.Exports.Where(x => x.ClassName == "TerrainComponent");
                foreach (var tc in terrainComponents)
                {
                    var tcBin = ObjectBinary.From<TerrainComponent>(tc);
                    var lm = tcBin.LightMap as LightMap_2D;
                    lm.ScaleVector2.X *= vTestOptions.LavaLightmapScalar;
                    lm.ScaleVector2.Y *= vTestOptions.LavaLightmapScalar;
                    lm.ScaleVector2.Z *= vTestOptions.LavaLightmapScalar;
                    tc.WriteBinary(tcBin);
                }

                CreateSignaledTextureStreaming(le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence"), cclavaTextureStreamingMaterials, vTestOptions);
            }
            else if (fName.CaseInsensitiveEquals("BIOA_PRC2AA_00_LAY"))
            {
                PortInCorrectedTerrain(me1File, le1File, "PRC2AA.Terrain_1", "BIOA_UNC20_00_LAY.pcc", vTestOptions);
                CorrectTerrainSetup(me1File, le1File, vTestOptions);
            }
            else if (fName.CaseInsensitiveEquals("BIOA_PRC2_CCAHERN"))
            {
                PortInCorrectedTerrain(me1File, le1File, "CCAHERN.Terrain_1", "BIOA_LAV60_00_LAY.pcc", vTestOptions);
                CorrectTerrainSetup(me1File, le1File, vTestOptions);
            }
            else if (fName.CaseInsensitiveEquals("BIOA_PRC2_CCSIM05_DSG"))
            {
                // Port in the custom sequence used for switching UIs
                InstallVTestHelperSequenceViaOut(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Central_Scoreboard_Matinee.SeqEvent_RemoteEvent_0", "ScoreboardSequence.UISwitcherLogic", false, vTestOptions, out _);

                // Port in the keybinding sequences
                InstallVTestHelperSequenceViaOut(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Central_Scoreboard_Matinee.SeqEvent_RemoteEvent_0", "ScoreboardSequence.KeybindsInstaller", true, vTestOptions, out var gate);
                InstallVTestHelperSequenceViaOut(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Central_Scoreboard_Matinee.SeqAct_Gate_3", "ScoreboardSequence.KeybindsUninstaller", false, vTestOptions, out _);

                var scoreboardSeq = le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Play_Central_Scoreboard_Matinee");
                var existingScoreboard = FindSequenceObjectByClassAndPosition(scoreboardSeq, "BioSeqAct_BioToggleCinematicMode", 3552, 808);
                KismetHelper.CreateOutputLink(existingScoreboard, "Out", gate, 1); // Open the gate again.

            }
            else if (fName.CaseInsensitiveEquals("BIOA_PRC2_CCSCOREBOARD_DSG"))
            {
                // Porting in ANY of these crashes the game. Why??

                // Port in the UI switching and keybinding for PC
                // Port in the custom sequence used for switching UIs. Should only run if not skipping the scoreboard
                InstallVTestHelperSequenceViaOut(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Post_Scenario_Scoreboard_Matinee.UIAction_PlaySound_0", "ScoreboardSequence.UISwitcherLogic", false, vTestOptions, out _);

                // Port in the keybinding sequences
                InstallVTestHelperSequenceViaOut(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Post_Scenario_Scoreboard_Matinee.BioSeqAct_MiniGame_1", "ScoreboardSequence.KeybindsInstaller", true, vTestOptions, out var gate);
                InstallVTestHelperSequenceViaOut(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Post_Scenario_Scoreboard_Matinee.UIAction_PlaySound_1", "ScoreboardSequence.KeybindsUninstaller", false, vTestOptions, out _);
            }
            else if (fName.CaseInsensitiveEquals("BIOA_PRC2"))
            {
                #region PRC1 BioSoundNodeWaveStreamingData
                // This is hack to port things over in ModdedSource. The streaming data was refenced by an object that doesn't acttually
                // use this (game will die if it tries). We remove this reference and setup our own.
                // This is a total hack but it works for less code.

                le1File.FindExport("TheWorld.PersistentLevel.AmbientSound_20").RemoveProperty("Base");
                AddWorldReferencedObjects(le1File, le1File.FindExport("DVDStreamingAudioData.PC.snd_prc1_music")); // This must stay in memory for the music 2DA to work for PRC1 audio
                #endregion

                #region Geth Pulse Gun Crust VFX Fix
                // This is a new material that adds HoloWipe VFX to the geth pulse gun, that is only referenced from our 2DA
                // Ordinarily this would be in a startup file (such as BIOC_Materials for basegame) but that doesn't seem to work so we have it in this file instead

                // Clone BIOG_WPN_ tree
                var gethPulseVfxRoot = vTestOptions.vTestHelperPackage.FindExport("BIOG_WPN_ALL_MASTER_L");
                var gethPulseVfxIFP = "BIOG_WPN_ALL_MASTER_L.Appearance.Geth.VTEST_WPN_GTH_Appr";
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, gethPulseVfxRoot, le1File, null,
                    true, new RelinkerOptionsPackage(), out var _);

                // Effectively clone/relink all references for VTEST_WPN_GTH_Appr
                var gethPulseVfx = le1File.FindExport(gethPulseVfxIFP);
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, vTestOptions.vTestHelperPackage.FindExport(gethPulseVfxIFP),
                    le1File, gethPulseVfx, true, new RelinkerOptionsPackage(), out var _);
                AddWorldReferencedObjects(le1File, gethPulseVfx);
                #endregion

                #region Full Blocking Load Fix
                // Adjust the triggerstreams to pre-stream in some files to prevent a full blocking load from occurring.
                // They all have the same state name

                string[] levelsToAdd = new[]
                {
                    "BIOA_PRC2_CCMain_Conv", // This will trigger blocking load as it goes directly to visible on the next change
                    "BIOA_PRC2_CCMain_SND", // This will trigger blocking load as it goes directly to visible on the next change

                    // These ones are here just to pre-load things into memory in the event the player just mashes their way through
                    "BIOA_PRC2_CCSim",
                    "BIOA_PRC2_CCSim_ART",
                    "BIOA_PRC2_CCSim_DSG",
                };

                foreach (var export in le1File.Exports.Where(x => x.ClassName == "BioTriggerStream"))
                {
                    var ss = export.GetProperty<ArrayProperty<StructProperty>>("StreamingStates");

                    if (ss != null && ss.Count == 2)
                    {
                        // State idx 1 is the one we want.
                        var state = ss[1];
                        if (state.GetProp<NameProperty>("StateName")?.Value.Name == "Load_Post_Scenario_Scoreboard")
                        {
                            Debug.WriteLine($"Updating streaming state for more preload: BIOA_PRC2 {export.ObjectName.Instanced}");
                            var loadChunkNames = state.GetProp<ArrayProperty<NameProperty>>("LoadChunkNames");
                            foreach (var lta in levelsToAdd)
                            {
                                loadChunkNames.Add(new NameProperty(lta));
                            }

                            export.WriteProperty(ss);
                        }
                    }
                }

                #endregion

                #region Level Load Blocking Texture Streaming
                InstallVTestHelperSequenceNoInput(le1File, "TheWorld.PersistentLevel.Main_Sequence", "HelperSequences.LevelLoadTextureStreaming", vTestOptions);
                // The original logic is removed in the ModdedSource file
                #endregion
            }
            else if (fName.CaseInsensitiveEquals("BIOA_PRC2AA"))
            {
                // Improved loader
                InstallVTestHelperSequenceNoInput(le1File, "TheWorld.PersistentLevel.Main_Sequence", "HelperSequences.LevelLoadTextureStreaming", vTestOptions);
            }

            // Not an else statement as this is level generic
            if (fName.StartsWith("BIOA_PRC2AA"))
            {
                // Lights are way overblown for this map. This value is pretty close to the original game
                foreach (var pl in le1File.Exports.Where(x => x.IsA("LightComponent")))
                {
                    var brightness = pl.GetProperty<FloatProperty>("Brightness")?.Value ?? 1;
                    pl.WriteProperty(new FloatProperty(brightness * .1f, "Brightness"));
                }
            }

            if (fName.StartsWith("BIOA_PRC2") && !fName.StartsWith("BIOA_PRC2AA"))
            {
                // Lights are way overblown for this map. This value is pretty close to the original game
                foreach (var pl in le1File.Exports.Where(x => x.IsA("LightComponent")))
                {
                    if (pl.InstancedFullPath == "TheWorld.PersistentLevel.PointLightToggleable_10.PointLightComponent_1341242")
                        continue; // Pointlight that fixes ahern conversation. Do not change this

                    var brightness = pl.GetProperty<FloatProperty>("Brightness")?.Value ?? 1;
                    pl.WriteProperty(new FloatProperty(brightness * .4f, "Brightness"));
                }
            }

            // Kill streak voice line sequence
            switch (fName)
            {
                case "BIOA_PRC2_CCAHERN_DSG":
                case "BIOA_PRC2_CCTHAI_DSG":
                case "BIOA_PRC2_CCLAVA_DSG":
                case "BIOA_PRC2_CCCAVE_DSG":
                case "BIOA_PRC2_CCCRATE_DSG":
                    {
                        InstallVTestHelperSequenceNoInput(le1File, "TheWorld.PersistentLevel.Main_Sequence", "HelperSequences.KillStreakVoiceLine", vTestOptions);
                        break;
                    }
            }

            LevelSpecificPostCorrections(fName, me1File, le1File, vTestOptions);


            var level = le1File.FindExport("TheWorld.PersistentLevel");
            if (level != null)
            {
                RebuildPersistentLevelChildren(level, vTestOptions);
            }
            //CorrectTriggerStreamsMaybe(me1File, le1File);
        }

        private static void FixLighting(IMEPackage le1File, VTestOptions vTestOptions)
        {
            if (vTestOptions.useDynamicLighting)
            {
                var fname = Path.GetFileNameWithoutExtension(le1File.FilePath);
                switch (fname)
                {
                    case "BIOA_PRC2_CCAHERN":
                        // Truck shadow
                        var truckDLE = le1File.FindExport("TheWorld.PersistentLevel.InterpActor_0.DynamicLightEnvironmentComponent_6");
                        truckDLE.WriteProperty(new EnumProperty("LightShadow_ModulateBetter", "ELightShadowMode", MEGame.LE1, "LightShadowMode"));
                        break;
                }
            }
        }

        private static void CorrectTerrainSetup(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // Correct AlphaMaps to match the original
            var mTerrain = me1File.Exports.First(x => x.ClassName == "Terrain");
            var me1Terrain = ObjectBinary.From<Terrain>(mTerrain);
            var lTerrain = le1File.Exports.First(x => x.ClassName == "Terrain");
            var le1Terrain = ObjectBinary.From<Terrain>(lTerrain);

            var properties = lTerrain.GetProperties();

            var fName = Path.GetFileNameWithoutExtension(le1File.FilePath);
            var alphamaps = new List<AlphaMap>();

            if (fName == "BIOA_PRC2_CCLAVA")
            {
                // 5 maps (vs 4 in the source)
                alphamaps.Add(me1Terrain.AlphaMaps[0]); // Default
                alphamaps.Add(me1Terrain.AlphaMaps[1]); // RiverOverride
                alphamaps.Add(new AlphaMap() { Data = new byte[me1Terrain.Heights.Length] }); // Water Rock Override (NOT USED)
                alphamaps.Add(me1Terrain.AlphaMaps[2]); // Rock02 Override (?)
                alphamaps.Add(new AlphaMap() { Data = new byte[me1Terrain.Heights.Length] }); // BLANK (NOT USED)

                CorrectTerrainMaterialsAndSlopes(mTerrain, lTerrain, false, vTestOptions); // Needs changes to avoid patches
            }
            else if (fName == "BIOA_PRC2AA_00_LAY")
            {
                // THERE ARE NO ALPHAMAPS
                CorrectTerrainMaterialsAndSlopes(mTerrain, lTerrain, true, vTestOptions); // Needs changes to avoid patches
            }
            else if (fName == "BIOA_PRC2_CCAHERN")
            {
                // The only layer used is Rock02. The other terrain layer is not used
                // We must correct the AlphaMapIndexes
                alphamaps.Add(me1Terrain.AlphaMaps[0]);
                var layers = properties.GetProp<ArrayProperty<StructProperty>>("Layers");
                for (int i = 0; i < layers.Count; i++)
                {
                    var layer = layers[i];
                    if (i == 3)
                    {
                        // ROCK02
                        layer.Properties.AddOrReplaceProp(new IntProperty(0, "AlphaMapIndex"));
                    }
                    else
                    {
                        layer.Properties.AddOrReplaceProp(new IntProperty(-1, "AlphaMapIndex"));
                    }
                }
            }

            le1Terrain.AlphaMaps = alphamaps.ToArray();
            lTerrain.WritePropertiesAndBinary(properties, le1Terrain);
        }

        private static void CorrectVFX(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // Needs a fadein
            var glitchRandom = le1File.FindExport("BIOA_PRC2_MatFX.VFX.Glitch_Random");
            if (glitchRandom != null)
            {
                var props = glitchRandom.GetProperties();
                props.AddOrReplaceProp(new NameProperty("EMT_HoloWipe", "m_nmEffectsMaterial"));
                props.AddOrReplaceProp(new BoolProperty(true, "m_bIgnorePooling"));
                props.AddOrReplaceProp(new EnumProperty("BIO_VFX_PRIORITY_ALWAYS", "EBioVFXPriority", MEGame.LE1, "ePriority"));
                glitchRandom.WriteProperties(props);
            }

            // Fixed to look fadeout
            var glitchedToDeath = le1File.FindExport("BIOA_PRC2_MatFX.DeathEffects.GlitchedToDeath");
            if (glitchedToDeath != null)
            {
                var props = glitchedToDeath.GetProperties();
                props.AddOrReplaceProp(new NameProperty("EMT_HoloWipe", "m_nmEffectsMaterial"));
                props.AddOrReplaceProp(new BoolProperty(true, "m_bIgnorePooling"));
                props.AddOrReplaceProp(new EnumProperty("BIO_VFX_PRIORITY_ALWAYS", "EBioVFXPriority", MEGame.LE1, "ePriority"));
                glitchedToDeath.WriteProperties(props);
            }

            // Correct missing Geth Holowipe
            // This is kind of a hack. Doing it properly would require renaming tons of objects which breaks the dynamic load system LE1 has

            var le1Rvr = le1File.FindExport(@"EffectsMaterials.Users.GTH_TNT_MASTER_MAT_USER.RvrMaterialMultiplexor_16");
            if (le1Rvr != null)
            {
                Debug.WriteLine("Correct Geth Holo VFX");
                var replacement = vTestOptions.vTestHelperPackage.FindExport(@"EffectsMaterials.Users.GTH_TNT_MASTER_MAT_USER.RvrMaterialMultiplexor_16");
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, replacement, le1File, le1Rvr, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out _);
            }

            //var nameIdx = le1File.findName("GTH_TNT_MASTER_MAT_USER");
            //if (nameIdx != -1)
            //{
            //    le1File.replaceName(nameIdx, "GTH_TNT_MASTER_MAT_USER_CROSSGEN");
            //}

            // DISABLED BECAUSE THIS IS WAY TOO COMPLICATED
            // There will just have to be non-unique memory items here as renaming the whole parent chain
            // would be a huge PITA

            // These ones are special as they are looked up by building a name.
            // We must also adjust the StrProperty that looks these up!
            //nameIdx = le1File.findName("BIOG_GTH_TRO_NKD_R");
            //if (nameIdx != -1)
            //{
            //    le1File.replaceName(nameIdx, "BIOG_GTH_TRO_CROSSGEN_NKD_R");
            //}

            //nameIdx = le1File.findName("BIOG_GTH_STP_NKD_R");
            //if (nameIdx != -1)
            //{
            //    le1File.replaceName(nameIdx, "BIOG_GTH_STP_CROSSGEN_NKD_R");
            //}



        }

        private static void FixAudioLengths(IMEPackage le1File, VTestOptions vTestOptions)
        {
            var fname = Path.GetFileNameWithoutExtension(le1File.FilePath);
            foreach (var exp in le1File.Exports.Where(x => x.ClassName == "BioSeqEvt_ConvNode").ToList())
            {
                switch (le1File.Localization)
                {
                    case MELocalization.None: // English also is None it seems (for things like Ocaren)
                    case MELocalization.INT:
                        FixINTAudioLengths(exp, vTestOptions);
                        break;
                }
            }
        }


        private static void FixINTAudioLengths(ExportEntry export, VTestOptions vTestOptions)
        {
            var ifp = export.InstancedFullPath;
            switch (ifp)
            {
                case "prc2_ahern_N.Node_Data_Sequence.BioSeqEvt_ConvNode_7":
                    SetGenderSpecificLength(export, 0.5f, 0.5f, vTestOptions); // "I lost a lot of good friends in the first contact war..."  AFFECTS BOTH
                    break;
                case "prc2_ahern_N.Node_Data_Sequence.BioSeqEvt_ConvNode_111":
                    SetGenderSpecificLength(export, 0, 1, vTestOptions); // The scores are tallied, and the winners appear... . Maleshep cuts off at 'Know'
                    break;
                case "prc2_ahern_N.Node_Data_Sequence.BioSeqEvt_ConvNode_10":
                    SetGenderSpecificLength(export, 0.5f, 0.5f, vTestOptions); // I never thought I'd see the day. Good work, Shepard. Really good work
                    break;
                case "prc2_ahern_N.Node_Data_Sequence.BioSeqEvt_ConvNode_217":
                    SetGenderSpecificLength(export, 0.5f, 0f, vTestOptions); // I got a brochure from ExoGeni and they dropped a prefab down on Intai'sae for me, here in teh Argus Rho cluster.
                    break;

                case "prc2_ochren_N.Node_Data_Sequence.BioSeqEvt_ConvNode_67":
                    SetGenderSpecificLength(export, 0, 1.1f, vTestOptions); // So you must be the famous commander shepard
                    break;
                case "prc2_ochren_N.Node_Data_Sequence.BioSeqEvt_ConvNode_68":
                    SetGenderSpecificLength(export, 0, 0.8f, vTestOptions); // "Do you need something? I'm sure I have a few minutes before someone forgets their password"
                    break;
                case "prc2_ochren_N.Node_Data_Sequence.BioSeqEvt_ConvNode_12":
                    // This line is not cut off, but it ends exactly as ocaren stops talking. This doesn't mesh well with the sarcasm and the following sigh
                    // so we add a bit of a pause for dramatic effect on this excellent delivery he has for this line
                    SetGenderSpecificLength(export, 0, 0.8f, vTestOptions); // "No, our agents train in a simulator killing real, actual people"
                    break;
                case "prc2_ochren_N.Node_Data_Sequence.BioSeqEvt_ConvNode_142":
                    SetGenderSpecificLength(export, 0, 0.8f, vTestOptions); //"Really? Thank me? Well, I guess I'll redouble my efforts."
                    break;
                case "prc2_ochren_N.Node_Data_Sequence.BioSeqEvt_ConvNode_195":
                    SetGenderSpecificLength(export, 0, 0.5f, vTestOptions); // What's my objective in capture mode?
                    break;
                case "prc2_ochren_N.Node_Data_Sequence.BioSeqEvt_ConvNode_125":
                    SetGenderSpecificLength(export, 0, 0.6f, vTestOptions); // (2nd) You have your choices between volcanic and tropical courses
                    break;
                case "prc2_ochren_N.Node_Data_Sequence.BioSeqEvt_ConvNode_192":
                    SetGenderSpecificLength(export, 0, 0.7f, vTestOptions); // [NEW] Boot up the Subterranean level (Capture mode)
                    break;
                case "prc2_ochren_N.Node_Data_Sequence.BioSeqEvt_ConvNode_206":
                    SetGenderSpecificLength(export, 0, 1.5f, vTestOptions); // I don't say this very often... but good luck (only when doing ahern's mission the first time) | THIS LINE IS WAY CUT OFFin 
                    break;
                case "prc2_ochren_N.Node_Data_Sequence.BioSeqEvt_ConvNode_35":
                    SetGenderSpecificLength(export, 0.5f, 0f, vTestOptions); // Survival FIRST TIME [NEW] I'll go with Survival Mode
                    break;

                case "prc2_jealous_jerk_N.Node_Data_Sequence.BioSeqEvt_ConvNode_36":
                    SetGenderSpecificLength(export, 0.5f, 0.5f, vTestOptions); // I look forward to the challenge (vidinos, first win)
                    break;
                case "prc2_jealous_jerk_N.Node_Data_Sequence.BioSeqEvt_ConvNode_32":
                    SetGenderSpecificLength(export, 2f, 0.0f, vTestOptions); // What? Oh yes... the weapon... Let no one say vidinos is not a man of his word | This line is REALLY bad with the startup time. It's like 2 whole seconds
                    break;
                case "prc2_jealous_jerk_N.Node_Data_Sequence.BioSeqEvt_ConvNode_30":
                    SetGenderSpecificLength(export, 0.8f, 0.0f, vTestOptions); // Skill at cheating the system, maybe. I'll get to the bottom of this soon enough
                    break;
                case "prc2_jealous_jerk_N.Node_Data_Sequence.BioSeqEvt_ConvNode_31":
                    SetGenderSpecificLength(export, 0.8f, 0.0f, vTestOptions); // I'll find out how you rigged the simulator. Your "record" won't stand. Heads will roll
                    break;
                case "prc2_jealous_jerk_N.Node_Data_Sequence.BioSeqEvt_ConvNode_12":
                    SetGenderSpecificLength(export, -0.8f, 0.0f, vTestOptions); // That wasn't luck. It was skill. | Femshep's dialogue is too long here
                    break;




            }
        }


        /// <summary>
        /// This code is a hack to work around some crazy issue in LE1 where the conversation length for each gender is different.
        /// This occurs in OT but to a lesser degree but whatever the issue is, it's somewhere we can't see in native.
        /// This code installs a gender check and directs flow to a different interp for each, which has the length adjusted
        /// by an offset.
        /// </summary>
        /// <param name="export">The conversation start event node that connects to the interp</param>
        /// <param name="femaleOffset">The interplength duration delta for females.</param>
        /// <param name="femaleOffset">The interplength duration delta for males.</param>
        private static void SetGenderSpecificLength(ExportEntry export, float femaleOffset, float maleOffset, VTestOptions vTestOptions)
        {
            // Split the nodes
            var sequence = SeqTools.GetParentSequence(export);
            var outbound = SeqTools.GetOutboundLinksOfNode(export);
            var interpFemale = outbound[0][0].LinkedOp as ExportEntry;
            var interpDataFemale = SeqTools.GetVariableLinksOfNode(interpFemale)[0].LinkedNodes[0] as ExportEntry;

            var interpMale = EntryCloner.CloneTree(interpFemale);
            var interpDataMale = EntryCloner.CloneTree(interpDataFemale);

            var pmCheckFemale = SequenceObjectCreator.CreateSequenceObject(export.FileRef, "BioSeqAct_PMCheckConditional", vTestOptions.cache);
            pmCheckFemale.WriteProperty(new IntProperty(144, "m_nIndex")); // PlayerIsFemale
            pmCheckFemale.RemoveProperty("VariableLinks"); // These are not necessary.

            KismetHelper.AddObjectsToSequence(sequence, false, interpMale, interpDataMale, pmCheckFemale);

            // Link everything up.
            outbound[0][0].LinkedOp = pmCheckFemale;
            SeqTools.WriteOutboundLinksToNode(export, outbound); // Start Event to Conditional check
            KismetHelper.CreateOutputLink(pmCheckFemale, "True", interpFemale); // Player Female -> Female Interp
            KismetHelper.CreateOutputLink(pmCheckFemale, "False", interpMale); // Player Male -> Male Interp
            KismetHelper.RemoveVariableLinks(interpMale); // Remove existing interp data
            KismetHelper.CreateVariableLink(interpMale, "Data", interpDataMale); // Hook up the male interp data instead


            // Update the lengths by the specified amounts.
            var femaleLen = interpDataMale.GetProperty<FloatProperty>("InterpLength");
            femaleLen.Value += femaleOffset;
            interpDataFemale.WriteProperty(femaleLen);

            var maleLen = interpDataMale.GetProperty<FloatProperty>("InterpLength");
            maleLen.Value += maleOffset;
            interpDataMale.WriteProperty(maleLen);
        }

        /// <summary>
        /// Installs a sequence from VTestHelper. The sequence will be connected via the In pin.
        /// </summary>
        /// <param name="le1File"></param>
        /// <param name="sourceSequenceOpIFP"></param>
        /// <param name="vTestSequenceIFP"></param>
        /// <param name="vTestOptions"></param>
        private static void InstallVTestHelperSequenceViaOut(IMEPackage le1File, string sourceSequenceOpIFP, string vTestSequenceIFP, bool runOnceOnly, VTestOptions vTestOptions, out ExportEntry gate)
        {
            gate = null;
            var sourceItemToOutFrom = le1File.FindExport(sourceSequenceOpIFP);
            var parentSequence = SeqTools.GetParentSequence(sourceItemToOutFrom, true);
            var donorSequence = vTestOptions.vTestHelperPackage.FindExport(vTestSequenceIFP);
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, donorSequence, le1File, parentSequence, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var newUiSeq);
            KismetHelper.AddObjectToSequence(newUiSeq as ExportEntry, parentSequence);

            if (runOnceOnly)
            {
                gate = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_Gate", vTestOptions.cache);
                KismetHelper.AddObjectToSequence(gate, parentSequence);
                // link it up
                KismetHelper.CreateOutputLink(sourceItemToOutFrom, "Out", gate);
                KismetHelper.CreateOutputLink(gate, "Out", gate, 2); // close self
                KismetHelper.CreateOutputLink(gate, "Out", newUiSeq as ExportEntry);
            }
            else
            {
                // link it up
                KismetHelper.CreateOutputLink(sourceItemToOutFrom, "Out", newUiSeq as ExportEntry);
            }
        }

        /// <summary>
        /// Installs a sequence from VTestHelper. The sequence should already contain it's own triggers like LevelLoaded.
        /// </summary>
        /// <param name="le1File"></param>
        /// <param name="eventIFP"></param>
        /// <param name="vTestSequenceIFP"></param>
        /// <param name="vTestOptions"></param>
        private static ExportEntry InstallVTestHelperSequenceNoInput(IMEPackage le1File, string sequenceIFP, string vTestSequenceIFP, VTestOptions vTestOptions)
        {
            var sequence = le1File.FindExport(sequenceIFP);
            var donorSequence = vTestOptions.vTestHelperPackage.FindExport(vTestSequenceIFP);
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, donorSequence, le1File, sequence, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var newUiSeq);
            KismetHelper.AddObjectToSequence(newUiSeq as ExportEntry, sequence);
            return newUiSeq as ExportEntry;
        }

        /// <summary>
        /// Installs a sequence from VTestHelper. The sequence should already contain it's own triggers like LevelLoaded.
        /// </summary>
        /// <param name="le1File"></param>
        /// <param name="eventIFP"></param>
        /// <param name="vTestSequenceIFP"></param>
        /// <param name="vTestOptions"></param>
        private static ExportEntry InstallVTestHelperSequenceViaEvent(IMEPackage le1File, string eventIFP, string vTestSequenceIFP, VTestOptions vTestOptions, string outName = "Out")
        {
            var targetEvent = le1File.FindExport(eventIFP);
            var sequence = SeqTools.GetParentSequence(targetEvent);
            var donorSequence = vTestOptions.vTestHelperPackage.FindExport(vTestSequenceIFP);
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, donorSequence, le1File, sequence, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var newUiSeq);
            KismetHelper.AddObjectToSequence(newUiSeq as ExportEntry, sequence);
            KismetHelper.CreateOutputLink(targetEvent, outName, newUiSeq as ExportEntry);
            return newUiSeq as ExportEntry;
        }

        private static void AddWorldReferencedObjects(IMEPackage le1File, params ExportEntry[] entriesToReference)
        {
            var world = le1File.FindExport("TheWorld");
            var worldBin = ObjectBinary.From<World>(world);
            var newItems = worldBin.ExtraReferencedObjects.ToList();
            newItems.AddRange(entriesToReference.Select(x => new UIndex(x.UIndex)));
            worldBin.ExtraReferencedObjects = newItems.ToArray();
            world.WriteBinary(worldBin);
        }

        private static string[] assetsToEnsureReferencedInSim = new[]
        {
            "BIOG_GTH_TRO_NKD_R.NKDa.GTH_TRO_NKDa_MDL", // Geth Trooper
            "BIOG_GTH_STP_NKD_R.NKDa.GTH_STP_NKDa_MDL", // Geth Prime
        };

        private static void LevelSpecificPostCorrections(string fName, IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            var upperFName = fName.ToUpper();

            // Semi-global
            switch (upperFName)
            {
                case "BIOA_PRC2_CCAHERN_DSG":
                    {

                        foreach (var exp in le1File.Exports.Where(x => x.ClassName == "Sequence").ToList())
                        {
                            var seqName = exp.GetProperty<StrProperty>("ObjName")?.Value;
                            if (seqName == "SUR_Ahern_Handler")
                            {
                                // Ahern music start right as message plays
                                {
                                    var delay = FindSequenceObjectByClassAndPosition(exp, "SeqAct_Delay", -7232, -1224);
                                    var remoteEvent = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_ActivateRemoteEvent", vTestOptions.cache);
                                    KismetHelper.AddObjectToSequence(remoteEvent, exp);
                                    KismetHelper.CreateOutputLink(delay, "Finished", remoteEvent);
                                    remoteEvent.WriteProperty(new NameProperty("StartSimMusic", "EventName"));
                                }

                                // Setup intensity increase 1
                                {
                                    var delay = FindSequenceObjectByClassAndPosition(exp, "SeqAct_Gate", -2072, -1152);
                                    var remoteEvent = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_ActivateRemoteEvent", vTestOptions.cache);
                                    KismetHelper.AddObjectToSequence(remoteEvent, exp);
                                    KismetHelper.CreateOutputLink(delay, "Out", remoteEvent);
                                    remoteEvent.WriteProperty(new NameProperty("MusicIntensity2", "EventName"));
                                }

                                // Setup texture loading
                                var streamInLocNode = le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.SUR_Ahern_Handler.SeqVar_Object_33");
                                FixSimMapTextureLoading(FindSequenceObjectByClassAndPosition(exp, "BioSeqAct_Delay", -8501, -1086), vTestOptions, streamInLocNode);

                                // Fix the UNCMineralSurvey to use the LE version instead of the OT version, which doesn't work well

                                var artPlacableUsed = le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.SUR_Ahern_Handler.BioSeqEvt_ArtPlaceableUsed_3");
                                KismetHelper.RemoveOutputLinks(artPlacableUsed);
                                var leMineralSurvey = InstallVTestHelperSequenceViaEvent(le1File, artPlacableUsed.InstancedFullPath, "HelperSequences.REF_UNCMineralSurvey", vTestOptions);
                                KismetHelper.CreateVariableLink(leMineralSurvey, "nMiniGameID", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.SUR_Ahern_Handler.SeqVar_Int_1"));

                                KismetHelper.CreateOutputLink(leMineralSurvey, "Succeeded", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.SUR_Ahern_Handler.BioSeqAct_SetRadarDisplay_0"));
                                KismetHelper.CreateOutputLink(leMineralSurvey, "Succeeded", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.SUR_Ahern_Handler.BioSeqAct_ModifyPropertyArtPlaceable_2"));
                            }
                            else if (seqName == "Spawn_Single_Guy")
                            {
                                RemoveBitExplosionEffect(exp);
                                FixGethFlashlights(exp, vTestOptions); // This is just for consistency
                            }
                            else if (seqName == "OL_Size")
                            {
                                // Fadein is handled by scoreboard DSG
                                var compareBool = FindSequenceObjectByClassAndPosition(exp, "SeqCond_CompareBool", 8064, 3672);
                                SeqTools.SkipSequenceElement(compareBool, "True"); // Skip out to true
                            }
                        }

                        // Fix the AI to actually charge. For some reason they don't, maybe AI changed internally when going to LE1
                        var root = le1File.FindExport("BIOA_PRC2_SIM_C.Mercenary");
                        var chargeAi = EntryImporter.EnsureClassIsInFile(le1File, "BioAI_Charge", new RelinkerOptionsPackage() { Cache = vTestOptions.cache });
                        foreach (var exp in le1File.Exports.Where(x => x.idxLink == root.UIndex))
                        {
                            if (!exp.ObjectName.Name.Contains("Sniper"))
                            {
                                exp.WriteProperty(new ObjectProperty(chargeAi, "AIController"));
                            }
                        }
                        // Make memory unique
                        root.ObjectName = "Mercenary_Ahern_Crossgen";

                        // Do not turn off fade to black on map finish
                    }
                    break;
                case "BIOA_PRC2_CCCAVE_L":
                    ReduceDirectionalLights(le1File, 0.1f);
                    break;
                case "BIOA_PRC2_CCLAVA_L":
                    ReduceDirectionalLights(le1File, 0.15f);
                    break;
                case "BIOA_PRC2_CCCRATE_L":
                    ReduceDirectionalLights(le1File, 0.25f);
                    break;
                case "BIOA_PRC2_CCAHERN_ART":
                    ReduceDirectionalLights(le1File, 0.1f);
                    break;
                case "BIOA_PRC2_CCTHAI_L":
                    ReduceDirectionalLights(le1File, 0.6f);
                    break;
                case "BIOA_PRC2_CCCAVE_DSG":
                case "BIOA_PRC2_CCLAVA_DSG":
                case "BIOA_PRC2_CCCRATE_DSG":
                case "BIOA_PRC2_CCTHAI_DSG":
                    // Might need Aherns
                    {
                        SetupMusicIntensity(le1File, upperFName, vTestOptions);
                        SetupKillStreakVO(le1File, vTestOptions);
                        // Force the pawns that will spawn to have their meshes in memory
                        // They are not referenced directly

                        var assetsToReference = le1File.Exports.Where(x => assetsToEnsureReferencedInSim.Contains(x.InstancedFullPath)).ToArray();
                        AddWorldReferencedObjects(le1File, assetsToReference);

                        foreach (var exp in le1File.Exports.Where(x => x.ClassName == "Sequence").ToList())
                        {
                            var seqName = exp.GetProperty<StrProperty>("ObjName")?.Value;

                            #region Skip broken SeqAct_ActorFactory for bit explosion | Increase Surival Mode Engagement

                            if (seqName == "Spawn_Single_Guy")
                            {
                                RemoveBitExplosionEffect(exp);
                                FixGethFlashlights(exp, vTestOptions);
                                // Increase survival mode engagement by forcing the player to engage with enemies that charge the player.
                                // This prevents them from camping and getting free survival time
                                if (IsContainedWithinSequenceNamed(exp, "SUR_Respawner") && !IsContainedWithinSequenceNamed(exp, "CAH_Respawner")) // Might force on CAH too since it's not that engaging.
                                {
                                    // Sequence objects + add to sequence
                                    var crustAttach = FindSequenceObjectByClassAndPosition(exp, "BioSeqAct_AttachCrustEffect", 5920, 1672);
                                    var currentPawn = FindSequenceObjectByClassAndPosition(exp, "SeqVar_Object", 4536, 2016);

                                    // AI change
                                    var delay = SequenceObjectCreator.CreateSequenceObject(le1File, "BioSeqAct_Delay", vTestOptions.cache);
                                    var delayDuration = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_RandomFloat", vTestOptions.cache);
                                    var aiChoiceRand = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_RandomFloat", vTestOptions.cache);
                                    var aiChoiceComp = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqCond_CompareFloat", vTestOptions.cache);
                                    var aiChoiceAssaultThreshold = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Float", vTestOptions.cache);
                                    var changeAiCharge = SequenceObjectCreator.CreateSequenceObject(le1File, "BioSeqAct_ChangeAI", vTestOptions.cache);
                                    var changeAiAssault = SequenceObjectCreator.CreateSequenceObject(le1File, "BioSeqAct_ChangeAI", vTestOptions.cache);
                                    var chargeAiLog = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_Log", vTestOptions.cache);
                                    var assaultAiLog = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_Log", vTestOptions.cache);
                                    
                                    // Toxic and Phasic
                                    var setWeaponAttributes = SequenceObjectCreator.CreateSequenceObject(le1File, "LEXSeqAct_SetWeaponAttribute", vTestOptions.cache);
                                    var toxicFactor = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Float", vTestOptions.cache);
                                    var phasicFactor = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Float", vTestOptions.cache);
                                    //var addToxicFactor = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Float", vTestOptions.cache);
                                    //var addPhasicFactor = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Float", vTestOptions.cache);
                                    //var respawnCount = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Float", vTestOptions.cache);

                                    KismetHelper.AddObjectsToSequence(exp, false, delay, delayDuration, aiChoiceRand, aiChoiceComp, aiChoiceAssaultThreshold, changeAiCharge, changeAiAssault, assaultAiLog, chargeAiLog, setWeaponAttributes, toxicFactor, phasicFactor);

                                    // Configure sequence object properties
                                    delayDuration.WriteProperty(new FloatProperty(11, "Min"));
                                    delayDuration.WriteProperty(new FloatProperty(20, "Max"));
                                    KismetHelper.SetComment(toxicFactor, "Toxic to counter player regen");
                                    KismetHelper.SetComment(phasicFactor, "Phasic to counter player powers");
                                    toxicFactor.WriteProperty(new FloatProperty(1, "FloatValue"));
                                    phasicFactor.WriteProperty(new FloatProperty(1, "FloatValue"));


                                    // CHARGE AI BRANCH
                                    var chargeAiClass = EntryImporter.EnsureClassIsInFile(le1File, "BioAI_Charge", new RelinkerOptionsPackage() { Cache = vTestOptions.cache });
                                    changeAiCharge.WriteProperty(new ObjectProperty(chargeAiClass, "ControllerClass"));
                                    KismetHelper.SetComment(chargeAiLog, "CROSSGEN: Engaging player with BioAI_Charge");

                                    // ASSAULT AI BRANCH
                                    var assaultAiClass = EntryImporter.EnsureClassIsInFile(le1File, "BioAI_Assault", new RelinkerOptionsPackage() { Cache = vTestOptions.cache });
                                    changeAiAssault.WriteProperty(new ObjectProperty(assaultAiClass, "ControllerClass"));
                                    KismetHelper.SetComment(chargeAiLog, "CROSSGEN: Engaging player with BioAI_Assault");

                                    // ASSAULT CHANCE - 1 in 4 chance
                                    aiChoiceRand.WriteProperty(new FloatProperty(0, "Min"));
                                    aiChoiceRand.WriteProperty(new FloatProperty(4, "Max"));
                                    aiChoiceAssaultThreshold.WriteProperty(new FloatProperty(3f, "FloatValue")); // The generated random number must be above this to change to assault. 

                                    // Connect sequence objects - Delay and branch pick
                                    KismetHelper.CreateOutputLink(crustAttach, "Done", delay);
                                    KismetHelper.CreateVariableLink(delay, "Duration", delayDuration);
                                    KismetHelper.CreateOutputLink(delay, "Finished", aiChoiceComp);
                                    KismetHelper.CreateVariableLink(aiChoiceComp, "A", aiChoiceRand);
                                    KismetHelper.CreateVariableLink(aiChoiceComp, "B", aiChoiceAssaultThreshold);

                                    // Connect sequence objects - CHARGE BRANCH
                                    KismetHelper.CreateOutputLink(aiChoiceComp, "A < B", changeAiCharge);
                                    KismetHelper.CreateOutputLink(changeAiCharge, "Out", chargeAiLog);
                                    KismetHelper.CreateVariableLink(changeAiCharge, "Pawn", currentPawn);
                                    KismetHelper.CreateVariableLink(chargeAiLog, "Object", currentPawn);

                                    // Connect sequence objects - CHARGE BRANCH
                                    KismetHelper.CreateOutputLink(aiChoiceComp, "A >= B", changeAiAssault);
                                    KismetHelper.CreateOutputLink(changeAiAssault, "Out", assaultAiLog);
                                    KismetHelper.CreateVariableLink(changeAiAssault, "Pawn", currentPawn);
                                    KismetHelper.CreateVariableLink(assaultAiLog, "Object", currentPawn);

                                    // Stop timer on any event in this sequence 
                                    var events = SeqTools.GetAllSequenceElements(exp).OfType<ExportEntry>().Where(x => x.IsA("SeqEvent")).ToList();
                                    foreach (var seqEvent in events)
                                    {
                                        KismetHelper.CreateOutputLink(seqEvent, "Out", delay, 1); // Cancel the delay as spawn stopped or changed (or restarted)
                                    }

                                    // Connect sequence object - toxic / phasic (Needs gated to only activate later!)
                                    KismetHelper.CreateOutputLink(crustAttach, "Done", setWeaponAttributes);
                                    KismetHelper.CreateVariableLink(setWeaponAttributes, "Pawn", currentPawn);
                                    KismetHelper.CreateVariableLink(setWeaponAttributes, "Toxic Factor", toxicFactor);
                                    KismetHelper.CreateVariableLink(setWeaponAttributes, "Phasic Factor", phasicFactor);

                                    exp.WriteProperty(new StrProperty("Spawn_Single_Guy_SUR", "ObjName"));
                                }
                            }

                            #endregion

                            #region Fix dual-finishing sequence. Someone thought they were being clever, bet they didn't know it'd cause an annurysm 12 years later

                            else if (seqName == "Hench_Take_Damage")
                            {
                                var sequenceObjects = exp.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects").Select(x => x.ResolveToEntry(le1File) as ExportEntry).ToList();
                                var attachEvents = sequenceObjects.Where(x => x.ClassName == "SeqAct_AttachToEvent").ToList(); // We will route one of these to the other
                                var starting = sequenceObjects.First(x => x.ClassName == "SeqEvent_SequenceActivated");
                                //var ending = sequenceObjects.First(x => x.ClassName == "SeqEvent_FinishSequence");
                                KismetHelper.RemoveOutputLinks(starting); // prevent dual outs
                                KismetHelper.CreateOutputLink(starting, "Out", attachEvents[0]);
                                KismetHelper.RemoveOutputLinks(attachEvents[0]);
                                KismetHelper.CreateOutputLink(attachEvents[0], "Out", attachEvents[1]); // Make it serial
                            }
                            #endregion

                            #region Issue Rally Command at map start to ensure squadmates don't split up, blackscreen off should be fade in not turn off
                            else if (seqName == "TA_V3_Gametype_Handler")
                            {
                                // Time Trial
                                var startObj = FindSequenceObjectByClassAndPosition(exp, "BioSeqAct_SetActionState"/*, 712, 2256*/);
                                var newObj = SequenceObjectCreator.CreateSequenceObject(le1File, "LEXSeqAct_SquadCommand", vTestOptions.cache);
                                KismetHelper.AddObjectToSequence(newObj, exp);
                                KismetHelper.CreateOutputLink(startObj, "Out", newObj); // RALLY
                                FixSimMapTextureLoading(FindSequenceObjectByClassAndPosition(exp, "BioSeqAct_Delay", 72, 1736), vTestOptions);
                            }
                            else if (seqName == "Check_Capping_Completion")
                            {
                                // Survival uses this as game mode?
                                // Capture...?
                                // Both use this same named item because why not
                                var startObj = FindSequenceObjectByClassAndPosition(exp, "BioSeqAct_SetActionState"/*, 584, 2200*/);
                                var newObj = SequenceObjectCreator.CreateSequenceObject(le1File, "LEXSeqAct_SquadCommand", vTestOptions.cache);
                                KismetHelper.AddObjectToSequence(newObj, exp);
                                KismetHelper.CreateOutputLink(startObj, "Out", newObj); // RALLY
                                FixSimMapTextureLoading(FindSequenceObjectByClassAndPosition(exp, "BioSeqAct_Delay"/*, -152, 1768*/), vTestOptions);

                                var surDecayStart = FindSequenceObjectByClassAndPosition(exp, "BioSeqAct_DUISetTextStringRef", 3544, 2472);
                                if (surDecayStart != null)
                                {
                                    // It's survival
                                    // Add signal to decay start
                                    var surDecaySignal = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_ActivateRemoteEvent", vTestOptions.cache);
                                    KismetHelper.AddObjectToSequence(surDecaySignal, exp);
                                    surDecaySignal.WriteProperty(new NameProperty("CROSSGEN_START_SUR_HEALTHGATE_DECAY", "EventName"));
                                    KismetHelper.CreateOutputLink(surDecayStart, "Out", surDecaySignal);
                                }

                            }
                            else if (seqName == "Vampire_Mode_Handler")
                            {
                                // Hunt
                                var startObj = FindSequenceObjectByClassAndPosition(exp, "BioSeqAct_SetActionState" /*, 1040, 2304*/);
                                var newObj = SequenceObjectCreator.CreateSequenceObject(le1File, "LEXSeqAct_SquadCommand", vTestOptions.cache);
                                KismetHelper.AddObjectToSequence(newObj, exp);
                                KismetHelper.CreateOutputLink(startObj, "Out", newObj); // RALLY

                                FixSimMapTextureLoading(FindSequenceObjectByClassAndPosition(exp, "BioSeqAct_Delay", 304, 1952), vTestOptions);
                            }
                            else if (seqName is "Play_Ahern_Quip_For_TA_Intro" or "Play_Ahern_Quip_For_SUR_Intro" or "Play_Ahern_Quip_For_VAM_Intro" or "Play_Ahern_Quip_For_CAH_Intro")
                            {
                                // Install music remote event at the end
                                var setBool = KismetHelper.GetSequenceObjects(exp).OfType<ExportEntry>().First(x => x.ClassName == "SeqAct_SetBool" && x.GetProperty<ArrayProperty<StructProperty>>("OutputLinks")[0].GetProp<ArrayProperty<StructProperty>>("Links").Count == 0);
                                var remoteEvent = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_ActivateRemoteEvent", vTestOptions.cache);
                                KismetHelper.AddObjectToSequence(remoteEvent, exp);
                                KismetHelper.CreateOutputLink(setBool, "Out", remoteEvent);
                                remoteEvent.WriteProperty(new NameProperty("StartSimMusic", "EventName"));
                            }

                            //else if (seqName == "Cap_And_Hold_Point")
                            //{
                            //    // Capture
                            //    var startObj = FindSequenceObjectByPosition(exp, 584, 2200, "BioSeqAct_SetActionState");
                            //    var newObj = SequenceObjectCreator.CreateSequenceObject(le1File, "LEXSeqAct_SquadCommand", MEGame.LE1, vTestOptions.cache);
                            //    KismetHelper.AddObjectToSequence(newObj, exp);
                            //    KismetHelper.CreateOutputLink(startObj, "Out", newObj); // RALLY
                            //}

                            #region Black Screen Fade In instead of just turning off
                            if (seqName is "Vampire_Mode_Handler" or "Check_Capping_Completion" or "TA_V3_Gametype_Handler")
                            {
                                var sequenceObjects = exp.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects").Select(x => x.ResolveToEntry(le1File) as ExportEntry).ToList();
                                var fadeFromBlacks = sequenceObjects.Where(x => x.ClassName == "BioSeqAct_BlackScreen").ToList(); // We will route one of these to the other
                                if (fadeFromBlacks.Count != 1)
                                    Debugger.Break();
                                foreach (var ffb in fadeFromBlacks)
                                {
                                    ffb.WriteProperty(new EnumProperty("BlackScreenAction_FadeFromBlack", "BlackScreenActionSet", MEGame.LE1, "m_eBlackScreenAction"));
                                }
                            }
                            #endregion

                            #endregion

                            #region Black screen on scoreboard
                            else if (seqName == "OL_Size")
                            {
                                // Fadein is handled by scoreboard DSG
                                var compareBool = FindSequenceObjectByClassAndPosition(exp, "SeqCond_CompareBool", 8064, 3672);
                                SeqTools.SkipSequenceElement(compareBool, "True"); // Skip out to true

                                // Add signal to stop decay and restore healthgate
                                if (IsContainedWithinSequenceNamed(exp, "SUR_Lava_Handler", true) ||
                                    IsContainedWithinSequenceNamed(exp, "SUR_Thai_Handler", true) ||
                                    IsContainedWithinSequenceNamed(exp, "SUR_Crate_Handler", true) ||
                                    IsContainedWithinSequenceNamed(exp, "SUR_Cave_Handler", true))
                                {
                                    // It's survival, we should signal to restore the healthgate
                                    var surRestoreHealthgateSignal = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_ActivateRemoteEvent", vTestOptions.cache);
                                    KismetHelper.AddObjectToSequence(surRestoreHealthgateSignal, exp);
                                    surRestoreHealthgateSignal.WriteProperty(new NameProperty("CROSSGEN_RESTORE_SUR_HEALTHGATE", "EventName"));
                                    KismetHelper.CreateOutputLink(FindSequenceObjectByClassAndPosition(exp, "SeqEvent_SequenceActivated"), "Out", surRestoreHealthgateSignal);
                                }

                            }
                            #endregion
                            else if (seqName == "SUR_Lava_Handler" || seqName == "SUR_Thai_Handler" || seqName == "SUR_Crate_Handler" || seqName == "SUR_Cave_Handler")
                            {
                                InstallVTestHelperSequenceNoInput(le1File, exp.InstancedFullPath, "HelperSequences.SurvivalHealthGateCurve", vTestOptions);
                            }
                        }
                    }
                    break;
                case "BIOA_PRC2_CCTHAI_SND":
                case "BIOA_PRC2_CCCAVE_SND":
                case "BIOA_PRC2_CCLAVA_SND":
                case "BIOA_PRC2_CCCRATE_SND":
                case "BIOA_PRC2_CCAHERN_SND":
                    InstallMusicVolume(le1File, vTestOptions);
                    break;
                case "BIOA_PRC2AA_00_LAY":
                    // Need to set 'bCanStepUpOn' = false for certain static meshes as the collision is mega jank
                    {
                        var meshesToFix = new[]
                        {
                            "TheWorld.PersistentLevel.StaticMeshActor_0",
                            "TheWorld.PersistentLevel.StaticMeshActor_1",
                            "TheWorld.PersistentLevel.StaticMeshActor_2",
                            "TheWorld.PersistentLevel.StaticMeshActor_3",
                        };

                        foreach (var m in meshesToFix)
                        {
                            var exp = le1File.FindExport(m);
                            exp.WriteProperty(new BoolProperty(false, "bCanStepUpOn"));
                        }
                    }
                    break;
            }

            // Individual
            switch (upperFName)
            {
                case "BIOA_PRC2_CCAHERN_DSG":
                    {
                        // Rally - This is not a template so it's done manually on this level
                        foreach (var exp in le1File.Exports.Where(x => x.ClassName == "Sequence").ToList())
                        {
                            var seqName = exp.GetProperty<StrProperty>("ObjName")?.Value;

                            if (seqName == "SUR_Ahern_Handler")
                            {
                                // Ahern's mission
                                var startObj = FindSequenceObjectByClassAndPosition(exp, "SeqAct_Teleport", -8784, -1080); // Attach to Teleport since once loading movie is gone we'll hear it
                                var newObj = SequenceObjectCreator.CreateSequenceObject(le1File, "LEXSeqAct_SquadCommand", vTestOptions.cache);
                                KismetHelper.AddObjectToSequence(newObj, exp);
                                KismetHelper.CreateOutputLink(startObj, "Out", newObj); // RALLY
                            }
                        }

                        InstallAhernAntiCheese(le1File);
                        break;
                    }
                case "BIOA_PRC2_CCSIM_04_DSG":
                    {
                        // Install texture streaming for Ocaren
                        var sequence = le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence");
                        var remoteEvent = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_RemoteEvent", vTestOptions.cache);
                        var streamInTextures = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_StreamInTextures", vTestOptions.cache);
                        KismetHelper.AddObjectsToSequence(sequence, false, remoteEvent, streamInTextures);
                        KismetHelper.CreateOutputLink(remoteEvent, "Out", streamInTextures);

                        remoteEvent.WriteProperty(new NameProperty("PrimeTexturesAhern", "EventName"));
                        var materials = new ArrayProperty<ObjectProperty>("ForceMaterials");

                        // OCAREN
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_11.BioMaterialInstanceConstant_151")));
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_11.BioMaterialInstanceConstant_152")));
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_11.BioMaterialInstanceConstant_153")));

                        streamInTextures.WriteProperty(materials);
                        streamInTextures.WriteProperty(new FloatProperty(12f, "Seconds")); // How long to force stream. We set this to 12 to ensure blackscreen and any delays between fully finish
                    }
                    break;
                case "BIOA_PRC2_CCMAIN_CONV":
                    {
                        // Ahern's post-mission dialogue. This installs the streaming textures event
                        var sequence = le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin");
                        var remoteEvent = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_RemoteEvent", vTestOptions.cache);
                        var streamInTextures = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_StreamInTextures", vTestOptions.cache);
                        KismetHelper.AddObjectsToSequence(sequence, false, remoteEvent, streamInTextures);

                        KismetHelper.CreateOutputLink(remoteEvent, "Out", streamInTextures);
                        KismetHelper.CreateVariableLink(streamInTextures, "Location", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SeqVar_Object_19")); //Location: Ahern Teleport Point

                        remoteEvent.WriteProperty(new NameProperty("PrimeTexturesAhern", "EventName"));
                        var materials = new ArrayProperty<ObjectProperty>("ForceMaterials");

                        // AHERN
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_4.BioMaterialInstanceConstant_103")));
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_4.BioMaterialInstanceConstant_104")));
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_4.BioMaterialInstanceConstant_105")));
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_4.BioMaterialInstanceConstant_106")));
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_4.BioMaterialInstanceConstant_107")));

                        // VIDINOS
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_5.BioMaterialInstanceConstant_120"))); //armor
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_5.BioMaterialInstanceConstant_121"))); //head
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_5.BioMaterialInstanceConstant_122"))); // eye

                        // BRYANT
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_8.BioMaterialInstanceConstant_97"))); // armor
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_8.BioMaterialInstanceConstant_101"))); //head
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_8.BioMaterialInstanceConstant_98"))); //hair
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_8.BioMaterialInstanceConstant_102"))); //scalp
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_8.BioMaterialInstanceConstant_100"))); // eye
                        materials.Add(new ObjectProperty(le1File.FindExport("TheWorld.PersistentLevel.BioPawn_8.BioMaterialInstanceConstant_99"))); //lash

                        streamInTextures.WriteProperty(materials);
                        streamInTextures.WriteProperty(new FloatProperty(12f, "Seconds")); // How long to force stream. We set this to 12 to ensure blackscreen and any delays between fully finish

                        // Install classes not in ME1
                        var bftsItems = new[]
                        {
                            ("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.BioSeqAct_Delay_5", "TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SeqAct_Interp_2","TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin", "Finished"),
                            ("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.BioSeqAct_Delay_6", "TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.BioSeqAct_BlackScreen_5","TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin", "Finished"),
                            ("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.BioSeqAct_Delay_2", "TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SeqAct_Interp_7","TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin", "Finished"),
                            ("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.BioSeqAct_Delay_3", "TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SeqAct_Interp_11","TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin", "Finished"),
                            ("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_0.Sequence_980.BioSeqAct_Delay_4","TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_0.Sequence_980.BioSeqAct_BlackScreen_2", "TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_0.Sequence_980", "Finished"),

                            ("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_2.Sequence_982.BioSeqAct_ModifyPropertyPawn_0","TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_2.Sequence_982.BioSeqAct_BlackScreen_3", "TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_2.Sequence_982","Out"),
                            ("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_1.Sequence_981.SeqAct_Delay_1","TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_1.Sequence_981.BioSeqAct_BlackScreen_4", "TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_1.Sequence_981", "Finished")

                        };

                        foreach (var bfts in bftsItems)
                        {
                            var startNode = le1File.FindExport(bfts.Item1);
                            var endNode = le1File.FindExport(bfts.Item2);
                            var bsequence = le1File.FindExport(bfts.Item3);
                            var streaming = SequenceObjectCreator.CreateSequenceObject(le1File, "BioSeqAct_BlockForTextureStreaming", vTestOptions.cache);
                            KismetHelper.AddObjectToSequence(streaming, bsequence);
                            KismetHelper.CreateOutputLink(startNode, bfts.Item4, streaming);
                            KismetHelper.CreateOutputLink(streaming, "Finished", endNode);
                        }

                        if (vTestOptions.debugBuild)
                        {
                            // Ahern always talks
                            le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SeqVar_Float_6").WriteProperty(new FloatProperty(0, "FloatValue")); // Change of not saying something
                            le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SeqVar_Float_9").WriteProperty(new FloatProperty(0, "FloatValue")); // Change of not saying something

                            // Console events to trigger post-cinematics

                            var ahernWinRE = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_Console", vTestOptions.cache);
                            var ahernLossRE = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_Console", vTestOptions.cache);
                            var ahernLoss2RE = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_Console", vTestOptions.cache);
                            var vidinos1RE = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_Console", vTestOptions.cache);
                            var vidinos2RE = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_Console", vTestOptions.cache);
                            var ocarenRE = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_Console", vTestOptions.cache);
                            var ahernSpecialRE = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_Console", vTestOptions.cache);

                            ahernWinRE.WriteProperty(new NameProperty("AhernWin", "ConsoleEventName"));
                            ahernLossRE.WriteProperty(new NameProperty("AhernLoss1", "ConsoleEventName"));
                            ahernLoss2RE.WriteProperty(new NameProperty("AhernLoss2", "ConsoleEventName"));
                            vidinos1RE.WriteProperty(new NameProperty("Vidinos1", "ConsoleEventName")); // Dickhead to Bryant
                            vidinos2RE.WriteProperty(new NameProperty("Vidinos2", "ConsoleEventName")); // Vidinos loses his challenge (win 8 missions)
                            ocarenRE.WriteProperty(new NameProperty("Ocaren", "ConsoleEventName"));
                            ahernSpecialRE.WriteProperty(new NameProperty("AhernSpecial", "ConsoleEventName"));


                            KismetHelper.CreateOutputLink(ahernLossRE, "Out", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SeqAct_Teleport_19"));
                            KismetHelper.CreateOutputLink(ahernLoss2RE, "Out", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SeqAct_Teleport_13"));
                            KismetHelper.CreateOutputLink(ahernWinRE, "Out", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SeqAct_Teleport_5"));
                            KismetHelper.CreateOutputLink(vidinos1RE, "Out", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_0"));
                            KismetHelper.CreateOutputLink(vidinos2RE, "Out", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SequenceReference_1"));
                            KismetHelper.CreateOutputLink(ocarenRE, "Out", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.SeqAct_Teleport_6"));
                            KismetHelper.CreateOutputLink(ahernSpecialRE, "Out", le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.BioSeqAct_BlackScreen_4"));


                            KismetHelper.AddObjectsToSequence(le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin"), false, ahernWinRE, ahernLossRE, ahernLoss2RE, vidinos1RE, vidinos2RE, ocarenRE, ahernSpecialRE);
                        }

                        // Shift the fade-in to 1 second later on the interps to give the lights a moment to warm up
                        // Disabled as kinkojiro changed sequencing
                        //var interps = new[]
                        //{
                        //    "TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.InterpData_2", // win
                        //    "TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.InterpData_6", // loss
                        //    "TheWorld.PersistentLevel.Main_Sequence.Match_End_Cin.InterpData_11", // loss
                        //};
                        //foreach (var interpIFP in interps)
                        //{
                        //    float warmupTime = 0.8f; // How long to keep the blackscreen on for lights to warm up. 0.7 is not enough

                        //    var interp = le1File.FindExport(interpIFP);
                        //    interp.WriteProperty(new FloatProperty(interp.GetProperty<FloatProperty>("InterpLength").Value + warmupTime, "InterpLength")); // update the length of hte interp
                        //    var groups = interp.GetProperty<ArrayProperty<ObjectProperty>>("InterpGroups").Select(x => x.ResolveToEntry(le1File) as ExportEntry).ToList();
                        //    foreach (var group in groups)
                        //    {
                        //        var tracks = group.GetProperty<ArrayProperty<ObjectProperty>>("InterpTracks").Select(x => x.ResolveToEntry(le1File) as ExportEntry).ToList();
                        //        foreach (var track in tracks)
                        //        {
                        //            bool isFirst = true;
                        //            switch (track.ClassName)
                        //            {
                        //                case "InterpTrackDirector":
                        //                    break; // Do not change
                        //                case "InterpTrackFade":
                        //                    var floatTrack = track.GetProperty<StructProperty>("FloatTrack");
                        //                    var points = floatTrack.GetProp<ArrayProperty<StructProperty>>("Points");
                        //                    foreach (var point in points)
                        //                    {
                        //                        point.GetProp<FloatProperty>("InVal").Value += warmupTime;
                        //                    }
                        //                    track.WriteProperty(floatTrack);
                        //                    break;
                        //                case "InterpTrackEvent":
                        //                    var eventTrack = track.GetProperty<ArrayProperty<StructProperty>>("EventTrack");
                        //                    foreach (var etk in eventTrack)
                        //                    {
                        //                        if (isFirst)
                        //                        {
                        //                            isFirst = false;
                        //                            continue;
                        //                        }

                        //                        etk.GetProp<FloatProperty>("Time").Value += warmupTime;
                        //                    }
                        //                    track.WriteProperty(eventTrack);
                        //                    break;
                        //                case "BioEvtSysTrackGesture":
                        //                    var m_aTrackKeys = track.GetProperty<ArrayProperty<StructProperty>>("m_aTrackKeys");
                        //                    foreach (var btk in m_aTrackKeys)
                        //                    {
                        //                        if (isFirst)
                        //                        {
                        //                            isFirst = false;
                        //                            continue;
                        //                        }

                        //                        btk.GetProp<FloatProperty>("fTime").Value += warmupTime;
                        //                    }
                        //                    track.WriteProperty(m_aTrackKeys);
                        //                    break;
                        //                case "InterpTrackMove":
                        //                    break; // Camera doesn't move
                        //            }
                        //        }
                        //    }
                        //}

                        break;
                    }
                case "BIOA_PRC2_CCLAVA_DSG":
                    {
                        // SeqAct_ChangeCollision changed and requires an additional property otherwise it doesn't work.
                        string[] collisionsToTurnOff = new[]
                        {
                            // Hut doors and kill volumes
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_1",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_3",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_5",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_6",
                        };

                        foreach (var cto in collisionsToTurnOff)
                        {
                            var exp = le1File.FindExport(cto);
                            exp.WriteProperty(new EnumProperty("COLLIDE_NoCollision", "ECollisionType", MEGame.LE1, "CollisionType"));
                        }

                        string[] collisionsToTurnOn = new[]
                        {
                            // Hut doors and kill volumes
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_0",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_9",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_2",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_4",
                        };

                        foreach (var cto in collisionsToTurnOn)
                        {
                            var exp = le1File.FindExport(cto);
                            exp.WriteProperty(new EnumProperty("COLLIDE_BlockAll", "ECollisionType", MEGame.LE1, "CollisionType"));
                        }

                        // Add code to disable reachspecs when turning the doors on so enemies do not try to use these areas
                        var hutSeq = le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility");
                        var cgSource = le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_4");
                        var disableReachSpecs = SequenceObjectCreator.CreateSequenceObject(le1File, "LEXSeqAct_ToggleReachSpec", vTestOptions.cache);

                        KismetHelper.AddObjectToSequence(disableReachSpecs, hutSeq);
                        KismetHelper.CreateOutputLink(cgSource, "Out", disableReachSpecs, 2);

                        string[] reachSpecsToDisable = new[]
                        {
                            // NORTH ROOM
                            "TheWorld.PersistentLevel.ReachSpec_1941", // CoverLink to PathNode
                            "TheWorld.PersistentLevel.ReachSpec_1937", // CoverLink to CoverLink
                            "TheWorld.PersistentLevel.ReachSpec_2529", // PathNode to PathNode

                            // SOUTH ROOM
                            "TheWorld.PersistentLevel.ReachSpec_1856", // CoverLink to PathNode
                            "TheWorld.PersistentLevel.ReachSpec_1849", // CoverLink to CoverLink
                        };

                        foreach (var rs in reachSpecsToDisable)
                        {
                            var obj = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Object", vTestOptions.cache);
                            KismetHelper.AddObjectToSequence(obj, hutSeq);
                            obj.WriteProperty(new ObjectProperty(le1File.FindExport(rs), "ObjValue"));
                            KismetHelper.CreateVariableLink(disableReachSpecs, "ReachSpecs", obj);
                        }
                    }
                    break;
                case "BIOA_PRC2_CCSIM03_LAY":
                    {
                        // The door lighting channels needs fixed up.
                        var door = le1File.FindExport(@"TheWorld.PersistentLevel.BioDoor_1.SkeletalMeshComponent_1");
                        var channels = door.GetProperty<StructProperty>("LightingChannels");
                        channels.Properties.AddOrReplaceProp(new BoolProperty(false, "Static"));
                        channels.Properties.AddOrReplaceProp(new BoolProperty(false, "Dynamic"));
                        channels.Properties.AddOrReplaceProp(new BoolProperty(false, "CompositeDynamic"));
                        door.WriteProperty(channels);
                    }
                    break;
                case "BIOA_PRC2_CCSIM_ART":
                    {
                        // Lights near the door need fixed up.
                        var doorPL = le1File.FindExport(@"TheWorld.PersistentLevel.StaticLightCollectionActor_11.PointLight_0_LC");
                        var lc = doorPL.GetProperty<StructProperty>("LightColor");
                        lc.GetProp<ByteProperty>("B").Value = 158;
                        lc.GetProp<ByteProperty>("G").Value = 194;
                        lc.GetProp<ByteProperty>("R").Value = 143;
                        doorPL.WriteProperty(lc);

                        var doorSL = le1File.FindExport(@"TheWorld.PersistentLevel.StaticLightCollectionActor_11.SpotLight_7_LC");
                        lc = doorSL.GetProperty<StructProperty>("LightColor");
                        lc.GetProp<ByteProperty>("B").Value = 215;
                        lc.GetProp<ByteProperty>("G").Value = 203;
                        lc.GetProp<ByteProperty>("R").Value = 195;
                        doorSL.WriteProperty(lc);
                    }
                    break;
                case "BIOA_PRC2_CCSPACE02_DSG":
                    {
                        // Port in a new DominantLight
                        var sourceLight = vTestOptions.vTestHelperPackage.FindExport(@"CCSPACE02_DSG.DominantDirectionalLight_1");
                        var destLevel = le1File.FindExport("TheWorld.PersistentLevel");
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceLight, le1File, destLevel, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out _);

                        // Correct some lighting channels
                        string[] unlitPSCs = new[]
                        {
                            "BIOA_PRC2_S.Prefab.PRC2_Skybox_Vista.PRC2_Skybox_Vista_Arc17.ParticleSystemComponent0",
                            "BIOA_PRC2_S.Prefab.PRC2_Skybox_Vista.PRC2_Skybox_Vista_Arc18.ParticleSystemComponent0",
                            "BIOA_PRC2_S.Prefab.PRC2_Skybox_Vista.PRC2_Skybox_Vista_Arc19.ParticleSystemComponent0"
                        };

                        foreach (var unlitPSC in unlitPSCs)
                        {
                            var exp = le1File.FindExport(unlitPSC);
                            var lightingChannels = exp.GetProperty<StructProperty>("LightingChannels");
                            lightingChannels.Properties.Clear();
                            lightingChannels.Properties.Add(new BoolProperty(true, "bInitialized"));
                            lightingChannels.Properties.Add(new BoolProperty(true, new NameReference("Cinematic", 4)));
                            exp.WriteProperty(lightingChannels);
                        }

                    }
                    break;
                case "BIOA_PRC2_CCCRATE":
                    {
                        // needs something to fill framebuffer
                        var sourceAsset = vTestOptions.vTestHelperPackage.FindExport(@"CROSSGENV.StaticMeshActor_32000");
                        var destLevel = le1File.FindExport("TheWorld.PersistentLevel");
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceAsset, le1File, destLevel, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var mesh);
                        PathEdUtils.SetLocation(mesh as ExportEntry, 15864, -25928, -5490);
                    }
                    break;
                case "BIOA_PRC2_CCCAVE":
                    {
                        // needs something to fill framebuffer
                        var sourceAsset = vTestOptions.vTestHelperPackage.FindExport(@"CROSSGENV.StaticMeshActor_32000");
                        var destLevel = le1File.FindExport("TheWorld.PersistentLevel");
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceAsset, le1File, destLevel, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var mesh);
                        PathEdUtils.SetLocation(mesh as ExportEntry, -16430, -28799, -2580);

                        // Needs a second one to hide the top too
                        var fb2 = EntryCloner.CloneTree(mesh) as ExportEntry;
                        var rotation = fb2.GetProperty<StructProperty>("Rotation");
                        rotation.Properties.AddOrReplaceProp(new FloatProperty(0f, "Pitch"));
                        fb2.WriteProperty(rotation);
                    }
                    break;
                case "BIOA_PRC2_CCSIM": // this this be in CCSIM05_DSG instead?
                    {
                        // SCOREBOARD FRAMEBUFFER
                        // This is handled by porting model from CCSIM03_LAY
                        // needs something to fill framebuffer
                        //var sourceAsset = vTestOptions.vTestHelperPackage.FindExport(@"CROSSGENV.StaticMeshActor_32000");
                        //var destLevel = le1File.FindExport("TheWorld.PersistentLevel");
                        //EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceAsset, le1File, destLevel, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var mesh);
                        //PathEdUtils.SetLocation(mesh as ExportEntry, -3750, -1624, -487);
                        //PathEdUtils.SetDrawScale3D(mesh as ExportEntry, 3, 3, 3);
                    }
                    break;
                case "BIOA_PRC2":
                    {
                        // Blocking Volumes for shep to stand on post-mission
                        int[] sourceTriggerStreams = new int[]
                        {
                            10, 11, 12, 13, 18 // 18 is technically not required (ahern) but left in event of future changes. These are the scoreboard triggerstreams
                        };

                        var sourceAsset = le1File.FindExport(@"TheWorld.PersistentLevel.BlockingVolume_15");
                        var destLevel = le1File.FindExport("TheWorld.PersistentLevel");

                        foreach (var sts in sourceTriggerStreams)
                        {
                            var newBlockingVolume = EntryCloner.CloneTree(sourceAsset);
                            //EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, sourceAsset, le1File, destLevel, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var newBlockingVolume);

                            var tsExport = le1File.FindExport(@"TheWorld.PersistentLevel.BioTriggerStream_" + sts);
                            var loc = PathEdUtils.GetLocation(tsExport);
                            PathEdUtils.SetLocation(newBlockingVolume as ExportEntry, loc.X, loc.Y, loc.Z - 256f);
                        }
                    }
                    break;
            }
        }

        private static void SetupKillStreakVO(IMEPackage le1File, VTestOptions vTestOptions)
        {
            foreach (var sequence in le1File.Exports.Where(x => x.ClassName == "Sequence").ToList())
            {
                var seqName = sequence.GetProperty<StrProperty>("ObjName")?.Value;
                if (seqName == "TA_V3_Gametype_Handler")
                {
                    var spawnerDeath = FindSequenceObjectByClassAndPosition(sequence, "SeqEvent_SequenceActivated", -1392, 2824);
                    if (spawnerDeath != null)
                    {
                        InstallRemoteEventSignal(le1File, spawnerDeath.InstancedFullPath, "EnemyKilled", vTestOptions);
                    }
                }
                else if (seqName == "CAH_Respawner")
                {
                    var spawner = FindSequenceObjectByClassAndPosition(sequence, "SequenceReference", 1920, 2512);
                    if (spawner != null)
                    {
                        InstallRemoteEventSignal(le1File, spawner.InstancedFullPath, "EnemyKilled", vTestOptions, "Enemy Killed");
                    }
                }
                else if (seqName == "Check_Capping_Completion")
                {
                    // SUR
                    var spawner = FindSequenceObjectByClassAndPosition(sequence, "SeqEvent_SequenceActivated", -204, 2984);
                    if (spawner != null)
                    {
                        InstallRemoteEventSignal(le1File, spawner.InstancedFullPath, "EnemyKilled", vTestOptions, "Enemy Killed");
                    }
                }
                else if (seqName == "Vampire_Mode_Handler")
                {
                    // SUR
                    var spawner = FindSequenceObjectByClassAndPosition(sequence, "SeqEvent_SequenceActivated", -2728, 2976);
                    if (spawner != null)
                    {
                        InstallRemoteEventSignal(le1File, spawner.InstancedFullPath, "EnemyKilled", vTestOptions, "Enemy Killed");
                    }
                }
            }
        }


        private static void FixAhernConversation(IMEPackage le1File, VTestOptions vTestOptions)
        {
            // Ahern's conversation switches the position of Ahern and Competitors depending on which conversation branch you choose
            // I have no idea why Demiurge did this but it's confusing and bad design.
            // This conversation is in multiple files so we just check for the IFP, if we find it, we make the corrections.

            var ahernConv = le1File.FindExport("prc2_ahern_D.prc2_ahern_dlg");
            if (ahernConv == null)
                return; // Not in this file

            var entryIndicesToFix = new[] { 59, 61, 66, 64, 71, 53, 58, 55 };

            var properties = ahernConv.GetProperties();

            var entryList = properties.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
            foreach (var idx in entryIndicesToFix)
            {
                var entry = entryList[idx];
                var replyList = entry.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");

                // Heuristic to tell if we need to update this.
                var stringRef = replyList[2].GetProp<StringRefProperty>("srParaphrase");
                if (stringRef.Value == 182514) // Competitors
                    continue; // competitors is already in slot 3 (left middle)
                else if (stringRef.Value == 182517) // Ahern
                {
                    Debug.WriteLine($"Fixing ahern conversation for entry node {idx} in {le1File.FileNameNoExtension}");
                    var temp = replyList[2];
                    replyList[2] = replyList[3]; // Move competitors to slot 3 (left) from slot 4 (bottom right)
                    replyList[3] = temp; // Move ahern to bottom right, slot 4
                }
            }


            ahernConv.WriteProperties(properties);

        }

        private static void ReduceDirectionalLights(IMEPackage le1File, float multiplier)
        {
            foreach (var exp in le1File.Exports.Where(x => x.ClassName == "DirectionalLightComponent").ToList())
            {
                var brightness = exp.GetProperty<FloatProperty>("Brightness");
                if (brightness != null)
                {
                    brightness.Value *= multiplier;
                    exp.WriteProperty(brightness);
                }
            }
        }

        private static void FixGethFlashlights(ExportEntry sequence, VTestOptions vTestOptions)
        {
            // Custom class by Kinkojiro to add and un-add the flashlight VFX
            var actorFactoryWithOwner = FindSequenceObjectByClassAndPosition(sequence, "SeqAct_ActorFactoryWithOwner");
            var attachFL = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "LEXSeqAct_AttachGethFlashLight", vTestOptions.cache);
            var deattachFL = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "LEXSeqAct_AttachGethFlashLight", vTestOptions.cache);
            KismetHelper.AddObjectsToSequence(sequence, false, attachFL, deattachFL);

            // ATTACH FLASHLIGHT
            {
                var outLinksFactory = SeqTools.GetOutboundLinksOfNode(actorFactoryWithOwner);
                var originalOutlink = outLinksFactory[0][2].LinkedOp;
                outLinksFactory[0][2].LinkedOp = attachFL; // repoint to attachFL
                SeqTools.WriteOutboundLinksToNode(actorFactoryWithOwner, outLinksFactory);
                KismetHelper.CreateOutputLink(attachFL, "Done", originalOutlink as ExportEntry);
                var currentPawn = FindSequenceObjectByClassAndPosition(sequence, "SeqVar_Object", 4536, 2016);
                KismetHelper.CreateVariableLink(attachFL, "Target", currentPawn);
            }
            // DETACH FLASHLIGHT
            {
                var attachCrustEffect = FindSequenceObjectByClassAndPosition(sequence, "BioSeqAct_AttachCrustEffect", 5752, 2000);
                var attachOutlinks = SeqTools.GetOutboundLinksOfNode(attachCrustEffect);
                var originalOutlink = attachOutlinks[0][1].LinkedOp;
                attachOutlinks[0][1].LinkedOp = deattachFL; // repoint to deattachFL
                attachOutlinks[0][1].InputLinkIdx = 1; // Detach
                SeqTools.WriteOutboundLinksToNode(attachCrustEffect, attachOutlinks);
                KismetHelper.CreateOutputLink(deattachFL, "Done", originalOutlink as ExportEntry);

                var cachedPawn = FindSequenceObjectByClassAndPosition(sequence, "SeqVar_Object", 5640, 2128);
                KismetHelper.CreateVariableLink(deattachFL, "Target", cachedPawn);
            }
        }

        private static void InstallAhernAntiCheese(IMEPackage le1File)
        {
            // Clones and adds 2 blocking volumes to prevent you from getting out of the playable area of the map.
            // One is due to bad collision, the other is due to how cover calcuation changed in LE1 which likely allowed
            // cover where it should be and you could spin out of cover through a box.

            var sourcebv = le1File.FindExport("TheWorld.PersistentLevel.BlockingVolume_23");
            var ds3d = CommonStructs.Vector3Prop(0.5f, 0.5f, 0.25f, "DrawScale3D");

            // Northern cheese point
            var northBV = EntryCloner.CloneTree(sourcebv);
            northBV.RemoveProperty("bCollideActors");
            PathEdUtils.SetLocation(northBV, -38705.57f, -28901.904f, -2350.1252f);
            (northBV.GetProperty<ObjectProperty>("BrushComponent").ResolveToEntry(le1File) as ExportEntry).WriteProperty(new BoolProperty(false, "BlockZeroExtent")); // do not block gunfire
            northBV.WriteProperty(ds3d);

            // South cheese
            var southBV = EntryCloner.CloneTree(northBV); // already has scaling and guns fire through it
            PathEdUtils.SetLocation(southBV, -38702.812f, -24870.682f, -2355.5256f);
        }


        private static void AddVTestSpecificStrings(ExportEntry tlkExport, string lang)
        {
            ME1TalkFile talkF = new ME1TalkFile(tlkExport);
            var stringRefs = talkF.StringRefs.ToList();
            // LANGUAGE SPECIFIC STRINGS HERE
            switch (lang)
            {
                case "RA":
                case "RU":
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338464, 1, "Загрузка данных"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338465, 1, "Настройка музыки"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338466, 1, "Отключить музыку"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338467, 1, "Включить музыку"));
                    break;
                case "ES":
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338465, 1, "Configuración de Música"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338466, 1, "Desactivar Música"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338467, 1, "Activar Música"));
                    break;
                case "IT":
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338465, 1, "Music Setting"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338466, 1, "Disable Music"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338467, 1, "Enable Music"));
                    break;
                case "PL":
                case "PLPC":
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338465, 1, "Music Setting"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338466, 1, "Disable Music"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338467, 1, "Enable Music"));
                    break;
                case "FR":
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338465, 1, "Music Setting"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338466, 1, "Disable Music"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338467, 1, "Enable Music"));
                    break;
                case "DE":
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338465, 1, "Music Setting"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338466, 1, "Disable Music"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338467, 1, "Enable Music"));
                    break;
                case "JA":
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338464, 1, "Downloading Data"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338465, 1, "Music Setting"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338466, 1, "Disable Music"));
                    stringRefs.Add(new ME1TalkFile.TLKStringRef(338467, 1, "Enable Music"));
                    break;
            }


            var huff = new HuffmanCompression();
            huff.LoadInputData(stringRefs);
            huff.serializeTalkfileToExport(tlkExport);
        }

        private static void PostUpdateTLKs(VTestOptions vTestOptions)
        {
            var basePath = Path.Combine(PAEMPaths.VTest_FinalDestDir, "DLC_MOD_Vegas_GlobalTlk_");
            var langsToUpdate = new[] { "RA", "RU", "DE", "FR", "IT", "ES", "JA", "PL", "PLPC" };
            foreach (var lang in langsToUpdate)
            {
                var tlkPackage = MEPackageHandler.OpenMEPackage(basePath + lang + ".pcc");

                // Add our specific TLK strings.
                AddVTestSpecificStrings(tlkPackage.FindExport("GlobalTlk_tlk"), lang);
                AddVTestSpecificStrings(tlkPackage.FindExport("GlobalTlk_tlk_M"), lang);

                tlkPackage.Save();

                // Add English VO TLKs
                switch (lang)
                {
                    case "DE":
                        tlkPackage.Save(basePath + "GE.pcc"); // English VO
                        break;
                    case "FR":
                        tlkPackage.Save(basePath + "FE.pcc"); // English VO
                        break;
                    case "IT":
                        tlkPackage.Save(basePath + "IE.pcc"); // English VO
                        break;
                }
            }
        }

        private static void RemoveBitExplosionEffect(ExportEntry exp)
        {
            // These sequences need the 'bit explosion' effect removed because BioWare changed something in SeqAct_ActorFactory and completely broke it
            // We are just going to use the crust effect instead
            var sequenceObjects = exp.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            foreach (var seqObjProp in sequenceObjects.ToList()) // ToList as we're going to modify it so we need a copy
            {
                if (!sequenceObjects.Contains(seqObjProp))
                    continue; // it's already been removed

                var seqObj = seqObjProp.ResolveToEntry(exp.FileRef) as ExportEntry;
                if (seqObj != null)
                {
                    if (seqObj.ClassName == "SeqAct_ActorFactory")
                    {
                        var outLinks = SeqTools.GetOutboundLinksOfNode(seqObj);
                        outLinks[0].RemoveAt(0); // Remove the first outlink, which goes to Delay
                        SeqTools.WriteOutboundLinksToNode(seqObj, outLinks); // remove the link so we don't try to connect to it when skipping
                        SeqTools.SkipSequenceElement(seqObj, "Finished");
                        sequenceObjects.Remove(seqObjProp);
                    }
                    else if (seqObj.ClassName == "BioSeqAct_Delay")
                    {
                        // We can ID these by the position data since they are built from a template and thus always have the same positions
                        // It also references destroying the spawned particle system
                        var props = seqObj.GetProperties();
                        if (props.GetProp<IntProperty>("ObjPosX")?.Value == 4440 && props.GetProp<IntProperty>("ObjPosY")?.Value == 2672)
                        {
                            // This needs removed too
                            var nextNodes = SeqTools.GetOutboundLinksOfNode(seqObj);
                            var nextNode = nextNodes[0][0].LinkedOp as ExportEntry;
                            var subNodeOfNext = SeqTools.GetVariableLinksOfNode(nextNode)[0].LinkedNodes[0] as ExportEntry;

                            // Remove all of them from the sequence
                            sequenceObjects.Remove(seqObjProp); // Delay
                            sequenceObjects.Remove(new ObjectProperty(subNodeOfNext.UIndex)); // Destroy
                            sequenceObjects.Remove(new ObjectProperty(nextNode.UIndex)); // SeqVar_Object
                        }
                    }
                }
            }

            exp.WriteProperty(sequenceObjects);
        }

        private static string[] PossibleLightMapUsers = new[]
    {
            "BIOA_Apartment_S.APT_ANgledCeinling_01",
            "BIOA_Apartment_S.APT_BackWall",
            "BIOA_Apartment_S.APT_Central_Floor_01",
            "BIOA_Apartment_S.APT_ExtWall_01",
            "BIOA_Apartment_S.APT_Floor01",
            "BIOA_Apartment_S.APT_GlassBrace_01",
            "BIOA_Apartment_S.APT_HallCeiling_01",
            "BIOA_Apartment_S.APT_HallWall_01",
            "BIOA_Apartment_S.APT_HallWallB_01",
            "BIOA_Apartment_S.APT_KitchenWall_01",
            "BIOA_Apartment_S.APT_MainCeiling_01",
            "BIOA_Apartment_S.APT_MainFloor_01",
            "BIOA_Apartment_S.APT_MainGlass_01",
            "BIOA_Apartment_S.APT_Pillar_01",
            "BIOA_Apartment_S.APT_SMPillar_01",
            "BIOA_Apartment_S.APT_Stairs_01",
            "BIOA_Apartment_S.Apt_Window_01",
            "BIOA_Apartment_S.APT_WindowFrame_01",
            "BIOA_LAV60_S.LAV70_ROCKND01",
            "BIOA_NOR10_S.NOR10_opeingE01",
            "BIOA_NOR10_S.NOR10_poleA01",
            "BIOA_PRC2_MatFX.CCSim_Room_Mesh",
            "BIOA_PRC2_MatFX.LoadEffect_Mesh_S",
            "BIOA_PRC2_MatFX.Scoreboard_CCMesh",
            "BIOA_PRC2_MatFX.StrategicRing_S",
            "BIOA_PRC2_PlatformUI_T.Square_Plane",
            "BIOA_PRC2_S.Asteroids.LAV70_GROUNDROCKA_Dup",
            "BIOA_PRC2_S.Colision.Beach_COLISION01",
            "BIOA_PRC2_S.Colision.Lobby_COLISION",
            "BIOA_PRC2_S.Colision.Mid_COLISION01",
            "BIOA_PRC2_S.Colision.Sim_COLISION",
            "BIOA_PRC2_S.CommandCenter.CCFloorBeams",
            "BIOA_PRC2_S.lobby.LobbyRailings",
            "BIOA_PRC2_S.lobby.LobbyRoom_S",
            "BIOA_PRC2_S.Mid.SpaceVista_Frame_Btm_S",
            "BIOA_PRC2_S.Mid.SpaceVista_Frame_S",
            "BIOA_PRC2_S.Mid.SpaceVista_Window_S",
            "BIOA_PRC2_S.MidRoom01_New",
            "BIOA_PRC2_S.PRC2_SimWallFiller",
            "BIOA_PRC2_S.PRC2_SimWindowFiller",
            "BIOA_PRC2_S.Railing_Corner_Light",
            "BIOA_PRC2_S.ScoreBoardBackground",
            "BIOA_PRC2_S.Ships.Turian_Bomber_01",
            "BIOA_PRC2_S.Sim.SimFloorLines",
            "BIOA_PRC2_S.Sim.SimRoom01_S",
            "BIOA_PRC2_S.Sim.SimRoom01Railings",
            "BIOA_PRC2_S.Sim.SimRoom02_S",
            "BIOA_PRC2_S.Sim.SimRoom03_S",
            "BIOA_PRC2_S.Sim.SimRoom03Railings",
            "BIOA_PRC2_Scoreboard_T.shepard_plane",
            "BIOA_PRC2_Scoreboard_T.Square_Plane",
            "BIOA_PRC2_T.LAVCubemap.LAV60_siloA01_Dup",
            "BIOA_PRC2_T.LAVCubemap.LAV60_silohouseA06_Dup",
            "BIOA_PRC2_T.LAVCubemap.LAV60_silohouseC06_Dup",
            "BIOA_PRC2_T.LAVCubemap.LAV60_silohousewinA01_Dup",
            "BIOA_PRC2_T.LAVCubemap.LAV70_drvingpannel10_Dup",
            "BIOA_PRC2_T.LAVCubemap.LAV70_siloB01_Dup",
            "BIOA_WAR30_S.WAR30_LIGHTSHAFT",
            "BioBaseResources.HUD_Holograms.Meshes.Player_Pos__old",
            "BIOG_v_DLC_Vegas.Meshes.UNC_Vegas_Rings",

            // THE FOLLOWING WERE FOUND TO HAVE SAME LOD COUNT AND VERTICE COUNT
            // Calcaulted in PackageEditorExperimentsM
            "BIOA_ICE50_S.ice50_ceilingredo01",
            "BIOA_JUG80_S.jug80_damwall04",
            "BIOA_PRO10_S.PRO10_BEAMLIFT00",
            "BIOA_PRO10_S.PRO10_CONCRETEBASE",
            "BIOA_PRO10_S.PRO10_CONCRETEXCONNECTOR00",
            "BIOA_PRO10_S.PRO10_RAILINGCOVER",
            "BIOA_PRO10_S.PRO10_STATIONRAILINGA00",
            "BIOA_PRO10_S.PRO10_STATIONRAILINGB00",
            "BIOA_PRO10_S.PRO10_STATIONWALLPLAIN00",
            "BIOA_PRO10_S.PRO10_STATIONWALLTOPCOVER",
            "BIOA_PRO10_S.PRO10_WALKWAYBLOCK",
            "BIOA_PRO10_S.PRO10_WALLNOCROUCHA",
            "BIOA_STA30_S.sta30_ceilingsim",
            "BIOA_STA30_S.STA30_WALL10",
            "BIOA_STA30_S.sta30_wall16",
            "BIOG__SKIES__.LAV00.lav00_skybox_mld",
            "BIOA_WAR20_S.WAR20_BRIDGERUBBLEPILE",
            "BIOA_FRE10_S.CARGO_CONTAINER",
            "BIOA_FRE10_S.FRE10_CONTAINERCONNECTOR",
            "BIOA_ICE20_S.ice20_signeye",
            "BIOA_ICE50_S.ice50_metalpaneltube00",
            "BIOA_ICE60_S.ICE60_curvejointC00",
            "BIOA_ICE60_S.ICE60_glasspaneltubeC00",
            "BIOA_ICE60_S.ICE60_steelframeA02",
            "BIOA_LAV60_S.LAV60_catwalk_24",
            "BIOA_LAV70_S.LAV70_CABLE-RAIL01",
            "BIOA_LAV70_S.LAV70_CABLE01",
            "BIOA_LAV70_S.LAV70_catwalk_B01",
            "BIOA_LAV70_S.LAV70_CAVE02",
            "BIOA_LAV70_S.LAV70_CAVE02b",
            "BIOA_LAV70_S.LAV70_edgechunkB01",
            "BIOA_LAV70_S.LAV70_edgechunkB02",
            "BIOA_LAV70_S.LAV70_edgechunkB61",
            "BIOA_LAV70_S.LAV70_ELVATOR-METALBAR",
            "BIOA_LAV70_S.LAV70_ELVATOR-PARTA01",
            "BIOA_LAV70_S.LAV70_ground01",
            "BIOA_LAV70_S.LAV70_LIGHT13",
            "BIOA_LAV70_S.LAV70_SmallRocksLowA01",
            "BIOA_LAV70_S.LAV70_TUBE-BLACKD01",
            "BIOA_LAV70_S.PRO10_ROCKCOVERA193",
            "BIOA_LAV70_S.PRO10_ROCKCOVERB253",
            "BIOA_LAV70_S.LAV70_lift03",
            "BIOA_LAV70_S.LAV70_lift_brace02",
            "BIOA_LAV70_S.LAV70_lift_door04",
            "BIOA_LAV70_S.LAV70_lift_fin00",
            "BIOA_LAV70_S.LAV70_mmover01",
            "BIOA_LAV70_S.LAV70_ROCKSLIDELOPOLYC01",
            "BIOG_V_Env_Hologram_Z.Mesh.Holomod_04",
            "BIOG_V_Env_Hologram_Z.Mesh.Screen_01",
            "BIOA_JUG80_S.jug80_rooftopB",
            "BIOA_JUG80_S.jug80_wallA_01",
            "BIOA_WAR30_S.WAR20_STAIRSMIDDLECOLLUM10",
            "BIOA_LAV60_S.LAV60_catwalk_Short05",
            "BIOA_LAV60_S.LAV60_downlightA18",
            "BIOA_LAV60_S.LAV60_drvingpannel100",
            "BIOA_LAV60_S.LAV60_fan-anime01",
            "BIOA_LAV60_S.LAV60_pathbarB112",
            "BIOA_LAV60_S.LAV60_sunroofA01",
            "BIOA_NOR10_S.NOR10_signnumberD01",
            "BIOA_NOR10_S.NOR10D_sidewalkpannel01",
            "BIOA_ICE60_S.ICE60_6x8platA01",
            "BIOA_NOR10_S.NOR10_CAPTLIGHTA01",
            "BIOA_NOR10_S.NOR10_commanderroom_newA",
            "BIOA_NOR10_S.NOR10_SCREENTVA01",
            "BIOA_ICE50_S.ice50_glasspaneltube00",
            "BIOA_NOR10_S.NOR10_FIRSTBHANDLEB00",
            "BIOA_NOR10_S.NOR10_LIFESTAIRS",
            "BIOA_NOR10_S.NOR10Dseat01",
            "BIOA_ICE50_S.ice50_midbeam01",
            "BIOA_ICE60_S.ICE60_sloped00",
            "BIOA_NOR10_S.NOR10_FIRSTBHANDLE01",
            "BIOG_V_Env_Hologram_Z.Mesh.Holomod_02",
            "BIOA_NOR10_S.NOR10_FIRSTC01",
            "BIOA_NOR10_S.NOR10_pilotlink01",
            "BIOA_NOR10_S.NOR10D_console01",
            "BIOG_V_Env_Hologram_Z.Mesh.Holomod_03",
            "BIOG_V_Env_Hologram_Z.Mesh.Holomod_13_Mesh",
            "BIOG_V_Env_Hologram_Z.Mesh.Keyboard_01",
            "BIOA_NOR10_S.NOR10_signnumberB01",
            "BIOG_APL_INT_Chair_Comf_02_L.Chair_Comfortable_02",
            "BIOA_NOR10_S.NOR10_TVSET01",
            "BIOA_NOR10_S.NOR10_table",
            "BIOG_APL_PHY_Glasses_01_L.Glasses_01",
            "BIOG_V_Env_Hologram_Z.Mesh.Holomod_05",
            "BIOG_V_Env_Hologram_Z.Mesh.Holomod_09_Mesh",
            "BIOG_V_Env_Hologram_Z.Mesh.Holomod_11_Mesh",
            "BIOA_UNC50_T.Meshes.UNC53planet",
            "BIOA_JUG40_S.jug40_fenceALONG",
            "BIOA_JUG40_S.JUG40_Sail02",
            "BIOA_JUG40_S.Jug40_Sail20",
            "BIOA_JUG40_S.Jug40_Sail32",
            "BIOA_JUG40_S.JUG40_treebranchesA",
            "BIOA_JUG40_S.Jug40_Wires01",
            "BIOG__SKIES__.Samples.sam02",
            "BIOG_V_Env_Jug_WaveCrash_Z.Meshes.sprayPlanes",
            "BIOA_NOR10_S.NOR10_medbedhandleA01",
            "BIOA_PRO10_S.Pro10_BridgeHindge01",
            "BIOA_PRO10_S.PRO10_ROCKCOVERSHARDA",
            "BIOA_UNC20_T.UNC_HORIZONLINE",

            "BIOA_JUG40_S.CROSSGENFIX_SANDBARB" // This is done via special donor, renamed, but we want the original lightmaps as we use a few less poly version (but def the lightmaps)
    };

        // These assets will be used from ME1 (not using LE1's version). They must exist with same IFP in LE1 however. 
        // Objects that match these will use static lighting
        private static string[] ME1OptimizationAssets = new[]
        {
            "BIOA_JUG40_S.JUG40_ROCKCOVERA", // rock cube things
            "BIOA_JUG40_S.JUG40_ROCKCOVERSHARDA", // thing rock cover
            //"BIOA_JUG40_S.CROSSGENFIX_SANDBARB", // sandbars
            "BIOA_JUG40_S.jug40_pillarD_02", // Pillar things under those other things
            "BIOA_JUG40_S.jug40_fenceALONG", // fence piece
            "BIOA_JUG40_S.JUG40_BASERAILING00",
            "BIOA_JUG40_S.JUG40_BASERAILINGRAMP",
            "BIOA_JUG40_S.JUG20_RAMPSHEET",
            "BIOA_JUG40_S.jug40_Object01", // Pathway to the 'hole of doom'



        };

        public static void PortME1OptimizationAssets(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            foreach (var me1oa in ME1OptimizationAssets)
            {
                var le1Version = le1File.FindExport(me1oa);
                if (le1Version != null)
                {
                    var me1Version = me1File.FindExport(me1oa);
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, me1Version, le1File, le1Version, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out _);
                }
            }
        }

        /// <summary>
        /// Creates dynamic lighting but tries to increase performance a bit
        /// </summary>
        /// <param name="Pcc"></param>
        /// <param name="silent"></param>
        public static void CreateDynamicLighting(IMEPackage Pcc, VTestOptions vTestOptions, bool silent = false)
        {
            var fname = Path.GetFileNameWithoutExtension(Pcc.FilePath);
            // Need to check if ledge actors compute lighting while hidden. If they do this will significantly harm performance
            var dynamicableExports = Pcc.Exports.Where(exp => (exp.IsA("MeshComponent") && exp.Parent.IsA("StaticMeshActorBase")) || (exp.IsA("BrushComponent") && !exp.Parent.IsA("Volume"))).ToList();
            foreach (ExportEntry exp in dynamicableExports)
            {
                PropertyCollection props = exp.GetProperties();
                if (props.GetProp<BoolProperty>("bAcceptsLights")?.Value == false)
                    //|| props.GetProp<BoolProperty>("CastShadow")?.Value == false) // CROSSGEN- OFF since we don't do dynamic shadows.
                {
                    // shadows/lighting has been explicitly forbidden, don't mess with it.
                    continue;
                }
                Debug.WriteLine($"CHECKING {exp.InstancedFullPath}");
                if (vTestOptions.allowTryingPortedMeshLightMap && !fname.StartsWith("BIOA_PRC2AA"))
                {
                    var sm = exp.GetProperty<ObjectProperty>("StaticMesh"); // name might need changed?
                    if (sm != null)
                    {
                        if (Pcc.TryGetEntry(sm.Value, out var smEntry) && ShouldPortLightmaps(smEntry))
                        {
                            Debug.WriteLine($"Not using dynamic lighting for mesh {smEntry.InstancedFullPath} on {exp.InstancedFullPath}");
                            var tcBin = ObjectBinary.From<StaticMeshComponent>(exp);
                            foreach (var lod in tcBin.LODData)
                            {
                                if (lod.LightMap is LightMap_2D lm2d)
                                {
                                    lm2d.ScaleVector2.X *= vTestOptions.LavaLightmapScalar;
                                    lm2d.ScaleVector2.Y *= vTestOptions.LavaLightmapScalar;
                                    lm2d.ScaleVector2.Z *= vTestOptions.LavaLightmapScalar;
                                }
                            }
                            exp.WriteBinary(tcBin);
                            continue; // We will try to use the original lightmaps for this
                        }
                    }
                }

                props.AddOrReplaceProp(new BoolProperty(false, "bUsePreComputedShadows"));
                props.AddOrReplaceProp(new BoolProperty(false, "bBioForcePreComputedShadows"));
                props.AddOrReplaceProp(new BoolProperty(false, "bCastDynamicShadow"));
                //props.AddOrReplaceProp(new BoolProperty(true, "CastShadow"));
                //props.AddOrReplaceProp(new BoolProperty(true, "bAcceptsDynamicDominantLightShadows"));
                props.AddOrReplaceProp(new BoolProperty(true, "bAcceptsLights"));
                props.AddOrReplaceProp(new BoolProperty(false, "bAcceptsDynamicLights"));

                var lightingChannels = props.GetProp<StructProperty>("LightingChannels") ??
                                       new StructProperty("LightingChannelContainer", false,
                                           new BoolProperty(true, "bIsInitialized"))
                                       {
                                           Name = "LightingChannels"
                                       };
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Static"));
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Dynamic"));
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "CompositeDynamic"));
                props.AddOrReplaceProp(lightingChannels);

                exp.WriteProperties(props);
            }

            foreach (ExportEntry exp in Pcc.Exports.Where(exp => exp.IsA("MeshComponent") && exp.Parent.IsA("DynamicSMActor"))) //Make interpactors dynamic
            {
                PropertyCollection props = exp.GetProperties();
                if (props.GetProp<BoolProperty>("bAcceptsLights")?.Value == false ||
                     props.GetProp<BoolProperty>("CastShadow")?.Value == false)
                {
                    // shadows/lighting has been explicitly forbidden, don't mess with it.
                    continue;
                }

                props.AddOrReplaceProp(new BoolProperty(false, "bUsePreComputedShadows"));
                props.AddOrReplaceProp(new BoolProperty(false, "bBioForcePreComputedShadows"));

                var lightingChannels = props.GetProp<StructProperty>("LightingChannels") ??
                                       new StructProperty("LightingChannelContainer", false,
                                           new BoolProperty(true, "bIsInitialized"))
                                       {
                                           Name = "LightingChannels"
                                       };
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Dynamic"));
                props.AddOrReplaceProp(lightingChannels);
                props.AddOrReplaceProp(new BoolProperty(true, "bAcceptsDynamicLights")); // Added in crossgen
                exp.WriteProperties(props);
            }


            foreach (ExportEntry exp in Pcc.Exports.Where(exp => exp.IsA("LightComponent")))
            {
                PropertyCollection props = exp.GetProperties();
                //props.AddOrReplaceProp(new BoolProperty(true, "bCanAffectDynamicPrimitivesOutsideDynamicChannel"));
                //props.AddOrReplaceProp(new BoolProperty(true, "bForceDynamicLight"));

                var lightingChannels = props.GetProp<StructProperty>("LightingChannels") ??
                                       new StructProperty("LightingChannelContainer", false,
                                           new BoolProperty(true, "bIsInitialized"))
                                       {
                                           Name = "LightingChannels"
                                       };
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Static"));
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Dynamic"));
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "CompositeDynamic"));
                props.AddOrReplaceProp(lightingChannels);

                exp.WriteProperties(props);
            }

            if (!silent)
                MessageBox.Show("Done!");
        }

        /// <summary>
        /// Checks if the specified sequence object is contained within a named sequence. Can be used to find sequences that are templated embedded within other different sequences.
        /// </summary>
        /// <param name="sequenceObject"></param>
        /// <param name="seqName"></param>
        /// <param name="fullParentChain"></param>
        /// <returns></returns>
        private static bool IsContainedWithinSequenceNamed(ExportEntry sequenceObject, string seqName, bool fullParentChain = true)
        {
            var parent = SeqTools.GetParentSequence(sequenceObject);
            while (parent != null)
            {
                var parentName = parent.GetProperty<StrProperty>("ObjName");
                if (parentName?.Value == seqName)
                    return true;
                if (!fullParentChain)
                    break;
                parent = SeqTools.GetParentSequence(parent);
            }

            return false;
        }

        private static void SetupMusicIntensity(IMEPackage le1File, string upperFName, VTestOptions vTestOptions)
        {
            foreach (var sequence in le1File.Exports.Where(x => x.ClassName == "Sequence").ToList())
            {
                var seqName = sequence.GetProperty<StrProperty>("ObjName")?.Value;
                if (seqName == "TA_V3_Gametype_Handler")
                {
                    var spawnerDeath = FindSequenceObjectByClassAndPosition(sequence, "SeqEvent_SequenceActivated", -1392, 2824);
                    if (spawnerDeath != null)
                    {
                        InstallVTestHelperSequenceViaEvent(le1File, spawnerDeath.InstancedFullPath, "HelperSequences.MusicIntensityTA", vTestOptions);
                    }
                }
                else if (seqName == "Check_Capping_Completion")
                {
                    // Capping and Survival both have same-named sequence (thanks demiurge!)

                    // CAH
                    var finishedCappingCAH = FindSequenceObjectByClassAndPosition(sequence, "SeqEvent_SequenceActivated", -1024, 2632);
                    if (finishedCappingCAH != null)
                    {
                        InstallVTestHelperSequenceViaEvent(le1File, finishedCappingCAH.InstancedFullPath, "HelperSequences.MusicIntensityCAH", vTestOptions);
                    }


                    // SUR
                    var finishedCappingSUR = FindSequenceObjectByClassAndPosition(sequence, "SeqAct_Log", 2456, 2760);
                    if (finishedCappingSUR != null)
                    {
                        var musicSUR = InstallVTestHelperSequenceViaEvent(le1File, finishedCappingSUR.InstancedFullPath, "HelperSequences.MusicIntensitySUR", vTestOptions);
                        var intensifyWaveIdx = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Int", vTestOptions.cache);
                        switch (upperFName)
                        {
                            // Values here are 1 indexed! So add one to your preferred starting wave
                            case "BIOA_PRC2_CCCAVE_DSG":
                                intensifyWaveIdx.WriteProperty(new IntProperty(4, "IntValue"));
                                break;
                            case "BIOA_PRC2_CCLAVA_DSG":
                                intensifyWaveIdx.WriteProperty(new IntProperty(5, "IntValue"));
                                break;
                            case "BIOA_PRC2_CCTHAI_DSG":
                                intensifyWaveIdx.WriteProperty(new IntProperty(5, "IntValue"));
                                break;
                        }
                        KismetHelper.AddObjectToSequence(intensifyWaveIdx, sequence);
                        KismetHelper.CreateVariableLink(musicSUR, "IntensifyWaveNum", intensifyWaveIdx);
                    }
                }
                else if (seqName == "Vampire_Mode_Handler")
                {
                    var updateWave = FindSequenceObjectByClassAndPosition(sequence, "SeqEvent_SequenceActivated", -2728, 2976);
                    if (updateWave != null)
                    {
                        InstallVTestHelperSequenceViaEvent(le1File, updateWave.InstancedFullPath, "HelperSequences.MusicIntensityVAM", vTestOptions);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a ActivateRemoteEvent kismet object as an output of the specified IFP.
        /// </summary>
        /// <param name="le1File"></param>
        /// <param name="spawnerDeathInstancedFullPath"></param>
        /// <param name="enemykilled"></param>
        /// <param name="vTestOptions"></param>
        private static void InstallRemoteEventSignal(IMEPackage le1File, string sourceIFP, string remoteEventName, VTestOptions vTestOptions, string outlinkName = "Out")
        {
            var source = le1File.FindExport(sourceIFP);
            var sequence = SeqTools.GetParentSequence(source);
            var remoteEvent = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_ActivateRemoteEvent", vTestOptions.cache);
            KismetHelper.AddObjectToSequence(remoteEvent, sequence);
            remoteEvent.WriteProperty(new NameProperty(remoteEventName, "EventName"));
            KismetHelper.CreateOutputLink(source, outlinkName, remoteEvent);
        }

        private static void InstallMusicVolume(IMEPackage le1File, VTestOptions vTestOptions)
        {
            var pl = le1File.FindExport("TheWorld.PersistentLevel");
            var helperMusicVol = vTestOptions.vTestHelperPackage.FindExport("CCMaps.BioMusicVolume_0");
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, helperMusicVol, le1File, pl, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var musicVolEntry);

            var musicVol = musicVolEntry as ExportEntry;
            var fileName = Path.GetFileNameWithoutExtension(le1File.FilePath).ToUpper();
            int soundState = 0; // The column in the 2DA to use (for the soundque)
            bool hasIntensity2 = false;
            bool hasIntensity3 = false; // Left in case we ever decide to use it. Maybe on ahern?
            switch (fileName)
            {
                case "BIOA_PRC2_CCTHAI_SND":
                    musicVol.WriteProperty(new NameProperty("CrossGen_Mus_Thai", "MusicID")); // Virmire Ride
                    PathEdUtils.SetLocation(musicVol, 1040, -28200, -2000);
                    hasIntensity2 = true;
                    break;
                case "BIOA_PRC2_CCLAVA_SND":
                    musicVol.WriteProperty(new NameProperty("CrossGen_Mus_Lava", "MusicID")); // Virmire Ride
                    PathEdUtils.SetLocation(musicVol, 28420, -26932, -26858);
                    hasIntensity2 = true;
                    break;
                case "BIOA_PRC2_CCCRATE_SND":
                    musicVol.WriteProperty(new NameProperty("CrossGen_Mus_Crate", "MusicID")); // Virmire Ride
                    PathEdUtils.SetLocation(musicVol, 15783, -27067, -5491);
                    // No intensity 2 as this map is < 2 minutes in length
                    break;
                case "BIOA_PRC2_CCCAVE_SND":
                    musicVol.WriteProperty(new NameProperty("CrossGen_Mus_Cave", "MusicID")); // Virmire Ride
                    PathEdUtils.SetLocation(musicVol, -16480, -28456, -2614);
                    hasIntensity2 = true;
                    break;
                case "BIOA_PRC2_CCAHERN_SND":
                    musicVol.WriteProperty(new NameProperty("CrossGen_Mus_Ahern", "MusicID")); // Virmire Ride
                    PathEdUtils.SetLocation(musicVol, -41129, -27013, -2679);
                    hasIntensity2 = true;
                    break;
            }

            // Install model references
            var model = le1File.Exports.First(x => x.ClassName == "Model"); // Every level file will have this in porting
            musicVol.WriteProperty(new ObjectProperty(model, "Brush"));
            le1File.FindExport("TheWorld.PersistentLevel.BioMusicVolume_0.BrushComponent_9").WriteProperty(new ObjectProperty(model, "Brush"));


            // Install sequencing to turn music on and off
            // Plot check?

            var sequence = le1File.FindExport("TheWorld.PersistentLevel.Main_Sequence");

            var startMusicEvt = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_RemoteEvent", vTestOptions.cache);
            var stopMusicEvt = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_RemoteEvent", vTestOptions.cache);
            var plotCheck = SequenceObjectCreator.CreateSequenceObject(le1File, "BioSeqAct_PMCheckState", vTestOptions.cache);
            var musOn = SequenceObjectCreator.CreateSequenceObject(le1File, "BioSeqAct_MusicVolumeEnable", vTestOptions.cache);
            var musOff = SequenceObjectCreator.CreateSequenceObject(le1File, "BioSeqAct_MusicVolumeDisable", vTestOptions.cache);
            var musVolSeqObj = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Object", vTestOptions.cache);

            var stateBeingSet = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Int", vTestOptions.cache);
            var musicStatePlotInt = SequenceObjectCreator.CreateSequenceObject(le1File, "BioSeqVar_StoryManagerInt", vTestOptions.cache);
            var setInt = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_SetInt", vTestOptions.cache);

            startMusicEvt.WriteProperty(new NameProperty("StartSimMusic", "EventName"));
            stopMusicEvt.WriteProperty(new NameProperty("StopSimMusic", "EventName"));
            musVolSeqObj.WriteProperty(new ObjectProperty(musicVol, "ObjValue"));

            // Sequencing
            KismetHelper.AddObjectsToSequence(sequence, false, startMusicEvt, stopMusicEvt, plotCheck, musOn, musOff, musVolSeqObj, stateBeingSet, musicStatePlotInt, setInt);

            KismetHelper.CreateOutputLink(startMusicEvt, "Out", plotCheck);
            KismetHelper.CreateOutputLink(plotCheck, "False", setInt); // CHANGE TO musOff IN FINAL BUILD
            KismetHelper.CreateOutputLink(setInt, "Out", musOn);
            KismetHelper.CreateOutputLink(stopMusicEvt, "Out", musOff);


            KismetHelper.CreateVariableLink(musOn, "Music Volume", musVolSeqObj);
            KismetHelper.CreateVariableLink(musOff, "Music Volume", musVolSeqObj);

            KismetHelper.CreateVariableLink(setInt, "Target", musicStatePlotInt);
            KismetHelper.CreateVariableLink(setInt, "Value", stateBeingSet);

            // Music bool
            KismetHelper.SetComment(plotCheck, "Music is disabled?");
            plotCheck.WriteProperty(new IntProperty(7657, "m_nIndex"));

            // Setup SetInt values
            stateBeingSet.WriteProperty(new IntProperty(soundState, "IntValue"));
            musicStatePlotInt.WriteProperty(new IntProperty(74, "m_nIndex")); // Global Soundstate (2DA columns)
            musicStatePlotInt.WriteProperty(new StrProperty("CurrentMusicState", "m_sRefName"));
            musicStatePlotInt.WriteProperty(new EnumProperty("None", "EBioRegionAutoSet", MEGame.LE1, "Region"));

            // Intensities
            var intensity2 = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Int", vTestOptions.cache);
            var intensity3 = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqVar_Int", vTestOptions.cache);

            intensity2.WriteProperty(new IntProperty(1, "IntValue")); // 0 indexed
            intensity3.WriteProperty(new IntProperty(2, "IntValue")); // 0 indexed

            var evtIntensity2 = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_RemoteEvent", vTestOptions.cache);
            var evtIntensity3 = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_RemoteEvent", vTestOptions.cache);
            evtIntensity2.WriteProperty(new NameProperty("MusicIntensity2", "EventName"));
            evtIntensity3.WriteProperty(new NameProperty("MusicIntensity3", "EventName"));

            var setInt2 = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_SetInt", vTestOptions.cache);
            var setInt3 = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_SetInt", vTestOptions.cache);

            KismetHelper.AddObjectsToSequence(sequence, false, intensity2, intensity3, evtIntensity2, evtIntensity3, setInt2, setInt3);
            if (hasIntensity2)
            {
                KismetHelper.CreateOutputLink(evtIntensity2, "Out", setInt2);
            }

            if (hasIntensity3)
            {
                KismetHelper.CreateOutputLink(evtIntensity3, "Out", setInt3);
            }

            KismetHelper.CreateVariableLink(setInt2, "Target", musicStatePlotInt);
            KismetHelper.CreateVariableLink(setInt3, "Target", musicStatePlotInt);

            KismetHelper.CreateVariableLink(setInt2, "Value", intensity2);
            KismetHelper.CreateVariableLink(setInt3, "Value", intensity3);

            // DEBUG
            if (vTestOptions.debugBuild)
            {
                var touch = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqEvent_Touch", vTestOptions.cache);
                KismetHelper.AddObjectToSequence(touch, sequence);
                touch.WriteProperty(new ObjectProperty(musicVol, "Originator"));

                var touchLog = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_Log", vTestOptions.cache);
                KismetHelper.AddObjectToSequence(touchLog, sequence);
                KismetHelper.SetComment(touchLog, "Touched Music Volume");

                var untouchLog = SequenceObjectCreator.CreateSequenceObject(le1File, "SeqAct_Log", vTestOptions.cache);
                KismetHelper.AddObjectToSequence(untouchLog, sequence);
                KismetHelper.SetComment(untouchLog, "UnTouched Music Volume");

                KismetHelper.CreateOutputLink(touch, "Touched", touchLog);
                KismetHelper.CreateOutputLink(touch, "UnTouched", untouchLog);
            }
        }

        /// <summary>
        /// Changes sequencing a bit to install a force-load of mips plus a delay
        /// </summary>
        /// <param name="findSequenceObjectByClassAndPosition"></param>
        private static void FixSimMapTextureLoading(ExportEntry startDelay, VTestOptions vTestOptions, ExportEntry streamingLocation = null)
        {
            var sequence = SeqTools.GetParentSequence(startDelay);
            var stopLoadingMovie = FindSequenceObjectByClassAndPosition(sequence, "BioSeqAct_StopLoadingMovie");
            KismetHelper.RemoveOutputLinks(startDelay);

            var streamInTextures = SequenceObjectCreator.CreateSequenceObject(startDelay.FileRef, "SeqAct_StreamInTextures", vTestOptions.cache);
            var streamInDelay = SequenceObjectCreator.CreateSequenceObject(startDelay.FileRef, "SeqAct_Delay", vTestOptions.cache);
            var remoteEventStreamIn = SequenceObjectCreator.CreateSequenceObject(startDelay.FileRef, "SeqAct_ActivateRemoteEvent", vTestOptions.cache);

            KismetHelper.AddObjectToSequence(remoteEventStreamIn, sequence);
            KismetHelper.AddObjectToSequence(streamInTextures, sequence);
            KismetHelper.AddObjectToSequence(streamInDelay, sequence);

            streamInDelay.WriteProperty(new FloatProperty(2.5f, "Duration")); // Load screen will be 2.5s
            streamInTextures.WriteProperty(new FloatProperty(5f, "Seconds")); // Force textures to stream in at full res for a bit over the load screen time
            remoteEventStreamIn.WriteProperty(new NameProperty("CROSSGEN_PrepTextures", "EventName")); // This is used to signal other listeners that they should also stream in textures

            streamingLocation ??= KismetHelper.GetSequenceObjects(sequence).OfType<ExportEntry>().First(x => x.ClassName == "SeqVar_External" && x.GetProperty<StrProperty>("VariableLabel")?.Value == "Scenario_Start_Location");
            KismetHelper.CreateVariableLink(streamInTextures, "Location", streamingLocation);

            KismetHelper.CreateOutputLink(startDelay, "Finished", remoteEventStreamIn); // Initial 1 frame delay to event signal
            KismetHelper.CreateOutputLink(remoteEventStreamIn, "Out", streamInTextures); // Event Signal to StreamInTextures
            KismetHelper.CreateOutputLink(remoteEventStreamIn, "Out", streamInDelay); // Event Signal to Loading Screen Delay
            KismetHelper.CreateOutputLink(streamInDelay, "Finished", stopLoadingMovie); // Loading Screen Delay to Stop Loading Movie
        }

        /// <summary>
        /// Sets up sequencing to stream in the listed materials for 5 seconds in the specified stream
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="materialsToStreamIn"></param>
        private static void CreateSignaledTextureStreaming(ExportEntry sequence, string[] materialsToStreamIn, VTestOptions vTestOptions)
        {

            var remoteEvent = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqEvent_RemoteEvent", vTestOptions.cache);
            var streamInTextures = SequenceObjectCreator.CreateSequenceObject(sequence.FileRef, "SeqAct_StreamInTextures", vTestOptions.cache);

            KismetHelper.AddObjectToSequence(remoteEvent, sequence);
            KismetHelper.AddObjectToSequence(streamInTextures, sequence);

            streamInTextures.WriteProperty(new FloatProperty(5f, "Seconds")); // Force textures to stream in at full res for a bit over the load screen time
            var materials = new ArrayProperty<ObjectProperty>("ForceMaterials");
            foreach (var matIFP in materialsToStreamIn)
            {
                var entry = sequence.FileRef.FindEntry(matIFP);
                if (entry == null) Debugger.Break(); // THIS SHOULDN'T HAPPEN
                materials.Add(new ObjectProperty(entry));
            }
            streamInTextures.WriteProperty(materials);

            remoteEvent.WriteProperty(new NameProperty("CROSSGEN_PrepTextures", "EventName"));

            KismetHelper.CreateOutputLink(remoteEvent, "Out", streamInTextures);

        }

        private static ExportEntry FindSequenceObjectByClassAndPosition(ExportEntry sequence, string className, int posX = int.MinValue, int posY = int.MinValue)
        {
            var seqObjs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects")
                .Select(x => x.ResolveToEntry(sequence.FileRef)).OfType<ExportEntry>().Where(x => x.ClassName == className).ToList();

            foreach (var obj in seqObjs)
            {
                if (posX != int.MinValue && posY != int.MinValue)
                {
                    var props = obj.GetProperties();
                    var foundPosX = props.GetProp<IntProperty>("ObjPosX")?.Value;
                    var foundPosY = props.GetProp<IntProperty>("ObjPosY")?.Value;
                    if (foundPosX != null && foundPosY != null &&
                        foundPosX == posX && foundPosY == posY)
                    {
                        return obj;
                    }
                }
                else if (seqObjs.Count == 1)
                {
                    return obj; // First object
                }
                else
                {
                    throw new Exception($"COULD NOT FIND OBJECT OF TYPE {className} in {sequence.InstancedFullPath}");
                }
            }

            return null;
        }

        private static void ReinstateCoverSlots(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            var coverLinks = le1File.Exports.Where(x => x.ClassName == "CoverLink");
            foreach (var le1CoverLink in coverLinks)
            {
                var me1CoverLink = me1File.FindExport(le1CoverLink.InstancedFullPath);
                var me1Slots = me1CoverLink.GetProperty<ArrayProperty<StructProperty>>("Slots");
                if (me1Slots != null && me1Slots.Any())
                {
                    var le1Slots = new ArrayProperty<StructProperty>("Slots");

                    foreach (var slot in me1Slots)
                    {
                        le1Slots.Add(ConvertCoverSlot(slot, me1File, le1File, vTestOptions));
                    }

                    le1CoverLink.WriteProperty(le1Slots);
                    //le1File.Save();
                }
            }
        }

        private static void PortInCorrectedTerrain(IMEPackage me1File, IMEPackage le1File, string vTestIFP, string materialsFile, VTestOptions vTestOptions)
        {
            // Port in the material's file terrain - but not it's subcomponents
            using var le1VanillaTerrainP = MEPackageHandler.OpenMEPackage(Path.Combine(LE1Directory.CookedPCPath, materialsFile));
            var le1DonorTerrain = le1VanillaTerrainP.Exports.FirstOrDefault(x => x.ClassName == "Terrain");
            le1DonorTerrain.RemoveProperty("TerrainComponents");

            var rop = new RelinkerOptionsPackage() { Cache = vTestOptions.cache };
            var le1TerrainBin = ObjectBinary.From<Terrain>(le1DonorTerrain);
            le1TerrainBin.WeightedTextureMaps = new UIndex[0]; // These don't work with our different data format for these maps
            le1DonorTerrain.WriteBinary(le1TerrainBin);

            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, le1DonorTerrain, le1File,
                le1File.FindExport("TheWorld.PersistentLevel"), true, rop, out var destTerrainEntry);
            var destTerrain = destTerrainEntry as ExportEntry;

            // Port in the precomputed components
            var me1Terrain = me1File.Exports.First(x => x.ClassName == "Terrain");
            var sourceTerrain = vTestOptions.vTestHelperPackage.FindExport(vTestIFP);
            var le1DonorTerrainComponents = sourceTerrain.GetProperty<ArrayProperty<ObjectProperty>>("TerrainComponents");
            var me1TerrainComponents = me1Terrain.GetProperty<ArrayProperty<ObjectProperty>>("TerrainComponents");
            ArrayProperty<ObjectProperty> components = new ArrayProperty<ObjectProperty>("TerrainComponents");
            for (int i = 0; i < me1TerrainComponents.Count; i++)
            {
                var me1SubComp = me1TerrainComponents[i].ResolveToEntry(me1File) as ExportEntry;
                var le1SubComp = le1DonorTerrainComponents[i].ResolveToEntry(le1File) as ExportEntry;
                rop.CrossPackageMap.Clear();
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, vTestOptions.vTestHelperPackage.GetUExport(le1SubComp.UIndex), le1File,
                    destTerrain, true, rop, out var newSubComp);
                components.Add(new ObjectProperty(newSubComp.UIndex));
                var portedTC = newSubComp as ExportEntry;
                if (vTestOptions.portTerrainLightmaps /*&& !le1File.FilePath.Contains("LAVA")*/) // DO NOT PORT LAVA RIGHT NOW AS ITS LIGHTMAP IS BROKEN. ONLY PORT AHERN'S
                {
                    // Install the original lightmaps
                    var me1TC = ObjectBinary.From<TerrainComponent>(me1SubComp);
                    var le1TC = ObjectBinary.From<TerrainComponent>(portedTC);

                    if (me1TC.LightMap is LightMap_2D lm2d)
                    {
                        le1TC.LightMap = me1TC.LightMap;
                        var le1LM = le1TC.LightMap as LightMap_2D; // This is same ref, I suppose...
                        // Port textures
                        if (lm2d.Texture1 > 0)
                        {
                            EntryExporter.ExportExportToPackage(me1File.GetUExport(lm2d.Texture1.value), le1File, out var tex1, vTestOptions.cache);
                            le1LM.Texture1 = tex1.UIndex;
                        }
                        if (lm2d.Texture2 > 0)
                        {
                            EntryExporter.ExportExportToPackage(me1File.GetUExport(lm2d.Texture2.value), le1File, out var tex2, vTestOptions.cache);
                            le1LM.Texture2 = tex2.UIndex;
                        }
                        if (lm2d.Texture3 > 0)
                        {
                            EntryExporter.ExportExportToPackage(me1File.GetUExport(lm2d.Texture3.value), le1File, out var tex3, vTestOptions.cache);
                            le1LM.Texture3 = tex3.UIndex;
                        }
                        if (lm2d.Texture4 > 0)
                        {
                            EntryExporter.ExportExportToPackage(me1File.GetUExport(lm2d.Texture4.value), le1File, out var tex4, vTestOptions.cache);
                            le1LM.Texture4 = tex4.UIndex;
                        }
                    }

                    portedTC.WriteBinary(le1TC);
                }

                // Port over component properties
                var propertiesME1 = me1SubComp.GetProperties();
                var shadowMaps = propertiesME1.GetProp<ArrayProperty<ObjectProperty>>("ShadowMaps");
                propertiesME1.RemoveNamedProperty("ShadowMaps");

                if (shadowMaps != null && shadowMaps.Any())
                {
                    var newShadowMaps = new ArrayProperty<ObjectProperty>("ShadowMaps");
                    // We need to port in shadowmaps.
                    foreach (var me1ShadowMap in shadowMaps)
                    {
                        EntryExporter.ExportExportToPackage(me1File.GetUExport(me1ShadowMap.Value), le1File, out var portedSMEntry, vTestOptions.cache);
                        portedSMEntry.idxLink = newSubComp.UIndex; // Move under the component like it does in LE1
                        newShadowMaps.Add(new ObjectProperty(portedSMEntry));
                    }
                    propertiesME1.Add(newShadowMaps);
                }

                portedTC.WriteProperties(propertiesME1); // The original game has no object refs
                //foreach (var prop in propertiesME1.Where(x => x is ArrayProperty<StructProperty> or BoolProperty))
                //{
                //    portedTC.WriteProperty(prop); // Irrelevant lights, some lighting bools
                //}
            }
            destTerrain.WriteProperty(components);

            // Manual fixes for VTest
            destTerrain.RemoveProperty("PrePivot"); // on lav60 donor terrain

            // Update the main terrain with our data, without touching anything about materials or layers
            PackageEditorExperimentsM.ImportUDKTerrainData(sourceTerrain, destTerrain, false);
        }

        private static void FixPinkVisorMaterial(IMEPackage package)
        {
            ExportEntry visorMatInstance = package.FindExport(@"BIOG_HMM_HGR_HVY_R.BRT.HMM_BRT_HVYa_MAT_1a");
            if (visorMatInstance is not null)
            {
                var vectorParameterValues = visorMatInstance.GetProperty<ArrayProperty<StructProperty>>("VectorParameterValues");
                foreach (var param in vectorParameterValues.Values)
                {
                    var name = param.GetProp<NameProperty>("ParameterName").Value;
                    if (name == "HGR_Colour_01")
                    {
                        param.Properties.AddOrReplaceProp(MakeLinearColorStruct("ParameterValue", 0.07058824f, 0.08235294f, 0.09019608f, 0));
                    }
                    else if (name == "HGR_Colour_02")
                    {
                        param.Properties.AddOrReplaceProp(MakeLinearColorStruct("ParameterValue", 0.05882353f, 0.07058824f, 0.08235294f, 0));
                    }
                }
                visorMatInstance.WriteProperty(vectorParameterValues);
            }
        }


        private static void CorrectTextures(IMEPackage package)
        {
            foreach (var exp in package.Exports.Where(x => x.IsTexture()))
            {
                var props = exp.GetProperties();
                var texinfo = ObjectBinary.From(exp) as UTexture2D;
                var numMips = texinfo.Mips.Count;
                var ns = props.GetProp<BoolProperty>("NeverStream");
                int lowMipCount = 0;
                for (int i = numMips - 1; i >= 0; i--)
                {
                    if (lowMipCount > 6 && (ns == null || ns.Value == false) && texinfo.Mips[i].IsLocallyStored && texinfo.Mips[i].StorageType != StorageTypes.empty)
                    {
                        exp.WriteProperty(new BoolProperty(true, "NeverStream"));
                        lowMipCount = -100; // This prevents this block from running again
                    }

                    if (texinfo.Mips[i].StorageType == StorageTypes.empty && exp.Parent.ClassName != "TextureCube")
                    {
                        // Strip this empty mip
                        //Debug.WriteLine($"Dropping empty mip {i} in {exp.InstancedFullPath}");
                        //texinfo.Mips.RemoveAt(i);
                    }

                    lowMipCount++;
                }
                exp.WriteBinary(texinfo);

                // Correct the MipTailBaseIdx. It's an indexer thing so it starts at 0
                //exp.WriteProperty(new IntProperty(texinfo.Mips.Count - 1, "MipTailBaseIdx"));

                // Correct the size. Is this required?

            }
        }

        private static void CorrectSequences(IMEPackage le1File, VTestOptions vTestOptions)
        {
            // Find sequences that aren't in other sequences
            foreach (var seq in le1File.Exports.Where(e => e is { ClassName: "Sequence" } && !e.Parent.IsA("SequenceObject")))
            {
                CorrectSequenceObjects(seq, vTestOptions);
            }
        }


        public static void RebuildPersistentLevelChildren(ExportEntry pl, VTestOptions vTestOptions)
        {
            ExportEntry[] actorsToAdd = pl.FileRef.Exports.Where(exp => exp.Parent == pl && exp.IsA("Actor")).ToArray();
            Level level = ObjectBinary.From<Level>(pl);
            level.Actors.Clear();
            foreach (var actor in actorsToAdd)
            {
                if (vTestOptions != null && (!vTestOptions.debugBuild || !vTestOptions.debugConvertStaticLightingToNonStatic))
                {
                    // Don't add things that are in collection actors. 
                    // In a debug build we want to not use them in a collection actor
                    // so that they are not static.
                    var lc = actor.GetProperty<ObjectProperty>("LightComponent");
                    if (lc != null && pl.FileRef.TryGetUExport(lc.Value, out var lightComp))
                    {
                        if (lightComp.Parent != null && lightComp.Parent.ClassName == "StaticLightCollectionActor")
                            continue; // don't add this one
                    }
                }
                else if (vTestOptions != null && vTestOptions.debugBuild && vTestOptions.debugConvertStaticLightingToNonStatic && actor.ClassName == "StaticLightCollectionActor")
                {
                    continue; // Debug builds with debugConvertStaticLightingToNonStatic don't add StaticLightCollectionActor, instead porting over the lights individually.
                }

                level.Actors.Add(new UIndex(actor.UIndex));
            }

            //if (level.Actors.Count > 1)
            //{

            // BioWorldInfo will always be present
            // or at least, it better be!
            // Slot 2 has to be blank in LE. In ME1 i guess it was a brush.
            level.Actors.Insert(1, new UIndex(0)); // This is stupid
                                                   //}

            pl.WriteBinary(level);
        }

        private static void CorrectSequenceObjects(ExportEntry seq, VTestOptions vTestOptions)
        {
            // Set ObjInstanceVersions to LE value
            if (seq.IsA("SequenceObject"))
            {
                if (LE1UnrealObjectInfo.SequenceObjects.TryGetValue(seq.ClassName, out var soi))
                {
                    seq.WriteProperty(new IntProperty(soi.ObjInstanceVersion, "ObjInstanceVersion"));
                }
                else
                {
                    Debug.WriteLine($"SequenceCorrection: Didn't correct {seq.UIndex} {seq.ObjectName}, not in LE1 ObjectInfo SequenceObjects");
                }

                var children = seq.GetChildren();
                foreach (var child in children)
                {
                    if (child is ExportEntry chExp)
                    {
                        CorrectSequenceObjects(chExp, vTestOptions);
                    }
                }
            }

            // Fix extra four bytes after SeqAct_Interp
            if (seq.ClassName == "SeqAct_Interp")
            {
                seq.WriteBinary(Array.Empty<byte>());
            }

            if (seq.ClassName == "SeqAct_SetInt")
            {
                seq.WriteProperty(new BoolProperty(true, "bIsUpdated"));
            }


            // Fix missing PropertyNames on VariableLinks
            if (seq.IsA("SequenceOp"))
            {
                var varLinks = seq.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
                if (varLinks is null) return;
                foreach (var t in varLinks.Values)
                {
                    string desc = t.GetProp<StrProperty>("LinkDesc").Value;

                    if (desc == "Target" && seq.ClassName == "SeqAct_SetBool")
                    {
                        t.Properties.AddOrReplaceProp(new NameProperty("Target", "PropertyName"));
                    }

                    if (desc == "Value" && seq.ClassName == "SeqAct_SetInt")
                    {
                        t.Properties.AddOrReplaceProp(new NameProperty("Values", "PropertyName"));
                    }
                }

                seq.WriteProperty(varLinks);
            }
        }

        private static Guid? tempDonorGuid = null;
        private static void CorrectTerrainMaterials(IMEPackage le1File)
        {
            // Todo: Improve this... somehow
            if (tempDonorGuid == null)
            {
                using var donorMatP = MEPackageHandler.OpenMEPackage(Path.Combine(LE1Directory.CookedPCPath, "BIOA_PRO10_11_LAY.pcc"));
                var terrain = donorMatP.FindExport("TheWorld.PersistentLevel.Terrain_0");
                var terrbinD = ObjectBinary.From<Terrain>(terrain);
                tempDonorGuid = terrbinD.CachedTerrainMaterials[0].ID;
            }

            var fname = Path.GetFileNameWithoutExtension(le1File.FilePath);
            var terrains = le1File.Exports.Where(x => x.ClassName == "Terrain").ToList();
            foreach (var terrain in terrains)
            {
                var terrbin = ObjectBinary.From<Terrain>(terrain);

                foreach (var terrainMat in terrbin.CachedTerrainMaterials)
                {
                    terrainMat.ID = tempDonorGuid.Value;
                }

                terrain.WriteBinary(terrbin);
            }
        }

        // ME1 -> LE1 Prefab's Sequence class was changed to a subclass. No different props though.
        private static void CorrectPrefabSequenceClass(IMEPackage le1File)
        {
            foreach (var le1Exp in le1File.Exports)
            {
                if (le1Exp.IsA("Prefab"))
                {
                    var prefabSeqObj = le1Exp.GetProperty<ObjectProperty>("PrefabSequence");
                    if (prefabSeqObj != null && prefabSeqObj.ResolveToEntry(le1File) is ExportEntry export)
                    {
                        var prefabSeqClass = le1File.FindImport("Engine.PrefabSequence");
                        if (prefabSeqClass == null)
                        {
                            var seqClass = le1File.FindImport("Engine.Sequence");
                            prefabSeqClass = new ImportEntry(le1File, seqClass.Parent?.UIndex ?? 0, "PrefabSequence") { PackageFile = seqClass.PackageFile, ClassName = "Class" };
                            le1File.AddImport(prefabSeqClass);
                        }

                        Debug.WriteLine($"Corrected Sequence -> PrefabSequence class type for {le1Exp.InstancedFullPath}");
                        export.Class = prefabSeqClass;
                    }
                }
                else if (le1Exp.IsA("PrefabInstance"))
                {
                    var seq = le1Exp.GetProperty<ObjectProperty>("SequenceInstance")?.ResolveToEntry(le1File) as ExportEntry;
                    if (seq != null && seq.ClassName == "Sequence")
                    {
                        var prefabSeqClass = le1File.FindImport("Engine.PrefabSequence");
                        if (prefabSeqClass == null)
                        {
                            var seqClass = le1File.FindImport("Engine.Sequence");
                            prefabSeqClass = new ImportEntry(le1File, seqClass.Parent?.UIndex ?? 0, "PrefabSequence") { PackageFile = seqClass.PackageFile, ClassName = "Class" };
                            le1File.AddImport(prefabSeqClass);
                        }

                        Debug.WriteLine($"Corrected Sequence -> PrefabSequence class type for {le1Exp.InstancedFullPath}");
                        seq.Class = prefabSeqClass;
                    }
                }
            }
        }

        private static void CorrectPathfindingNetwork(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            var le1PL = le1File.FindExport("TheWorld.PersistentLevel");
            if (le1PL == null)
                return; // This file doesn't have a level
            Level me1L = ObjectBinary.From<Level>(me1File.FindExport("TheWorld.PersistentLevel"));
            Level le1L = ObjectBinary.From<Level>(le1PL);

            //PropertyCollection mcs = new PropertyCollection();
            //mcs.AddOrReplaceProp(new FloatProperty(400, "Radius"));
            //mcs.AddOrReplaceProp(new FloatProperty(400, "Height"));
            //StructProperty maxPathSize = new StructProperty("Cylinder", mcs, "MaxPathSize");

            // NavList Chain start and end
            if (me1L.NavListEnd.value != 0 && le1File.FindExport(me1File.GetUExport(me1L.NavListEnd.value).InstancedFullPath) is { } matchingNavEnd)
            {
                le1L.NavListEnd = new UIndex(matchingNavEnd.UIndex);

                if (me1L.NavListStart.value != 0 && le1File.FindExport(me1File.GetUExport(me1L.NavListStart.value).InstancedFullPath) is { } matchingNavStart)
                {
                    le1L.NavListStart = new UIndex(matchingNavStart.UIndex);
                    while (matchingNavStart != null)
                    {
                        int uindex = matchingNavStart.UIndex;
                        var props = matchingNavStart.GetProperties();
                        //props.AddOrReplaceProp(maxPathSize);
                        var next = props.GetProp<ObjectProperty>("nextNavigationPoint");
                        //matchingNavStart.WriteProperties(props);
                        matchingNavStart = next?.ResolveToEntry(le1File) as ExportEntry;
                        if (matchingNavStart == null && uindex != matchingNavEnd.UIndex)
                        {
                            Debugger.Break();
                        }
                    }
                }
            }

            // CoverList Chain start and end
            if (me1L.CoverListEnd.value != 0 && le1File.FindExport(me1File.GetUExport(me1L.CoverListEnd.value).InstancedFullPath) is { } matchingCoverEnd)
            {
                le1L.CoverListEnd = new UIndex(matchingCoverEnd.UIndex);

                if (me1L.CoverListStart.value != 0 && le1File.FindExport(me1File.GetUExport(me1L.CoverListStart.value).InstancedFullPath) is { } matchingCoverStart)
                {
                    le1L.CoverListStart = new UIndex(matchingCoverStart.UIndex);
                    while (matchingCoverStart != null)
                    {
                        int uindex = matchingCoverStart.UIndex;
                        var props = matchingCoverStart.GetProperties();
                        //props.AddOrReplaceProp(maxPathSize);
                        var next = props.GetProp<ObjectProperty>("NextCoverLink");
                        //matchingNavStart.WriteProperties(props);
                        matchingCoverStart = next?.ResolveToEntry(le1File) as ExportEntry;
                        if (matchingCoverStart == null && uindex != matchingCoverEnd.UIndex)
                        {
                            Debugger.Break();
                        }
                    }
                }
            }

            // Cross level actors
            foreach (var exportIdx in me1L.CrossLevelActors)
            {
                var me1E = me1File.GetUExport(exportIdx.value);
                if (le1File.FindExport(me1E.InstancedFullPath) is { } crossLevelActor)
                {
                    le1L.CrossLevelActors.Add(new UIndex(crossLevelActor.UIndex));
                }
            }

            // Regenerate the 'End' struct cause it will have ported wrong
            CorrectReachSpecs(me1File, le1File);
            CorrectBioWaypointSet(me1File, le1File, vTestOptions); // Has NavReference -> ActorReference
            le1PL.WriteBinary(le1L);
        }

        /// <summary>
        /// Corrects UDK/ME1/ME2 reachspec system to ME3 / LE
        /// </summary>
        /// <param name="me1File"></param>
        /// <param name="le1File"></param>
        public static void CorrectReachSpecs(IMEPackage me1File, IMEPackage le1File)
        {

            // Have to do LE1 -> ME1 for references as not all reachspecs may have been ported
            foreach (var le1Exp in le1File.Exports.Where(x => x.IsA("ReachSpec")))
            {
                var le1End = le1Exp.GetProperty<StructProperty>("End");
                if (le1End != null)
                {
                    var me1Exp = me1File.FindExport(le1Exp.InstancedFullPath);
                    var me1End = me1Exp.GetProperty<StructProperty>("End");
                    var le1Props = le1Exp.GetProperties();
                    le1Props.RemoveNamedProperty("End");

                    PropertyCollection newEnd = new PropertyCollection();
                    newEnd.Add(me1End.GetProp<StructProperty>("Guid"));

                    var me1EndEntry = me1End.GetProp<ObjectProperty>("Nav");
                    me1EndEntry ??= me1End.GetProp<ObjectProperty>("Actor"); // UDK uses 'Actor' but it's in wrong position
                    if (me1EndEntry != null)
                    {
                        newEnd.Add(new ObjectProperty(le1File.FindExport(me1File.GetUExport(me1EndEntry.Value).InstancedFullPath).UIndex, "Actor"));
                    }
                    else
                    {
                        newEnd.Add(new ObjectProperty(0, "Actor")); // This is probably cross level or end of chain
                    }

                    StructProperty nes = new StructProperty("ActorReference", newEnd, "End", true);
                    le1Props.AddOrReplaceProp(nes);
                    le1Exp.WriteProperties(le1Props);
                    le1Exp.WriteBinary(new byte[0]); // When porting from UDK there's some binary data. This removes it

                    // Test properties
                    le1Exp.GetProperties();
                }
            }
        }

        public static void CorrectBioWaypointSet(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            foreach (var lbwps in le1File.Exports.Where(x => x.ClassName == "BioWaypointSet"))
            {
                var matchingMe1 = me1File.FindExport(lbwps.InstancedFullPath);
                var mWaypointRefs = matchingMe1.GetProperty<ArrayProperty<StructProperty>>("WaypointReferences");
                var lWaypointRefs = lbwps.GetProperty<ArrayProperty<StructProperty>>("WaypointReferences");
                lWaypointRefs.Clear(); // We're going to reconstruct these

                foreach (var mWay in mWaypointRefs)
                {
                    var le1Props = GlobalUnrealObjectInfo.getDefaultStructValue(MEGame.LE1, "ActorReference", true, vTestOptions.cache);
                    ConvertNavRefToActorRef(mWay, le1Props, me1File, le1File, vTestOptions);
                    lWaypointRefs.Add(new StructProperty("ActorReference", le1Props, isImmutable: true));
                }
                lbwps.WriteProperty(lWaypointRefs);
            }
        }

        /// <summary>
        /// Unlocks the Ahern Mission early on Debug builds for testing purposes
        /// </summary>
        /// <param name="le1File"></param>
        /// <param name="vTestOptions"></param>
        public static void DebugUnlockAhernMission(IMEPackage le1File, VTestOptions vTestOptions)
        {
            if (vTestOptions.debugBuild && le1File.FindExport("prc2_ochren_D.prc2_ochren_dlg") is { } conversation)
            {
                var replies = conversation.GetProperty<ArrayProperty<StructProperty>>("m_ReplyList");
                replies[100].Properties.AddOrReplaceProp(new IntProperty(-1, "nConditionalFunc"));
                replies[108].Properties.AddOrReplaceProp(new IntProperty(-1, "nConditionalFunc"));
                Debug.WriteLine($"Unlocking Ahern Mission in Ochren Conversation in file {le1File.FileNameNoExtension}");
                conversation.WriteProperty(replies);
            }
        }

        /// <summary>
        /// Changes weapon references in Geth 2DAs to use our new weapon entries that include HoloWipe VFX. Needs proper 2DA to work.
        /// </summary>
        /// <param name="le1File"></param>
        /// <param name="vTestOptions"></param>
        private static void CorrectGethEquipment2DAs(IMEPackage le1File, VTestOptions vTestOptions)
        {
            var tables = le1File.Exports.Where(e =>
                e.ClassName == "Bio2DANumberedRows" && e.ParentName == "Geth" &&
                e.ObjectName.ToString().Contains("_Equipment"));

            foreach (var t in tables)
            {
                var binary = ObjectBinary.From<Bio2DABinary>(t);
                var initial = binary.Cells[0].IntValue;
                binary.Cells[0].IntValue = initial switch
                {
                    22 => 630,
                    23 => 631,
                    423 => 632,
                    519 => 633,
                    _ => initial
                };
                t.WriteBinary(binary);
                t.ObjectName = "VTEST_" + t.ObjectName;
            }
        }
        #endregion

        #region QA Methods
        public static void VTest_CheckFile(IMEPackage package, VTestOptions vTestOptions)
        {
            #region Check Level has at least 2 actors
            var level = package.FindExport("TheWorld.PersistentLevel");
            {
                if (level != null)
                {
                    var levelBin = ObjectBinary.From<Level>(level);
                    if (levelBin.Actors.Count < 2)
                        Debugger.Break(); // THIS SHOULD NOT OCCUR OR GAME WILL DIE
                                          //Debug.WriteLine($"{Path.GetFileName(package.FilePath)} actor list count: {levelBin.Actors.Count}");
                }
            }

            VTestCheckImports(package, vTestOptions);
            VTestCheckTextures(package, vTestOptions);

            #endregion

        }
        #endregion
    }
}
