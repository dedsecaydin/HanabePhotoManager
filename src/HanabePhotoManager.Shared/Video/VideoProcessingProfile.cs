using System.Globalization;
using System.IO;

namespace HanabePhotoManager.App.Video;

internal enum SonySourceGamut { SGamut3Cine, SGamut3 }
internal enum VideoEncoder { H264Software, H265Software, H264Nvenc, H265Nvenc, H264Amf, H265Amf }
internal enum VideoQualityMode { ConstantQuality, TargetBitrate }
internal enum VideoPreset { SpaceSaver, HighQuality, Custom }
internal enum InputVideoRange { Full, Limited }
internal enum InputYuvMatrix { Bt709, Bt2020 }

/// <summary>A frozen description used by both the short preview and the final encode.</summary>
internal sealed record VideoProcessingProfile(
    SonySourceGamut SourceGamut,
    string? CustomLutPath,
    VideoEncoder Encoder = VideoEncoder.H265Software,
    int OutputBitDepth = 10,
    VideoQualityMode QualityMode = VideoQualityMode.ConstantQuality,
    int Quality = 20,
    int TargetBitrateMbps = 20,
    bool AllowSoftwareFallback = false,
    InputVideoRange InputRange = InputVideoRange.Full,
    InputYuvMatrix InputMatrix = InputYuvMatrix.Bt709)
{
    public void Validate()
    {
        if (!Enum.IsDefined(SourceGamut) || !Enum.IsDefined(Encoder) || !Enum.IsDefined(QualityMode) ||
            !Enum.IsDefined(InputRange) || !Enum.IsDefined(InputMatrix))
            throw new ArgumentException("视频处理配置包含未知选项。");
        if (OutputBitDepth is not (8 or 10)) throw new ArgumentOutOfRangeException(nameof(OutputBitDepth));
        if (Encoder is VideoEncoder.H264Software or VideoEncoder.H264Nvenc or VideoEncoder.H264Amf && OutputBitDepth == 10)
            throw new ArgumentException("当前 H.264 选项仅提供 8-bit；请选 H.265 或改为 8-bit。");
        if (Quality is < 1 or > 40) throw new ArgumentOutOfRangeException(nameof(Quality));
        if (TargetBitrateMbps is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(TargetBitrateMbps));
        if (CustomLutPath is { Length: > 0 }) CubeLut.Validate(CustomLutPath);
    }

    public string EncoderName => Encoder switch
    {
        VideoEncoder.H264Software => "libx264", VideoEncoder.H265Software => "libx265",
        VideoEncoder.H264Nvenc => "h264_nvenc", VideoEncoder.H265Nvenc => "hevc_nvenc",
        VideoEncoder.H264Amf => "h264_amf", _ => "hevc_amf"
    };

    public string PixelFormat => OutputBitDepth == 10 ? "yuv420p10le" : "yuv420p";
    public VideoProcessingProfile SoftwareVersion() => this with
    {
        Encoder = Encoder is VideoEncoder.H264Nvenc or VideoEncoder.H264Amf ? VideoEncoder.H264Software : VideoEncoder.H265Software
    };

    internal IEnumerable<string> EncoderArguments(bool preview)
    {
        yield return "-c:v"; yield return EncoderName;
        yield return "-pix_fmt"; yield return PixelFormat;
        if (Encoder is VideoEncoder.H264Software or VideoEncoder.H265Software)
        {
            yield return "-preset"; yield return preview ? "ultrafast" : "medium";
            if (QualityMode == VideoQualityMode.ConstantQuality) { yield return "-crf"; yield return Quality.ToString(CultureInfo.InvariantCulture); }
        }
        else if (QualityMode == VideoQualityMode.ConstantQuality)
        {
            if (Encoder is VideoEncoder.H264Nvenc or VideoEncoder.H265Nvenc)
            {
                yield return "-rc"; yield return "vbr";
                yield return "-cq"; yield return Quality.ToString(CultureInfo.InvariantCulture);
                yield return "-b:v"; yield return "0";
            }
            else
            {
                yield return "-rc"; yield return "cqp";
                yield return "-qp_i"; yield return Quality.ToString(CultureInfo.InvariantCulture);
                yield return "-qp_p"; yield return Quality.ToString(CultureInfo.InvariantCulture);
            }
        }
        if (QualityMode == VideoQualityMode.TargetBitrate)
        {
            yield return "-b:v"; yield return $"{TargetBitrateMbps}M";
        }
    }
}

internal static class VideoProcessingPresets
{
    internal static VideoProcessingProfile Apply(VideoPreset preset, VideoProcessingProfile current) => preset switch
    {
        VideoPreset.SpaceSaver => current with { Encoder = VideoEncoder.H265Software, OutputBitDepth = 10,
            QualityMode = VideoQualityMode.ConstantQuality, Quality = 28, TargetBitrateMbps = 12,
            AllowSoftwareFallback = false },
        VideoPreset.HighQuality => current with { Encoder = VideoEncoder.H265Software, OutputBitDepth = 10,
            QualityMode = VideoQualityMode.ConstantQuality, Quality = 18, TargetBitrateMbps = 40,
            AllowSoftwareFallback = false },
        VideoPreset.Custom => current,
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };
}

internal static class CubeLut
{
    internal static void Validate(string path)
    {
        if (!File.Exists(path) || !Path.GetExtension(path).Equals(".cube", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("请选择有效的 .cube 文件。", path);
        int? size = null; var count = 0;
        double[]? domainMin = null, domainMax = null;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("TITLE ", StringComparison.OrdinalIgnoreCase)) continue;
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields[0].Equals("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase))
            {
                if (size is not null || fields.Length != 2 || !int.TryParse(fields[1], out var parsed) || parsed is < 2 or > 65)
                    throw new InvalidDataException("LUT_3D_SIZE 无效。");
                size = parsed; continue;
            }
            if (fields[0] is "DOMAIN_MIN" or "DOMAIN_MAX")
            {
                if (fields.Length != 4 || !fields.Skip(1).All(x => double.TryParse(x, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)))
                    throw new InvalidDataException(".cube DOMAIN_MIN/MAX 无效。");
                var values = fields.Skip(1).Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                if (fields[0] == "DOMAIN_MIN") domainMin = values; else domainMax = values;
                continue;
            }
            if (fields.Length != 3 || !fields.All(x => double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)))
                throw new InvalidDataException(".cube 包含无效数据。");
            count++;
        }
        if (size is null || count != size * size * size) throw new InvalidDataException(".cube 数据数量与 LUT_3D_SIZE 不符。");
        if (domainMin is not null && domainMax is not null && Enumerable.Range(0, 3).Any(i => domainMin[i] >= domainMax[i]))
            throw new InvalidDataException(".cube DOMAIN_MAX 必须大于 DOMAIN_MIN。");
    }
}
