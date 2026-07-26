#!/usr/bin/env python3
"""Export and validate the pure SigLIP2 visual encoder."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import onnx
import onnxruntime as ort
import torch
import torch.nn.functional as functional
from PIL import Image, ImageDraw
from huggingface_hub import HfApi
from transformers import AutoModel, AutoProcessor

DEFAULT_MODEL = "google/siglip2-base-patch16-224"


class NormalizedVisualEncoder(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.vision_model = model.vision_model

    def forward(self, pixel_values):
        output = self.vision_model(pixel_values=pixel_values)
        return functional.normalize(output.pooler_output, p=2, dim=-1)


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-id", default=DEFAULT_MODEL)
    parser.add_argument("--revision")
    parser.add_argument("--labels", required=True)
    parser.add_argument("--test-images", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--opset", type=int, default=17)
    parser.add_argument("--device", choices=["cpu", "cuda"], default="cpu")
    return parser.parse_args()


def normalized_features(value):
    if hasattr(value, "pooler_output"):
        value = value.pooler_output
    return functional.normalize(value, p=2, dim=-1)


def ensure_validation_images(directory: Path):
    directory.mkdir(parents=True, exist_ok=True)
    images = sorted(
        path for path in directory.iterdir()
        if path.suffix.lower() in {".jpg", ".jpeg", ".png", ".bmp", ".webp"}
    )
    if len(images) >= 3:
        return images
    colors = [(210, 64, 74), (53, 128, 194), (67, 160, 91)]
    for index, color in enumerate(colors, 1):
        path = directory / f"validation_{index}.png"
        image = Image.new("RGB", (320, 240), color)
        draw = ImageDraw.Draw(image)
        draw.rectangle((30 * index, 25, 260, 195), outline="white", width=8)
        draw.ellipse((90, 55, 210, 175), fill=tuple(max(0, channel - 35) for channel in color))
        image.save(path)
    return sorted(path for path in directory.glob("validation_*.png"))


def main():
    args = parse_args()
    output = Path(args.output_dir)
    output.mkdir(parents=True, exist_ok=True)
    label_path = Path(args.labels)
    labels = [line.strip() for line in label_path.read_text(encoding="utf-8").splitlines() if line.strip()]
    if not labels:
        raise SystemExit("Labels file is empty.")
    images = ensure_validation_images(Path(args.test_images))
    if len(images) < 3:
        raise SystemExit("At least 3 validation images are required.")

    device = torch.device(args.device)
    model = AutoModel.from_pretrained(args.model_id, revision=args.revision).eval().to(device)
    processor = AutoProcessor.from_pretrained(args.model_id, revision=args.revision)
    visual = NormalizedVisualEncoder(model).eval().to(device)
    image_size = int(model.config.vision_config.image_size)
    dummy = torch.zeros(1, 3, image_size, image_size, device=device)
    model_path = output / "siglip2_visual.onnx"
    torch.onnx.export(
        visual,
        (dummy,),
        str(model_path),
        input_names=["pixel_values"],
        output_names=["image_embedding"],
        dynamic_axes={"pixel_values": {0: "batch"}, "image_embedding": {0: "batch"}},
        opset_version=args.opset,
        do_constant_folding=True,
        dynamo=False,
    )
    onnx.checker.check_model(onnx.load(str(model_path)))

    prompt_template = "This is a photo of {label}."
    prompts = [prompt_template.format(label=label) for label in labels]
    text_inputs = processor(text=prompts, padding="max_length", return_tensors="pt")
    text_inputs = {key: value.to(device) for key, value in text_inputs.items()}
    with torch.no_grad():
        text_embeddings = normalized_features(model.get_text_features(**text_inputs)).cpu().numpy()
    embeddings = {label: vector.astype(float).tolist() for label, vector in zip(labels, text_embeddings)}
    (output / "label_embeddings.json").write_text(
        json.dumps(embeddings, ensure_ascii=False, indent=2), encoding="utf-8"
    )

    session = ort.InferenceSession(str(model_path), providers=["CPUExecutionProvider"])
    validation = []
    for path in images[: max(3, len(images))]:
        image = Image.open(path).convert("RGB")
        values = processor(images=image, return_tensors="pt")["pixel_values"]
        with torch.no_grad():
            torch_vector = visual(values.to(device)).cpu().numpy()
        onnx_vector = session.run(["image_embedding"], {"pixel_values": values.numpy()})[0]
        validation.append({
            "image": str(path),
            "max_abs_error": float(np.max(np.abs(torch_vector - onnx_vector))),
            "cosine_similarity": float(np.sum(torch_vector * onnx_vector)),
            "pytorch_l2_norm": float(np.linalg.norm(torch_vector)),
            "onnx_l2_norm": float(np.linalg.norm(onnx_vector)),
        })

    sha256 = hashlib.sha256(model_path.read_bytes()).hexdigest()
    revision = HfApi().model_info(args.model_id, revision=args.revision).sha
    manifest = {
        "model_id": args.model_id,
        "resolved_revision": revision,
        "model_file": model_path.name,
        "labels_file": "label_embeddings.json",
        "image_size": image_size,
        "input_name": "pixel_values",
        "input_shape": ["batch", 3, image_size, image_size],
        "input_dtype": "float32",
        "output_name": "image_embedding",
        "output_shape": ["batch", int(text_embeddings.shape[1])],
        "output_dtype": "float32",
        "embedding_dimension": int(text_embeddings.shape[1]),
        "preprocessing": "RGB; shortest-edge resize then center crop; rescale 1/255; mean [0.5,0.5,0.5]; std [0.5,0.5,0.5]",
        "label_prompt_template": prompt_template,
        "normalization": "L2 for image and text embeddings",
        "score_type": "SimilarityScore",
        "sha256": sha256,
    }
    (output / "model_manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    report = {
        "model_id": args.model_id,
        "resolved_revision": revision,
        "images_tested": len(validation),
        "passed": all(item["max_abs_error"] <= 1e-4 and item["cosine_similarity"] >= .9999 for item in validation),
        "tolerance": {"max_abs_error": 1e-4, "cosine_similarity": .9999},
        "results": validation,
    }
    (output / "validation_report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if not report["passed"]:
        raise SystemExit("PyTorch/ONNX validation failed.")


if __name__ == "__main__":
    main()
