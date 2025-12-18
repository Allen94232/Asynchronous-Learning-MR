# 摺紙 VR 教學系統使用指南

## 系統架構

### 1. OrigamiSyncController（摺紙同步控制器）
**功能：** 同步 Alembic 摺紙動畫與教師 Avatar 錄製/播放

**主要特性：**
- ✅ 錄製時顯示半透明摺紙預覽（"鬼影"模式）
- ✅ 播放時同步摺紙動畫與教師動作
- ✅ 閒置時顯示摺紙初始狀態（不再隱藏）
- ✅ 自動偵測錄製/播放狀態
- ✅ 支援 AvatarRecordingManager 單獨使用
- ✅ 時間偏移設定（可延遲或提前摺紙動畫）

### 2. OrigamiStepGuide（摺紙步驟指示系統）
**功能：** 在 VR 中顯示摺疊步驟的視覺提示

**主要特性：**
- ✅ 顯示摺疊方向箭頭（帶脈動動畫）
- ✅ 顯示摺疊輔助線（虛線）
- ✅ 步驟說明文字（支援留空）
- ✅ 步驟持續時間控制（映射到 Alembic 動畫進度）
- ✅ 手動步驟切換（N 鍵，需完成當前步驟）
- ✅ 步驟完成後自動暫停動畫
- ✅ 編輯器視覺化預覽

---

## 設置步驟

### 第一步：導入 Alembic 檔案
1. 將 `Origami Animation.abc` 放在 `Assets/ABC Files/` 資料夾
2. Unity 會自動導入並創建預製件

### 第二步：在場景中設置摺紙對象
1. 將 Alembic 預製件拖到場景中
2. 確保對象有 `AlembicStreamPlayer` 組件

### 第三步：添加 OrigamiSyncController
```
在 Alembic GameObject 上添加組件：
1. Add Component → OrigamiSyncController
2. 設置參數：
   - Alembic Player: 自動偵測（或手動指定）
   - Recording Manager: 指向 TeacherRecordingManager 或 AvatarRecordingManager
   - Playback Manager: 指向 StudentPlaybackManager
   - Show Preview During Recording: ✓（啟用預覽）
   - Preview Alpha: 0.5（半透明度）
   - Time Offset: 0（無延遲）
   - Forward Distance: 0.5（紙張在攝影機前方的距離，米）
   - Downward Distance: 0.3（紙張在攝影機下方的距離，米）
```

### 第四步：添加 OrigamiStepGuide（可選）
```
創建空 GameObject 並添加組件：
1. GameObject → Create Empty → 命名為 "OrigamiGuide"
2. Add Component → OrigamiStepGuide
3. 設置參數：
   - Sync Controller: 指向前面創建的 OrigamiSyncController
   - Arrow Width: 0.02
   - Line Width: 0.01
   - Enable Animation: ✓
   - Enable Manual Control: ✓（啟用手動步驟切換）
   - Pause After Step: ✓（步驟完成後暫停）
```

### 第五步：配置摺疊步驟
在 OrigamiStepGuide 的 Inspector 中：

```
Steps → Size: 3（例如 3 個步驟）

步驟 1：
- Step Name: "對角線摺疊"
- Duration: 3（持續 3 秒）
- Arrow Start: (-0.1, 0.05, 0)
- Arrow End: (0.1, -0.05, 0)
- Fold Line Point 1: (-0.15, 0, 0)
- Fold Line Point 2: (0.15, 0, 0)
- Instruction: "沿著虛線將紙對摺"（可留空）
- Arrow Color: Yellow
- Line Color: Orange (Alpha 0.8)

步驟 2：
- Step Name: "展開"
- Duration: 2（持續 2 秒）
- Arrow Start: (0.1, -0.05, 0)
- Arrow End: (-0.1, 0.05, 0)
- Instruction: ""（空白，不顯示文字）
- Arrow Color: Cyan

... 以此類推

**重要：** 
- 系統會自動將步驟時間映射到 Alembic 動畫總時長
- 例如：步驟總時長 10 秒，Alembic 動畫 20 秒 → 每個步驟會控制對應比例的動畫進度
```

---

## 使用流程

### 教師端錄製（TeacherRecording 場景）

1. **準備階段**
   - 進入 VR
   - 確認摺紙預覽隱藏

2. **開始錄製**（按 R 鍵）
   - 摺紙預覽自動顯示（半透明綠色）
   - 自動開始第一個步驟
   - 箭頭和輔助線顯示當前步驟

3. **錄製中**
   - 跟著箭頭和輔助線進行摺紙
   - 每個步驟完成後動畫會暫停
   - 按 N 鍵切換到下一步驟
   - 動畫會按照步驟持續時間播放對應的 Alembic 進度

4. **停止錄製**（按 R 鍵）
   - 預覽自動恢復正常顯示
   - 動畫重置到起點
   - 按 S 鍵儲存

### 學生端播放（StudentPlaying 場景）

1. **載入課程**（按 L 鍵）
   - 載入最新錄製

2. **開始播放**（按 P 鍵）
   - 摺紙動畫完整顯示（不透明）
   - 教師 Avatar 同步播放動作
   - 箭頭和輔助線同步顯示步驟

3. **暫停/繼續**（按 P 或 Space 鍵）
   - 可以暫停觀看細節

---

## 調整與優化

### 時間同步問題
如果摺紙動畫與教師動作不同步：

