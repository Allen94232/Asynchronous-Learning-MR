using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 摺紙步驟視覺指示系統
/// 在 VR 中顯示摺疊箭頭、輔助線等視覺提示
/// 支援步驟進度控制和手動切換
/// </summary>
public class OrigamiStepGuide : MonoBehaviour
{
    [System.Serializable]
    public class FoldingStep
    {
        [Tooltip("步驟名稱")]
        public string stepName = "步驟 1";
        
        [Tooltip("步驟持續時間（秒）")]
        public float duration = 5f;
        
        [Tooltip("摺疊方向箭頭起點")]
        public Vector3 arrowStart = Vector3.zero;
        
        [Tooltip("摺疊方向箭頭終點")]
        public Vector3 arrowEnd = Vector3.up;
        
        [Tooltip("輔助線點 1（摺線）")]
        public Vector3 foldLinePoint1 = Vector3.zero;
        
        [Tooltip("輔助線點 2（摺線）")]
        public Vector3 foldLinePoint2 = Vector3.right;
        
        [Tooltip("步驟說明文字（可留空）")]
        [TextArea(2, 4)]
        public string instruction = "";
        
        [Tooltip("箭頭顏色")]
        public Color arrowColor = Color.yellow;
        
        [Tooltip("輔助線顏色")]
        public Color lineColor = new Color(1f, 0.5f, 0f, 0.8f);
        
        // 運行時計算的數據
        [HideInInspector] public float startTime = 0f;
        [HideInInspector] public float endTime = 0f;
        [HideInInspector] public float progressStart = 0f;  // 動畫進度起點 (0-1)
        [HideInInspector] public float progressEnd = 0f;    // 動畫進度終點 (0-1)
    }

    [Header("步驟設定")]
    [Tooltip("所有摺疊步驟")]
    public List<FoldingStep> steps = new List<FoldingStep>();
    
    [Header("視覺設定")]
    [Tooltip("箭頭寬度")]
    public float arrowWidth = 0.02f;
    
    [Tooltip("箭頭頭部大小")]
    public float arrowHeadSize = 0.05f;
    
    [Tooltip("輔助線寬度")]
    public float lineWidth = 0.01f;
    
    [Tooltip("虛線段長度")]
    public float dashLength = 0.02f;
    
    [Tooltip("虛線間隔")]
    public float dashGap = 0.01f;
    
    [Tooltip("是否顯示步驟編號")]
    public bool showStepNumber = true;
    
    [Tooltip("文字 UI 預製件（可選）")]
    public GameObject textPrefab;

    [Header("動畫設定")]
    [Tooltip("箭頭脈動速度")]
    public float pulseSpeed = 2f;
    
    [Tooltip("箭頭脈動強度")]
    [Range(0f, 1f)]
    public float pulseStrength = 0.3f;
    
    [Tooltip("是否啟用動畫")]
    public bool enableAnimation = true;

    [Header("同步設定")]
    [Tooltip("摺紙同步控制器")]
    public OrigamiSyncController syncController;
    
    [Tooltip("錄製管理器（用於記錄步驟事件）")]
    public AvatarRecordingManager recordingManager;
    
    [Header("手動控制")]
    [Tooltip("啟用手動步驟切換（N 鍵）")]
    public bool enableManualControl = true;
    
    [Tooltip("步驟完成後暫停動畫")]
    public bool pauseAfterStep = true;
    
    [Header("除錯設定")]
    [Tooltip("顯示除錯訊息")]
    public bool showDebugLogs = false;

    // 私有變數
    private int currentStepIndex = -1;
    private GameObject currentArrow;
    private GameObject currentLine;
    private GameObject currentText;
    private LineRenderer arrowLineRenderer;
    private LineRenderer foldLineRenderer;
    private float animationTimer = 0f;
    private bool isStepComplete = false;
    private float totalStepDuration = 0f;
    private float stepStartTime = 0f;  // 當前步驟開始的真實時間

