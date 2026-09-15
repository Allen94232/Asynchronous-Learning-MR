# YOLO Model Testing Guide

The repository currently contains weights and test data but does not contain the older helper scripts referenced by previous documentation. In particular, `test_single_image.py`, `test_with_confidence.py`, and `test_model.bat` are not present.

## Test with the Ultralytics CLI

After installing `ultralytics`, run inference directly:

```bash
yolo predict model=best.pt source=test_images conf=0.3 save=True
```

If `test_images/` is empty, provide another image or directory path.

## Test with Python

```python
from ultralytics import YOLO

model = YOLO("best.pt")
results = model.predict(
    source="test_images",
    conf=0.3,
    save=True,
)

for result in results:
    for box in result.boxes:
        class_id = int(box.cls.item())
        confidence = float(box.conf.item())
        print(result.names[class_id], confidence)
```

## Confidence-Threshold Evaluation

Do not select a threshold from a single image. Compare several thresholds over a labeled validation set and inspect precision, recall, and the cost of false acceptance for each fold step.

## Capture Checklist

- The complete paper shape is visible.
- Camera distance and angle match the MR setup.
- Lighting includes realistic variation.
- Backgrounds cover both clean and difficult environments.
- Transitional fold states are included and consistently labeled.

## Unity Integration

`ShapeDetector.cs` expects a Python script named `detect_shapes.py`. That script is currently missing, so successful standalone model inference does not by itself make the Unity validation path runnable.

When the detector is restored, validate:

1. Screenshot output and color format.
2. Model and class-name paths.
3. JSON output compatibility.
4. Confidence-threshold behavior.
5. Timeout and process-error handling.

See the [model directory README](README.md) for the current limitation.
