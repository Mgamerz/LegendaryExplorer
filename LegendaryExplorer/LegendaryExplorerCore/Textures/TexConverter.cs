﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Textures
{
    public static class TexConverter
    {
        #region Native Interop
        private const string TEXCONVERTER_DLL_FILENAME = "TexConverter.dll";

        private enum DXGIFormat : uint
        {
            UNKNOWN = 0,
            R32G32B32A32_TYPELESS = 1,
            R32G32B32A32_FLOAT = 2,
            R32G32B32A32_UINT = 3,
            R32G32B32A32_SINT = 4,
            R32G32B32_TYPELESS = 5,
            R32G32B32_FLOAT = 6,
            R32G32B32_UINT = 7,
            R32G32B32_SINT = 8,
            R16G16B16A16_TYPELESS = 9,
            R16G16B16A16_FLOAT = 10,
            R16G16B16A16_UNORM = 11,
            R16G16B16A16_UINT = 12,
            R16G16B16A16_SNORM = 13,
            R16G16B16A16_SINT = 14,
            R32G32_TYPELESS = 15,
            R32G32_FLOAT = 16,
            R32G32_UINT = 17,
            R32G32_SINT = 18,
            R32G8X24_TYPELESS = 19,
            D32_FLOAT_S8X24_UINT = 20,
            R32_FLOAT_X8X24_TYPELESS = 21,
            X32_TYPELESS_G8X24_UINT = 22,
            R10G10B10A2_TYPELESS = 23,
            R10G10B10A2_UNORM = 24,
            R10G10B10A2_UINT = 25,
            R11G11B10_FLOAT = 26,
            R8G8B8A8_TYPELESS = 27,
            R8G8B8A8_UNORM = 28,
            R8G8B8A8_UNORM_SRGB = 29,
            R8G8B8A8_UINT = 30,
            R8G8B8A8_SNORM = 31,
            R8G8B8A8_SINT = 32,
            R16G16_TYPELESS = 33,
            R16G16_FLOAT = 34,
            R16G16_UNORM = 35,
            R16G16_UINT = 36,
            R16G16_SNORM = 37,
            R16G16_SINT = 38,
            R32_TYPELESS = 39,
            D32_FLOAT = 40,
            R32_FLOAT = 41,
            R32_UINT = 42,
            R32_SINT = 43,
            R24G8_TYPELESS = 44,
            D24_UNORM_S8_UINT = 45,
            R24_UNORM_X8_TYPELESS = 46,
            X24_TYPELESS_G8_UINT = 47,
            R8G8_TYPELESS = 48,
            R8G8_UNORM = 49,
            R8G8_UINT = 50,
            R8G8_SNORM = 51,
            R8G8_SINT = 52,
            R16_TYPELESS = 53,
            R16_FLOAT = 54,
            D16_UNORM = 55,
            R16_UNORM = 56,
            R16_UINT = 57,
            R16_SNORM = 58,
            R16_SINT = 59,
            R8_TYPELESS = 60,
            R8_UNORM = 61,
            R8_UINT = 62,
            R8_SNORM = 63,
            R8_SINT = 64,
            A8_UNORM = 65,
            R1_UNORM = 66,
            R9G9B9E5_SHAREDEXP = 67,
            R8G8_B8G8_UNORM = 68,
            G8R8_G8B8_UNORM = 69,
            BC1_TYPELESS = 70,
            BC1_UNORM = 71,
            BC1_UNORM_SRGB = 72,
            BC2_TYPELESS = 73,
            BC2_UNORM = 74,
            BC2_UNORM_SRGB = 75,
            BC3_TYPELESS = 76,
            BC3_UNORM = 77,
            BC3_UNORM_SRGB = 78,
            BC4_TYPELESS = 79,
            BC4_UNORM = 80,
            BC4_SNORM = 81,
            BC5_TYPELESS = 82,
            BC5_UNORM = 83,
            BC5_SNORM = 84,
            B5G6R5_UNORM = 85,
            B5G5R5A1_UNORM = 86,
            B8G8R8A8_UNORM = 87,
            B8G8R8X8_UNORM = 88,
            R10G10B10_XR_BIAS_A2_UNORM = 89,
            B8G8R8A8_TYPELESS = 90,
            B8G8R8A8_UNORM_SRGB = 91,
            B8G8R8X8_TYPELESS = 92,
            B8G8R8X8_UNORM_SRGB = 93,
            BC6H_TYPELESS = 94,
            BC6H_UF16 = 95,
            BC6H_SF16 = 96,
            BC7_TYPELESS = 97,
            BC7_UNORM = 98,
            BC7_UNORM_SRGB = 99,
            AYUV = 100,
            Y410 = 101,
            Y416 = 102,
            NV12 = 103,
            P010 = 104,
            P016 = 105,
            _420_OPAQUE = 106,
            YUY2 = 107,
            Y210 = 108,
            Y216 = 109,
            NV11 = 110,
            AI44 = 111,
            IA44 = 112,
            P8 = 113,
            A8P8 = 114,
            B4G4R4A4_UNORM = 115,

            P208 = 130,
            V208 = 131,
            V408 = 132,
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct TextureBuffer
        {
            public byte* PixelData;
            public nuint PixelDataLength;
            public uint Width;
            public uint Height;
            public DXGIFormat Format;
            public void* _ScratchImage; // Only used to keep track of things by the native code
        }

        [DllImport(TEXCONVERTER_DLL_FILENAME, EntryPoint = "Initialize")]
        private static unsafe extern int TCInitialize();

        [DllImport(TEXCONVERTER_DLL_FILENAME, EntryPoint = "Dispose")]
        private static unsafe extern int TCDispose();

        [DllImport(TEXCONVERTER_DLL_FILENAME, EntryPoint = "ConvertTexture")]
        private static unsafe extern int TCConvertTexture(TextureBuffer* inputBuffer, TextureBuffer* outputBuffer);

        [DllImport(TEXCONVERTER_DLL_FILENAME, EntryPoint = "SaveTexture", CharSet = CharSet.Ansi)]
        private static unsafe extern int TCSaveTexture(TextureBuffer* inputBuffer, string outputFilename);

        [DllImport(TEXCONVERTER_DLL_FILENAME, EntryPoint = "LoadTexture", CharSet = CharSet.Ansi)]
        private static unsafe extern int TCLoadTexture(string inputFilename, TextureBuffer* outputBuffer);

        [DllImport(TEXCONVERTER_DLL_FILENAME, EntryPoint = "FreePixelData")]
        private static unsafe extern int TCFreePixelData(TextureBuffer* textureBuffer);
        #endregion

        // TODO: Keep track of whether each thread has initialized yet
        private static bool IsInitialized = false;
        private static void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                IsInitialized = true;
                Marshal.ThrowExceptionForHR(TCInitialize());
            }
        }

        private static DXGIFormat GetDXGIFormatForPixelFormat(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.ARGB:
                    return DXGIFormat.B8G8R8A8_UNORM;
                case PixelFormat.ATI2:
                    return DXGIFormat.BC5_UNORM; // TODO: Is the data actually signed instead?
                case PixelFormat.BC5:
                    return DXGIFormat.BC5_UNORM;
                case PixelFormat.BC7:
                    return DXGIFormat.BC7_UNORM;
                case PixelFormat.DXT1:
                    return DXGIFormat.BC1_UNORM;
                case PixelFormat.DXT3:
                    return DXGIFormat.BC2_UNORM;
                case PixelFormat.DXT5:
                    return DXGIFormat.BC3_UNORM;
                case PixelFormat.G8:
                    return DXGIFormat.R8_UNORM;
                case PixelFormat.RGB:
                    return DXGIFormat.UNKNOWN; // There is no hardware support for 24bit pixels. (see https://stackoverflow.com/a/27971434)
                case PixelFormat.V8U8:
                    return DXGIFormat.R8G8_UNORM; // TODO: Not quite sure if these are equivalent
                default:
                    return DXGIFormat.UNKNOWN;
            }
        }

        private static PixelFormat GetPixelFormatForDXGIFormat(DXGIFormat format)
        {
            switch (format)
            {
                case DXGIFormat.B8G8R8A8_UNORM:
                    return PixelFormat.ARGB;
                case DXGIFormat.BC5_UNORM:
                    return PixelFormat.BC5; // TODO: When should this return ATI2?
                case DXGIFormat.BC7_UNORM:
                    return PixelFormat.BC7;
                case DXGIFormat.BC1_UNORM:
                    return PixelFormat.DXT1;
                case DXGIFormat.BC2_UNORM:
                    return PixelFormat.DXT3;
                case DXGIFormat.BC3_UNORM:
                    return PixelFormat.DXT5;
                case DXGIFormat.R8_UNORM:
                    return PixelFormat.G8;
                case DXGIFormat.R8G8_UNORM:
                    return PixelFormat.V8U8; // TODO: Not quite sure if these are equivalent
                default:
                    return PixelFormat.Unknown;
            }
        }

        public static unsafe byte[] ConvertTexture(byte[] pixelData, uint width, uint height, Image.ImageFormat inputFormat, Image.ImageFormat outputFormat)
        {
            TexConverter.EnsureInitialized();

            // TODO: Implement!
            throw new NotImplementedException();
        }

        public static unsafe void SaveTexture(byte[] pixelData, uint width, uint height, PixelFormat pixelFormat, string filename)
        {
            TexConverter.EnsureInitialized();

            fixed (byte* pixelDataPointer = pixelData)
            {
                TextureBuffer sourceBuffer = new TextureBuffer()
                {
                    PixelData = pixelDataPointer,
                    PixelDataLength = (nuint)pixelData.LongLength,
                    Width = width,
                    Height = height,
                    Format = GetDXGIFormatForPixelFormat(pixelFormat)
                };
                int result = TCSaveTexture(&sourceBuffer, filename);
                Marshal.ThrowExceptionForHR(result);
            }
        }

        public static unsafe byte[] LoadTexture(string filename, out uint width, out uint height, out PixelFormat pixelFormat)
        {
            TexConverter.EnsureInitialized();

            TextureBuffer outputBuffer = new TextureBuffer();

            int hr = TCLoadTexture(filename, &outputBuffer);
            Marshal.ThrowExceptionForHR(hr);

            byte[] result = new byte[outputBuffer.PixelDataLength];
            Marshal.Copy((IntPtr)outputBuffer.PixelData, result, 0, (int)outputBuffer.PixelDataLength);

            hr = TCFreePixelData(&outputBuffer);
            Marshal.ThrowExceptionForHR(hr);

            width = outputBuffer.Width;
            height = outputBuffer.Height;
            pixelFormat = GetPixelFormatForDXGIFormat(outputBuffer.Format);
            return result;
        }
    }
}
