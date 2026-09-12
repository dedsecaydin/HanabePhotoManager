using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>Restricted mounted removable-volume reader. No write/control-to-modify operation is exposed.</summary>
public sealed class ReadOnlyRecoveryVolume : IDisposable
{
    private readonly SafeFileHandle _handle;
    private readonly uint _mediaChange;
    private readonly int _sectorSize;
    public long Length { get; }
    public string Root { get; }
    public ReadOnlyRecoveryVolume(string root, string outputRoot)
    {
        if (root.Length != 3 || !char.IsAsciiLetter(root[0]) || root[1..] != @":\")
            throw new ArgumentException("请选择有盘符的可移动存储卡。");
        if (new DriveInfo(root).DriveType != DriveType.Removable)
            throw new IOException("直接扫描目前仅允许可移动存储卡或 U 盘。");
        Root = root;
        _handle = Open(root, 0x80000000); // GENERIC_READ only.
        try
        {
            ValidateDestination(outputRoot);
            using var system = Open(Path.GetPathRoot(Environment.SystemDirectory)!, 0);
            if (Disks(_handle).Intersect(Disks(system)).Any()) throw new IOException("不能扫描系统所在的物理磁盘。");
            Length = BitConverter.ToInt64(Control(_handle, 0x7405c, 8));
            var geometry = Control(_handle, 0x70000, 24);
            _sectorSize = BitConverter.ToInt32(geometry, 20);
            if (_sectorSize is < 512 or > 65536 || (_sectorSize & (_sectorSize - 1)) != 0 || Length <= 0 || Length % _sectorSize != 0)
                throw new IOException("设备扇区或容量无法安全识别。");
            _mediaChange = BitConverter.ToUInt32(Control(_handle, 0x2d0800, 4));
        }
        catch { _handle.Dispose(); throw; }
    }

    public void ValidateDestination(string outputRoot)
    {
        using var destination = Open(Path.GetPathRoot(Path.GetFullPath(outputRoot))!, 0);
        if (Disks(_handle).Intersect(Disks(destination)).Any()) throw new IOException("输出位置与来源位于同一块物理磁盘，已停止操作。");
    }

    public byte[] Read(long offset, int count)
    {
        if (offset < 0 || offset > Length || count < 0 || count > 4 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(count));
        if (BitConverter.ToUInt32(Control(_handle, 0x2d0800, 4)) != _mediaChange)
            throw new IOException("存储卡已更换，请重新选择设备并扫描。");
        count = (int)Math.Min(count, Length - offset);
        if (count == 0) return [];
        var alignedOffset = offset / _sectorSize * _sectorSize;
        var skip = (int)(offset - alignedOffset);
        var alignedCount = checked((skip + count + _sectorSize - 1) / _sectorSize * _sectorSize);
        var allocation = Marshal.AllocHGlobal(alignedCount + _sectorSize);
        try
        {
            var address = new IntPtr((allocation.ToInt64() + _sectorSize - 1) & ~((long)_sectorSize - 1));
            if (!SetFilePointerEx(_handle, alignedOffset, out _, 0) || !ReadFile(_handle, address, alignedCount, out var read, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (read < skip + count) throw new EndOfStreamException("设备读取不完整，请检查连接。");
            var result = new byte[count];
            Marshal.Copy(IntPtr.Add(address, skip), result, 0, count);
            return result;
        }
        finally { Marshal.FreeHGlobal(allocation); }
    }

    private static SafeFileHandle Open(string root, uint access)
    {
        var handle = CreateFileW(@"\\.\" + root.TrimEnd('\\'), access, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        return handle;
    }
    private static uint[] Disks(SafeFileHandle handle)
    {
        var bytes = Control(handle, 0x560000, 4096);
        var count = BitConverter.ToUInt32(bytes);
        if (count == 0 || count > 128 || 8L + count * 24L > bytes.Length) throw new IOException("无法确认物理磁盘关系。");
        return Enumerable.Range(0, (int)count).Select(i => BitConverter.ToUInt32(bytes, 8 + i * 24)).ToArray();
    }
    private static byte[] Control(SafeFileHandle handle, uint code, int size)
    {
        var bytes = new byte[size];
        if (!DeviceIoControl(handle, code, IntPtr.Zero, 0, bytes, size, out var returned, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (returned < (code == 0x560000 ? 32 : size)) throw new IOException("设备返回的信息不完整。");
        return bytes;
    }
    public void Dispose() => _handle.Dispose();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input, int inputSize, byte[] output, int outputSize, out int returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFilePointerEx(SafeFileHandle handle, long offset, out long position, uint method);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ReadFile(SafeFileHandle handle, IntPtr buffer, int count, out int read, IntPtr overlapped);
}
