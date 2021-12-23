﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Shaders
{
    /*
     * This class is for reading each game's global shader cache. Because the ShaderCache in those files is so large,
     * parsing it with the ShaderCache ObjectBinary class is very slow and uses an enormous amount of memory.
     * This class parses only what it needs to, and then caches file offsets to make subsequent reads even faster
     */
    public static class RefShaderCacheReader
    {
        public static string GlobalShaderFileName(MEGame game) => game.IsLEGame() ? "RefShaderCache-PC-D3D-SM5.upk" : "RefShaderCache-PC-D3D-SM3.upk";
        private static string shaderfilePath(MEGame game) => Path.Combine(MEDirectories.GetCookedPath(game), GlobalShaderFileName(game));

        private static Dictionary<Guid, int> ME3ShaderOffsets;
        private static Dictionary<Guid, int> ME2ShaderOffsets;
        private static Dictionary<Guid, int> ME1ShaderOffsets;

        private static Dictionary<Guid, int> LE3ShaderOffsets;
        private static Dictionary<Guid, int> LE2ShaderOffsets;
        private static Dictionary<Guid, int> LE1ShaderOffsets;
        private static Dictionary<Guid, int> ShaderOffsets(MEGame game) => game switch
        {
            MEGame.ME3 => ME3ShaderOffsets,
            MEGame.ME2 => ME2ShaderOffsets,
            MEGame.ME1 => ME1ShaderOffsets,
            MEGame.LE3 => LE3ShaderOffsets,
            MEGame.LE2 => LE2ShaderOffsets,
            MEGame.LE1 => LE1ShaderOffsets,
            _ => null
        };

        public static bool IsShaderOffsetsDictInitialized(MEGame game) => ShaderOffsets(game)?.Count > 0;

        private static int ME3MaterialShaderMapsOffset = 206341927;
        private static int ME2MaterialShaderMapsOffset = 132795914;
        private static int ME1MaterialShaderMapsOffset = 69550225;

        private static int LE3MaterialShaderMapsOffset = 1263553925;
        private static int LE2MaterialShaderMapsOffset = 1014140890;
        private static int LE1MaterialShaderMapsOffset = 720539980;

        private static long LE3RefShaderCacheSize = 1296009525;
        private static long LE2RefShaderCacheSize = 1035352391;
        private static long LE1RefShaderCacheSize = 731880291;

        private static int MaterialShaderMapsOffset(MEGame game)
        {
            if (game.IsLEGame())
            {
                var expectedSize = game switch
                {
                    MEGame.LE3 => LE3RefShaderCacheSize,
                    MEGame.LE2 => LE2RefShaderCacheSize,
                    MEGame.LE1 => LE1RefShaderCacheSize,
                    _ => 0
                };
                var actualsize = new FileInfo(shaderfilePath(game)).Length;
                if (expectedSize != actualsize)
                {
                    GetMaterialShaderMap(game, null);
                    switch (game)
                    {
                        case MEGame.LE3:
                            LE3RefShaderCacheSize = actualsize;
                            break;
                        case MEGame.LE2:
                            LE2RefShaderCacheSize = actualsize;
                            break;
                        case MEGame.LE1:
                            LE1RefShaderCacheSize = actualsize;
                            break;
                    }
                }
            }
            return game switch
            {
                MEGame.ME3 => ME3MaterialShaderMapsOffset,
                MEGame.ME2 => ME2MaterialShaderMapsOffset,
                MEGame.ME1 => ME1MaterialShaderMapsOffset,
                MEGame.LE3 => LE3MaterialShaderMapsOffset,
                MEGame.LE2 => LE2MaterialShaderMapsOffset,
                MEGame.LE1 => LE1MaterialShaderMapsOffset,
                _ => 0
            };
        }

        private static void populateOffsets(MEGame game, int offsetOfShaderCacheOffset)
        {
            string filePath = shaderfilePath(game);
            if (File.Exists(filePath))
            {
                Dictionary<Guid, int> offsetDict = game switch
                {
                    MEGame.ME3 => ME3ShaderOffsets ??= new Dictionary<Guid, int>(),
                    MEGame.ME2 => ME2ShaderOffsets ??= new Dictionary<Guid, int>(),
                    MEGame.ME1 => ME1ShaderOffsets ??= new Dictionary<Guid, int>(),
                    MEGame.LE3 => LE3ShaderOffsets ??= new Dictionary<Guid, int>(),
                    MEGame.LE2 => LE2ShaderOffsets ??= new Dictionary<Guid, int>(),
                    MEGame.LE1 => LE1ShaderOffsets ??= new Dictionary<Guid, int>(),
                    _ => null
                };
                if (offsetDict == null || offsetDict.Count > 0) return;

                using FileStream fs = File.OpenRead(filePath);
                fs.JumpTo(offsetOfShaderCacheOffset);
                int binaryOffset = fs.ReadInt32() + 12;
                fs.JumpTo(binaryOffset);
                fs.Skip(1);
                int nameCount = fs.ReadInt32();
                fs.Skip(nameCount * 12);
                if (game is not MEGame.ME2)
                {
                    nameCount = fs.ReadInt32();
                    fs.Skip(nameCount * 12);
                }

                int shaderCount = fs.ReadInt32();
                for (int i = 0; i < shaderCount; i++)
                {
                    fs.Skip(8);
                    Guid shaderGuid = fs.ReadGuid();
                    int shaderEndOffset = fs.ReadInt32();
                    offsetDict.Add(shaderGuid, (int)fs.Position + 2);
                    fs.Skip(shaderEndOffset - fs.Position);
                }

                if (game != MEGame.ME1)
                {
                    nameCount = fs.ReadInt32();
                    fs.Skip(nameCount * 12);
                }
                switch (game)
                {
                    case MEGame.ME3:
                        ME3MaterialShaderMapsOffset = (int)fs.Position;
                        break;
                    case MEGame.ME2:
                        ME2MaterialShaderMapsOffset = (int)fs.Position;
                        break;
                    case MEGame.ME1:
                        ME1MaterialShaderMapsOffset = (int)fs.Position;
                        break;
                    case MEGame.LE3:
                        LE3MaterialShaderMapsOffset = (int)fs.Position;
                        //Debug.WriteLine($"{nameof(LE3MaterialShaderMapsOffset)}: {LE3MaterialShaderMapsOffset}");
                        break;
                    case MEGame.LE2:
                        LE2MaterialShaderMapsOffset = (int)fs.Position;
                        //Debug.WriteLine($"{nameof(LE2MaterialShaderMapsOffset)}: {LE2MaterialShaderMapsOffset}");
                        break;
                    case MEGame.LE1:
                        LE1MaterialShaderMapsOffset = (int)fs.Position;
                        //Debug.WriteLine($"{nameof(LE1MaterialShaderMapsOffset)}: {LE1MaterialShaderMapsOffset}");
                        break;
                }
            }
        }

        public static MaterialShaderMap GetMaterialShaderMap(MEGame game, StaticParameterSet staticParameterSet)
        {
            string filePath = shaderfilePath(game);
            if (File.Exists(filePath))
            {
                using FileStream fs = File.OpenRead(filePath);
                using IMEPackage shaderCachePackage = MEPackageHandler.OpenMEPackageFromStream(fs, quickLoad:true);
                ReadNames(fs, shaderCachePackage);

                int offsetOfShaderCacheOffset = shaderCachePackage.ExportOffset + 36;
                populateOffsets(game, offsetOfShaderCacheOffset);
                if (staticParameterSet is null)
                {
                    return null;
                }

                var sc = new SerializingContainer2(fs, shaderCachePackage, true);
                sc.ms.JumpTo(MaterialShaderMapsOffset(game));

                int count = fs.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    StaticParameterSet sps = null;
                    sc.Serialize(ref sps);
                    if (sps == staticParameterSet)
                    {
                        MaterialShaderMap msm = null;
                        sc.Serialize(ref msm);
                        return msm;
                    }

                    if (game >= MEGame.ME3)
                    {
                        sc.ms.Skip(8);
                    }

                    int nextMSMOffset = sc.ms.ReadInt32();
                    sc.ms.Skip(nextMSMOffset - sc.ms.Position);
                }
            }

            return null;
        }

        public static string GetShaderDissasembly(MEGame game, Guid shaderGuid)
        {
            var offsets = ShaderOffsets(game);
            if (offsets != null && offsets.TryGetValue(shaderGuid, out int offset))
            {
                using FileStream fs = File.OpenRead(shaderfilePath(game));
                fs.JumpTo(offset);
                int size = fs.ReadInt32();
                ShaderReader.DisassembleShader(fs.ReadToBuffer(size), out string disassembly);
                return disassembly;
            }

            return "";
        }

        public static byte[] GetShaderBytecode(MEGame game, Guid shaderGuid)
        {
            var offsets = ShaderOffsets(game);
            if (offsets != null && offsets.TryGetValue(shaderGuid, out int offset))
            {
                using FileStream fs = File.OpenRead(shaderfilePath(game));
                fs.JumpTo(offset);
                int size = fs.ReadInt32();
                return fs.ReadToBuffer(size);
            }

            return null;
        }

        public static void RemoveStaticParameterSetsThatAreInTheGlobalCache(HashSet<StaticParameterSet> paramSets, MEGame game)
        {
            string filePath = shaderfilePath(game);
            if (File.Exists(filePath))
            {
                using FileStream fs = File.OpenRead(filePath);
                //read just the header of the package, then read the name list
                using IMEPackage shaderCachePackage = MEPackageHandler.OpenMEPackageFromStream(fs, quickLoad: true);
                ReadNames(fs, shaderCachePackage);
                var sc = new SerializingContainer2(fs, shaderCachePackage, true);
                sc.ms.JumpTo(MaterialShaderMapsOffset(game));

                int count = fs.ReadInt32();
                for (int i = 0; i < count && paramSets.Count > 0; i++)
                {
                    StaticParameterSet sps = null;
                    sc.Serialize(ref sps);
                    if (paramSets.Contains(sps))
                    {
                        paramSets.Remove(sps);
                    }

                    if (game >= MEGame.ME3)
                    {
                        sc.ms.Skip(8);
                    }

                    int nextMSMOffset = sc.ms.ReadInt32();
                    sc.ms.Skip(nextMSMOffset - sc.ms.Position);
                }
            }
        }

        private static void ReadNames(FileStream fs, IMEPackage shaderCachePackage)
        {
            fs.JumpTo(shaderCachePackage.NameOffset);
            var names = new List<string>(shaderCachePackage.NameCount);
            for (int i = 0; i < shaderCachePackage.NameCount; i++)
            {
                var name = fs.ReadUnrealString();
                names.Add(name);
                if (shaderCachePackage.Game == MEGame.ME1 && shaderCachePackage.Platform != GamePlatform.PS3)
                    fs.Skip(8);
                else if (shaderCachePackage.Game == MEGame.ME2 && shaderCachePackage.Platform != GamePlatform.PS3)
                    fs.Skip(4);
            }

            shaderCachePackage.restoreNames(names);
        }
    }
}
