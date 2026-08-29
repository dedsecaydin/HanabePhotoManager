using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ImportResumeStoreTests
{
    [Fact]
    public void SaveAndLoad_PreservesTheConfirmedTargetDateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "HanabePhotoManagerTests", Guid.NewGuid().ToString("N"), "resume.json");
        try
        {
            var store = new ImportResumeStore(path);
            store.Save(new ImportResumeState
            {
                Entries = [new ImportResumeEntry
                {
                    GroupKey = "DSC_1234",
                    Category = "Jpeg",
                    PrimaryPath = @"D:\source\DSC_1234.JPG",
                    Year = 2026,
                    Month = 8,
                    Day = 19,
                    TargetDateDirectory = @"D:\library\8月\08.19_客户寿司",
                }]
            });

            store.Load()!.Entries.Single().TargetDateDirectory.Should().Be(@"D:\library\8月\08.19_客户寿司");
        }
        finally
        {
            var directory = Path.GetDirectoryName(path)!;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
