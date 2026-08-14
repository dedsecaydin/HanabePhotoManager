param(
    [string]$ModelDirectory = (Join-Path $env:LOCALAPPDATA "HanabePhotoManager\models\ChineseCLIP")
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $ModelDirectory | Out-Null
Write-Host "Chinese-CLIP ONNX must be exported from a matching checkpoint before use."
Write-Host "Place image_encoder.onnx, text_encoder.onnx, and vocab.txt in: $ModelDirectory"
Write-Host "Recommended upstream checkpoint: OFA-Sys/chinese-clip-vit-base-patch16"
Write-Host "The application intentionally does not download model binaries automatically."
