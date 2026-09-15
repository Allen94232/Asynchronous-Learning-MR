# Multi-Detection Validation Contract

The Unity validation code supports responses containing more than one detection. `ShapeDetector.VerificationResult` stores `all_detections`, and the playback logic can accept a matching fold-state class even when it is not the highest-confidence object in the image.

## Expected Response

```json
{
  "success": false,
  "expected": "shape_1",
  "detected": "shape_2",
  "confidence": 0.42,
  "message": "No primary match",
  "all_detections": [
    {
      "class_name": "shape_2",
      "class_id": 1,
      "confidence": 0.42,
      "bbox": [131, 1543, 1019, 1920]
    },
    {
      "class_name": "shape_1",
      "class_id": 0,
      "confidence": 0.35,
      "bbox": [220, 300, 900, 1500]
    }
  ]
}
```

## Unity-Side Behavior

- `HasMatchingShape(expected, threshold)` checks every entry in `all_detections`.
- `GetBestMatchingDetection()` returns the strongest matching entry.
- `StudentPlaybackManager` uses these helpers during validation.
- The confidence threshold should be selected from validation data, not from a single example.

## Runtime Limitation

The repository does not currently include `detect_shapes.py`, so the Python side that produces this response is unavailable in a clean clone. This document defines the contract a replacement detector must implement.

## Required Detector Behavior

1. Load the intended model and class mapping.
2. Evaluate every detection above the configured minimum confidence.
3. Return valid JSON on standard output.
4. Avoid additional standard-output logs that would corrupt JSON parsing.
5. Return a non-zero exit status and diagnostic message on failure.
6. Never embed local credentials or machine-specific absolute paths.

## Testing

Test at least these cases:

- No detections
- One matching detection
- Several detections with the match ranked first
- Several detections with the match ranked below another class
- Malformed image input
- Missing model file
- Detector timeout
