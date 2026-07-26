# SigLIP2 model files

This directory contains the verified pure visual export for
`google/siglip2-base-patch16-224`.

Required runtime files:

- `siglip2_visual.onnx`
- `model_manifest.json`
- `label_embeddings.json`

The image and text embeddings are generated from the same resolved checkpoint
and L2 normalized. Ranking uses cosine `SimilarityScore`; it is not a
probability.

`tools/SigLIP2Export/export.py` checks the checkpoint, exports the visual
encoder, generates label embeddings, records SHA-256 and validates PyTorch
against ONNX Runtime with at least three images. Generated reports remain under
the ignored `tools/SigLIP2Export/output` directory.
