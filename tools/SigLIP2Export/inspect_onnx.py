#!/usr/bin/env python3
"""
Inspect ONNX model inputs/outputs using onnx package.
Prints name, dtype and shape for each input and output.
"""
import argparse
import onnx


def parse_args():
    p = argparse.ArgumentParser(description="Inspect ONNX model I/O")
    p.add_argument("--onnx", required=True, help="Path to ONNX file")
    return p.parse_args()


def tensor_shape(proto):
    # safe access to shape dimensions
    return [d.dim_value if (d.dim_value is not None and d.dim_value > 0) else 'dynamic' for d in proto.type.tensor_type.shape.dim]


def main():
    args = parse_args()
    model = onnx.load(args.onnx)
    graph = model.graph
    print(f"Model ir_version: {model.ir_version}")
    print("Inputs:")
    for inp in graph.input:
        elem_type = inp.type.tensor_type.elem_type
        shape = tensor_shape(inp)
        print(f"- {inp.name}: type={elem_type}, shape={shape}")
    print("Outputs:")
    for out in graph.output:
        elem_type = out.type.tensor_type.elem_type
        shape = tensor_shape(out)
        print(f"- {out.name}: type={elem_type}, shape={shape}")

if __name__ == '__main__':
    main()
