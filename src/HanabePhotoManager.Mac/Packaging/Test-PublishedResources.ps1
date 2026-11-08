param([Parameter(Mandatory=$true)][string]$Publish, [Parameter(Mandatory=$true)][string]$Target)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $Publish).Path.TrimEnd('\')
$targetRoot = (Resolve-Path -LiteralPath $Target).Path
function Get-ResourceHash([string]$path) {
    $file = [IO.File]::OpenRead($path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToBase64String($sha.ComputeHash($file)) }
    finally { $sha.Dispose(); $file.Dispose() }
}
foreach ($required in @('HanabePhotoManager.dll', 'HanabePhotoManager.runtimeconfig.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) { throw "Missing publish resource: $required" }
}
Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($source.Length + 1)
    $destination = Join-Path $targetRoot $relative
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) { throw "Signed bundle is missing: $relative" }
    $stream = $_.OpenRead()
    try {
        $magicBytes = New-Object byte[] 4
        $count = $stream.Read($magicBytes, 0, 4)
        $magic = if ($count -eq 4) { [BitConverter]::ToUInt32($magicBytes, 0) } else { 0 }
    }
    finally { $stream.Dispose() }
    # Signing legitimately changes thin and universal Mach-O files; compare all other resources byte for byte.
    if ($magic -notin @([uint32]4277009103, [uint32]3405691582, [uint32]3199925962, [uint32]3216703178)) {
        if ((Get-ResourceHash $_.FullName) -cne (Get-ResourceHash $destination)) {
            throw "Signed bundle changed resource: $relative"
        }
    }
}
