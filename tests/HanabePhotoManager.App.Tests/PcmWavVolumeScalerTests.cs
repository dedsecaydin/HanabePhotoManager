using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PcmWavVolumeScalerTests
{
    [Fact]
    public void Scale_PreservesHeaderAndScalesSigned16BitSamples()
    {
        var wav = new byte[48];
        "RIFF"u8.CopyTo(wav);
        "WAVE"u8.CopyTo(wav.AsSpan(8));
        "data"u8.CopyTo(wav.AsSpan(36));
        BitConverter.GetBytes(4).CopyTo(wav, 40);
        BitConverter.GetBytes((short)10000).CopyTo(wav, 44);
        BitConverter.GetBytes((short)-10000).CopyTo(wav, 46);

        var scaled = PcmWavVolumeScaler.Scale(wav, 0.5);

        scaled[..44].Should().Equal(wav[..44]);
        BitConverter.ToInt16(scaled, 44).Should().Be(5000);
        BitConverter.ToInt16(scaled, 46).Should().Be(-5000);
    }
}
