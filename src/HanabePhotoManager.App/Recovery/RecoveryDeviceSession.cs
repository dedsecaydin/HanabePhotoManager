using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.App.Recovery;

/// <summary>The elevated process can only read a validated removable volume; the ordinary app writes exports.</summary>
internal sealed class RecoveryDeviceSession : IDisposable
{
    private readonly NamedPipeServerStream _pipe;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly object _gate = new();
    private bool _disposed;
    public long Length { get; private set; }
    internal RecoveryDeviceSession(NamedPipeServerStream pipe, long length = 0)
    {
        Length = length;
        _pipe = pipe;
        _reader = new BinaryReader(pipe, System.Text.Encoding.UTF8, true);
        _writer = new BinaryWriter(pipe, System.Text.Encoding.UTF8, true);
    }

    public static async Task<RecoveryDeviceSession> ConnectAsync(string root, CancellationToken token)
    {
        var name = "HanabeRecovery-" + Guid.NewGuid().ToString("N");
        var session = new RecoveryDeviceSession(new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly));
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            start.ArgumentList.Add("--recovery-reader"); start.ArgumentList.Add(name); start.ArgumentList.Add(root);
            using var process = Process.Start(start) ?? throw new IOException("无法启动只读设备进程。");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            using var registration = timeout.Token.Register(session.Dispose);
            await session._pipe.WaitForConnectionAsync(timeout.Token);
            session.Length = await Task.Run(() =>
            {
                var length = session._reader.ReadInt64();
                if (length < 0) throw new IOException(session._reader.ReadString());
                return length;
            }, timeout.Token);
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    public Stream OpenRead() => new DeviceStream(this);
    public void ValidateDestination()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer.Write(-1L); _writer.Write(0); _writer.Flush();
            var result = _reader.ReadInt32();
            if (result != 0) throw new IOException(_reader.ReadString());
        }
    }
    private byte[] Read(long offset, int count)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer.Write(offset); _writer.Write(count); _writer.Flush();
            var read = _reader.ReadInt32();
            if (read < 0) throw new IOException(_reader.ReadString());
            if (read > count) throw new InvalidDataException("设备响应超出请求范围。");
            var bytes = _reader.ReadBytes(read);
            if (bytes.Length != read) throw new EndOfStreamException("设备读取连接中断。");
            return bytes;
        }
    }
    public void Dispose() { _disposed = true; _pipe.Dispose(); }

    public static int RunReader(string pipeName, string root)
    {
        if (!pipeName.StartsWith("HanabeRecovery-", StringComparison.Ordinal) || pipeName.Length != 47) return 2;
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);
        try
        {
            pipe.Connect(60_000);
            using var reader = new BinaryReader(pipe, System.Text.Encoding.UTF8, true);
            using var writer = new BinaryWriter(pipe, System.Text.Encoding.UTF8, true);
            ReadOnlyRecoveryVolume volume;
            try { volume = new ReadOnlyRecoveryVolume(root, @"D:\"); }
            catch (Exception ex) { writer.Write(-1L); writer.Write(ex.Message); writer.Flush(); return 1; }
            using (volume)
            {
                writer.Write(volume.Length); writer.Flush();
                while (pipe.IsConnected)
                {
                    var offset = reader.ReadInt64(); var count = reader.ReadInt32();
                    try
                    {
                        if (offset == -1) { volume.ValidateDestination(@"D:\"); writer.Write(0); }
                        else { var bytes = volume.Read(offset, count); writer.Write(bytes.Length); writer.Write(bytes); }
                        writer.Flush();
                    }
                    catch (Exception ex) { writer.Write(-1); writer.Write(ex.Message); writer.Flush(); return 1; }
                }
            }
            return 0;
        }
        catch (IOException) { return 1; }
        catch (TimeoutException) { return 1; }
    }

    private sealed class DeviceStream(RecoveryDeviceSession session) : Stream
    {
        private byte[] _cache = [];
        private long _cacheOffset = -1;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => session.Length;
        public override long Position { get; set; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (offset > buffer.Length - count) throw new ArgumentException("Buffer range is invalid.");
            if (count == 0 || Position == Length) return 0;
            if (Position < 0 || Position > Length) throw new IOException("读取位置超出设备范围。");
            var total = 0;
            while (total < count && Position < Length)
            {
                if (Position < _cacheOffset || Position >= _cacheOffset + _cache.Length)
                {
                    _cacheOffset = Position / (1024 * 1024) * (1024 * 1024);
                    _cache = session.Read(_cacheOffset, (int)Math.Min(1024 * 1024, Length - _cacheOffset));
                    if (_cache.Length == 0) throw new EndOfStreamException();
                }
                var take = Math.Min(count - total, _cache.Length - (int)(Position - _cacheOffset));
                Array.Copy(_cache, (int)(Position - _cacheOffset), buffer, offset + total, take);
                Position += take; total += take;
            }
            return total;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            var next = checked((origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? Position : Length) + offset);
            if (next < 0 || next > Length) throw new IOException("读取位置超出设备范围。");
            return Position = next;
        }
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
