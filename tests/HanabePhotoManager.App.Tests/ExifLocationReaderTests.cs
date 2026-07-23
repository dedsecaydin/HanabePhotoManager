using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ExifLocationReaderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"hanabe-no-gps-{Guid.NewGuid():N}.jpg");

    [Theory]
    [InlineData(36.067, 120.382)]
    [InlineData(-33.8688, 151.2093)]
    [InlineData(0, 0)]
    public void Validate_AcceptsCoordinatesInBothHemispheres(double latitude, double longitude)
    {
        ExifLocationReader.Validate(latitude, longitude).Should().Be(new PhotoCoordinate(latitude, longitude));
    }

    [Theory]
    [InlineData(90.01, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 180.01)]
    [InlineData(double.NaN, 0)]
    public void Validate_RejectsMalformedOrOutOfRangeCoordinates(double latitude, double longitude)
    {
        ExifLocationReader.Validate(latitude, longitude).Should().BeNull();
    }

    [Fact]
    public void TryRead_FileWithoutGpsReturnsNullAndDoesNotModifySource()
    {
        File.WriteAllBytes(_path, [0xFF, 0xD8, 0xFF, 0xD9]);
        var before = File.ReadAllBytes(_path);

        new ExifLocationReader().TryRead(_path).Should().BeNull();

        File.ReadAllBytes(_path).Should().Equal(before);
    }

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }
}
