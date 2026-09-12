using System.IO;
using System.IO.Pipes;
using FluentAssertions;
using HanabePhotoManager.App.Recovery;
using HanabePhotoManager.Infrastructure.Files;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class RecoveryDeviceSessionTests
{
    [Theory]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"\\server\share")]
    public void ReaderRejectsArbitraryDeviceAndDirectoryPaths(string path)
    {
        Action open = () => { using var reader = new ReadOnlyRecoveryVolume(path, @"D:\"); };
        open.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task RemoteStream_HandlesUnalignedReadsAndIndependentPositions()
    {
        var data = Enumerable.Range(0, 2 * 1024 * 1024 + 37).Select(i => (byte)(i % 251)).ToArray();
        var name = "HanabeTest-" + Guid.NewGuid().ToString("N");
        using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(pipe.WaitForConnectionAsync(), client.ConnectAsync());
        using var session = new RecoveryDeviceSession(pipe, data.Length);
        var worker = Task.Run(() =>
        {
            using var reader = new BinaryReader(client, System.Text.Encoding.UTF8, true);
            using var writer = new BinaryWriter(client, System.Text.Encoding.UTF8, true);
            try
            {
                while (true)
                {
                    var offset = reader.ReadInt64(); var count = reader.ReadInt32();
                    writer.Write(count); writer.Write(data, (int)offset, count); writer.Flush();
                }
            }
            catch (IOException) { }
        });
        using (var first = session.OpenRead())
        using (var second = session.OpenRead())
        {
            first.Position = 1024 * 1024 - 11;
            var buffer = new byte[53]; first.ReadExactly(buffer);
            buffer.Should().Equal(data.Skip(1024 * 1024 - 11).Take(53));
            second.ReadByte().Should().Be(data[0]);
            first.Position.Should().Be(1024 * 1024 + 42);
            first.Seek(-7, SeekOrigin.End);
            first.Read(buffer).Should().Be(7);
            first.ReadByte().Should().Be(-1);
            first.CanWrite.Should().BeFalse();
        }
        session.Dispose();
        await worker.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
