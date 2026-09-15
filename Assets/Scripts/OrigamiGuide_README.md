# 摺紙步驟提示系統

目前專案包含兩個步驟提示元件：

| 元件 | 用途 |
| --- | --- |
| [`OrigamiStepGuideSimple.cs`](OrigamiStepGuideSimple.cs) | 目前建議使用；以綠色起點、紅色終點與黃色摺痕線提示動作 |
| [`OrigamiStepGuide.cs`](OrigamiStepGuide.cs) | 較早期的箭頭式提示版本，保留供既有場景使用 |

## Simple 版本流程

1. 顯示綠色起點、紅色終點與黃色摺痕線。
2. 使用者以食指 pinch 或手部接近觸發所有起點。
3. 系統移除綠色起點並播放該段 Alembic 動畫。
4. 動畫完成後暫停。
5. 預設等待 `0.5` 秒，再顯示下一步提示。

## Inspector 設定

每個 Step 包含：

- Step Name
- Duration
- Start Points
- End Points
- Fold Line Point 1 / 2
- 對應的動畫進度範圍

視覺與互動設定包括：

- Sphere Radius
- Start Point Color / End Point Color
- Fold Line Width / Color
- Dash Length / Gap
- Pinch Threshold
- Trigger Distance
- Wait Time After Step
- Enable Manual Control

所有提示座標都使用元件所在 GameObject 的 local space。

## 必要參照

- `Sync Controller`：指定 `OrigamiSyncController`
- `Recording Manager`：依場景指定錄製管理元件
- 手部追蹤：可以指定 `OVRHand`，目前程式也包含透過 `OVRPlugin.GetHandState()` 讀取狀態的路徑

## 操作

| 操作 | 結果 |
| --- | --- |
| Pinch／接近綠色起點 | 標記該起點已觸發 |
| `N` | 啟用 Manual Control 時觸發下一步 |
| `TriggerNextStep()` | 讓 UI 或其他程式觸發下一步 |
| `JumpToStep(index)` | 跳到指定步驟；索引從 0 開始 |

## 常見問題

### 提示沒有出現

- 確認 Steps 不為空。
- 確認 Camera Culling Mask 包含提示物件 Layer。
- 確認元件與摺紙模型使用預期的 local coordinate system。

### Pinch 無法觸發

- 確認 Quest 已啟用 hand tracking。
- 確認手部狀態可被 OVR Plugin 讀取。
- 暫時提高 Trigger Distance 或降低 Pinch Threshold 進行測試。
- 啟用 Debug Logs 查看目前狀態。

### 動畫與提示不同步

- 確認 `OrigamiSyncController` 已指定 Alembic player。
- 核對每個 Step 的 duration 與進度範圍。
- 使用 `JumpToStep()` 測試各步驟是否落在正確動畫區段。
