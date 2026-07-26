#!/usr/bin/env python3
"""
Inspect a Hugging Face checkpoint and emit a JSON report.

Requirements implemented:
- Default model: google/siglip2-base-patch16-224
- CLI args: --model-id, --revision, --device, --load-model
- Always output: HF revision/commit, AutoConfig type, AutoProcessor type, processor full serializable config,
  image processor settings (size, resize, crop, rescale, mean, std, do_convert_rgb), tokenizer type and model_max_length
- Only when --load-model: load model weights and output actual model class, presence of get_image_features/get_text_features,
  vision_config, text_config, projection/embedding dim if confirmable, model parameter count
- Unknown/unconfirmable fields are null
- Write output to tools/SigLIP2Export/output/checkpoint_inspection.json
- Clear errors and non-zero exit on network/download failures
- Do not save tokens or print auth info

This script is intentionally conservative and avoids guessing.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import tempfile
from typing import Any, Dict, Optional

try:
    # Core libs
    import torch
    from huggingface_hub import HfApi
    from huggingface_hub.utils import EntryNotFoundError, RepositoryNotFoundError
    from transformers import (
        AutoConfig,
        AutoProcessor,
        AutoTokenizer,
        AutoModel,
    )
except Exception as e:  # pragma: no cover - user environment may differ
    print(f"ERROR: required libraries not available: {e}", file=sys.stderr)
    sys.exit(2)


DEFAULT_MODEL = "google/siglip2-base-patch16-224"
OUTPUT_PATH = os.path.join(os.path.dirname(__file__), "output", "checkpoint_inspection.json")


def safe_to_dict(obj: Any) -> Optional[Dict]:
    if obj is None:
        return None
    if hasattr(obj, "to_dict"):
        try:
            return obj.to_dict()
        except Exception:
            return None
    if hasattr(obj, "__dict__"):
        try:
            return dict(obj.__dict__)
        except Exception:
            return None
    return None


def read_processor_config(processor) -> Dict[str, Any]:
    """Serialize processor by calling save_pretrained to a temp dir and reading json files."""
    out: Dict[str, Any] = {}
    try:
        with tempfile.TemporaryDirectory() as td:
            processor.save_pretrained(td)
            # read all json files in temp dir
            for fn in os.listdir(td):
                if fn.lower().endswith(".json"):
                    path = os.path.join(td, fn)
                    try:
                        with open(path, "r", encoding="utf-8") as f:
                            out[fn] = json.load(f)
                    except Exception:
                        out[fn] = None
    except Exception:
        return {"error": "无法序列化 processor 配置"}
    return out


def extract_image_processor_info(processor) -> Dict[str, Optional[Any]]:
    # processors might expose image_processor or feature_extractor
    img_proc = None
    for attr in ("image_processor", "feature_extractor", "image_processor_2", "feature_extractor_2"):
        img_proc = getattr(processor, attr, None)
        if img_proc is not None:
            break
    if img_proc is None:
        return {
            "size": None,
            "resize": None,
            "crop": None,
            "rescale": None,
            "mean": None,
            "std": None,
            "do_convert_rgb": None,
        }

    info = {
        "size": None,
        "resize": None,
        "crop": None,
        "rescale": None,
        "mean": None,
        "std": None,
        "do_convert_rgb": None,
    }

    # Many feature extractors have .size, .image_mean, .image_std, .do_resize, .do_center_crop, .do_convert_rgb
    # Access safely and avoid guessing
    try:
        if hasattr(img_proc, "size"):
            info["size"] = getattr(img_proc, "size")
        if hasattr(img_proc, "image_size"):
            info["size"] = getattr(img_proc, "image_size")
        if hasattr(img_proc, "do_resize"):
            info["resize"] = getattr(img_proc, "do_resize")
        if hasattr(img_proc, "do_center_crop"):
            info["crop"] = getattr(img_proc, "do_center_crop")
        if hasattr(img_proc, "rescale"):
            info["rescale"] = getattr(img_proc, "rescale")
        # common names
        if hasattr(img_proc, "image_mean"):
            info["mean"] = getattr(img_proc, "image_mean")
        if hasattr(img_proc, "image_std"):
            info["std"] = getattr(img_proc, "image_std")
        if hasattr(img_proc, "do_convert_rgb"):
            info["do_convert_rgb"] = getattr(img_proc, "do_convert_rgb")
        # some use mean/std as lists in attributes mean/std
        if info["mean"] is None and hasattr(img_proc, "mean"):
            info["mean"] = getattr(img_proc, "mean")
        if info["std"] is None and hasattr(img_proc, "std"):
            info["std"] = getattr(img_proc, "std")
    except Exception:
        # If any access fails, set fields we couldn't read to None
        pass

    return info


def confirm_projection_dim_from_config(cfg: Any) -> Optional[int]:
    d = safe_to_dict(cfg)
    if not d:
        return None
    # look for explicit keys
    for key in ("projection_dim", "proj_dim", "projection", "projection_size", "embedding_dim", "dim", "embed_dim"):
        if key in d and isinstance(d[key], int):
            return d[key]
    return None


def confirm_projection_dim_from_model(model) -> Optional[int]:
    # look for parameters with 'projection' or 'proj' in name and infer last dimension
    dims = set()
    try:
        for name, p in model.named_parameters():
            lname = name.lower()
            if "projection" in lname or ".proj" in lname or "proj" in lname:
                shape = tuple(p.shape)
                if len(shape) >= 1:
                    dims.add(shape[-1])
        if len(dims) == 1:
            return dims.pop()
    except Exception:
        return None
    return None


def main(argv: Optional[list[str]] = None) -> int:
    p = argparse.ArgumentParser(description="Inspect a Hugging Face checkpoint and emit JSON report")
    p.add_argument("--model-id", default=DEFAULT_MODEL, help="Hugging Face model id (default: %(default)s)")
    p.add_argument("--revision", default=None, help="Model revision/branch/commit")
    p.add_argument("--device", default=None, help="Torch device to load model on (cpu/cuda:0). Only used when --load-model is set")
    p.add_argument("--load-model", action="store_true", help="If set, load full model weights (may be large)")
    args = p.parse_args(argv)

    report: Dict[str, Any] = {
        "model_id": args.model_id,
        "requested_revision": args.revision if args.revision else None,
    }

    # Ensure output dir exists
    out_dir = os.path.dirname(OUTPUT_PATH)
    os.makedirs(out_dir, exist_ok=True)

    # Query Hugging Face for model info (to get resolved commit sha)
    hf_api = HfApi()
    try:
        mi = hf_api.model_info(args.model_id, revision=args.revision)
        report["resolved_revision"] = mi.sha if getattr(mi, "sha", None) else None
    except RepositoryNotFoundError:
        print(f"ERROR: model '{args.model_id}' not found on Hugging Face.", file=sys.stderr)
        return 3
    except EntryNotFoundError:
        print(f"ERROR: revision '{args.revision}' for model '{args.model_id}' not found.", file=sys.stderr)
        return 4
    except Exception as e:
        print(f"ERROR: failed to fetch model info from Hugging Face: {e}", file=sys.stderr)
        return 5

    # Load AutoConfig
    try:
        config = AutoConfig.from_pretrained(args.model_id, revision=args.revision, trust_remote_code=True)
        report["auto_config_type"] = type(config).__name__
        report["config"] = config.to_dict() if hasattr(config, "to_dict") else None
    except Exception as e:
        print(f"ERROR: failed to load AutoConfig: {e}", file=sys.stderr)
        return 6

    # Load processor
    try:
        processor = AutoProcessor.from_pretrained(args.model_id, revision=args.revision, trust_remote_code=True)
        report["auto_processor_type"] = type(processor).__name__
        report["processor_serialized_config"] = read_processor_config(processor)
        report["image_processor"] = extract_image_processor_info(processor)
    except Exception as e:
        print(f"ERROR: failed to load AutoProcessor: {e}", file=sys.stderr)
        return 7

    # Load tokenizer (always)
    try:
        tokenizer = AutoTokenizer.from_pretrained(args.model_id, revision=args.revision, trust_remote_code=True)
        report["tokenizer_type"] = type(tokenizer).__name__
        # model_max_length can be None for some tokenizers
        try:
            report["tokenizer_model_max_length"] = int(getattr(tokenizer, "model_max_length", None)) if getattr(tokenizer, "model_max_length", None) is not None else None
        except Exception:
            report["tokenizer_model_max_length"] = None
    except Exception as e:
        print(f"ERROR: failed to load tokenizer: {e}", file=sys.stderr)
        return 8

    # If load-model specified, load full model
    if args.load_model:
        # Determine torch device
        device = args.device if args.device else ("cuda" if torch.cuda.is_available() else "cpu")
        try:
            model = AutoModel.from_pretrained(args.model_id, revision=args.revision, trust_remote_code=True)
            # move model to device
            try:
                model.to(device)
            except Exception:
                # best-effort; ignore move errors but note device
                pass

            report["loaded_model_class"] = f"{type(model).__module__}.{type(model).__name__}"
            report["has_get_image_features"] = bool(getattr(model, "get_image_features", None))
            report["has_get_text_features"] = bool(getattr(model, "get_text_features", None))

            # vision/text config if present on model.config
            vis_cfg = getattr(model.config, "vision_config", None)
            txt_cfg = getattr(model.config, "text_config", None)
            report["vision_config"] = vis_cfg.to_dict() if vis_cfg is not None and hasattr(vis_cfg, "to_dict") else None
            report["text_config"] = txt_cfg.to_dict() if txt_cfg is not None and hasattr(txt_cfg, "to_dict") else None

            # projection dim: try config then model params
            proj_dim = confirm_projection_dim_from_config(model.config)
            if proj_dim is None:
                proj_dim = confirm_projection_dim_from_model(model)
            report["projection_dim"] = proj_dim

            # parameter count
            try:
                param_count = sum(p.numel() for p in model.parameters())
                report["parameter_count"] = int(param_count)
            except Exception:
                report["parameter_count"] = None

        except Exception as e:
            # model download or load failure
            print(f"ERROR: failed to download or load model weights: {e}", file=sys.stderr)
            return 9
    else:
        # Not loading full model: set related fields to None
        report["loaded_model_class"] = None
        report["has_get_image_features"] = None
        report["has_get_text_features"] = None
        report["vision_config"] = None
        report["text_config"] = None
        report["projection_dim"] = None
        report["parameter_count"] = None

    # Always include the AutoConfig type again for clarity
    report["auto_config_type"] = type(config).__name__

    # Write to JSON file
    try:
        with open(OUTPUT_PATH, "w", encoding="utf-8") as f:
            json.dump(report, f, ensure_ascii=False, indent=2, default=str)
    except Exception as e:
        print(f"ERROR: failed to write output file '{OUTPUT_PATH}': {e}", file=sys.stderr)
        return 10

    # Also print a small status
    print(f"Wrote inspection report to: {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    exit_code = main()
    if exit_code:
        sys.exit(exit_code)
