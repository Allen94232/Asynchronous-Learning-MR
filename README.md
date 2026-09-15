# Asynchronous Learning MR

A mixed-reality prototype for recording and replaying avatar-based origami instruction on Meta Quest. The project combines avatar motion, voice recording, step-based playback, hand interaction, passthrough, and an experimental shape-detection workflow.

## System Workflow

1. An instructor records an avatar demonstration and voice in the teacher scene.
2. Recording data is stored for later playback.
3. A learner replays the demonstration in mixed reality.
4. The origami guide organizes playback into instructional steps.
5. Shape-detection components can be used to support fold-state checking.

## Main Features

- Meta Avatar recording and playback
- Continuous microphone capture and synchronized audio playback
- Teacher recording and student playback interfaces
- Step-based origami guidance and timeline synchronization
- Controller and hand-tracking interaction components
- Mixed-reality passthrough scenes
- YOLO-based origami dataset and shape-detection utilities
- Experimental avatar dialogue and text-to-speech components

## Key Scenes

| Scene | Purpose |
| --- | --- |
| [`TeacherRecording.unity`](Assets/Scenes/TeacherRecording.unity) | Instructor recording workflow |
| [`StudentPlaying.unity`](Assets/Scenes/StudentPlaying.unity) | Learner playback workflow |
| [`StudentPlayingWithPassthrough.unity`](Assets/Scenes/StudentPlayingWithPassthrough.unity) | Playback in mixed reality |
| [`Mic Test.unity`](Assets/Scenes/Mic%20Test.unity) | Microphone testing |
| [`LLM Avatar.unity`](Assets/Scenes/LLM%20Avatar.unity) | Experimental avatar dialogue |

## Core Components

| Component | Role |
| --- | --- |
| [`AvatarRecordingManager.cs`](Assets/Scripts/AvatarRecordingManager.cs) | Coordinates avatar and audio recording |
| [`AvatarRecordingPlayback.cs`](Assets/Scripts/AvatarRecordingPlayback.cs) | Replays recorded avatar data |
| [`TeacherRecordingManager.cs`](Assets/Scripts/TeacherRecordingManager.cs) | Controls the instructor workflow |
| [`StudentPlaybackManager.cs`](Assets/Scripts/StudentPlaybackManager.cs) | Controls the learner workflow |
| [`OrigamiSyncController.cs`](Assets/Scripts/OrigamiSyncController.cs) | Synchronizes origami animation and instruction |
| [`OrigamiStepGuide.cs`](Assets/Scripts/OrigamiStepGuide.cs) | Manages step-based guidance |
| [`ShapeDetector.cs`](Assets/Scripts/ShapeDetector.cs) | Connects fold-state detection to the Unity workflow |
| [`AvatarLLMController.cs`](Assets/Scripts/AvatarLLMController.cs) | Experimental avatar dialogue controller |

Additional implementation notes are available in [`Assets/Scripts`](Assets/Scripts).

## Technology Stack

- Unity `6000.0.23f1`
- Meta XR SDK `81.0.0`
- Meta Avatars SDK `40.0.1`
- Universal Render Pipeline `17.0.3`
- OpenXR `1.12.1`
- C# and Python
- YOLO-based object detection assets

## Getting Started

1. Install Unity `6000.0.23f1`.
2. Clone the repository and add its root directory through Unity Hub.
3. Open one of the scenes listed above.
4. Configure the Meta Platform App ID required by the avatar and platform SDKs.
5. Enable Developer Mode on the target Quest headset.
6. Switch the build target to Android and use **Build and Run**.

## Repository Structure

```text
.
├── Assets/
│   ├── Scenes/           # Teacher, learner, passthrough, and test scenes
│   ├── Scripts/          # Recording, playback, interaction, and guide logic
│   └── share_model/      # Shape-detection dataset and documentation
├── Packages/             # Unity package manifest and lock file
└── ProjectSettings/      # Unity project configuration
```

## Notes

- Meta platform services require a valid App ID and an authorized test account.
- Large generated Unity folders such as `Library`, `Temp`, and `Obj` are intentionally excluded.
- Hardware-dependent recording, passthrough, and hand-tracking features should be tested on a Quest device.
