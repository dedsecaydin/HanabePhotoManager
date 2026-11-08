param(
    [Parameter(Mandatory=$true)][string]$Path,
    [Parameter(Mandatory=$true)][uint32]$ExpectedCpu,
    [switch]$RequireEntitlements,
    [string]$InfoPlist,
    [string]$CodeResources
)
$ErrorActionPreference = 'Stop'
$bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path).Path)
function Read-LE32([long]$position) {
    if ($position -lt 0 -or $position + 4 -gt $bytes.Length) { throw 'Mach-O data is truncated.' }
    return [BitConverter]::ToUInt32($bytes, [int]$position)
}
function Read-BE32([long]$position) {
    if ($position -lt 0 -or $position + 4 -gt $bytes.Length) { throw 'Signature data is truncated.' }
    return [uint32](([uint64]$bytes[$position] -shl 24) -bor ([uint64]$bytes[$position+1] -shl 16) -bor ([uint64]$bytes[$position+2] -shl 8) -bor [uint64]$bytes[$position+3])
}
if ((Read-LE32 0) -ne 4277009103 -or (Read-LE32 4) -ne $ExpectedCpu) { throw 'Unexpected Mach-O format or architecture.' }
$count = Read-LE32 16; $commandEnd = [long]32 + (Read-LE32 20); $offset = [long]32
if ($commandEnd -gt $bytes.Length) { throw 'Mach-O commands are truncated.' }
$signatureOffset = -1; $signatureLength = 0
for ($i = 0; $i -lt $count; $i++) {
    $command = Read-LE32 $offset; $size = Read-LE32 ($offset + 4)
    if ($size -lt 8 -or $offset + $size -gt $commandEnd) { throw 'Invalid Mach-O command.' }
    if ($command -eq 29) {
        if ($size -lt 16 -or $signatureOffset -ge 0) { throw 'Invalid signature command.' }
        $signatureOffset = [long](Read-LE32 ($offset + 8)); $signatureLength = Read-LE32 ($offset + 12)
    }
    $offset += $size
}
if ($offset -ne $commandEnd -or $signatureOffset -lt 0 -or $signatureLength -lt 12 -or $signatureOffset + $signatureLength -gt $bytes.Length) { throw 'Mach-O signature is missing or truncated.' }
if ((Read-BE32 $signatureOffset) -ne [Convert]::ToUInt32('FADE0CC0', 16)) { throw 'Invalid signature container.' }
$superLength = Read-BE32 ($signatureOffset + 4); $slotCount = Read-BE32 ($signatureOffset + 8)
if ($superLength -gt $signatureLength -or 12 + [long]$slotCount * 8 -gt $superLength) { throw 'Invalid signature slot table.' }
$slots = @{}
for ($i = 0; $i -lt $slotCount; $i++) {
    $type = Read-BE32 ($signatureOffset + 12 + 8 * $i)
    $relative = Read-BE32 ($signatureOffset + 16 + 8 * $i)
    if ($relative -lt 12 + [long]$slotCount * 8 -or $relative + 8 -gt $superLength -or $slots.ContainsKey($type)) { throw 'Invalid signature slot.' }
    $position = $signatureOffset + $relative
    $length = Read-BE32 ($position + 4)
    if ($length -lt 8 -or [long]$relative + $length -gt $superLength) { throw 'Invalid signature blob.' }
    $slots[$type] = @{ Position = $position; Length = $length }
}
if (-not $slots.ContainsKey([uint32]0)) { throw 'CodeDirectory is missing.' }
$directorySlot = $slots[[uint32]0]
# Older Intel binaries may use SHA-1 for the primary directory and SHA-256
# for an alternate directory. Verify the SHA-256 directory in either case.
if ($directorySlot.Length -ge 44 -and $bytes[$directorySlot.Position + 37] -ne 2) {
    $directorySlot = $null
    foreach ($type in @(4096..4100)) {
        if ($slots.ContainsKey([uint32]$type) -and $slots[[uint32]$type].Length -ge 44 -and $bytes[$slots[[uint32]$type].Position + 37] -eq 2) {
            $directorySlot = $slots[[uint32]$type]; break
        }
    }
    if ($null -eq $directorySlot) { throw 'SHA-256 CodeDirectory is missing.' }
}
$directory = $directorySlot.Position; $directoryLength = $directorySlot.Length
if ((Read-BE32 $directory) -ne [Convert]::ToUInt32('FADE0C02', 16) -or $directoryLength -lt 44) { throw 'Invalid CodeDirectory.' }
$flags = Read-BE32 ($directory + 12); $hashOffset = Read-BE32 ($directory + 16)
$specialCount = Read-BE32 ($directory + 24); $codeCount = Read-BE32 ($directory + 28); $codeLimit = Read-BE32 ($directory + 32)
$hashSize = $bytes[$directory + 36]; $hashType = $bytes[$directory + 37]; $pageExponent = $bytes[$directory + 39]
if (($flags -band 65538) -ne 65538 -or $hashType -ne 2 -or $hashSize -ne 32 -or $pageExponent -gt 30 -or $codeCount -eq 0 -or $codeLimit -gt $signatureOffset) { throw 'Unexpected ad-hoc signature parameters.' }
$pageSize = [long]1 -shl $pageExponent
if ($codeCount -ne [Math]::Ceiling($codeLimit / [double]$pageSize) -or $hashOffset -lt [long]$specialCount * $hashSize -or [long]$hashOffset + [long]$codeCount * $hashSize -gt $directoryLength) { throw 'Invalid CodeDirectory hash table.' }
$sha = [Security.Cryptography.SHA256]::Create()
try {
    for ($i = 0; $i -lt $codeCount; $i++) {
        $start = [long]$i * $pageSize; $length = [int][Math]::Min($pageSize, $codeLimit - $start)
        $actual = $sha.ComputeHash($bytes, [int]$start, $length)
        $expected = New-Object byte[] $hashSize
        [Array]::Copy($bytes, [int]($directory + $hashOffset + $i * $hashSize), $expected, 0, $hashSize)
        if ([Convert]::ToBase64String($actual) -cne [Convert]::ToBase64String($expected)) { throw "Invalid code-page hash $i." }
    }
    function Assert-SpecialHash([int]$slot, [byte[]]$data) {
        if ($specialCount -lt $slot) { throw "Missing special hash $slot." }
        $expected = New-Object byte[] $hashSize
        [Array]::Copy($bytes, [int]($directory + $hashOffset - $slot * $hashSize), $expected, 0, $hashSize)
        if ([Convert]::ToBase64String($sha.ComputeHash($data)) -cne [Convert]::ToBase64String($expected)) { throw "Invalid special hash $slot." }
    }
    if ($RequireEntitlements) {
        foreach ($slot in @(5, 7)) {
            if (-not $slots.ContainsKey([uint32]$slot)) { throw "Missing entitlement slot $slot." }
            $blob = $slots[[uint32]$slot]; $data = New-Object byte[] $blob.Length
            [Array]::Copy($bytes, [int]$blob.Position, $data, 0, [int]$blob.Length)
            Assert-SpecialHash $slot $data
        }
        $xmlBlob = $slots[[uint32]5]
        if ((Read-BE32 $xmlBlob.Position) -ne [Convert]::ToUInt32('FADE7171', 16)) { throw 'Invalid entitlement XML blob.' }
        $xmlText = [Text.Encoding]::UTF8.GetString($bytes, [int]$xmlBlob.Position + 8, [int]$xmlBlob.Length - 8)
        $xml = New-Object Xml.XmlDocument
        $xml.XmlResolver = $null
        $xml.LoadXml($xmlText)
        foreach ($name in @('com.apple.security.cs.allow-jit', 'com.apple.security.cs.allow-unsigned-executable-memory', 'com.apple.security.cs.allow-dyld-environment-variables', 'com.apple.security.cs.disable-library-validation')) {
            $key = $xml.SelectSingleNode("/plist/dict/key[text()='$name']")
            if ($null -eq $key -or $null -eq $key.NextSibling -or $key.NextSibling.Name -ne 'true') { throw "Missing entitlement $name." }
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($InfoPlist)) { Assert-SpecialHash 1 ([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $InfoPlist).Path)) }
    if (-not [string]::IsNullOrWhiteSpace($CodeResources)) { Assert-SpecialHash 3 ([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $CodeResources).Path)) }
}
finally { $sha.Dispose() }
