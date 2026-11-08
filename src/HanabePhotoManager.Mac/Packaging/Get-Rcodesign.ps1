$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$root = Join-Path $repo '.artifacts\macos-tools'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$archiveName = 'apple-codesign-0.29.0-x86_64-pc-windows-msvc.zip'
$archive = Join-Path $root $archiveName
$archiveHash = '54BB500E2DA7A8DE02FCAE0F331D1CAC6E6D7173B4281042FF9C528BA3159AAA'
$executableHash = '4763D9E8A66A01A5CFF91AD77071EC8A7032F2EBD103BA78FCCDDAB397BB5DEA'
$directory = Join-Path $root 'rcodesign-0.29.0'
$executable = Join-Path $directory 'apple-codesign-0.29.0-x86_64-pc-windows-msvc\rcodesign.exe'

if (Test-Path -LiteralPath $executable) {
    if ((Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash -ne $executableHash) { throw 'Cached rcodesign executable checksum mismatch.' }
    Write-Output $executable
    return
}
if (Test-Path -LiteralPath $directory) { throw 'Incomplete rcodesign directory already exists; refusing to overwrite it.' }
if (Test-Path -LiteralPath $archive) {
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveHash) { throw 'Cached rcodesign archive checksum mismatch.' }
}
else {
    $temporary = $archive + '.' + [Guid]::NewGuid().ToString('N') + '.download'
    try {
        Invoke-WebRequest -Uri "https://github.com/indygreg/apple-platform-rs/releases/download/apple-codesign/0.29.0/$archiveName" -OutFile $temporary -UseBasicParsing
        if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $archiveHash) { throw 'Downloaded rcodesign archive checksum mismatch.' }
        Move-Item -LiteralPath $temporary -Destination $archive
    }
    finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
}
Expand-Archive -LiteralPath $archive -DestinationPath $directory
if (-not (Test-Path -LiteralPath $executable) -or (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash -ne $executableHash) {
    throw 'Extracted rcodesign executable checksum mismatch.'
}
Write-Output $executable