    void Start()
    {
        // 尋找同步控制器
        if (syncController == null)
        {
            syncController = FindObjectOfType<OrigamiSyncController>();
        }
        
        // 尋找錄製管理器
        if (recordingManager == null)
        {
            recordingManager = FindObjectOfType<AvatarRecordingManager>();
            if (recordingManager == null)
            {
                // 嘗試尋找 TeacherRecordingManager
                var teacherManager = FindObjectOfType<TeacherRecordingManager>();
                if (teacherManager != null)
                {
                    Debug.LogWarning("[OrigamiGuide] 找到 TeacherRecordingManager，但步驟記錄功能僅支援 AvatarRecordingManager");
                }
            }
        }
        
        // 初始化預設步驟（如果列表為空）
        if (steps.Count == 0)
        {
            CreateDefaultSteps();
        }
        
        // 計算步驟時間和進度映射
        CalculateStepTimings();
        
        // 初始化視覺元素
        InitializeVisuals();
    }

    void CreateDefaultSteps()
    {
        // 範例步驟 1：對角線摺疊
        steps.Add(new FoldingStep
        {
            stepName = "對角線摺疊",
            duration = 3f,
            arrowStart = new Vector3(-0.1f, 0.05f, 0f),
            arrowEnd = new Vector3(0.1f, -0.05f, 0f),
            foldLinePoint1 = new Vector3(-0.15f, 0f, 0f),
            foldLinePoint2 = new Vector3(0.15f, 0f, 0f),
            instruction = "沿著虛線將紙對摺",
            arrowColor = Color.yellow,
            lineColor = new Color(1f, 0.5f, 0f, 0.8f)
        });
        
        // 範例步驟 2：展開
        steps.Add(new FoldingStep
        {
            stepName = "展開",
            duration = 2f,
            arrowStart = new Vector3(0.1f, -0.05f, 0f),
            arrowEnd = new Vector3(-0.1f, 0.05f, 0f),
            foldLinePoint1 = Vector3.zero,
            foldLinePoint2 = Vector3.zero,
            instruction = "輕輕展開紙張",
            arrowColor = Color.cyan,
            lineColor = Color.clear
        });
        
        Debug.Log($"[OrigamiGuide] 已創建 {steps.Count} 個預設步驟");
    }
    
    /// <summary>
    /// 計算步驟時間和進度映射
    /// </summary>
    void CalculateStepTimings()
    {
        if (steps.Count == 0) return;
        
        // 計算總持續時間
        totalStepDuration = 0f;
        foreach (var step in steps)
        {
            totalStepDuration += step.duration;
        }
        
        // 獲取 Alembic 總時長
        float alembicDuration = syncController != null && syncController.alembicPlayer != null 
            ? syncController.alembicPlayer.Duration 
            : totalStepDuration;
        
        // 計算每個步驟的開始/結束時間和進度
        float currentTime = 0f;
        for (int i = 0; i < steps.Count; i++)
        {
            steps[i].startTime = currentTime;
            steps[i].endTime = currentTime + steps[i].duration;
            
            // 計算對應的動畫進度 (0-1)
            steps[i].progressStart = currentTime / totalStepDuration;
            steps[i].progressEnd = steps[i].endTime / totalStepDuration;
            
            currentTime = steps[i].endTime;
            
            Debug.Log($"[OrigamiGuide] 步驟 {i + 1}: {steps[i].stepName} - " +
                     $"時間 {steps[i].startTime:F1}s-{steps[i].endTime:F1}s, " +
                     $"進度 {steps[i].progressStart:F2}-{steps[i].progressEnd:F2}");
        }
        
        Debug.Log($"[OrigamiGuide] 總步驟時間: {totalStepDuration}s, Alembic 時長: {alembicDuration}s");
    }

    void InitializeVisuals()
    {
        // 創建箭頭對象
        currentArrow = new GameObject("FoldingArrow");
        currentArrow.transform.SetParent(transform);
        currentArrow.transform.localPosition = Vector3.zero;
        currentArrow.transform.localRotation = Quaternion.identity;
        currentArrow.transform.localScale = Vector3.one;
        arrowLineRenderer = currentArrow.AddComponent<LineRenderer>();
        ConfigureLineRenderer(arrowLineRenderer, arrowWidth);
        
        // 創建輔助線對象
        currentLine = new GameObject("FoldLine");
        currentLine.transform.SetParent(transform);
        currentLine.transform.localPosition = Vector3.zero;
        currentLine.transform.localRotation = Quaternion.identity;
        currentLine.transform.localScale = Vector3.one;
        foldLineRenderer = currentLine.AddComponent<LineRenderer>();
        ConfigureLineRenderer(foldLineRenderer, lineWidth);
        
        // 初始隱藏
        currentArrow.SetActive(false);
        currentLine.SetActive(false);
        
        if (showDebugLogs)
            Debug.Log("[OrigamiGuide] 視覺元素已初始化（Local Space 模式）");
    }

