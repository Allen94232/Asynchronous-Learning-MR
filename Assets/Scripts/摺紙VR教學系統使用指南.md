# 摺紙 MR 教學系統使用指南

## 系統組成

| 元件 | 功能 |
| --- | --- |
| `AvatarRecordingManager` | 記錄 Avatar stream、音訊與步驟事件 |
| `TeacherRecordingManager` | 管理教師端錄製流程 |
| `StudentPlaybackManager` | 管理學生端載入、播放、跳轉與驗證 |
| `OrigamiSyncController` | 控制 Alembic 摺紙動畫時間 |
| `OrigamiStepGuideSimple` | 顯示綠色起點、紅色終點與黃色摺痕線 |

## 教師端

使用場景：[`TeacherRecording.unity`](../Scenes/TeacherRecording.unity)

1. 確認 Meta Avatar 與麥克風已初始化。
2. 確認 Alembic player、Sync Controller 與 Step Guide 的參照完整。
3. 開始錄製後依序觸發各步驟的綠色起點。
4. 每次觸發會推進對應的摺紙動畫區段。
5. 停止並儲存錄製。

錄製控制以場景中的 UI 為主；鍵盤快捷鍵僅供 Editor 測試，應以目前 manager script 的實際綁定為準。

## 學生端

使用場景：

- [`StudentPlaying.unity`](../Scenes/StudentPlaying.unity)
- [`StudentPlayingWithPassthrough.unity`](../Scenes/StudentPlayingWithPassthrough.unity)

1. 載入錄製資料。
2. 開始播放 Avatar、音訊與摺紙動畫。
3. 使用播放控制暫停、繼續或跳轉。
4. 在步驟邊界確認視覺提示與 Alembic 動畫同步。
5. 若啟用形狀驗證，先依模型目錄的 `requirements.txt` 安裝 Python 套件，並確認 `best.pt` 可載入。

## Alembic 設定

1. 將 `.abc` 資產放入 Unity project。
2. 確認物件包含 `AlembicStreamPlayer`。
3. 在 `OrigamiSyncController` 指定 Alembic player。
4. 核對 animation duration 與各 Step 的進度範圍。
5. 必要時使用 time offset 微調同步。

## Step Guide 設定

目前建議使用 `OrigamiStepGuideSimple`。每一步需設定：

- 起點與終點
- 摺痕線兩端
- 動畫 duration／progress range
- Pinch threshold 與 trigger distance

詳見[摺紙步驟提示系統](OrigamiGuide_README.md)。

## 驗證清單

- Quest hand tracking 可以穩定取得左右手狀態。
- Avatar、音訊和 Alembic 使用相同播放時間。
- 暫停後繼續不會重播或跳過音訊。
- 跳到任意時間時，Avatar pose、音訊與摺紙步驟一致。
- 最後一步完成後可以安全停止或重新開始。
- Passthrough scene 的提示在真實背景下仍清楚可見。

## 模型驗證

`Assets/share_model/detect_shapes.py` 已提供 Unity 所需的命令列與 JSON 介面。啟用前仍須安裝 Python 依賴、確認 `best.pt` 的類別名稱，並以實際 MR 拍攝資料校正信心度門檻。詳見[模型與資料集說明](../share_model/README.md)。
