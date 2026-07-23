# MobileNetV2 ONNX model notice

- Model: `mobilenetv2-7.onnx`
- Source: ONNX Model Zoo mirror, `onnxmodelzoo/mobilenetv2-7`
- License: Apache License 2.0
- SHA-256: `C1C513582D56AFCEFF8516C73804E484C81C6A830712AB6D682253F4A3CD042F`
- Input: 224 × 224 RGB; values normalized with ImageNet mean `[0.485, 0.456, 0.406]` and standard deviation `[0.229, 0.224, 0.225]`

`imagenet_classes.txt` is sourced from the official PyTorch Hub repository and is used only for local post-processing. Hanabe maps the highest-scoring ImageNet objects into its broader local photo categories. Photos and inference results are never uploaded.
