// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback.Video
{
    using System;
    using System.Runtime.InteropServices;

    public sealed unsafe class Direct3D : IDisposable
    {
        public const int FormatBgra = 87;

        public const int FormatNv12 = 103;

        public const uint BindShaderResource = 0x8;

        public const uint BindRenderTarget = 0x20;

        public const uint BindDecoder = 0x200;

        public const uint MiscSharedKeyedMutex = 0x100;

        public const int UsageDefault = 0;

        public const int UsageStaging = 3;

        public const uint CpuAccessRead = 0x20000;

        public const int FrameProgressive = 0;

        public const int FrameTopFieldFirst = 1;

        public const int FrameBottomFieldFirst = 2;

        public const int OutputRateNormal = 0;

        public const int OutputRateHalf = 1;

        public const uint ProcessorFormatInput = 0x1;

        public const uint ProcessorFormatOutput = 0x2;

        public const int ColorSpaceRgbFullP709 = 0;

        public const int ColorSpaceStudioP601 = 6;

        public const int ColorSpaceFullP601 = 7;

        public const int ColorSpaceStudioP709 = 8;

        public const int ColorSpaceFullP709 = 9;

        public const int ColorSpaceStudioP2020 = 10;

        public const int ColorSpaceFullP2020 = 11;

        public const int ColorSpaceStudioPqP2020 = 13;

        public const int ColorSpaceStudioHlgP2020 = 18;

        public const int ColorSpaceFullHlgP2020 = 19;

        private const int WaitTimeout = 0x102;

        private static readonly Guid FactoryId = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");

        private static readonly Guid ResourceId = new Guid("035f3ab4-482e-4e50-b41f-8a7f8bd8960b");

        private static readonly Guid KeyedMutexId = new Guid("9d8e1289-d7b3-465f-8126-250e349af85d");

        private static readonly Guid MultithreadId = new Guid("9B7E4E00-342C-4106-A19F-4F2704F689F0");

        private static readonly Guid VideoDeviceId = new Guid("10EC4D5B-975A-4689-B9E4-D0AAC30FE333");

        private static readonly Guid VideoContextId = new Guid("61F21C45-3C0E-4a74-9CEA-67100D9AD5E4");

        private static readonly Guid VideoContext1Id = new Guid("A7F026DA-A5F8-4487-A564-15E34357651E");

        private Direct3D(IntPtr device, IntPtr context, IntPtr videoDevice, IntPtr videoContext, IntPtr videoContext1, string adapter)
        {
            Device = device;
            Context = context;
            VideoDevice = videoDevice;
            VideoContext = videoContext;
            VideoContext1 = videoContext1;
            Adapter = adapter;
        }

        public IntPtr Device { get; private set; }

        public IntPtr Context { get; private set; }

        public IntPtr VideoDevice { get; private set; }

        public IntPtr VideoContext { get; private set; }

        public IntPtr VideoContext1 { get; private set; }

        public string Adapter { get; }

        public int FeatureLevel => ((delegate* unmanaged[Stdcall]<IntPtr, int>)Slot(Device, 37))(Device);

        public bool IsRemoved => ((delegate* unmanaged[Stdcall]<IntPtr, int>)Slot(Device, 39))(Device) < 0;

        public static Direct3D Create(byte[] luid)
        {
            var adapter = FindAdapter(luid, out var name);

            try
            {
                var levels = stackalloc int[] { 0xc100, 0xc000, 0xb100, 0xb000 };
                IntPtr device;
                IntPtr context;
                int level;

                Check(D3D11CreateDevice(adapter, adapter == IntPtr.Zero ? 1 : 0, IntPtr.Zero, 0x800 | 0x20, levels, 4, 7, &device, &level, &context), "D3D11CreateDevice");

                var multithread = Query(context, MultithreadId);

                if (multithread != IntPtr.Zero)
                {
                    ((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Slot(multithread, 5))(multithread, 1);

                    Release(multithread);
                }

                var videoDevice = Query(device, VideoDeviceId);
                var videoContext = Query(context, VideoContextId);

                if (videoDevice == IntPtr.Zero || videoContext == IntPtr.Zero)
                {
                    Release(videoDevice);
                    Release(videoContext);
                    Release(context);
                    Release(device);

                    throw new PlayerException("The graphics card has no D3D11 video support");
                }

                return new Direct3D(device, context, videoDevice, videoContext, Query(context, VideoContext1Id), name);
            }
            finally
            {
                Release(adapter);
            }
        }

        private static IntPtr FindAdapter(byte[] luid, out string name)
        {
            name = "default adapter";

            IntPtr factory;
            var id = FactoryId;

            if (CreateDXGIFactory1(&id, &factory) < 0)
            {
                return IntPtr.Zero;
            }

            try
            {
                IntPtr first = IntPtr.Zero;
                string firstName = null;

                for (uint index = 0; ; index++)
                {
                    IntPtr adapter;

                    if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)Slot(factory, 12))(factory, index, &adapter) < 0)
                    {
                        break;
                    }

                    AdapterDesc1 description;

                    ((delegate* unmanaged[Stdcall]<IntPtr, AdapterDesc1*, int>)Slot(adapter, 10))(adapter, &description);

                    var adapterName = new string(description.Description);
                    var matches = luid != null && luid.Length == 8 && BitConverter.ToUInt32(luid, 0) == description.LuidLow && BitConverter.ToInt32(luid, 4) == description.LuidHigh;

                    if (matches)
                    {
                        Release(first);

                        name = adapterName.TrimEnd('\0');

                        return adapter;
                    }

                    if (first == IntPtr.Zero)
                    {
                        first = adapter;
                        firstName = adapterName.TrimEnd('\0');
                    }
                    else
                    {
                        Release(adapter);
                    }
                }

                if (first != IntPtr.Zero)
                {
                    name = firstName;
                }

                return first;
            }
            finally
            {
                Release(factory);
            }
        }

        public IntPtr CreateTexture(uint width, uint height, int format, int usage, uint bind, uint cpuAccess, uint misc)
        {
            var description = new Texture2DDesc
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = format,
                SampleCount = 1,
                SampleQuality = 0,
                Usage = usage,
                BindFlags = bind,
                CpuAccessFlags = cpuAccess,
                MiscFlags = misc
            };

            IntPtr texture;

            Check(((delegate* unmanaged[Stdcall]<IntPtr, Texture2DDesc*, IntPtr, IntPtr*, int>)Slot(Device, 5))(Device, &description, IntPtr.Zero, &texture), "CreateTexture2D");

            return texture;
        }

        public IntPtr CreateRenderTargetView(IntPtr texture)
        {
            IntPtr view;

            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr*, int>)Slot(Device, 9))(Device, texture, IntPtr.Zero, &view), "CreateRenderTargetView");

            return view;
        }

        public static Texture2DDesc Describe(IntPtr texture)
        {
            Texture2DDesc description;

            ((delegate* unmanaged[Stdcall]<IntPtr, Texture2DDesc*, void>)Slot(texture, 10))(texture, &description);

            return description;
        }

        public void UpdateSubresource(IntPtr resource, IntPtr data, uint rowPitch, uint depthPitch)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, IntPtr, IntPtr, uint, uint, void>)Slot(Context, 48))(Context, resource, 0, IntPtr.Zero, data, rowPitch, depthPitch);
        }

        public void Clear(IntPtr renderTargetView, float red, float green, float blue, float alpha)
        {
            var color = stackalloc float[] { red, green, blue, alpha };

            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, float*, void>)Slot(Context, 50))(Context, renderTargetView, color);
        }

        public void CopyResource(IntPtr destination, IntPtr source)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, void>)Slot(Context, 47))(Context, destination, source);
        }

        public MappedSubresource Map(IntPtr resource)
        {
            MappedSubresource mapped;

            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, uint, MappedSubresource*, int>)Slot(Context, 14))(Context, resource, 0, 1, 0, &mapped), "Map");

            return mapped;
        }

        public void Unmap(IntPtr resource)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, void>)Slot(Context, 15))(Context, resource, 0);
        }

        public void Flush()
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, void>)Slot(Context, 111))(Context);
        }

        public IntPtr CreateProcessorEnumerator(ContentDesc description)
        {
            IntPtr enumerator;

            Check(((delegate* unmanaged[Stdcall]<IntPtr, ContentDesc*, IntPtr*, int>)Slot(VideoDevice, 10))(VideoDevice, &description, &enumerator), "CreateVideoProcessorEnumerator");

            return enumerator;
        }

        public static uint ProcessorFormatSupport(IntPtr enumerator, int format)
        {
            uint flags = 0;

            return ((delegate* unmanaged[Stdcall]<IntPtr, int, uint*, int>)Slot(enumerator, 8))(enumerator, format, &flags) < 0 ? 0 : flags;
        }

        public IntPtr CreateProcessor(IntPtr enumerator)
        {
            IntPtr processor;

            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, IntPtr*, int>)Slot(VideoDevice, 4))(VideoDevice, enumerator, 0, &processor), "CreateVideoProcessor");

            return processor;
        }

        public IntPtr CreateInputView(IntPtr texture, IntPtr enumerator, uint slice)
        {
            var description = new InputViewDesc { FourCC = 0, ViewDimension = 1, MipSlice = 0, ArraySlice = slice };
            IntPtr view;

            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, InputViewDesc*, IntPtr*, int>)Slot(VideoDevice, 8))(VideoDevice, texture, enumerator, &description, &view), "CreateVideoProcessorInputView");

            return view;
        }

        public IntPtr CreateOutputView(IntPtr texture, IntPtr enumerator)
        {
            var description = new OutputViewDesc { ViewDimension = 1 };
            IntPtr view;

            Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, OutputViewDesc*, IntPtr*, int>)Slot(VideoDevice, 9))(VideoDevice, texture, enumerator, &description, &view), "CreateVideoProcessorOutputView");

            return view;
        }

        public void SetOutputTargetRect(IntPtr processor, Rect rect)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, Rect*, void>)Slot(VideoContext, 13))(VideoContext, processor, 1, &rect);
        }

        public void SetOutputBackground(IntPtr processor, float red, float green, float blue)
        {
            var color = new VideoColor { First = red, Second = green, Third = blue, Fourth = 1 };

            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, VideoColor*, void>)Slot(VideoContext, 14))(VideoContext, processor, 0, &color);
        }

        public void SetStreamFrameFormat(IntPtr processor, int format)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, void>)Slot(VideoContext, 27))(VideoContext, processor, 0, format);
        }

        public void SetStreamOutputRate(IntPtr processor, int rate)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, int, IntPtr, void>)Slot(VideoContext, 29))(VideoContext, processor, 0, rate, 0, IntPtr.Zero);
        }

        public void SetStreamSourceRect(IntPtr processor, Rect rect)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, Rect*, void>)Slot(VideoContext, 30))(VideoContext, processor, 0, 1, &rect);
        }

        public void SetStreamDestRect(IntPtr processor, Rect rect)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, Rect*, void>)Slot(VideoContext, 31))(VideoContext, processor, 0, 1, &rect);
        }

        public void SetStreamAutoProcessing(IntPtr processor, bool enabled)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, void>)Slot(VideoContext, 37))(VideoContext, processor, 0, enabled ? 1 : 0);
        }

        public void SetOutputColorSpace(IntPtr processor, int colorSpace)
        {
            if (VideoContext1 != IntPtr.Zero)
            {
                ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, void>)Slot(VideoContext1, 70))(VideoContext1, processor, colorSpace);
            }
        }

        public void SetStreamColorSpace(IntPtr processor, int colorSpace)
        {
            if (VideoContext1 != IntPtr.Zero)
            {
                ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, void>)Slot(VideoContext1, 74))(VideoContext1, processor, 0, colorSpace);
            }
        }

        public int Blt(IntPtr processor, IntPtr outputView, IntPtr inputView, uint frameNumber)
        {
            var stream = new ProcessorStream { Enable = 1, OutputIndex = 0, InputFrameOrField = frameNumber, InputSurface = inputView };

            return ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, uint, uint, ProcessorStream*, int>)Slot(VideoContext, 53))(VideoContext, processor, outputView, 0, 1, &stream);
        }

        public static IntPtr SharedHandle(IntPtr texture)
        {
            var resource = Query(texture, ResourceId);

            if (resource == IntPtr.Zero)
            {
                throw new PlayerException("The texture has no DXGI resource");
            }

            try
            {
                IntPtr handle;

                Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(resource, 8))(resource, &handle), "GetSharedHandle");

                return handle;
            }
            finally
            {
                Release(resource);
            }
        }

        public static IntPtr KeyedMutex(IntPtr texture) => Query(texture, KeyedMutexId);

        public static bool Acquire(IntPtr mutex, ulong key, uint milliseconds)
        {
            var result = ((delegate* unmanaged[Stdcall]<IntPtr, ulong, uint, int>)Slot(mutex, 8))(mutex, key, milliseconds);

            return result == 0;
        }

        public static void ReleaseKey(IntPtr mutex, ulong key)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, ulong, int>)Slot(mutex, 9))(mutex, key);
        }

        public static void AddRef(IntPtr unknown)
        {
            if (unknown != IntPtr.Zero)
            {
                ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(unknown, 1))(unknown);
            }
        }

        public static void Release(IntPtr unknown)
        {
            if (unknown != IntPtr.Zero)
            {
                ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(unknown, 2))(unknown);
            }
        }

        public static bool IsDeviceLoss(int result)
        {
            return result == unchecked((int)0x887A0005) || result == unchecked((int)0x887A0006) || result == unchecked((int)0x887A0007) || result == unchecked((int)0x887A0020);
        }

        private static IntPtr Query(IntPtr unknown, Guid id)
        {
            if (unknown == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            IntPtr result;

            return ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)Slot(unknown, 0))(unknown, &id, &result) < 0 ? IntPtr.Zero : result;
        }

        private static void* Slot(IntPtr unknown, int index)
        {
            return (*(void***)unknown)[index];
        }

        private static void Check(int result, string what)
        {
            if (result < 0)
            {
                throw new Direct3DException(what, result);
            }
        }

        public void Dispose()
        {
            Release(VideoContext1);
            Release(VideoContext);
            Release(VideoDevice);
            Release(Context);
            Release(Device);

            VideoContext1 = VideoContext = VideoDevice = Context = Device = IntPtr.Zero;
        }

        [DllImport("d3d11.dll", ExactSpelling = true)]
        private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, int* featureLevels, uint featureLevelCount, uint sdkVersion, IntPtr* device, int* featureLevel, IntPtr* context);

        [DllImport("dxgi.dll", ExactSpelling = true)]
        private static extern int CreateDXGIFactory1(Guid* id, IntPtr* factory);

        [StructLayout(LayoutKind.Sequential)]
        public struct Texture2DDesc
        {
            public uint Width;
            public uint Height;
            public uint MipLevels;
            public uint ArraySize;
            public int Format;
            public uint SampleCount;
            public uint SampleQuality;
            public int Usage;
            public uint BindFlags;
            public uint CpuAccessFlags;
            public uint MiscFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ContentDesc
        {
            public int InputFrameFormat;
            public uint InputRateNumerator;
            public uint InputRateDenominator;
            public uint InputWidth;
            public uint InputHeight;
            public uint OutputRateNumerator;
            public uint OutputRateDenominator;
            public uint OutputWidth;
            public uint OutputHeight;
            public int Usage;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public Rect(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MappedSubresource
        {
            public IntPtr Data;
            public uint RowPitch;
            public uint DepthPitch;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct InputViewDesc
        {
            public uint FourCC;
            public int ViewDimension;
            public uint MipSlice;
            public uint ArraySlice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OutputViewDesc
        {
            public int ViewDimension;
            public uint MipSlice;
            public uint FirstArraySlice;
            public uint ArraySize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VideoColor
        {
            public float First;
            public float Second;
            public float Third;
            public float Fourth;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessorStream
        {
            public int Enable;
            public uint OutputIndex;
            public uint InputFrameOrField;
            public uint PastFrames;
            public uint FutureFrames;
            public IntPtr PastSurfaces;
            public IntPtr InputSurface;
            public IntPtr FutureSurfaces;
            public IntPtr PastSurfacesRight;
            public IntPtr InputSurfaceRight;
            public IntPtr FutureSurfacesRight;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AdapterDesc1
        {
            public fixed char Description[128];
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public nuint DedicatedVideoMemory;
            public nuint DedicatedSystemMemory;
            public nuint SharedSystemMemory;
            public uint LuidLow;
            public int LuidHigh;
            public uint Flags;
        }
    }

    public sealed class Direct3DException : Exception
    {
        public Direct3DException(string what, int result) : base($"{what} failed with 0x{result:X8}")
        {
            Result = result;
        }

        public int Result { get; }

        public bool IsDeviceLoss => Direct3D.IsDeviceLoss(Result);
    }
}
