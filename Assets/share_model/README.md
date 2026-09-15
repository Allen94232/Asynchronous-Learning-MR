# Origami Shape Model and Dataset

This directory contains YOLO model weights, dataset metadata, labeled images, and documentation for the experimental origami fold-state detector.

## Current Contents

- `best.pt`: current model weight file used by the Unity integration
- `yolov8n.pt`: base YOLOv8 nano weights
- `dataset.yaml`: dataset configuration
- `dataset/`: train, validation, and test images and labels
- `runs/`: committed training outputs
- model backup files

## Runtime Entry Point

[`ShapeDetector.cs`](../Scripts/ShapeDetector.cs) calls:

```text
Assets/share_model/detect_shapes.py
```

The script loads `best.pt` by default, accepts the Unity command-line arguments, and writes one JSON response to standard output. Diagnostic output is redirected to standard error so Unity's JSON parser is not polluted.

## Documentation

- [Training guide](TRAINING_GUIDE.md)
- [Model improvement guide](IMPROVE_MODEL_GUIDE.md)
- [Testing guide](TESTING_GUIDE.md)
- [Multi-detection contract](MULTI_DETECTION_README.md)

## Environment

Typical Python dependencies for training and local experiments are:

```bash
python -m venv .venv
.venv\Scripts\activate
python -m pip install -r requirements.txt
```

Use the activation command appropriate for your operating system.

## Repository Hygiene

- Do not commit API keys, machine-specific paths, or virtual environments.
- Treat `runs/`, cache files, and backup weights as generated artifacts unless they are intentionally preserved for reproducibility.
- Record the Ultralytics version, dataset revision, and evaluation metrics for each model promoted to `best.pt`.
