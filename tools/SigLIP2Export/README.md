SigLIP2 Export Tool (planning stage)

Purpose:
This directory contains developer tooling to export the SigLIP2 visual encoder ONNX model and generate associated label embeddings, manifest, and validation artifacts.

Important:
- This tool is for development/export only and is NOT part of the WPF application runtime.
- Do NOT commit model binaries to the repository unless using Git LFS or an agreed artifact store.

Environment requirements:
- Python 3.10+ (recommended)
- pip
- A virtual environment is strongly recommended (.venv/)

Suggested Python dependencies (see requirements.txt):
- torch
- transformers
- optimum (optional, for optimized export paths)
- onnx
- onnxruntime
- huggingface_hub
- Pillow
- numpy

Usage (high-level):
1. Create a Python virtual environment and install requirements.txt
2. Place label file (one label per line) or use labels.example.txt
3. Put test images into tools/SigLIP2Export/test_images/ (not tracked by git)
4. Run inspection to verify the checkpoint and processor:
   python inspect_checkpoint.py --model-id google/siglip2-base-patch16-224 --revision main
5. Use export.py to attempt export (export is destructive only to output/ directory):
   python export.py --model-id google/siglip2-base-patch16-224 --revision main --labels labels.example.txt --test-images test_images --output-dir output --opset 17 --device cpu

Outputs (in output/ directory):
- siglip2_visual.onnx  (visual encoder ONNX)
- label_embeddings.json
- model_manifest.json
- export_report.md

License & model considerations:
- Ensure you comply with the model's license when downloading and redistributing.
- For large model files, use Git LFS or external artifact hosting rather than committing binaries to the repo.

Notes:
- This planning-stage README accompanies script skeletons provided here; the export scripts do not perform ONNX export until the full pipeline is implemented and validated.
