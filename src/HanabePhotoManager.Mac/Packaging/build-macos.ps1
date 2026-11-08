# 在 Windows 上打出 macOS 安装包：Hanabe.app（Apple 芯片 arm64 + Intel x64 各一份），自带 .NET 运行时。
# 输出：.artifacts\macos\Hanabe-macOS-arm64.zip、Hanabe-macOS-x64.zip
# 说明：zip 内写入 Unix 权限（可执行位），在 Mac 上用“访达”双击解压即可保留。
param([ValidateSet('osx-arm64', 'osx-x64')][string[]]$Runtimes = @('osx-arm64', 'osx-x64'))
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$project = Join-Path $repo 'src\HanabePhotoManager.Mac\HanabePhotoManager.Mac.csproj'
$outRoot = Join-Path $repo ('.artifacts\macos\run-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $outRoot | Out-Null
$signer = & (Join-Path $PSScriptRoot 'Get-Rcodesign.ps1')
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-Icns([string]$png, [string]$dest) {
    # icns：头 'icns' + 总长度；条目 ic08(256) ic09(512) ic10(1024)，内容为 PNG
    $entries = @(@('ic08', 256), @('ic09', 512), @('ic10', 1024))
    $src = [System.Drawing.Image]::FromFile($png)
    $chunks = foreach ($e in $entries) {
        $bmp = New-Object System.Drawing.Bitmap $e[1], $e[1]
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = 'HighQualityBicubic'
        $g.DrawImage($src, 0, 0, $e[1], $e[1])
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $bmp.Dispose()
        ,@($e[0], $ms.ToArray())
    }
    $src.Dispose()
    $total = 8 + ($chunks | ForEach-Object { 8 + $_[1].Length } | Measure-Object -Sum).Sum
    $fs = [System.IO.File]::Create($dest)
    $w = New-Object System.IO.BinaryWriter $fs
    function BE([int]$v) { $b = [BitConverter]::GetBytes([int]$v); [Array]::Reverse($b); $b }
    $w.Write([Text.Encoding]::ASCII.GetBytes('icns')); $w.Write((BE $total))
    foreach ($c in $chunks) { $w.Write([Text.Encoding]::ASCII.GetBytes($c[0])); $w.Write((BE (8 + $c[1].Length))); $w.Write($c[1]) }
    $w.Dispose()
}

foreach ($rid in $Runtimes) {
    $publish = Join-Path $outRoot "publish-$rid"
    Write-Host "== dotnet publish $rid"
    dotnet publish $project -c Release -r $rid --self-contained true -p:UseAppHost=true -p:DebugType=None -o $publish -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid" }

    $app = Join-Path $outRoot "$rid\unsigned\Hanabe.app"
    $macos = Join-Path $app 'Contents\MacOS'; $res = Join-Path $app 'Contents\Resources'
    New-Item -ItemType Directory -Force $macos, $res | Out-Null
    Copy-Item (Join-Path $publish '*') $macos -Recurse
    Copy-Item (Join-Path $PSScriptRoot 'Info.plist') (Join-Path $app 'Contents\Info.plist')
    New-Icns (Join-Path $repo 'src\HanabePhotoManager.Mac\Assets\HanabeAppLogo.png') (Join-Path $res 'Hanabe.icns')

    & (Join-Path $PSScriptRoot 'Prepare-Vlc.ps1') -Runtime $rid -Destination (Join-Path $macos 'vlc') -ExtractionRoot (Join-Path $outRoot "$rid\vlc-extract")
    & (Join-Path $PSScriptRoot 'Add-MachORPath.ps1') -Path (Join-Path $macos 'HanabePhotoManager') -RPath '@executable_path/vlc/lib'
    # Sign in place: rcodesign's separate-output mode does not copy ordinary resources.
    $signedApp = $app
    Push-Location $repo
    try {
        # A drive-qualified path is interpreted by rcodesign as a scope; use a relative path.
        & $signer sign --code-signature-flags runtime --entitlements-xml-file 'src/HanabePhotoManager.Mac/Packaging/Entitlements.plist' $signedApp
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $rid" }
    }
    finally { Pop-Location }
    $cpu = if ($rid -eq 'osx-arm64') { [uint32]16777228 } else { [uint32]16777223 }
    $signedMacos = Join-Path $signedApp 'Contents\MacOS'
    & (Join-Path $PSScriptRoot 'Test-PublishedResources.ps1') -Publish $publish -Target $signedMacos
    $verify = Join-Path $PSScriptRoot 'Test-MachOSignature.ps1'
    & $verify -Path (Join-Path $signedMacos 'HanabePhotoManager') -ExpectedCpu $cpu -RequireEntitlements -InfoPlist (Join-Path $signedApp 'Contents\Info.plist') -CodeResources (Join-Path $signedApp 'Contents\_CodeSignature\CodeResources')
    Get-ChildItem -LiteralPath (Join-Path $signedMacos 'vlc') -Recurse -File -Filter '*.dylib' | ForEach-Object {
        & $verify -Path $_.FullName -ExpectedCpu $cpu
    }
    $app = $signedApp

    $arch = $rid.Replace('osx-', '')
    $zip = Join-Path $outRoot "Hanabe-macOS-$arch.zip"
    $archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
    $base = Split-Path $app
    Get-ChildItem $app -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($base.Length + 1).Replace('\', '/')
        $entry = $archive.CreateEntry($rel, 'Optimal')
        # 可执行文件与 .dylib 给 0755，其余 0644（高 16 位为 Unix 权限）
        $exec = $rel -like 'Hanabe.app/Contents/MacOS/HanabePhotoManager' -or $rel -like '*.dylib' -or $rel -like '*/createdump'
        $mode = if ($exec) { 0x81ED } else { 0x81A4 }
        $entry.ExternalAttributes = $mode -shl 16
        $in = $_.OpenRead(); $out = $entry.Open(); $in.CopyTo($out); $out.Dispose(); $in.Dispose()
    }
    $archive.Dispose()
    Write-Host ("   -> {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
}
