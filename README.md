# Asynchronous Learning MR

一個基於 Unity 的混合實境 (MR) 異步學習平台，使用 Meta Quest VR 設備和 Avatar SDK。

## 專案概述

這是一個用於異步學習的混合實境應用程序，允許教師錄製課程內容，學生可以在 VR 環境中回放並與之互動。

## 主要功能

- **Avatar 系統**: 使用 Meta Avatar SDK 2.0 實現虛擬化身
- **音訊錄製與回放**: 支援教師錄製課程音訊，學生回放學習
- **手勢追蹤**: 利用 VR 控制器和手勢追蹤進行互動
- **Passthrough 模式**: 支援混合實境 passthrough 功能
- **形狀檢測**: 集成 YOLOv8 物體檢測模型用於摺紙形狀識別

## 技術棧

- **Unity Version**: 2022.3 或更高版本
- **VR SDK**: Meta XR SDK
- **Avatar SDK**: Meta Avatar SDK 2.0
- **ML Framework**: YOLOv8 (用於物體檢測)
- **語言**: C#, Python

## 快速開始

1. 使用 Unity 2022.3 或更高版本打開專案
2. 安裝 Meta XR SDK 和 Avatar SDK 2.0
3. 連接 Meta Quest 設備並啟用開發者模式
4. 構建並部署到設備

## 專案結構

```
Assets/
├── Scripts/           # C# 腳本文件
├── Scenes/           # Unity 場景文件
├── Resources/        # 資源文件
├── share_model/      # YOLOv8 模型和訓練數據
└── Plugins/          # 外部插件

Packages/             # Unity 包管理
ProjectSettings/      # 專案設置
```

## 系統需求

- Windows 10 或更高版本
- Unity 2022.3+
- Meta Quest 2/3/Pro VR 頭戴設備
- 足夠的磁碟空間用於 Unity 專案和資源

## 授權

此專案僅供教育用途。

## 貢獻

歡迎提交 Issues 和 Pull Requests。

## 聯絡方式

如有問題，請通過 GitHub Issues 聯繫。
