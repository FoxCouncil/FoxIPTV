// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using System;
    using System.Runtime.InteropServices;
    using FFmpeg.AutoGen;
    using FoxIPTV.Playback;
    using FoxIPTV.Playback.Video;

    public class Direct3DTests
    {
        private const uint Size = 128;

        [Fact]
        public unsafe void VideoProcessor_TurnsStudioRedNv12IntoRed()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            using var d3d = Direct3D.Create(null);

            var source = d3d.CreateTexture(Size, Size, Direct3D.FormatNv12, Direct3D.UsageDefault, Direct3D.BindDecoder, 0, 0);
            var target = d3d.CreateTexture(Size, Size, Direct3D.FormatBgra, Direct3D.UsageDefault, Direct3D.BindRenderTarget, 0, 0);
            var staging = d3d.CreateTexture(Size, Size, Direct3D.FormatBgra, Direct3D.UsageStaging, 0, Direct3D.CpuAccessRead, 0);
            var enumerator = IntPtr.Zero;
            var processor = IntPtr.Zero;
            var input = IntPtr.Zero;
            var output = IntPtr.Zero;

            try
            {
                var planes = new byte[Size * Size * 3 / 2];

                Array.Fill(planes, (byte)81, 0, (int)(Size * Size));

                for (var i = (int)(Size * Size); i < planes.Length; i += 2)
                {
                    planes[i] = 90;
                    planes[i + 1] = 240;
                }

                fixed (byte* data = planes)
                {
                    d3d.UpdateSubresource(source, (IntPtr)data, Size, Size * Size * 3 / 2);
                }

                enumerator = d3d.CreateProcessorEnumerator(new Direct3D.ContentDesc { InputRateNumerator = 30, InputRateDenominator = 1, InputWidth = Size, InputHeight = Size, OutputRateNumerator = 30, OutputRateDenominator = 1, OutputWidth = Size, OutputHeight = Size });

                Assert.NotEqual(0u, Direct3D.ProcessorFormatSupport(enumerator, Direct3D.FormatNv12) & Direct3D.ProcessorFormatInput);
                Assert.NotEqual(0u, Direct3D.ProcessorFormatSupport(enumerator, Direct3D.FormatBgra) & Direct3D.ProcessorFormatOutput);

                processor = d3d.CreateProcessor(enumerator);
                input = d3d.CreateInputView(source, enumerator, 0);
                output = d3d.CreateOutputView(target, enumerator);

                d3d.SetStreamAutoProcessing(processor, false);
                d3d.SetStreamFrameFormat(processor, Direct3D.FrameProgressive);
                d3d.SetStreamOutputRate(processor, Direct3D.OutputRateNormal);
                d3d.SetStreamSourceRect(processor, new Direct3D.Rect(0, 0, (int)Size, (int)Size));
                d3d.SetStreamDestRect(processor, new Direct3D.Rect(0, 0, (int)Size, (int)Size));
                d3d.SetOutputTargetRect(processor, new Direct3D.Rect(0, 0, (int)Size, (int)Size));
                d3d.SetOutputBackground(processor, 0, 0, 1);
                d3d.SetOutputColorSpace(processor, Direct3D.ColorSpaceRgbFullP709);
                d3d.SetStreamColorSpace(processor, Direct3D.ColorSpaceStudioP601);

                Assert.Equal(0, d3d.Blt(processor, output, input, 0));

                d3d.CopyResource(staging, target);

                var mapped = d3d.Map(staging);
                var pixel = (byte*)mapped.Data + mapped.RowPitch * (Size / 2) + Size / 2 * 4;
                var (blue, green, red) = (pixel[0], pixel[1], pixel[2]);

                d3d.Unmap(staging);

                Assert.True(red > 200 && green < 60 && blue < 60, $"expected red, got R{red} G{green} B{blue}");
                Assert.False(d3d.IsRemoved);
            }
            finally
            {
                Direct3D.Release(output);
                Direct3D.Release(input);
                Direct3D.Release(processor);
                Direct3D.Release(enumerator);
                Direct3D.Release(staging);
                Direct3D.Release(target);
                Direct3D.Release(source);
            }
        }

        [Fact]
        public void SharedTexture_HasHandleAndKeyedMutex()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            using var d3d = Direct3D.Create(null);

            var texture = d3d.CreateTexture(64, 64, Direct3D.FormatBgra, Direct3D.UsageDefault, Direct3D.BindRenderTarget | Direct3D.BindShaderResource, 0, Direct3D.MiscSharedKeyedMutex);
            var mutex = Direct3D.KeyedMutex(texture);
            var view = d3d.CreateRenderTargetView(texture);

            try
            {
                Assert.NotEqual(IntPtr.Zero, Direct3D.SharedHandle(texture));
                Assert.NotEqual(IntPtr.Zero, mutex);

                Assert.True(Direct3D.Acquire(mutex, 0, 100));

                d3d.Clear(view, 0, 0, 0, 1);
                d3d.Flush();

                Direct3D.ReleaseKey(mutex, 1);

                Assert.False(Direct3D.Acquire(mutex, 0, 10));
                Assert.True(Direct3D.Acquire(mutex, 1, 100));

                Direct3D.ReleaseKey(mutex, 0);

                var description = Direct3D.Describe(texture);

                Assert.Equal(64u, description.Width);
                Assert.Equal(Direct3D.FormatBgra, description.Format);
            }
            finally
            {
                Direct3D.Release(view);
                Direct3D.Release(mutex);
                Direct3D.Release(texture);
            }
        }

        [Fact]
        public unsafe void HardwareDevice_ShareOutlivesTheOriginal()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            Assert.True(FFmpegNative.Initialize(), FFmpegNative.Failure);

            using var d3d = Direct3D.Create(null);

            var original = HardwareDevice.FromD3D11(d3d.Device);

            Assert.NotNull(original);

            using var share = original.Share();

            original.Dispose();

            Assert.Null(original.Share());

            var context = (AVHWDeviceContext*)share.Reference->data;

            Assert.Equal(AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA, context->type);
            Assert.Equal(d3d.Device, (IntPtr)((AVD3D11VADeviceContext*)context->hwctx)->device);
        }
    }
}
