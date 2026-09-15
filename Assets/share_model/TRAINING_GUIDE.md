# YOLO Model Training Guide

This guide describes a reproducible training path using the files that currently exist in `Assets/share_model`. It does not depend on repository-local helper scripts.

## Requirements

```bash
python -m venv .venv
.venv\Scripts\activate
python -m pip install ultralytics opencv-python
```

Use the activation command appropriate for your operating system.

## Dataset

Review `dataset.yaml` before training. Paths should be relative to the dataset root or valid on the current machine; do not commit personal absolute paths.

The labeled data is organized into train, validation, and test splits under `dataset/`.

## Start a New Training Run

```python
from ultralytics import YOLO

model = YOLO("yolov8n.pt")
results = model.train(
    data="dataset.yaml",
    epochs=100,
    imgsz=640,
    name="origami_v2",
)
```

Tune batch size, device, augmentation, and patience for the available hardware and dataset. Do not publish fixed training-time estimates because runtime varies substantially by device.

## Fine-Tune Existing Weights

To initialize a new training run from the current best weights:

```python
from ultralytics import YOLO

model = YOLO("best.pt")
results = model.train(
    data="dataset.yaml",
    epochs=50,
    imgsz=640,
    name="origami_finetune",
)
```

This is a new run initialized from existing weights. It is different from resuming an interrupted run.

## Resume an Interrupted Run

Resume from that run's `last.pt`, which preserves optimizer, scheduler, and epoch state:

```python
from ultralytics import YOLO

model = YOLO("runs/detect/origami_v2/weights/last.pt")
results = model.train(resume=True)
```

## Evaluate

```python
from ultralytics import YOLO

model = YOLO("runs/detect/origami_v2/weights/best.pt")
metrics = model.val(data="dataset.yaml")
print(metrics.box.map50)
print(metrics.box.map)
```

Before replacing `best.pt`, compare the new model on the same held-out test set and record at least mAP50, mAP50–95, per-class performance, and representative failure cases.

## Promote a Model

1. Preserve the run configuration and evaluation output.
2. Copy the selected weights to `best.pt`.
3. Test the model using the same image preprocessing expected by the Unity detector.
4. Verify all fold-state class names still match `ShapeDetector.cs`.
5. Commit the new weight only when the large-file policy is clear.
