param(
    [Parameter(Mandatory=$true)][ValidateSet('osx-arm64','osx-x64')][string]$Runtime,
    [Parameter(Mandatory=$true)][string]$Destination,
    [Parameter(Mandatory=$true)][string]$ExtractionRoot
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repo '.artifacts')).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
function Require-ArtifactPath([string]$value) {
    $resolved = [IO.Path]::GetFullPath($value)
    if (-not $resolved.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'VLC staging must remain in the project artifacts directory.' }
    return $resolved
}
$Destination = Require-ArtifactPath $Destination
$ExtractionRoot = Require-ArtifactPath $ExtractionRoot
if ($Destination.TrimEnd('\') -eq $ExtractionRoot.TrimEnd('\') -or
    $Destination.StartsWith($ExtractionRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $ExtractionRoot.StartsWith($Destination.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'VLC extraction and output directories must be separate.'
}
foreach ($directory in @($Destination, $ExtractionRoot)) {
    if ((Test-Path -LiteralPath $directory) -and (Get-ChildItem -LiteralPath $directory -Force | Select-Object -First 1)) {
        throw "VLC staging directory must be empty: $directory"
    }
}
$downloads = Join-Path $artifactRoot 'videolan-official'
New-Item -ItemType Directory -Force -Path $downloads, $Destination, $ExtractionRoot | Out-Null

function Get-VerifiedFile([string]$url, [string]$path, [string]$expectedHash) {
    if (Test-Path -LiteralPath $path) {
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $expectedHash) { throw "Checksum mismatch for $path" }
        return
    }
    $temporary = $path + '.' + [Guid]::NewGuid().ToString('N') + '.download'
    try {
        Invoke-WebRequest -Uri $url -OutFile $temporary -UseBasicParsing
        if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expectedHash) { throw "Checksum mismatch for $url" }
        Move-Item -LiteralPath $temporary -Destination $path
    }
    finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
}

$architecture = if ($Runtime -eq 'osx-arm64') { 'arm64' } else { 'intel64' }
$expectedCpu = if ($Runtime -eq 'osx-arm64') { [uint32]16777228 } else { [uint32]16777223 }
$expectedHash = if ($Runtime -eq 'osx-arm64') { '64A89D93CDD30B0E97131743E246373DB82D6826EA882D265D46D74B136DA2B7' } else { '1EF6C903E2DC026D4E58DDF7B2A9ED0EB6F8CAF80A9A9F46F96490340B962707' }
$archive = Join-Path $downloads "vlc-3.0.24-$architecture.dmg"
Get-VerifiedFile "https://download.videolan.org/pub/videolan/vlc/3.0.24/macosx/vlc-3.0.24-$architecture.dmg" $archive $expectedHash

$sevenZip = Get-Command '7z.exe' -ErrorAction SilentlyContinue
$sevenZipPath = if ($sevenZip) { $sevenZip.Source } else { Join-Path ([Environment]::GetFolderPath('ProgramFiles')) '7-Zip\7z.exe' }
if (-not (Test-Path -LiteralPath $sevenZipPath)) { throw '7-Zip is required to read the official VLC DMG.' }
& $sevenZipPath x $archive "-o$ExtractionRoot" 'VLC media player\VLC.app\Contents\MacOS\lib\libvlc.5.dylib' 'VLC media player\VLC.app\Contents\MacOS\lib\libvlccore.9.dylib' 'VLC media player\VLC.app\Contents\MacOS\plugins\*' -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Could not extract official VLC files for $Runtime." }
$source = Join-Path $ExtractionRoot 'VLC media player\VLC.app\Contents\MacOS'
$libraryDirectory = Join-Path $Destination 'lib'
$pluginDirectory = Join-Path $Destination 'plugins'
New-Item -ItemType Directory -Force -Path $libraryDirectory, $pluginDirectory | Out-Null

function Assert-MachOArchitecture([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    try {
        $reader = New-Object IO.BinaryReader($stream)
        if ($reader.ReadUInt32() -ne 4277009103 -or $reader.ReadUInt32() -ne $expectedCpu) { throw "Unexpected VLC architecture: $path" }
    }
    finally { $stream.Dispose() }
}
foreach ($pair in @(@('libvlc.5.dylib','libvlc.dylib'), @('libvlccore.9.dylib','libvlccore.dylib'))) {
    $inputPath = Join-Path (Join-Path $source 'lib') $pair[0]
    Assert-MachOArchitecture $inputPath
    Copy-Item -LiteralPath $inputPath -Destination (Join-Path $libraryDirectory $pair[1])
}
$excluded = @('libmacosx_plugin.dylib', 'libosx_notifications_plugin.dylib')
Get-ChildItem -LiteralPath (Join-Path $source 'plugins') -File | Where-Object { $_.Name -notin $excluded } | ForEach-Object {
    if ($_.Extension -eq '.dylib') { Assert-MachOArchitecture $_.FullName }
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $pluginDirectory $_.Name)
}
if (@(Get-ChildItem -LiteralPath $pluginDirectory -Filter '*.dylib' -File).Count -lt 100 -or
    -not (Test-Path -LiteralPath (Join-Path $pluginDirectory 'libvout_macosx_plugin.dylib')) -or
    -not (Test-Path -LiteralPath (Join-Path $pluginDirectory 'libavcodec_plugin.dylib'))) {
    throw "VLC plugin extraction is incomplete for $Runtime."
}
foreach ($license in @(
    @('COPYING', '8177F97513213526DF2CF6184D8FF986C675AFB514D4E68A404010521B880643'),
    @('COPYING.LIB', 'DC626520DCD53A22F727AF3EE42C770E56C97A64FE3ADB063799D8AB032FE551')
)) {
    $path = Join-Path $downloads $license[0]
    Get-VerifiedFile "https://raw.githubusercontent.com/videolan/vlc/3.0.24/$($license[0])" $path $license[1]
    Copy-Item -LiteralPath $path -Destination (Join-Path $Destination ('VLC-' + $license[0]))
}
[IO.File]::WriteAllText((Join-Path $Destination 'VLC-SOURCE.txt'), "VLC 3.0.24 source: https://download.videolan.org/pub/videolan/vlc/3.0.24/vlc-3.0.24.tar.xz`n", [Text.UTF8Encoding]::new($false))
