param(
    [Parameter(Mandatory=$true)][string]$Path,
    [Parameter(Mandatory=$true)][string]$RPath
)
$ErrorActionPreference = 'Stop'
$file = (Resolve-Path -LiteralPath $Path).Path
if ([string]::IsNullOrWhiteSpace($RPath) -or $RPath.Contains([char]0)) { throw 'Invalid rpath.' }
$bytes = [IO.File]::ReadAllBytes($file)
if ($bytes.Length -lt 32 -or [BitConverter]::ToUInt32($bytes, 0) -ne 4277009103) { throw 'Expected a little-endian 64-bit Mach-O.' }
$cpu = [BitConverter]::ToUInt32($bytes, 4)
if ($cpu -ne 16777223 -and $cpu -ne 16777228) { throw 'Unsupported Mach-O architecture.' }
$commands = [BitConverter]::ToUInt32($bytes, 16)
$commandBytes = [BitConverter]::ToUInt32($bytes, 20)
$end = [long]32 + $commandBytes
if ($end -gt $bytes.Length) { throw 'Mach-O load commands exceed file size.' }
$offset = 32
$firstSection = [long]$bytes.Length
$signed = $false
for ($i = 0; $i -lt $commands; $i++) {
    if ($offset + 8 -gt $end) { throw 'Truncated Mach-O command.' }
    $command = [BitConverter]::ToUInt32($bytes, $offset)
    $size = [BitConverter]::ToUInt32($bytes, $offset + 4)
    if ($size -lt 8 -or $offset + [long]$size -gt $end) { throw 'Invalid Mach-O command size.' }
    if ($command -eq 2147483676) {
        if ($size -lt 12) { throw 'Invalid LC_RPATH.' }
        $nameOffset = [BitConverter]::ToUInt32($bytes, $offset + 8)
        if ($nameOffset -lt 12 -or $nameOffset -ge $size) { throw 'Invalid LC_RPATH string.' }
        $start = $offset + [int]$nameOffset
        $stop = $start
        while ($stop -lt $offset + $size -and $bytes[$stop] -ne 0) { $stop++ }
        if ($stop -eq $offset + $size) { throw 'Unterminated LC_RPATH.' }
        if ([Text.Encoding]::UTF8.GetString($bytes, $start, $stop - $start) -ceq $RPath) { return }
    }
    if ($command -eq 29) { $signed = $true }
    if ($command -eq 25) {
        if ($size -lt 72) { throw 'Invalid LC_SEGMENT_64.' }
        $sectionCount = [BitConverter]::ToUInt32($bytes, $offset + 64)
        if (72 + [long]$sectionCount * 80 -gt $size) { throw 'Invalid Mach-O section table.' }
        for ($section = 0; $section -lt $sectionCount; $section++) {
            $sectionOffset = [BitConverter]::ToUInt32($bytes, $offset + 72 + $section * 80 + 48)
            if ($sectionOffset -gt 0 -and $sectionOffset -lt $firstSection) { $firstSection = $sectionOffset }
        }
    }
    $offset += [int]$size
}
if ($offset -ne $end -or $firstSection -eq $bytes.Length) { throw 'Cannot safely identify Mach-O header padding.' }
if ($signed) { throw 'Refusing to modify an already signed Mach-O.' }
$name = [Text.Encoding]::UTF8.GetBytes($RPath)
$newSize = [int](([long](12 + $name.Length + 1) + 7) -band -8)
if ($end + $newSize -gt $firstSection -or $end + $newSize -gt $bytes.Length) { throw 'Insufficient Mach-O header padding.' }
for ($i = [int]$end; $i -lt $end + $newSize; $i++) { if ($bytes[$i] -ne 0) { throw 'Mach-O header padding is not empty.' } }
[BitConverter]::GetBytes([uint32]2147483676).CopyTo($bytes, [int]$end)
[BitConverter]::GetBytes([uint32]$newSize).CopyTo($bytes, [int]$end + 4)
[BitConverter]::GetBytes([uint32]12).CopyTo($bytes, [int]$end + 8)
$name.CopyTo($bytes, [int]$end + 12)
[BitConverter]::GetBytes([uint32]($commands + 1)).CopyTo($bytes, 16)
[BitConverter]::GetBytes([uint32]($commandBytes + $newSize)).CopyTo($bytes, 20)
$temporary = $file + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
$backup = $file + '.' + [Guid]::NewGuid().ToString('N') + '.bak'
$replaced = $false
try {
    [IO.File]::WriteAllBytes($temporary, $bytes)
    [IO.File]::Replace($temporary, $file, $backup)
    $replaced = $true
}
finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    if ($replaced -and (Test-Path -LiteralPath $backup)) { Remove-Item -LiteralPath $backup -Force }
}