    void ConfigureLineRenderer(LineRenderer lr, float width)
    {
        lr.startWidth = width;
        lr.endWidth = width;
        
        // 使用 Unlit/Color shader 讓線條在 Game View 中可見
        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = Color.white; // 預設顏色，會被步驟顏色覆蓋
        
        // 設定渲染佇列為 Transparent，確保在最上層渲染
        mat.renderQueue = 3000;
        
        lr.material = mat;
        lr.numCapVertices = 5;
        lr.numCornerVertices = 5;
        
        // **關鍵：使用 Local Space 相對位置**
        lr.useWorldSpace = false;
        
        // 禁用陰影，確保線條清晰可見
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        
        // 設定渲染層級遮罩（確保在所有相機中可見）
        lr.renderingLayerMask = 1;
    }

    void Update()
    {
        if (syncController == null) return;
        
        // **檢查是否在播放模式（學生端或教師端播放）**
        bool isPlayingRecording = false;
        float playbackTime = 0f;
        
        if (recordingManager != null && recordingManager.IsPlaying)
        {
            isPlayingRecording = true;
            playbackTime = recordingManager.RecordingDuration;
        }
        
        // **播放模式：根據錄製的步驟事件同步步驟**
        if (isPlayingRecording && recordingManager.CurrentRecording != null)
        {
            int targetStep = recordingManager.GetCurrentOrigamiStep(playbackTime);
            
            if (targetStep >= 0 && targetStep != currentStepIndex && targetStep < steps.Count)
            {
                // 自動切換到目標步驟（不記錄事件）
                currentStepIndex = targetStep;
                isStepComplete = false;
                stepStartTime = Time.time;
                OnStepChanged();
                
                if (showDebugLogs)
                    Debug.Log($"[OrigamiGuide] 播放同步：切換到步驟 {currentStepIndex + 1} (時間: {playbackTime:F2}s)");
            }
            
            if (currentStepIndex >= 0)
            {
                UpdateVisuals();
            }
        }
        // **錄製模式：手動控制**
        else if (enableManualControl)
        {
            HandleManualControl();
            
            // 獲取當前步驟時間
            float currentStepTime = GetCurrentStepTime();
            
            // 更新視覺效果
            if (currentStepIndex >= 0)
            {
                UpdateVisuals();
                
                // 檢查步驟是否完成
                CheckStepCompletion(currentStepTime);
                
                // 控制 Alembic 播放速度（只在手動模式下）
                UpdateAlembicPlayback();
            }
        }
        else
        {
            // 自動模式：根據 Alembic 進度更新步驟
            float currentStepTime = GetCurrentStepTime();
            UpdateCurrentStep(currentStepTime);
            
            if (currentStepIndex >= 0)
            {
                UpdateVisuals();
            }
        }
    }
    
    /// <summary>
    /// 處理手動控制輸入
    /// </summary>
    void HandleManualControl()
    {
        // N 鍵：下一步
        if (Input.GetKeyDown(KeyCode.N))
        {
            float currentStepTime = GetCurrentStepTime();
            
            if (currentStepIndex >= 0 && currentStepIndex < steps.Count)
            {
                FoldingStep currentStep = steps[currentStepIndex];
                Debug.Log($"[OrigamiGuide] 按下 N 鍵 - 當前步驟 {currentStepIndex + 1}, 時間: {currentStepTime:F2}s / {currentStep.duration:F2}s, 完成: {isStepComplete}");
            }
            
            if (isStepComplete || currentStepIndex < 0)
            {
                NextStep();
            }
            else
            {
                FoldingStep currentStep = steps[currentStepIndex];
                float remaining = currentStep.duration - currentStepTime;
                Debug.LogWarning($"[OrigamiGuide] 當前步驟尚未完成，還需要 {remaining:F2} 秒");
            }
        }
    }
    
    /// <summary>
    /// 獲取當前步驟的時間（相對於步驟開始）
    /// </summary>
    float GetCurrentStepTime()
    {
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count)
            return 0f;
        
        // 手動控制模式下，始終計算經過的時間
        if (enableManualControl)
        {
            return Time.time - stepStartTime;
        }
        
