# 摺紙步驟指示系統使用說明

## 檔案
- `OrigamiStepGuideSimple.cs` - 新的簡化版（推薦使用）
- `OrigamiStepGuide.cs` - 原始複雜版（已備份）

## 簡化版特色

### 三大視覺要素
1. **起始點（綠色球體）** - 標示手部需要觸發的位置
2. **終點（紅色球體）** - 標示摺疊目標位置
3. **摺痕線（黃色虛線）** - 標示摺線位置

### 工作流程
```
步驟完成 → 等待 3 秒 → 顯示下一步驟提示（綠球+紅球+黃線）
         → 偵測手部 Pinch 抓取綠球
         → 所有綠球都被觸發後 → 開始播放摺紙動畫
         → 移除綠球，保留紅球和黃線
         → 動畫播放完畢 → 重複循環
```

## Unity 設定步驟

### 1. 替換腳本
1. 選擇你的 `OrigamiStepGuide` GameObject
2. 移除舊的 `OrigamiStepGuide` 組件
3. 添加新的 `OrigamiStepGuideSimple` 組件

### 2. 設定步驟
在 Inspector 中展開 `Steps` 列表：

**每個步驟包含：**
- `Step Name` - 步驟名稱
- `Duration` - 動畫持續時間（秒）
- `Start Points` - 起始點位置列表（Local Space，綠色球）
  - 可以有 1 個或多個點
  - 位置相對於 OrigamiStepGuide 父物件（通常是摺紙物件中心）
- `End Points` - 終點位置列表（Local Space，紅色球）
  - 可以有多個點
- `Fold Line Point 1/2` - 摺痕線的兩個端點（黃色虛線）

**範例設定：**
```
步驟 1: 對折
  - Start Points: [(0, 0, 0.1)]  // 紙張上方中間
  - End Points: [(0, 0, -0.1)]   // 紙張下方中間
  - Fold Line: ((-0.1, 0, 0), (0.1, 0, 0))  // 水平摺線
  
步驟 2: 兩邊往內折
  - Start Points: [(-0.1, 0, 0), (0.1, 0, 0)]  // 左右兩個角
  - End Points: [(0, 0, 0), (0, 0, 0)]         // 中心點
  - Fold Line: ((0, -0.1, 0), (0, 0.1, 0))    // 垂直摺線
```

### 3. 視覺設定
```
Sphere Radius: 0.03          // 球體大小
Start Point Color: Green     // 起始點顏色
End Point Color: Red         // 終點顏色
Fold Line Width: 0.01        // 摺痕線寬度
Fold Line Color: Yellow      // 摺痕線顏色
Dash Length: 0.02            // 虛線段長度
Dash Gap: 0.01               // 虛線間隔
```

### 4. 手勢觸發設定
```
Left Hand: 拖曳左手 OVRHand 到此欄位
Right Hand: 拖曳右手 OVRHand 到此欄位
Pinch Threshold: 0.7         // Pinch 強度閾值（0-1）
Trigger Distance: 0.05       // 觸發距離（米）
Use Pinch Gesture: ✓         // 啟用 Pinch（取消則用簡單碰觸）
```

### 5. 時間設定
```
Wait Time After Step: 3      // 步驟完成後等待秒數
```

### 6. 同步設定
```
Sync Controller: 拖曳 OrigamiSyncController
Recording Manager: 拖曳 AvatarRecordingManager 或 TeacherRecordingManager
```

### 7. 手動控制
```
Enable Manual Control: ✓     // 啟用 N 鍵手動觸發
Pause After Step: ✓          // 步驟完成後暫停動畫
Show Debug Logs: ✓           // 顯示除錯訊息
```

## 使用方式

### 教師端（錄製模式）
1. 按 `R` 開始錄製
2. 等待綠色球體出現
3. 用手 Pinch 抓取所有綠色球體（或按 `N` 手動觸發）
4. 系統自動播放摺紙動畫
5. 重複步驟 2-4 直到所有步驟完成
6. 按 `R` 停止錄製

**Console 會顯示：**
```
[OrigamiGuideSimple] === 步驟 1/5: 對折 ===
[OrigamiGuideSimple] 等待觸發 2 個起始點...
[OrigamiGuideSimple] ✓ 起始點 1/2 已觸發
[OrigamiGuideSimple] ✓ 起始點 2/2 已觸發
[OrigamiGuideSimple] ✓ 起始點已觸發，開始播放動畫
[OrigamiGuideSimple] 步驟 1 完成
[OrigamiGuideSimple] 等待 3 秒後顯示下一步驟...
```

### 學生端（播放模式）
1. 按 `L` 載入錄製檔案
2. 按 `P` 播放
3. 系統自動在正確時間點切換步驟
4. 視覺提示（紅球和黃線）自動同步顯示

**注意：** 播放模式不會顯示綠色起始點（因為已經被觸發過了）

## 觸發邏輯

### 單一起始點
- 左手或右手任一手觸發即可

### 多個起始點
- 需要觸發所有起始點才會開始動畫
- 可以用左右手同時觸發不同的點
- 已觸發的點會變半透明

### Pinch 手勢檢測
- 食指 Pinch（拇指和食指捏合）
- Pinch 強度 >= 設定閾值（預設 0.7）
- 手部距離球體 < 觸發距離（預設 5cm）

### 簡單碰觸模式
- 關閉 `Use Pinch Gesture`
- 只要手部進入觸發距離即可

## 常見問題

### Q: Game View 看不到球體和線條？
A: 檢查：
1. OrigamiStepGuideSimple 是否在 Camera 視野範圍內
2. Camera 的 Culling Mask 是否包含該 Layer
3. 球體顏色是否與背景太接近

### Q: 手勢無法觸發？
A: 檢查：
1. Left Hand 和 Right Hand 是否正確設定
2. Console 是否顯示 "未找到 OVRHand 組件"
3. 觸發距離是否太小（試試增加到 0.1）
4. Pinch 閾值是否太高（試試降到 0.5）

### Q: 起始點位置不對？
A: 記住：
- 所有位置都是 **Local Space**（相對於父物件）
- OrigamiStepGuideSimple 應該是 OrigamiSyncController 的子物件
- 摺紙物件中心為 (0, 0, 0)
- 在 Scene View 中使用 Gizmos 輔助定位

### Q: 想用 UI 按鈕觸發步驟？
A: 調用 `TriggerNextStep()` 方法：
```csharp
// 在 UI Button 的 OnClick 事件中
origamiStepGuideSimple.TriggerNextStep();
```

## 進階用法

### 跳轉到指定步驟
```csharp
origamiStepGuideSimple.JumpToStep(2); // 跳到第 3 個步驟（索引從 0 開始）
```

### 檢查當前步驟
```csharp
int currentStep = origamiStepGuideSimple.currentStepIndex;
bool isWaiting = origamiStepGuideSimple.isWaitingForTrigger;
bool isPlaying = origamiStepGuideSimple.isPlayingStep;
```

## 與原版的差異

| 功能 | 原版 | 簡化版 |
|------|------|--------|
| 視覺元素 | 複雜箭頭 + 摺線 | 綠球 + 紅球 + 黃線 |
| 觸發方式 | 僅 N 鍵 | Pinch 手勢 + N 鍵 |
| 步驟流程 | 自動播放 | 等待觸發 → 播放 |
| 顏色 | 可自訂 | 固定（綠/紅/黃） |
| 動畫效果 | 脈動動畫 | 無 |
| 程式碼 | 827 行 | 486 行 |

