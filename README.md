# Asynchronous Learning MR

A Unity-based Mixed Reality (MR) asynchronous learning platform built for Meta Quest VR devices using the Meta Avatar SDK.

## Project Overview

This is a mixed reality application designed for asynchronous learning. It allows instructors to record instructional content, which students can later replay and interact with in a VR environment.

## Key Features

- **Avatar System**: Virtual avatars implemented using Meta Avatar SDK 2.0
- **Audio Recording and Playback**: Supports instructor audio recording and student playback
- **Hand Tracking**: Interaction via VR controllers and hand tracking
- **Passthrough Mode**: Supports mixed reality passthrough functionality
- **Shape Detection**: Integrated YOLOv8 object detection model for origami shape recognition

## Technology Stack

- **Unity Version**: 2022.3 or later
- **VR SDK**: Meta XR SDK
- **Avatar SDK**: Meta Avatar SDK 2.0
- **ML Framework**: YOLOv8 (for object detection)
- **Languages**: C#, Python

## Quick Start

1. Open the project using Unity 2022.3 or later
2. Install the Meta XR SDK and Avatar SDK 2.0
3. Connect a Meta Quest device and enable Developer Mode
4. Build and deploy to the device

## Project Structure

Assets/
├── Scripts/ # C# script files
├── Scenes/ # Unity scene files
├── Resources/ # Asset resources
├── share_model/ # YOLOv8 models and training data
└── Plugins/ # External plugins

Packages/ # Unity package management
ProjectSettings/ # Project settings

## System Requirements

- Windows 10 or later
- Unity 2022.3+
- Meta Quest 2/3/Pro VR headset
- Sufficient disk space for the Unity project and assets
