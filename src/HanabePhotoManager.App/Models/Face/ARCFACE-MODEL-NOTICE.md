# ArcFace user model policy

Hanabe Photo Manager does not download, include, or redistribute InsightFace
ArcFace pretrained weights.

The ArcFace R100 option accepts only detector and recognizer ONNX files selected
by the user. It stays disabled until both files exist and the user explicitly
records that the models are self-trained or licensed for this use. Model files
remain outside the application package and are never copied into the repository.

The configured detector must expose YuNet-compatible `cls_*`, `obj_*`, `bbox_*`
and `kps_*` outputs with five landmarks. The recognizer must accept an RGB
`NCHW` 112 x 112 float tensor and return one embedding per input face.
