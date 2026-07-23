SigLIP2 model directory

This directory should contain the SigLIP2 visual encoder ONNX model and a model_manifest.json describing the model and its expected inputs/outputs.

Required files:

- model_manifest.json  (described below)
- <model_file>        (the ONNX file referenced by model_manifest.json)
- label_embeddings.json (optional at Stage A; will be consumed in Stage B)

Notes:
- Do NOT commit large model binaries to the repository unless you use Git LFS or otherwise agree on storage strategy.
- The session manager will detect Git LFS pointer files and fail with an explicit error: ensure that the real ONNX binary is present at the referenced path.

model_manifest.json fields (required at minimum for Stage A):
- model_id: string
- model_file: string (relative path inside this directory, e.g. "siglip2_visual.onnx")
- image_size: int (recommended square size, e.g. 256)
- input_name: string (TODO: confirm exact input tensor name from model provider)
- output_name: string (TODO: confirm exact output tensor name from model provider)
- embedding_dimension: int (TODO or set by provider)
- preprocessing: string (description of preprocessing steps: resize, mean/std, channel order)
- label_prompt_template: string (format used to build textual prompts for label embedding if applicable)
- score_type: string (e.g., "cosine", "dot")
- sha256: string (optional hex sha256 of model file)

Stage A behavior:
- The session manager will read model_manifest.json and attempt to load the ONNX model.
- It will enumerate ONNX inputs and outputs and expose that metadata for inspection.
- If input_name/output_name are not yet confirmed, leave them as TODO in the manifest; the manager will not guess and will instead expose the model I/O metadata for manual mapping.
