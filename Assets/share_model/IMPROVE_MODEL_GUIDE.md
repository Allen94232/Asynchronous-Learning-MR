# Model Improvement Guide

Model improvement should be driven by measured failure cases rather than fixed promises about training time or accuracy.

## Recommended Loop

1. Save representative false positives, false negatives, difficult backgrounds, lighting changes, and transitional fold states.
2. Correctly label the new images.
3. Keep a held-out test set that is not reused for training decisions.
4. Fine-tune from `best.pt` or retrain from a base model.
5. Compare the candidate model against the current model on the same test set.
6. Promote the new model only when the trade-off is understood.

## Fine-Tuning with New Data

Merge the new labeled examples into a versioned dataset split, update `dataset.yaml`, and run a new training session initialized from `best.pt`:

```python
from ultralytics import YOLO

model = YOLO("best.pt")
model.train(
    data="dataset.yaml",
    epochs=50,
    imgsz=640,
    name="origami_finetune",
)
```

Do not use `resume=True` for this case; resuming is intended for continuing an interrupted run from its saved training state.

## When to Retrain

Consider a fresh run from pretrained base weights when:

- The class definition has changed.
- The new dataset distribution differs substantially from the old one.
- Existing labels contain systematic errors.
- A controlled comparison is needed across model sizes or training settings.

## Data Quality Checklist

- Every image has the correct class and bounding box.
- Train, validation, and test images do not contain near-duplicates.
- Each class contains varied backgrounds, angles, distances, and lighting.
- Transitional states follow a documented labeling rule.
- Evaluation includes the physical camera position used by the MR prototype.

## Reporting

For each promoted model, record:

- Dataset revision and class mapping
- Ultralytics version
- Base checkpoint
- Training arguments
- mAP50 and mAP50–95
- Per-class precision and recall
- Known failure cases

See the [training guide](TRAINING_GUIDE.md) for runnable examples.
