# Asynchronous Learning MR

A mixed-reality prototype for recording and replaying avatar-based origami instruction on Meta Quest. The project combines avatar motion, continuous voice recording, step-based playback, hand interaction, passthrough, and an experimental fold-state detection workflow.

## System Workflow

1. An instructor records an avatar demonstration and voice in the teacher scene.
2. Recording data and origami step events are stored for later playback.
3. A learner replays the demonstration in mixed reality.
4. The guide synchronizes visual fold cues with the Alembic animation.
5. The optional shape-detection path can be connected to an external Python detector.

## Main Features

- Meta Avatar recording and playback
- Continuous microphone capture and synchronized audio playback
- Teacher recording and student playback interfaces
- Step-based origami guidance and timeline synchronization
- Controller and hand-tracking interaction components
- Mixed-reality passthrough scenes
- YOLO model weights and an origami image dataset
- Experimental text-to-avatar speech and gesture components

## Key Scenes

| Scene | Purpose |
| --- | --- |
| [`TeacherRecording.unity`](Assets/Scenes/TeacherRecording.unity) | Instructor recording workflow |
| [`StudentPlaying.unity`](Assets/Scenes/StudentPlaying.unity) | Learner playback workflow |
| [`StudentPlayingWithPassthrough.unity`](Assets/Scenes/StudentPlayingWithPassthrough.unity) | Playback in mixed reality |
| [`Mic Test.unity`](Assets/Scenes/Mic%20Test.unity) | Microphone testing |
| [`LLM Avatar.unity`](Assets/Scenes/LLM%20Avatar.unity) | Experimental text-driven avatar scene |

## Core Components

| Component | Role |
| --- | --- |
| [`AvatarRecordingManager.cs`](Assets/Scripts/AvatarRecordingManager.cs) | Coordinates avatar, audio, and step-event recording |
| [`AvatarRecordingPlayback.cs`](Assets/Scripts/AvatarRecordingPlayback.cs) | Replays recorded avatar data |
| [`TeacherRecordingManager.cs`](Assets/Scripts/TeacherRecordingManager.cs) | Controls the instructor workflow |
| [`StudentPlaybackManager.cs`](Assets/Scripts/StudentPlaybackManager.cs) | Controls playback, seeking, and validation |
| [`OrigamiSyncController.cs`](Assets/Scripts/OrigamiSyncController.cs) | Synchronizes the Alembic animation |
| [`OrigamiStepGuideSimple.cs`](Assets/Scripts/OrigamiStepGuideSimple.cs) | Displays step cues and handles hand triggers |
| [`ShapeDetector.cs`](Assets/Scripts/ShapeDetector.cs) | Defines the Unity side of fold-state detection |
| [`AvatarLLMController.cs`](Assets/Scripts/AvatarLLMController.cs) | Drives test speech and avatar gestures |

## Documentation

- [Text-driven avatar controller](Assets/Scripts/AvatarLLMController_README.md)
- [Origami guide setup](Assets/Scripts/OrigamiGuide_README.md)
- [Hand tracking and playback seeking](Assets/Scripts/%E6%89%8B%E9%83%A8%E8%BF%BD%E8%B9%A4%E8%88%87%E6%99%82%E9%96%93%E8%B7%B3%E8%BD%89%E8%AA%AA%E6%98%8E.md)
- [Teacher and learner workflow](Assets/Scripts/%E6%91%BA%E7%B4%99VR%E6%95%99%E5%AD%B8%E7%B3%BB%E7%B5%B1%E4%BD%BF%E7%94%A8%E6%8C%87%E5%8D%97.md)
- [Shape model and dataset](Assets/share_model/README.md)

## Technology Stack

- Unity `6000.0.23f1`
- Meta XR SDK `81.0.0`
- Meta Avatars SDK `40.0.1`
- Universal Render Pipeline `17.0.3`
- OpenXR `1.12.1`
- C# and Python
- YOLO model weights and labeled image data

## Getting Started

1. Install Unity `6000.0.23f1`.
2. Clone the repository and add its root directory through Unity Hub.
3. Configure the Meta Platform App ID used by the avatar and platform SDKs.
4. Open one of the scenes listed above.
5. Enable Developer Mode on the target Quest headset.
6. Switch the build target to Android and use **Build and Run**.

## Shape-Detection Status

The repository includes model weights, a dataset, the Unity integration, and a compatible `Assets/share_model/detect_shapes.py` entry point. Install the Python dependencies before enabling validation, then verify the model classes and confidence threshold against the current dataset. See the [model documentation](Assets/share_model/README.md).

## Repository Structure

```text
.
├── Assets/
│   ├── Scenes/           # Teacher, learner, passthrough, and test scenes
│   ├── Scripts/          # Recording, playback, interaction, and guide logic
│   └── share_model/      # Model weights, dataset, and model documentation
├── Packages/             # Unity package manifest and lock file
└── ProjectSettings/      # Unity project configuration
```

## Notes

- Meta platform services require a valid App ID and an authorized test account.
- Keep API keys and device-specific addresses outside version control.
- Hardware-dependent recording, passthrough, and hand-tracking features should be tested on a Quest device.