```csharp
// 在 OrigamiSyncController 調整
Time Offset: -0.5  // 摺紙提前 0.5 秒
Time Offset: 0.5   // 摺紙延後 0.5 秒
```

### 預覽透明度調整
```csharp
Preview Alpha: 0.3  // 更透明
Preview Alpha: 0.7  // 更不透明
```

### 步驟時間微調
在 OrigamiStepGuide 中調整每個步驟的 Duration（持續時間），系統會自動映射到 Alembic 動畫進度。

**工作原理：**
1. 系統計算所有步驟的總時長（例如：3s + 2s + 5s = 10s）
2. 將總時長映射到 Alembic 動畫的總時長
3. 每個步驟控制對應比例的動畫播放

**手動控制模式：**
- 啟用 `Enable Manual Control` 後，可用 N 鍵手動切換步驟
- 每個步驟完成後動畫會自動暫停
- 必須等步驟播完才能切換到下一步

### 箭頭和輔助線位置
使用 Scene 視圖中的 Gizmos（彩色線條）來視覺化調整箭頭和輔助線的位置。

---

## 進階功能

### 1. 動態添加步驟（腳本）
```csharp
OrigamiStepGuide guide = GetComponent<OrigamiStepGuide>();

guide.AddStep(new OrigamiStepGuide.FoldingStep
{
    stepName = "新步驟",
    startTime = 5f,
    endTime = 8f,
    arrowStart = Vector3.zero,
    arrowEnd = Vector3.up,
    instruction = "向上摺疊"
});
```

### 2. 手動控制步驟
```csharp
OrigamiStepGuide guide = GetComponent<OrigamiStepGuide>();
guide.JumpToStep(2); // 跳到第 3 步（索引從 0 開始）
```

### 3. 控制摺紙動畫時間
```csharp
OrigamiSyncController sync = GetComponent<OrigamiSyncController>();
sync.SetTime(3.5f); // 設置到 3.5 秒
sync.ResetAnimation(); // 重置到開始
```

---

## 故障排除

### 問題 1：摺紙不顯示
**解決方法：**
- 檢查 Alembic Player 是否正常工作
- 確認 OrigamiSyncController 組件已添加
- 錄製時：檢查 `Show Preview During Recording` 是否勾選
- 閒置時：摺紙會顯示在第一幀位置（CurrentTime = 0）

### 問題 2：箭頭/輔助線不顯示
**解決方法：**
- 確認 OrigamiStepGuide 的 steps 列表有內容
- 檢查當前時間是否在某個步驟的時間範圍內
- 確認 Sync Controller 已正確指定

### 問題 3：摺紙動畫不同步
**解決方法：**
- 調整 Time Offset 參數
- 檢查 AlembicStreamPlayer 的 Duration 是否正確
- 確認錄製檔案的 duration 與摺紙動畫長度匹配
- 手動模式下：檢查 `Enable Manual Control` 是否啟用
- 確認步驟持續時間設置正確，系統會自動映射到動畫進度

### 問題 4：播放時沒有恢復透明度
**解決方法：**
- 檢查 `ApplyOriginalMaterials()` 是否被正確調用
- 確認原始材質已正確保存

### 問題 5：N 鍵無法切換步驟
**解決方法：**
- 確認 `Enable Manual Control` 已啟用
- 等待當前步驟完成（時間達到 Duration）
- 檢查 Console 是否有 "當前步驟尚未完成" 訊息
- 確認 Sync Controller 已正確指定並有 Alembic Player

---

## 效能優化建議

1. **材質優化**
   - 使用簡單的 Shader（如 Unlit）減少渲染開銷
   - 預覽材質使用 Standard Transparent

2. **Alembic 設定**
   - 在 AlembicStreamPlayer 中設置適當的 Time Range
   - 降低不必要的頂點精度

3. **LineRenderer 優化**
   - 減少 Corner Vertices 和 Cap Vertices 數量
   - 使用較少的虛線段

---

## 擴展建議

### 1. 添加語音提示
整合 Unity Audio 播放步驟說明的語音：
```csharp
public AudioClip[] stepAudioClips;

void OnStepChanged()
{
    if (currentStepIndex >= 0 && currentStepIndex < stepAudioClips.Length)
    {
        audioSource.PlayOneShot(stepAudioClips[currentStepIndex]);
    }
}
```

### 2. 整合手部追蹤
檢測手部是否在摺疊位置附近，提供即時反饋。

### 3. 添加 3D UI 面板
使用 World Space Canvas 顯示更詳細的步驟說明和圖示。

### 4. 錄製摺疊軌跡
記錄教師手部接觸紙張的位置，生成熱力圖。

---

## 鍵盤快捷鍵總結

| 鍵 | 功能 | 場景 |
|---|---|---|
| R | 開始/停止錄製 | 教師端 |
| S | 儲存錄製 | 教師端 |
| L | 載入最新課程 | 學生端 |
| P | 播放/暫停 | 兩者 |
| Space | 播放/暫停（替代） | 學生端 |
| **N** | **切換到下一步驟** | **摺紙教學** |
| R | 重新播放 | 學生端 |

---

## 聯繫與支援

如有任何問題或需要進一步自訂，請參考：
- Unity Alembic 文檔：https://docs.unity3d.com/Packages/com.unity.formats.alembic@latest
- Meta Avatar SDK 文檔：https://developer.oculus.com/documentation/unity/meta-avatars-overview/
