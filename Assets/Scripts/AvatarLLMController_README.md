# 文字驅動 Avatar 控制器（封存實驗）

`AvatarLLMController` 是早期封存的實驗性元件，不屬於目前教師錄製或學生播放的主要流程。它以輸入文字測試 Remote Avatar 語音與嘴型資料；目前未連接真正的 LLM，`SimpleTTSService` 只會產生 Mock 音訊，Avatar skeleton 手勢也尚未完成。

## 目前可用功能

- 指定 Local Avatar、Remote Avatar 與玩家相機
- 透過 `Speak(string text)` 播放測試音訊
- 開始、停止及查詢播放狀態
- 產生測試用 viseme 資料
- 讓 Remote Avatar 位於玩家附近並朝向玩家

## 尚未實作

- LLM API 呼叫與對話狀態
- 真實文字轉語音服務
- 語音辨識
- Avatar skeleton 的指向、解說、思考、點頭與眨眼動作
- 完整語意到手勢的動作規劃

`SimpleTTSService` 雖然列出 Azure、Google 與 Wit.ai provider，但非 Mock provider 目前會自動切回 Mock 模式。

## 場景設定

1. 在場景建立一個 GameObject，加入 `SimpleTTSService`。
2. 保持 Provider 為 `Mock`。
3. 建立另一個 GameObject，加入 `AvatarLLMController`。
4. 指定 Local Avatar、Remote Avatar 與 Player Camera。
5. 在 `Test Text` 輸入測試文字。
6. 確認 Remote Avatar 可正常載入後進入 Play Mode。

## 測試按鍵

| 按鍵 | 功能 |
| --- | --- |
| `Space` | 切換開始／停止測試語音 |
| `T` | 播放 Test Text |
| `Esc` | 停止播放 |

## 程式介面

```csharp
AvatarLLMController controller = FindObjectOfType<AvatarLLMController>();

controller.Speak("歡迎來到混合實境學習空間。");

if (controller.IsSpeaking())
{
    controller.StopSpeaking();
}
```

## 串接真實服務時

- API 金鑰只能從環境變數或安全的後端取得，不要寫入 Unity scene、ScriptableObject 或 repository。
- 將 LLM 回覆與 TTS 分成兩個服務層，避免把網路呼叫寫進 Avatar 動作元件。
- Quest 裝置上的網路與音訊格式需另外測試。
- 雲端 TTS 回傳的音訊與 viseme 時間必須使用同一時間基準。

## 相關檔案

- [`AvatarLLMController.cs`](AvatarLLMController.cs)
- [`SimpleTTSService.cs`](SimpleTTSService.cs)
- [`LLM Avatar.unity`](../Scenes/LLM%20Avatar.unity)
