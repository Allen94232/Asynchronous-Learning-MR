#!/usr/bin/env python3
"""YOLO inference entry point used by Unity's ShapeDetector component."""

from __future__ import annotations

import argparse
import contextlib
import json
import sys
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Detect origami fold states.")
    parser.add_argument("image", type=Path, help="Image to analyze")
    parser.add_argument("--unity", action="store_true", help="Emit the Unity JSON contract")
    parser.add_argument("--conf", type=float, default=0.5, help="Minimum confidence")
    parser.add_argument("--verify", type=int, help="Expected shape number")
    parser.add_argument("--model", type=Path, help="YOLO weights; defaults to best.pt")
    return parser.parse_args()


def emit(payload: dict[str, Any]) -> None:
    print(json.dumps(payload, ensure_ascii=False, separators=(",", ":")))


def error_payload(message: str) -> dict[str, Any]:
    return {
        "success": False,
        "expected": "",
        "detected": "",
        "confidence": 0.0,
        "message": "",
        "error": message,
        "all_detections": [],
        "detected_any": False,
        "class_name": "",
        "class_id": -1,
    }


def main() -> int:
    args = parse_args()
    script_dir = Path(__file__).resolve().parent
    image_path = args.image.expanduser().resolve()
    model_path = (args.model or (script_dir / "best.pt")).expanduser().resolve()

    if not image_path.is_file():
        emit(error_payload(f"Image not found: {image_path}"))
        return 2
    if not model_path.is_file():
        emit(error_payload(f"Model not found: {model_path}"))
        return 2
    if not 0.0 < args.conf <= 1.0:
        emit(error_payload("--conf must be greater than 0 and at most 1"))
        return 2

    try:
        with contextlib.redirect_stdout(sys.stderr):
            from ultralytics import YOLO
            model = YOLO(str(model_path))
            result = model.predict(source=str(image_path), conf=args.conf, verbose=False)[0]
    except Exception as exc:
        emit(error_payload(f"Inference failed: {exc}"))
        return 1

    detections: list[dict[str, Any]] = []
    for box in result.boxes:
        class_id = int(box.cls.item())
        confidence = float(box.conf.item())
        bbox = [float(value) for value in box.xyxy[0].tolist()]
        detections.append({
            "class_name": str(result.names[class_id]),
            "class_id": class_id,
            "confidence": confidence,
            "bbox": bbox,
        })

    detections.sort(key=lambda item: item["confidence"], reverse=True)
    best = detections[0] if detections else None
    expected = f"shape_{args.verify}" if args.verify is not None else ""
    matching = next((item for item in detections if item["class_name"] == expected), None)
    success = matching is not None if expected else bool(detections)

    payload = {
        "success": success,
        "expected": expected,
        "detected": best["class_name"] if best else "",
        "confidence": best["confidence"] if best else 0.0,
        "message": (
            f"Detected {matching['class_name']}" if matching
            else ("No matching shape detected" if expected else
                  ("Detection complete" if detections else "No shapes detected"))
        ),
        "error": "",
        "all_detections": detections,
        "detected_any": bool(detections),
        "class_name": best["class_name"] if best else "",
        "class_id": best["class_id"] if best else -1,
    }
    emit(payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