        return 0f;
    }
    
    /// <summary>
    /// 檢查步驟是否完成
    /// </summary>
    void CheckStepCompletion(float currentStepTime)
    {
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count)
            return;
        
        FoldingStep currentStep = steps[currentStepIndex];
        
        // 檢查是否完成（留一點容差）
        bool wasComplete = isStepComplete;
        isStepComplete = currentStepTime >= currentStep.duration - 0.1f;
        
        // Debug: 顯示步驟進度
        if (Time.frameCount % 30 == 0) // 每 30 幀顯示一次
        {
            Debug.Log($"[OrigamiGuide] 步驟 {currentStepIndex + 1} 進度: {currentStepTime:F2}s / {currentStep.duration:F2}s, 完成: {isStepComplete}");
        }
        
        // 步驟剛完成
        if (isStepComplete && !wasComplete)
        {
            OnStepCompleted();
        }
    }
    
    /// <summary>
    /// 更新 Alembic 播放速度以匹配步驟
    /// </summary>
    void UpdateAlembicPlayback()
    {
        if (syncController == null || syncController.alembicPlayer == null)
            return;
        
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count)
            return;
        
        FoldingStep currentStep = steps[currentStepIndex];
        
        // 計算當前步驟進度 (0-1)
        float stepTime = Time.time - stepStartTime;
        float stepProgress = Mathf.Clamp01(stepTime / currentStep.duration);
        
        // 如果步驟完成且需要暫停，停止更新
        if (isStepComplete && pauseAfterStep)
        {
            return;
        }
        
        // 計算對應的 Alembic 時間
        float targetProgress = Mathf.Lerp(currentStep.progressStart, currentStep.progressEnd, stepProgress);
        float targetTime = targetProgress * syncController.alembicPlayer.Duration;
        
        // 設置 Alembic 時間
        syncController.alembicPlayer.CurrentTime = targetTime;
    }
    
    /// <summary>
    /// 步驟完成時調用
    /// </summary>
    void OnStepCompleted()
    {
        Debug.Log($"[OrigamiGuide] 步驟 {currentStepIndex + 1} 完成: {steps[currentStepIndex].stepName}");
        
        // 將動畫精確設置到步驟結束位置
        if (syncController.alembicPlayer != null)
        {
            float targetProgress = steps[currentStepIndex].progressEnd;
            syncController.alembicPlayer.CurrentTime = targetProgress * syncController.alembicPlayer.Duration;
            
            Debug.Log($"[OrigamiGuide] 動畫已暫停在進度 {targetProgress:F2}");
        }
    }
    
    /// <summary>
    /// 切換到下一步
    /// </summary>
    void NextStep()
    {
        if (currentStepIndex >= steps.Count - 1)
        {
            Debug.Log("[OrigamiGuide] 已經是最後一步");
            return;
        }
        
        currentStepIndex++;
        isStepComplete = false;
        stepStartTime = Time.time;  // 重置步驟開始時間
        
        Debug.Log($"[OrigamiGuide] 切換到步驟 {currentStepIndex + 1}: {steps[currentStepIndex].stepName}，開始時間: {stepStartTime:F2}");
        
        // **記錄步驟事件到錄製系統**
        RecordStepEvent();
        
        // 設置動畫到新步驟的開始位置
        if (syncController.alembicPlayer != null)
        {
            float targetProgress = steps[currentStepIndex].progressStart;
            syncController.alembicPlayer.CurrentTime = targetProgress * syncController.alembicPlayer.Duration;
        }
        
        // 更新視覺效果
        OnStepChanged();
    }

    void UpdateCurrentStep(float currentStepTime)
    {
        // 自動模式：根據 Alembic 進度自動切換（僅在非手動模式下使用）
        if (!enableManualControl && syncController.alembicPlayer != null)
        {
            float alembicProgress = syncController.alembicPlayer.CurrentTime / syncController.alembicPlayer.Duration;
            
            int newStepIndex = -1;
            for (int i = 0; i < steps.Count; i++)
            {
                if (alembicProgress >= steps[i].progressStart && alembicProgress < steps[i].progressEnd)
                {
                    newStepIndex = i;
                    break;
                }
            }
            
            // 步驟改變
            if (newStepIndex != currentStepIndex)
            {
                currentStepIndex = newStepIndex;
                isStepComplete = false;
                stepStartTime = Time.time;
                OnStepChanged();
            }
        }
    }

    void OnStepChanged()
    {
        if (currentStepIndex < 0)
        {
            // 沒有當前步驟，隱藏所有視覺元素
            if (currentArrow != null) currentArrow.SetActive(false);
            if (currentLine != null) currentLine.SetActive(false);
            if (currentText != null) currentText.SetActive(false);
            
            Debug.Log("[OrigamiGuide] 隱藏指示");
        }
        else
        {
            // 顯示當前步驟
            FoldingStep step = steps[currentStepIndex];
            
            Debug.Log($"[OrigamiGuide] 顯示步驟 {currentStepIndex + 1}: {step.stepName}");
            
            // 顯示箭頭
            if (currentArrow != null)
            {
                currentArrow.SetActive(true);
                UpdateArrow(step);
            }
            
            // 顯示輔助線
            if (currentLine != null && step.lineColor.a > 0)
            {
                currentLine.SetActive(true);
                UpdateFoldLine(step);
            }
            else if (currentLine != null)
            {
                currentLine.SetActive(false);
            }
            
            // 更新文字（如果有）
            UpdateInstructionText(step);
        }
    }

    void UpdateVisuals()
    {
        if (currentStepIndex < 0 || !enableAnimation) return;
        
        // 脈動動畫
        animationTimer += Time.deltaTime * pulseSpeed;
        float pulse = 1f + Mathf.Sin(animationTimer) * pulseStrength;
        
        // 應用到箭頭
        if (arrowLineRenderer != null)
        {
            arrowLineRenderer.startWidth = arrowWidth * pulse;
            arrowLineRenderer.endWidth = arrowWidth * pulse * 1.5f; // 箭頭頭部稍大
        }
    }

    void UpdateArrow(FoldingStep step)
    {
        if (arrowLineRenderer == null) return;
        
        Vector3 direction = (step.arrowEnd - step.arrowStart).normalized;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.forward).normalized;
        
        // 創建箭頭形狀：線 + 箭頭
        List<Vector3> points = new List<Vector3>();
        
        // 主線
        points.Add(step.arrowStart);
        points.Add(step.arrowEnd - direction * arrowHeadSize);
        
        // 箭頭左側
        Vector3 arrowLeft = step.arrowEnd - direction * arrowHeadSize + perpendicular * arrowHeadSize * 0.5f;
        points.Add(arrowLeft);
        
        // 箭頭尖端
        points.Add(step.arrowEnd);
        
        // 箭頭右側
        Vector3 arrowRight = step.arrowEnd - direction * arrowHeadSize - perpendicular * arrowHeadSize * 0.5f;
        points.Add(arrowRight);
        
        // 回到主線末端
        points.Add(step.arrowEnd - direction * arrowHeadSize);
        
        arrowLineRenderer.positionCount = points.Count;
        arrowLineRenderer.SetPositions(points.ToArray());
        arrowLineRenderer.startColor = step.arrowColor;
        arrowLineRenderer.endColor = step.arrowColor;
    }

    void UpdateFoldLine(FoldingStep step)
    {
        if (foldLineRenderer == null) return;
        
        // 創建虛線效果
        Vector3 start = step.foldLinePoint1;
        Vector3 end = step.foldLinePoint2;
        Vector3 direction = (end - start).normalized;
        float totalLength = Vector3.Distance(start, end);
        
        List<Vector3> dashPoints = new List<Vector3>();
        float currentLength = 0f;
        bool isDash = true;
        
        while (currentLength < totalLength)
        {
            if (isDash)
            {
                // 畫線段
                dashPoints.Add(start + direction * currentLength);
                currentLength += dashLength;
                dashPoints.Add(start + direction * Mathf.Min(currentLength, totalLength));
            }
            else
            {
                // 間隔（不畫線，但需要移動位置）
                currentLength += dashGap;
            }
            isDash = !isDash;
        }
        
        foldLineRenderer.positionCount = dashPoints.Count;
        foldLineRenderer.SetPositions(dashPoints.ToArray());
        foldLineRenderer.startColor = step.lineColor;
        foldLineRenderer.endColor = step.lineColor;
    }

    void UpdateInstructionText(FoldingStep step)
    {
        // 如果 instruction 為空，不顯示文字
        if (string.IsNullOrWhiteSpace(step.instruction))
        {
            if (currentText != null)
            {
                currentText.SetActive(false);
            }
            return;
        }
        
        // 如果有 TextMeshPro 或 Canvas 文字，在這裡更新
        if (textPrefab != null && currentText == null)
        {
            currentText = Instantiate(textPrefab, transform);
        }
        
        if (currentText != null)
        {
            currentText.SetActive(true);
            
            // 更新文字內容和位置
            var textComponent = currentText.GetComponent<UnityEngine.UI.Text>();
            if (textComponent != null)
            {
                textComponent.text = $"{currentStepIndex + 1}. {step.instruction}";
            }
            
            // 或 TextMeshPro
            var tmpComponent = currentText.GetComponent<TMPro.TextMeshPro>();
            if (tmpComponent != null)
            {
                tmpComponent.text = $"{currentStepIndex + 1}. {step.instruction}";
            }
        }
    }

    /// <summary>
    /// 在編輯器中繪製視覺化輔助線
    /// </summary>
    void OnDrawGizmos()
    {
        if (steps == null || steps.Count == 0) return;
        
        foreach (var step in steps)
        {
            // 繪製箭頭
            Gizmos.color = step.arrowColor;
            Gizmos.DrawLine(
                transform.TransformPoint(step.arrowStart),
                transform.TransformPoint(step.arrowEnd)
            );
            
            // 繪製箭頭頭部
            Vector3 worldEnd = transform.TransformPoint(step.arrowEnd);
            Gizmos.DrawSphere(worldEnd, arrowHeadSize);
            
            // 繪製輔助線
            if (step.lineColor.a > 0)
            {
                Gizmos.color = step.lineColor;
                Gizmos.DrawLine(
                    transform.TransformPoint(step.foldLinePoint1),
                    transform.TransformPoint(step.foldLinePoint2)
                );
            }
        }
    }

    /// <summary>
    /// 手動跳到指定步驟
    /// </summary>
    public void JumpToStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= steps.Count)
        {
            Debug.LogWarning($"[OrigamiGuide] 無效的步驟索引: {stepIndex}");
            return;
        }
        
        currentStepIndex = stepIndex;
        isStepComplete = false;
        stepStartTime = Time.time;
        
        // **記錄步驟事件**
        RecordStepEvent();
        
        OnStepChanged();
        
        // 同步 Alembic 時間到步驟開始位置
        if (syncController != null && syncController.alembicPlayer != null)
        {
            float targetProgress = steps[stepIndex].progressStart;
            syncController.alembicPlayer.CurrentTime = targetProgress * syncController.alembicPlayer.Duration;
            
            Debug.Log($"[OrigamiGuide] 跳到步驟 {stepIndex + 1}: {steps[stepIndex].stepName}");
        }
    }
    
    /// <summary>
    /// 重新計算步驟時間（在修改步驟後調用）
    /// </summary>
    public void RecalculateStepTimings()
    {
        CalculateStepTimings();
    }
    
    /// <summary>
    /// 開始第一步
    /// </summary>
    public void StartFirstStep()
    {
        if (steps.Count > 0)
        {
            JumpToStep(0);
        }
    }

    /// <summary>
    /// 添加新步驟
    /// </summary>
    public void AddStep(FoldingStep newStep)
    {
        steps.Add(newStep);
        CalculateStepTimings();
        Debug.Log($"[OrigamiGuide] 已添加步驟: {newStep.stepName}");
    }

    /// <summary>
    /// 清除所有步驟
    /// </summary>
    public void ClearSteps()
    {
        steps.Clear();
        currentStepIndex = -1;
        OnStepChanged();
    }
    
    /// <summary>
    /// 記錄當前步驟事件到錄製系統
    /// </summary>
    void RecordStepEvent()
    {
        if (recordingManager == null || !recordingManager.IsRecording)
            return;
        
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count)
            return;
        
        recordingManager.RecordOrigamiStepEvent(currentStepIndex, steps[currentStepIndex].stepName);
    }
    
    /// <summary>
    /// 重置到初始狀態（錄製結束後呼叫）
    /// </summary>
    public void ResetToStart()
    {
        currentStepIndex = -1;
        isStepComplete = false;
        stepStartTime = 0f;
        
        // 隱藏所有視覺元素
        if (currentArrow != null) currentArrow.SetActive(false);
        if (currentLine != null) currentLine.SetActive(false);
        if (currentText != null) currentText.SetActive(false);
        
        Debug.Log("[OrigamiGuide] 已重置到初始狀態");
    }
}
