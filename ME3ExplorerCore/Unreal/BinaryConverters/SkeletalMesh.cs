﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3ExplorerCore.Gammtek;
using ME3ExplorerCore.Gammtek.Extensions;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.SharpDX;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class SkeletalMesh : ObjectBinary
    {
        public BoxSphereBounds Bounds;
        public UIndex[] Materials;
        public Vector3 Origin;
        public Rotator RotOrigin;
        public MeshBone[] RefSkeleton;
        public int SkeletalDepth;
        public StaticLODModel[] LODModels;
        public OrderedMultiValueDictionary<NameReference, int> NameIndexMap;
        public PerPolyBoneCollisionData[] PerPolyBoneKDOPs;
        public string[] BoneBreakNames; //ME3 and UDK
        public UIndex[] ClothingAssets; //ME3 and UDK
        public uint unk1; //UDK
        public uint unk2; //UDK
        public float[] unkFloats; //UDK
        public uint unk3; //UDK


        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref Bounds);
            sc.Serialize(ref Materials, SCExt.Serialize);
            sc.Serialize(ref Origin);
            sc.Serialize(ref RotOrigin);
            sc.Serialize(ref RefSkeleton, SCExt.Serialize);
            sc.Serialize(ref SkeletalDepth);
            sc.Serialize(ref LODModels, SCExt.Serialize);
            sc.Serialize(ref NameIndexMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref PerPolyBoneKDOPs, SCExt.Serialize);

            if (sc.Game >= MEGame.ME3)
            {
                if (sc.IsSaving && sc.Game == MEGame.UDK)
                {
                    ClothingAssets = new UIndex[0];
                }
                sc.Serialize(ref BoneBreakNames, SCExt.Serialize);
                sc.Serialize(ref ClothingAssets, SCExt.Serialize);
            }
            else
            {
                BoneBreakNames = Array.Empty<string>();
                ClothingAssets = Array.Empty<UIndex>();
            }

            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref unk1);
                sc.Serialize(ref unk2);
                sc.Serialize(ref unkFloats, SCExt.Serialize);
                sc.Serialize(ref unk3);
            }
            else if (sc.IsLoading)
            {
                unk1 = 1;
                unkFloats = new []{1f, 0f, 0f, 0f};
            }
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex t, string)> uIndexes = Materials.Select((t, i) => (t, $"Materials[{i}]")).ToList();

            if (game == MEGame.ME3)
            {
                uIndexes.AddRange(ClothingAssets.Select((t, i) => (t, $"ClothingAssets[{i}]")));
            }
            return uIndexes;
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(RefSkeleton.Select((bone, i) => (bone.Name, $"RefSkeleton[{i}].BoneName")));
            names.AddRange(NameIndexMap.Select((kvp, i) => (kvp.Key, $"NameIndexMap[{i}]")));

            return names;
        }
    }

    public class MeshBone
    {
        public NameReference Name;
        public uint Flags;
        public Quaternion Orientation;
        public Vector3 Position;
        public int NumChildren;
        public int ParentIndex;
        public Color BoneColor; //ME3 and UDK
    }

    public class StaticLODModel
    {
        public SkelMeshSection[] Sections;
        public bool NeedsCPUAccess; //UDK
        public byte DataTypeSize; //UDK
        public ushort[] IndexBuffer; //BulkSerialized
        public ushort[] ShadowIndices; //not in UDK
        public ushort[] ActiveBoneIndices;
        public byte[] ShadowTriangleDoubleSided; //not in UDK
        public SkelMeshChunk[] Chunks;
        public uint Size;
        public uint NumVertices;
        public MeshEdge[] Edges; //Not in UDK
        public byte[] RequiredBones;
        public ushort[] RawPointIndices; //BulkData
        public uint NumTexCoords; //UDK
        public SoftSkinVertex[] ME1VertexBufferGPUSkin; //BulkSerialized
        public SkeletalMeshVertexBuffer VertexBufferGPUSkin;
    }

    public class SkelMeshSection
    {
        public ushort MaterialIndex;
        public ushort ChunkIndex;
        public uint BaseIndex;
        public int NumTriangles; //ushort in ME1 and ME2
        public byte TriangleSorting; //UDK
    }

    public class SkelMeshChunk
    {
        public uint BaseVertexIndex;
        public RigidSkinVertex[] RigidVertices;
        public SoftSkinVertex[] SoftVertices;
        public ushort[] BoneMap;
        public int NumRigidVertices;
        public int NumSoftVertices;
        public int MaxBoneInfluences;
    }

    public class RigidSkinVertex
    {
        public Vector3 Position;
        public PackedNormal TangentX;
        public PackedNormal TangentY;
        public PackedNormal TangentZ;
        public Vector2 UV;
        public Vector2 UV2; //UDK
        public Vector2 UV3; //UDK
        public Vector2 UV4; //UDK
        public Color BoneColor; //UDK
        public byte Bone;
    }

    public class SoftSkinVertex
    {
        public Vector3 Position;
        public PackedNormal TangentX; // Tangent, U-direction
        public PackedNormal TangentY; // Binormal, V-direction
        public PackedNormal TangentZ; // Normal
        public Vector2 UV;
        public Vector2 UV2; //UDK
        public Vector2 UV3; //UDK
        public Vector2 UV4; //UDK
        public Color BoneColor; //UDK
        public byte[] InfluenceBones = new byte[4];
        public byte[] InfluenceWeights = new byte[4];
    }

    public class SkeletalMeshVertexBuffer
    {
        public int NumTexCoords; //UDK
        public bool bUseFullPrecisionUVs; //should always be false
        public bool bUsePackedPosition; //ME3 or UDK
        public Vector3 MeshExtension; //ME3 or UDK
        public Vector3 MeshOrigin; //ME3 or UDK
        public GPUSkinVertex[] VertexData; //BulkSerialized
    }

    public class GPUSkinVertex
    {
        public PackedNormal TangentX;
        public PackedNormal TangentZ;
        public byte[] InfluenceBones = new byte[4];
        public byte[] InfluenceWeights = new byte[4];
        public Vector3 Position; //serialized first in ME2
        public Vector2DHalf UV;
    }

    public class PerPolyBoneCollisionData
    {
        public kDOPTree kDOPTreeME1ME2;
        public kDOPTreeCompact kDOPTreeME3UDK;
        public Vector3[] CollisionVerts;
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref MeshBone mb)
        {
            if (sc.IsLoading)
            {
                mb = new MeshBone();
            }
            sc.Serialize(ref mb.Name);
            sc.Serialize(ref mb.Flags);
            sc.Serialize(ref mb.Orientation);
            sc.Serialize(ref mb.Position);
            sc.Serialize(ref mb.NumChildren);
            sc.Serialize(ref mb.ParentIndex);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref mb.BoneColor);
            }
            else if (sc.IsLoading)
            {
                mb.BoneColor = new Color(255, 255, 255, 255);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref SkelMeshSection sms)
        {
            if (sc.IsLoading)
            {
                sms = new SkelMeshSection();
            }
            sc.Serialize(ref sms.MaterialIndex);
            sc.Serialize(ref sms.ChunkIndex);
            sc.Serialize(ref sms.BaseIndex);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref sms.NumTriangles);
            }
            else
            {
                ushort tmp = (ushort)sms.NumTriangles;
                sc.Serialize(ref tmp);
                sms.NumTriangles = tmp;
            }

            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref sms.TriangleSorting);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref RigidSkinVertex rsv)
        {
            if (sc.IsLoading)
            {
                rsv = new RigidSkinVertex();
            }
            sc.Serialize(ref rsv.Position);
            sc.Serialize(ref rsv.TangentX);
            sc.Serialize(ref rsv.TangentY);
            sc.Serialize(ref rsv.TangentZ);
            sc.Serialize(ref rsv.UV);
            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref rsv.UV2);
                sc.Serialize(ref rsv.UV3);
                sc.Serialize(ref rsv.UV4);
                sc.Serialize(ref rsv.BoneColor);
            }
            else if (sc.IsLoading)
            {
                rsv.BoneColor = new Color(255,255,255,255);
            }
            sc.Serialize(ref rsv.Bone);
        }
        public static void Serialize(this SerializingContainer2 sc, ref SoftSkinVertex ssv)
        {
            if (sc.IsLoading)
            {
                ssv = new SoftSkinVertex();
            }
            sc.Serialize(ref ssv.Position);
            sc.Serialize(ref ssv.TangentX);
            sc.Serialize(ref ssv.TangentY);
            sc.Serialize(ref ssv.TangentZ);
            sc.Serialize(ref ssv.UV);
            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref ssv.UV2);
                sc.Serialize(ref ssv.UV3);
                sc.Serialize(ref ssv.UV4);
                sc.Serialize(ref ssv.BoneColor);
            }
            else if (sc.IsLoading)
            {
                ssv.BoneColor = new Color(255, 255, 255, 255);
            }
            for (int i = 0; i < 4; i++)
            {
                sc.Serialize(ref ssv.InfluenceBones[i]);
            }
            for (int i = 0; i < 4; i++)
            {
                sc.Serialize(ref ssv.InfluenceWeights[i]);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref SkelMeshChunk smc)
        {
            if (sc.IsLoading)
            {
                smc = new SkelMeshChunk();
            }
            sc.Serialize(ref smc.BaseVertexIndex);
            sc.Serialize(ref smc.RigidVertices, Serialize);
            sc.Serialize(ref smc.SoftVertices, Serialize);
            sc.Serialize(ref smc.BoneMap, SCExt.Serialize);
            sc.Serialize(ref smc.NumRigidVertices);
            sc.Serialize(ref smc.NumSoftVertices);
            sc.Serialize(ref smc.MaxBoneInfluences);
        }
        public static void Serialize(this SerializingContainer2 sc, ref GPUSkinVertex gsv)
        {
            if (sc.IsLoading)
            {
                gsv = new GPUSkinVertex();
            }

            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref gsv.Position);
            }
            sc.Serialize(ref gsv.TangentX);
            sc.Serialize(ref gsv.TangentZ);
            for (int i = 0; i < 4; i++)
            {
                sc.Serialize(ref gsv.InfluenceBones[i]);
            }
            for (int i = 0; i < 4; i++)
            {
                sc.Serialize(ref gsv.InfluenceWeights[i]);
            }
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref gsv.Position);
            }
            sc.Serialize(ref gsv.UV);
        }
        public static void Serialize(this SerializingContainer2 sc, ref SkeletalMeshVertexBuffer svb)
        {
            if (sc.IsLoading)
            {
                svb = new SkeletalMeshVertexBuffer();
            }

            if (sc.Game == MEGame.UDK)
            {
                svb.bUsePackedPosition = true;
            }
            else
            {
                svb.bUsePackedPosition = false;
                svb.bUseFullPrecisionUVs = false;
            }

            if (sc.Game == MEGame.UDK)
            {
                svb.NumTexCoords = 1;
                sc.Serialize(ref svb.NumTexCoords);
            }
            else if (sc.IsLoading)
            {
                svb.NumTexCoords = 1;
            }
            sc.Serialize(ref svb.bUseFullPrecisionUVs);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref svb.bUsePackedPosition);
                sc.Serialize(ref svb.MeshExtension);
                sc.Serialize(ref svb.MeshOrigin);
            }
            int elementSize = 32;
            sc.Serialize(ref elementSize);

            //vertexData
            int count = svb.VertexData?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                svb.VertexData = new GPUSkinVertex[count];
            }

            for (int j = 0; j < count; j++)
            {
                ref GPUSkinVertex gsv = ref svb.VertexData[j];
                if (sc.IsLoading)
                {
                    gsv = new GPUSkinVertex();
                }

                if (sc.Game == MEGame.ME2)
                {
                    sc.Serialize(ref gsv.Position);
                }
                sc.Serialize(ref gsv.TangentX);
                sc.Serialize(ref gsv.TangentZ);
                for (int i = 0; i < 4; i++)
                {
                    sc.Serialize(ref gsv.InfluenceBones[i]);
                }
                for (int i = 0; i < 4; i++)
                {
                    sc.Serialize(ref gsv.InfluenceWeights[i]);
                }
                if (sc.Game >= MEGame.ME3)
                {
                    if (false)
                    {
                        if (sc.IsLoading)
                        {
                            uint packedPos = sc.ms.ReadUInt32();
                            gsv.Position = new Vector3(packedPos.bits(31, 21) / 1023f,
                                packedPos.bits(20, 10) / 1023f,
                                packedPos.bits(9, 0) / 1023f) * svb.MeshExtension + svb.MeshOrigin;
                        }
                        else
                        {
                            Vector3 pos = (gsv.Position - svb.MeshOrigin) - svb.MeshExtension; //puts values in -1.0 to 1.0 range
                            uint x;
                            uint y;
                            uint z;
                            unchecked
                            {
                                x = (uint)Math.Truncate(pos.X * 1023.0).ToInt32().Clamp(-1023, 1023) << 21;
                                y = (uint)Math.Truncate(pos.Y * 1023.0).ToInt32().Clamp(-1023, 1023) << 21 >> 11;
                                z = (uint)Math.Truncate(pos.Z * 1023.0).ToInt32().Clamp(-1023, 1023) << 22 >> 22;
                            }
                            sc.ms.Writer.WriteUInt32(x | y | z);
                        }
                    }
                    else
                    {
                        sc.Serialize(ref gsv.Position);
                    }
                }

                if (svb.bUseFullPrecisionUVs)
                {
                    Vector2 fullUV = gsv.UV;
                    sc.Serialize(ref fullUV);
                    gsv.UV = fullUV;
                }
                else
                {
                    sc.Serialize(ref gsv.UV);
                }

                if (svb.NumTexCoords > 1)
                {
                    if (sc.IsLoading)
                    {
                        sc.ms.Skip((svb.NumTexCoords - 1) * (svb.bUseFullPrecisionUVs ? 8 : 4));
                    }
                    else
                    {
                        throw new Exception("Should never be saving more than one UV! Num UVs (NumTexCoords): "+svb.NumTexCoords);
                    }
                }
            }

            if (sc.IsLoading)
            {
                svb.NumTexCoords = 1;
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticLODModel slm)
        {
            if (sc.IsLoading)
            {
                slm = new StaticLODModel();
            }
            sc.Serialize(ref slm.Sections, Serialize);
            int indexSize = 2;
            slm.DataTypeSize = 2;
            if (sc.Game == MEGame.UDK && sc.IsSaving && slm.IndexBuffer.Length > ushort.MaxValue)
            {
                slm.DataTypeSize = 4;
                indexSize = 4;
            }
            if (sc.Game == MEGame.UDK)
            {
                slm.NeedsCPUAccess = true;
                sc.Serialize(ref slm.NeedsCPUAccess);
                sc.Serialize(ref slm.DataTypeSize);
            }
            sc.Serialize(ref indexSize);
            if (sc.Game == MEGame.UDK && indexSize == 4)
            {
                //have to do this manually due to the size mismatch
                //as far as I know, despite being saved as uints when the IndexBuffer is longer than ushort.MaxValue,
                //the actual indicies themselves should not exceed the range of a ushort
                int count = slm.IndexBuffer?.Length ?? 0;
                sc.Serialize(ref count);
                if (sc.IsLoading)
                {
                    slm.IndexBuffer = new ushort[count];
                }

                for (int i = 0; i < count; i++)
                {
                    if (sc.IsLoading)
                        slm.IndexBuffer[i] = (ushort)sc.ms.ReadUInt32();
                    else
                        sc.ms.Writer.WriteUInt32(slm.IndexBuffer[i]);
                }
            }
            else
            {
                sc.Serialize(ref slm.IndexBuffer, SCExt.Serialize);
            }
            if (sc.Game != MEGame.UDK)
            {
                sc.Serialize(ref slm.ShadowIndices, SCExt.Serialize);
            }
            sc.Serialize(ref slm.ActiveBoneIndices, SCExt.Serialize);
            if (sc.Game != MEGame.UDK)
            {
                sc.Serialize(ref slm.ShadowTriangleDoubleSided, SCExt.Serialize);
            }
            sc.Serialize(ref slm.Chunks, Serialize);
            sc.Serialize(ref slm.Size);
            sc.Serialize(ref slm.NumVertices);
            if (sc.Game <= MEGame.ME3 && slm.NumVertices > ushort.MaxValue)
            {
                throw new Exception($"Mass Effect games do not support SkeletalMeshes with more than {ushort.MaxValue} vertices!");
            }
            if (sc.Game != MEGame.UDK)
            {
                sc.Serialize(ref slm.Edges, Serialize);
            }
            sc.Serialize(ref slm.RequiredBones, SCExt.Serialize);
            if (sc.Game == MEGame.UDK)
            {
                int[] UDKRawPointIndices = sc.IsSaving ? Array.ConvertAll(slm.RawPointIndices, u => (int)u) : Array.Empty<int>();
                sc.SerializeBulkData(ref UDKRawPointIndices, SCExt.Serialize);
                slm.RawPointIndices = Array.ConvertAll(UDKRawPointIndices, i => (ushort)i);
            }
            else
            {
                sc.SerializeBulkData(ref slm.RawPointIndices, SCExt.Serialize);
            }
            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref slm.NumTexCoords);
            }
            else if (sc.IsLoading)
            {
                slm.NumTexCoords = 1;
            }
            if (sc.Game == MEGame.ME1)
            {
                if (sc.IsSaving && slm.ME1VertexBufferGPUSkin == null)
                {
                    GPUSkinVertex[] vertexData = slm.VertexBufferGPUSkin.VertexData;
                    slm.ME1VertexBufferGPUSkin = new SoftSkinVertex[vertexData.Length];
                    for (int i = 0; i < vertexData.Length; i++)
                    {
                        GPUSkinVertex vert = vertexData[i];
                        var normal = (Vector4)vert.TangentZ;
                        var tangent = (Vector4)vert.TangentX;
                        slm.ME1VertexBufferGPUSkin[i] = new SoftSkinVertex
                        {
                            Position = vert.Position,
                            TangentX = vert.TangentX,
                            TangentY = (PackedNormal)(new Vector4(Vector3.Cross((Vector3)normal, (Vector3)tangent), normal.W * tangent.W) * normal.W),
                            TangentZ = vert.TangentZ,
                            UV = new Vector2(vert.UV.X, vert.UV.Y),
                            InfluenceBones = vert.InfluenceBones.TypedClone(),
                            InfluenceWeights = vert.InfluenceWeights.TypedClone()
                        };
                    }
                }

                int softSkinVertexSize = 40;
                sc.Serialize(ref softSkinVertexSize);
                sc.Serialize(ref slm.ME1VertexBufferGPUSkin, Serialize);
            }
            else
            {
                if (sc.IsSaving && slm.VertexBufferGPUSkin == null)
                {
                    slm.VertexBufferGPUSkin = new SkeletalMeshVertexBuffer
                    {
                        MeshExtension = new Vector3(1, 1, 1),
                        NumTexCoords = 1,
                        VertexData = new GPUSkinVertex[slm.ME1VertexBufferGPUSkin.Length]
                    };
                    for (int i = 0; i < slm.ME1VertexBufferGPUSkin.Length; i++)
                    {
                        SoftSkinVertex vert = slm.ME1VertexBufferGPUSkin[i];
                        slm.VertexBufferGPUSkin.VertexData[i] = new GPUSkinVertex
                        {
                            Position = vert.Position,
                            TangentX = vert.TangentX,
                            TangentZ = vert.TangentZ,
                            UV = new Vector2DHalf(vert.UV.X, vert.UV.Y),
                            InfluenceBones = vert.InfluenceBones.TypedClone(),
                            InfluenceWeights = vert.InfluenceWeights.TypedClone()
                        };
                    }
                }
                sc.Serialize(ref slm.VertexBufferGPUSkin);
            }

            if (sc.Game >= MEGame.ME3)
            {
                if (sc.IsLoading)
                {
                    int vertexInfluenceSize = 0;
                    sc.Serialize(ref vertexInfluenceSize);
                    if (vertexInfluenceSize > 0)
                    {
                        if (sc.Game == MEGame.UDK)
                        {
                            int[] vertexInfluences = null;
                            sc.Serialize(ref vertexInfluences, SCExt.Serialize);
                            int dummy = 0;
                            sc.Serialize(ref dummy);
                        }
                        else
                        {
                            throw new Exception($"VertexInfluences exist on this SkeletalMesh! Mesh in: {sc.Pcc.FilePath}");
                        }
                    }
                }
                else
                {
                    sc.ms.Writer.WriteInt32(0);
                }
            }

            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref slm.NeedsCPUAccess);
                sc.Serialize(ref slm.DataTypeSize);
                int elementSize = 2;
                sc.Serialize(ref elementSize);
                ushort[] secondIndexBuffer = new ushort[0];
                sc.Serialize(ref secondIndexBuffer, SCExt.Serialize);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref PerPolyBoneCollisionData bcd)
        {
            if (sc.IsLoading)
            {
                bcd = new PerPolyBoneCollisionData();
            }
            if (sc.IsSaving)
            {
                if (sc.Game >= MEGame.ME3 && bcd.kDOPTreeME3UDK == null)
                {
                    bcd.kDOPTreeME3UDK = KDOPTreeBuilder.ToCompact(bcd.kDOPTreeME1ME2.Triangles, bcd.CollisionVerts);
                }
                else if (sc.Game <= MEGame.ME2 && bcd.kDOPTreeME1ME2 == null)
                {
                    //todo: need to convert kDOPTreeCompact to kDOPTree
                    throw new NotImplementedException("Cannot convert this SkeletalMesh to ME1 or ME2 format :(");
                }
            }
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref bcd.kDOPTreeME3UDK);
            }
            else
            {
                sc.Serialize(ref bcd.kDOPTreeME1ME2);
            }

            sc.Serialize(ref bcd.CollisionVerts, Serialize);
        }
    }
}