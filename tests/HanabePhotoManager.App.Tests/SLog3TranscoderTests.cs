using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using HanabePhotoManager.App.Video;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class SLog3TranscoderTests
{
    [Fact]
    public void Presets_ChangeOutputQualityWithoutChangingSourceInterpretation()
    {
        var original = new VideoProcessingProfile(SonySourceGamut.SGamut3, null,
            VideoEncoder.H264Software, 8, InputRange: InputVideoRange.Limited,
            InputMatrix: InputYuvMatrix.Bt2020);
        var compact = VideoProcessingPresets.Apply(VideoPreset.SpaceSaver, original);
        var quality = VideoProcessingPresets.Apply(VideoPreset.HighQuality, original);
        Assert.Equal(VideoEncoder.H265Software, compact.Encoder);
        Assert.Equal(10, compact.OutputBitDepth);
        Assert.True(compact.Quality > quality.Quality); // larger CRF means smaller output
        Assert.Equal(original.SourceGamut, compact.SourceGamut);
        Assert.Equal(original.InputRange, quality.InputRange);
        Assert.Equal(original.InputMatrix, quality.InputMatrix);
        Assert.Equal(original, VideoProcessingPresets.Apply(VideoPreset.Custom, original));
    }
    [Fact]
    public void StandardTransform_MapsSonyReferenceGrayToRec709()
    {
        Assert.Equal(0.18, SonyRec709Transform.DecodeSLog3(420.0 / 1023.0), 6);
        var (r, g, b) = SonyRec709Transform.Convert(420.0 / 1023.0, 420.0 / 1023.0,
            420.0 / 1023.0, SonySourceGamut.SGamut3Cine);
        Assert.InRange(r, 0.407, 0.410);
        Assert.InRange(Math.Abs(r - g), 0, 0.000001);
        Assert.InRange(Math.Abs(r - b), 0, 0.000001);
    }

    [Fact]
    public void Profile_BuildsSameColorFilterForPreviewAndExport_AndPreservesGeometryAndFps()
    {
        var profile = new VideoProcessingProfile(SonySourceGamut.SGamut3, null,
            VideoEncoder.H265Software, 10, InputRange: InputVideoRange.Limited);
        var preview = SLog3Transcoder.BuildArguments("input.mov", "preview.mp4", profile, true);
        var export = SLog3Transcoder.BuildArguments("input.mov", "output.mp4", profile, false);
        Assert.Equal(preview[Array.IndexOf(preview, "-vf") + 1], export[Array.IndexOf(export, "-vf") + 1]);
        Assert.Contains("in_range=limited", preview[Array.IndexOf(preview, "-vf") + 1]);
        Assert.Contains("format=gbrpf32le", preview[Array.IndexOf(preview, "-vf") + 1]);
        Assert.Contains("format=yuv420p10le", preview[Array.IndexOf(preview, "-vf") + 1]);
        Assert.DoesNotContain("-r", export);
        Assert.DoesNotContain("-s", export);
    }

    [Fact]
    public void InvalidCube_IsRejectedBeforeEncoderStarts()
    {
        var directory = Path.Combine(@"D:\HanabePhoto", ".artifacts", "slog3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var lut = Path.Combine(directory, "bad.cube");
            File.WriteAllText(lut, "LUT_3D_SIZE 2\n0 0 0\n");
            Assert.Throws<InvalidDataException>(() => new VideoProcessingProfile(SonySourceGamut.SGamut3Cine, lut).Validate());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void InvalidCubeDomain_IsRejected()
    {
        var directory = Path.Combine(@"D:\HanabePhoto", ".artifacts", "slog3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var lut = Path.Combine(directory, "bad-domain.cube");
            File.WriteAllText(lut, "LUT_3D_SIZE 2\nDOMAIN_MIN 1 0 0\nDOMAIN_MAX 0 1 1\n" +
                string.Concat(Enumerable.Repeat("0 0 0\n", 8)));
            Assert.Throws<InvalidDataException>(() => CubeLut.Validate(lut));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Queue_RestartsInterruptedJobAsRetryableWithFrozenSettings()
    {
        var directory = Path.Combine(@"D:\HanabePhoto", ".artifacts", "slog3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "source.mp4");
            File.WriteAllText(source, "fixture");
            var original = new SLog3ViewModel(directory);
            original.Add([source]);
            var job = Assert.Single(original.Jobs);
            job.State = "处理中";
            job.FrozenProfile = new VideoProcessingProfile(SonySourceGamut.SGamut3, null,
                VideoEncoder.H265Software, 10, InputRange: InputVideoRange.Limited);
            job.FrozenOutputDirectory = Path.Combine(directory, "exports");
            original.SaveQueue();
            var restored = new SLog3ViewModel(directory);
            var pending = Assert.Single(restored.Jobs);
            Assert.Equal("失败", pending.State);
            Assert.Equal(InputVideoRange.Limited, pending.FrozenProfile!.InputRange);
            Assert.Equal(job.FrozenOutputDirectory, pending.FrozenOutputDirectory);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void Window_InitializesOnStaThread()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new SLog3Window();
                Assert.Equal(@"D:\HanabePhoto\SLog3Exports", window.OutputPathBox.Text);
                window.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void ExportName_NeverReusesExistingResult()
    {
        var directory = Path.Combine(@"D:\HanabePhoto", ".artifacts", "slog3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var first = SLog3Transcoder.FindAvailableOutputPath(@"D:\Camera\C0001.MOV", directory);
            File.WriteAllText(first, "existing");
            var second = SLog3Transcoder.FindAvailableOutputPath(@"D:\Camera\C0001.MOV", directory);
            Assert.NotEqual(first, second);
            Assert.EndsWith("_2.mp4", second);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task IdentityLut_ExportsAndPreviewsWithoutChangingSource()
    {
        // FFmpeg is an external executable, so this smoke test runs on machines where it is installed.
        try
        {
            using var version = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            if (version is null) return;
            await version.WaitForExitAsync();
            if (version.ExitCode != 0) return;
        }
        catch (System.ComponentModel.Win32Exception) { return; }

        var directory = Path.Combine(@"D:\HanabePhoto", ".artifacts", "slog3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "source.mp4");
            using (var fixture = Process.Start(new ProcessStartInfo("ffmpeg")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
                ArgumentList = { "-nostdin", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=gray:s=64x64:d=1", "-c:v", "mpeg4", "-y", source }
            })!)
            {
                var error = await fixture.StandardError.ReadToEndAsync();
                await fixture.WaitForExitAsync();
                Assert.True(fixture.ExitCode == 0, error);
            }
            var sourceHash = SHA256.HashData(File.ReadAllBytes(source));
            var lut = Path.Combine(directory, "identity.cube");
            File.WriteAllText(lut, "LUT_3D_SIZE 2\n0 0 0\n1 0 0\n0 1 0\n1 1 0\n0 0 1\n1 0 1\n0 1 1\n1 1 1\n");

            await SLog3Transcoder.ExportAsync(source, directory, lut, CancellationToken.None);
            var exported = Path.Combine(directory, "source_SLog3_Rec709.mp4");
            Assert.True(new FileInfo(exported).Length > 0);
            Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));

            var preview = Path.Combine(directory, "preview.mp4");
            await SLog3Transcoder.ConvertAsync(source, preview, lut, preview: true, CancellationToken.None);
            Assert.True(new FileInfo(preview).Length > 0);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task StandardTransform_EncodesTenBitAndGrayPatch()
    {
        try { _ = FfmpegExecutableLocator.Resolve(); }
        catch (FileNotFoundException) { return; }
        var directory = Path.Combine(@"D:\HanabePhoto", ".artifacts", "slog3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "gray.mkv");
            var ffmpeg = FfmpegExecutableLocator.Resolve();
            using (var fixture = Process.Start(new ProcessStartInfo(ffmpeg)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
                ArgumentList = { "-nostdin", "-loglevel", "error", "-f", "lavfi", "-i",
                    "color=c=0x696969:s=64x64:r=1:d=1", "-frames:v", "1", "-c:v", "ffv1", "-y", source }
            })!)
            {
                var error = await fixture.StandardError.ReadToEndAsync();
                await fixture.WaitForExitAsync();
                Assert.True(fixture.ExitCode == 0, error);
            }
            var originalHash = SHA256.HashData(File.ReadAllBytes(source));
            var sourceInfo = await VideoSourceProbe.ReadAsync(source, CancellationToken.None);
            if (sourceInfo is not null)
            {
                Assert.Equal(64, sourceInfo.Width);
                Assert.Equal(64, sourceInfo.Height);
                Assert.Equal("1/1", sourceInfo.FrameRate);
            }
            var profile = new VideoProcessingProfile(SonySourceGamut.SGamut3Cine, null,
                VideoEncoder.H265Software, 10, InputRange: InputVideoRange.Limited);
            var output = await SLog3Transcoder.ExportAsync(source, directory, profile, CancellationToken.None);
            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source)));
            var ffprobe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe");
            if (File.Exists(ffprobe))
            {
                using var probe = Process.Start(new ProcessStartInfo(ffprobe)
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                    ArgumentList = { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height,avg_frame_rate,pix_fmt,color_range,color_primaries,color_transfer,color_space", "-of", "default=noprint_wrappers=1", output }
                })!;
                var metadata = await probe.StandardOutput.ReadToEndAsync();
                await probe.WaitForExitAsync();
                Assert.Contains("pix_fmt=yuv420p10le", metadata);
                Assert.True(metadata.Contains("color_primaries=bt709", StringComparison.Ordinal), metadata);
                Assert.Contains("color_range=tv", metadata);
                Assert.Contains("color_transfer=bt709", metadata);
                Assert.Contains("width=64", metadata);
                Assert.Contains("height=64", metadata);
                Assert.Contains("avg_frame_rate=1/1", metadata);
            }
            var frame = Path.Combine(directory, "frame.rgb");
            using (var decode = Process.Start(new ProcessStartInfo(ffmpeg)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
                ArgumentList = { "-nostdin", "-loglevel", "error", "-i", output, "-frames:v", "1",
                    "-f", "rawvideo", "-pix_fmt", "rgb24", "-y", frame }
            })!)
            {
                var error = await decode.StandardError.ReadToEndAsync();
                await decode.WaitForExitAsync();
                Assert.True(decode.ExitCode == 0, error);
            }
            var pixel = File.ReadAllBytes(frame)[0];
            Assert.InRange(pixel, 95, 120); // neutral 18% gray maps near 0.41 in BT.709
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ConcurrentDestinationCreation_IsPreserved()
    {
        try { _ = FfmpegExecutableLocator.Resolve(); }
        catch (FileNotFoundException) { return; }
        var directory = Path.Combine(@"D:\HanabePhoto", ".artifacts", "slog3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "source.mp4");
            var output = Path.Combine(directory, "result.mp4");
            using (var fixture = Process.Start(new ProcessStartInfo(FfmpegExecutableLocator.Resolve())
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
                ArgumentList = { "-nostdin", "-loglevel", "error", "-f", "lavfi", "-i",
                    "color=c=gray:s=64x64:d=1", "-c:v", "mpeg4", "-y", source }
            })!)
            {
                var error = await fixture.StandardError.ReadToEndAsync();
                await fixture.WaitForExitAsync();
                Assert.True(fixture.ExitCode == 0, error);
            }
            var profile = new VideoProcessingProfile(SonySourceGamut.SGamut3Cine, null,
                VideoEncoder.H264Software, 8, InputRange: InputVideoRange.Limited);
            var progress = new CallbackProgress(() => File.WriteAllText(output, "other user's result"));
            await Assert.ThrowsAsync<IOException>(() => SLog3Transcoder.ConvertAsync(source, output, profile,
                false, CancellationToken.None, progress));
            Assert.Equal("other user's result", File.ReadAllText(output));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task SoftwareEncoderProbe_VerifiesActualTenBitOutput()
    {
        try { _ = FfmpegExecutableLocator.Resolve(); }
        catch (FileNotFoundException) { return; }
        var capability = await VideoEncoderProbe.ProbeAsync(VideoEncoder.H265Software, 10, CancellationToken.None);
        Assert.True(capability.Available, capability.Reason);
    }

    [Fact]
    public async Task FailedJob_RetryKeepsFrozenProfileAndOriginal()
    {
        try { _ = FfmpegExecutableLocator.Resolve(); }
        catch (FileNotFoundException) { return; }
        var directory = Path.Combine(@"D:\HanabePhoto", ".artifacts", "slog3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "broken.mp4");
            File.WriteAllText(source, "not a video");
            var hash = SHA256.HashData(File.ReadAllBytes(source));
            var model = new SLog3ViewModel(directory) { OutputDirectory = Path.Combine(directory, "exports"),
                Encoder = VideoEncoder.H264Software, BitDepth = 8 };
            model.Add([source]);
            await model.ExportAsync();
            var job = Assert.Single(model.Jobs);
            Assert.Equal("失败", job.State);
            Assert.Equal(VideoEncoder.H264Software, job.FrozenProfile!.Encoder);
            model.Encoder = VideoEncoder.H265Software;
            await model.ExportAsync(failuresOnly: true);
            Assert.Equal("失败", job.State);
            Assert.Equal(VideoEncoder.H264Software, job.FrozenProfile.Encoder);
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(source)));
            Assert.Empty(Directory.GetFiles(model.OutputDirectory, "*.mp4"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CancellationBeforePublish_LeavesNoOutputOrStage()
    {
        try { _ = FfmpegExecutableLocator.Resolve(); }
        catch (FileNotFoundException) { return; }
        var directory = Path.Combine(@"D:\HanabePhoto", ".artifacts", "slog3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "source.mp4");
            var output = Path.Combine(directory, "result.mp4");
            using (var fixture = Process.Start(new ProcessStartInfo(FfmpegExecutableLocator.Resolve())
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
                ArgumentList = { "-nostdin", "-loglevel", "error", "-f", "lavfi", "-i",
                    "color=c=gray:s=64x64:d=1", "-c:v", "mpeg4", "-y", source }
            })!)
            {
                var error = await fixture.StandardError.ReadToEndAsync();
                await fixture.WaitForExitAsync();
                Assert.True(fixture.ExitCode == 0, error);
            }
            var hash = SHA256.HashData(File.ReadAllBytes(source));
            using var cancellation = new CancellationTokenSource();
            var progress = new CallbackProgress(cancellation.Cancel);
            var profile = new VideoProcessingProfile(SonySourceGamut.SGamut3Cine, null,
                VideoEncoder.H264Software, 8, InputRange: InputVideoRange.Limited);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SLog3Transcoder.ConvertAsync(source,
                output, profile, false, cancellation.Token, progress));
            Assert.False(File.Exists(output));
            Assert.Empty(Directory.GetFiles(directory, ".slog3-stage-*.mp4"));
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(source)));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task BatchFreezesCustomLutBytesBeforeProcessing()
    {
        try { _ = FfmpegExecutableLocator.Resolve(); }
        catch (FileNotFoundException) { return; }
        var directory = Path.Combine(@"D:\HanabePhoto", ".artifacts", "slog3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "broken.mp4");
            File.WriteAllText(source, "invalid media");
            var lut = Path.Combine(directory, "look.cube");
            File.WriteAllText(lut, "LUT_3D_SIZE 2\n0 0 0\n1 0 0\n0 1 0\n1 1 0\n0 0 1\n1 0 1\n0 1 1\n1 1 1\n");
            var original = SHA256.HashData(File.ReadAllBytes(lut));
            var model = new SLog3ViewModel(directory) { OutputDirectory = Path.Combine(directory, "exports"),
                Encoder = VideoEncoder.H264Software, BitDepth = 8, LutPath = lut };
            model.Add([source]);
            await model.ExportAsync();
            var frozen = Assert.Single(model.Jobs).FrozenProfile!.CustomLutPath!;
            File.WriteAllText(lut, "changed by user");
            Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(frozen)));
            Assert.StartsWith(Path.Combine(directory, "video", "luts"), frozen);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class CallbackProgress(Action callback) : IProgress<double>
    {
        public void Report(double value) => callback();
    }
}

