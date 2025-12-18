using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 摺紙步驟視覺指示系統（簡化版）
/// 三大要素：起始點（綠球）、終點（紅球）、摺痕線（黃虛線）
/// 支援 Pinch 手勢觸發步驟
/// </summary>
public class OrigamiStepGuideSimple : MonoBehaviour
{
    [System.Serializable]
    public class FoldingStep
    {
        [Tooltip("步驟名稱")]
        public string stepName = "步驟 1";
        
        [Tooltip("步驟持續時間（秒）")]
        public float duration = 5f;
        
        [Tooltip("起始點位置（綠色球，可多個）")]
        public List<Vector3> startPoints = new List<Vector3> { Vector3.zero };
        
        [Tooltip("終點位置（紅色球，可多個）")]
        public List<Vector3> endPoints = new List<Vector3> { Vector3.up };
        
        [Tooltip("摺痕線點 1")]
        public Vector3 foldLinePoint1 = Vector3.zero;
        
        [Tooltip("摺痕線點 2")]
        public Vector3 foldLinePoint2 = Vector3.right;
        
        // 運行時計算的數據
        [HideInInspector] public float startTime = 0f;
        [HideInInspector] public float endTime = 0f;
        [HideInInspector] public float progressStart = 0f;
        [HideInInspector] public float progressEnd = 0f;
    }

    [Header("步驟設定")]
    public List<FoldingStep> steps = new List<FoldingStep>();

    [Header("視覺設定 - 球體")]
    [Tooltip("球體半徑")]
    public float sphereRadius = 0.03f;
    
    [Tooltip("起始點顏色（綠色）")]
    public Color startPointColor = Color.green;
    
    [Tooltip("終點顏色（紅色）")]
    public Color endPointColor = Color.red;

    [Header("視覺設定 - 摺痕線")]
    [Tooltip("摺痕線寬度")]
    public float foldLineWidth = 0.01f;
    
    [Tooltip("摺痕線顏色（黃色）")]
    public Color foldLineColor = Color.yellow;
    
    [Tooltip("虛線段長度")]
    public float dashLength = 0.02f;
    
    [Tooltip("虛線間隔")]
    public float dashGap = 0.01f;

    [Header("手勢觸發設定")]
    [Tooltip("左手 OVRHand 組件")]
    public OVRHand leftHand;
    
    [Tooltip("右手 OVRHand 組件")]
    public OVRHand rightHand;
    
    [Tooltip("Pinch 強度閾值（0-1）")]
    [Range(0f, 1f)]
    public float pinchThreshold = 0.7f;
    
    [Tooltip("觸發距離閾值（米）")]
    public float triggerDistance = 0.05f;
    
    [Tooltip("使用 Pinch 手勢（false = 簡單碰觸）")]
    public bool usePinchGesture = true;

    [Header("時間設定")]
    [Tooltip("步驟完成後等待時間（秒）")]
    public float waitTimeAfterStep = 0.5f;

    [Header("同步設定")]
    public OrigamiSyncController syncController;
    
    [Tooltip("錄製管理器（支持 AvatarRecordingManager 或 TeacherRecordingManager）")]
    public MonoBehaviour recordingManager;
    
    [Header("手動控制")]
    [Tooltip("啟用 N 鍵手動觸發")]
    public bool enableManualControl = true;
    
    [Tooltip("步驟完成後暫停動畫")]
    public bool pauseAfterStep = true;
    
    [Header("除錯設定")]
    public bool showDebugLogs = true;
    
    [Header("Scene 視圖預覽")]
    [Tooltip("在 Scene 視圖中顯示指引（不運行時）")]
    public bool showGizmosInEditor = true;
    
    [Tooltip("預覽的步驟索引（-1 = 全部顯示）")]
    [Range(-1, 20)]
    public int previewStepIndex = -1;

    // 私有變數
    private int currentStepIndex = -1;
    private bool isWaitingForTrigger = false;
    private bool isPlayingStep = false;
    private HashSet<int> triggeredStartPoints = new HashSet<int>();
    private float stepStartTime = 0f;
    private float totalStepDuration = 0f;
    
    // 視覺物件
    private List<GameObject> currentStartSpheres = new List<GameObject>();
    private List<GameObject> currentEndSpheres = new List<GameObject>();
    private GameObject foldLineObject;
    private LineRenderer foldLineRenderer;
    
    // 手部位置快取
    private Vector3 leftHandIndexTip;
    private Vector3 rightHandIndexTip;
    private bool leftHandTracked = false;
    private bool rightHandTracked = false;

    void Start()
    {
        // 尋找同步控制器
        if (syncController == null)
            syncController = FindObjectOfType<OrigamiSyncController>();
        
        // 尋找錄製管理器（支持多種類型）
        if (recordingManager == null)
        {
            recordingManager = FindObjectOfType<AvatarRecordingManager>();
            if (recordingManager == null)
                recordingManager = FindObjectOfType<TeacherRecordingManager>();
            
            if (recordingManager == null)
            {
                Debug.LogWarning("[OrigamiGuideSimple] 未找到 AvatarRecordingManager 或 TeacherRecordingManager，步驟事件記錄功能將無法使用。");
            }
        }
        
        // 尋找手部組件
        FindHands();
        
        // 創建摺痕線物件
        InitializeFoldLine();
        
        // 計算步驟時間
        CalculateStepTimings();
        
        if (steps.Count > 0)
        {
            StartCoroutine(StepSequence());
        }
        
        Debug.Log($"[OrigamiGuideSimple] 初始化完成，共 {steps.Count} 個步驟");
    }

    void FindHands()
    {
        if (leftHand == null || rightHand == null)
        {
            OVRHand[] hands = FindObjectsOfType<OVRHand>();
            foreach (var hand in hands)
            {
                // OVRHand 沒有 HandType 屬性，需要透過名稱或 OVRSkeleton 判斷
                var skeleton = hand.GetComponent<OVRSkeleton>();
                if (skeleton != null)
                {
                    if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandLeft)
                        leftHand = hand;
                    else if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandRight)
                        rightHand = hand;
                }
            }
        }
        
        if (leftHand == null || rightHand == null)
        {
            Debug.LogWarning("[OrigamiGuideSimple] 未找到 OVRHand 組件！手勢觸發功能將無法使用。");
        }
    }

    void InitializeFoldLine()
    {
        foldLineObject = new GameObject("FoldLine");
        foldLineObject.transform.SetParent(transform);
        foldLineObject.transform.localPosition = Vector3.zero;
        foldLineObject.transform.localRotation = Quaternion.identity;
        foldLineObject.transform.localScale = Vector3.one;
        
        foldLineRenderer = foldLineObject.AddComponent<LineRenderer>();
        foldLineRenderer.startWidth = foldLineWidth;
        foldLineRenderer.endWidth = foldLineWidth;
        
        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = foldLineColor;
        mat.renderQueue = 3000;
        
        foldLineRenderer.material = mat;
        foldLineRenderer.useWorldSpace = false;
        foldLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        foldLineRenderer.receiveShadows = false;
        
        foldLineObject.SetActive(false);
    }

    void CalculateStepTimings()
    {
        totalStepDuration = 0f;
        foreach (var step in steps)
        {
            totalStepDuration += step.duration;
        }
        
        float currentTime = 0f;
        for (int i = 0; i < steps.Count; i++)
        {
            steps[i].startTime = currentTime;
            steps[i].endTime = currentTime + steps[i].duration;
            steps[i].progressStart = totalStepDuration > 0 ? currentTime / totalStepDuration : 0;
            steps[i].progressEnd = totalStepDuration > 0 ? steps[i].endTime / totalStepDuration : 1;
            currentTime = steps[i].endTime;
        }
    }

    IEnumerator StepSequence()
    {
        for (int i = 0; i < steps.Count; i++)
        {
            currentStepIndex = i;
            FoldingStep step = steps[i];
            
            if (showDebugLogs)
                Debug.Log($"[OrigamiGuideSimple] === 步驟 {i + 1}/{steps.Count}: {step.stepName} ===");
            
            // 顯示提示（起始點 + 終點 + 摺痕線）
            ShowStepGuide(step);
            
            // 等待觸發
            isWaitingForTrigger = true;
            triggeredStartPoints.Clear();
            
            if (showDebugLogs)
                Debug.Log($"[OrigamiGuideSimple] 等待觸發 {step.startPoints.Count} 個起始點...");
            
            yield return new WaitUntil(() => !isWaitingForTrigger);
            
            // 觸發後，移除起始點，開始播放動畫
            HideStartPoints();
            
            if (showDebugLogs)
                Debug.Log($"[OrigamiGuideSimple] ✓ 起始點已觸發，開始播放動畫");
            
            // 記錄步驟事件
            RecordStepEvent();
            
            // 播放步驟動畫
            isPlayingStep = true;
            stepStartTime = Time.time;
            
            // 更新 Alembic 播放
            UpdateAlembicForStep(step);
            
            // 等待步驟完成
            yield return new WaitForSeconds(step.duration);
            
            isPlayingStep = false;
            
            // 隱藏所有提示
            HideStepGuide();
            
            if (showDebugLogs)
                Debug.Log($"[OrigamiGuideSimple] 步驟 {i + 1} 完成");
            
            // 等待後顯示下一步驟
            if (i < steps.Count - 1)
            {
                if (showDebugLogs)
                    Debug.Log($"[OrigamiGuideSimple] 等待 {waitTimeAfterStep} 秒後顯示下一步驟...");
                yield return new WaitForSeconds(waitTimeAfterStep);
            }
            else
            {
                // 最後一個步驟完成，設為可以重新開始
                if (showDebugLogs)
                    Debug.Log("[OrigamiGuideSimple] ✓ 所有步驟完成！按 N 可重新開始。");
                
                // 設為等待觸發狀態，但不顯示任何提示
                currentStepIndex = -1;
                isWaitingForTrigger = true;
            }
        }
    }

    // 私有變數 - 用於追蹤錄製狀態
    private bool wasRecording = false;
    private bool guidelinesCurrentlyVisible = false;
    
    void Update()
    {
        // 檢查播放模式（學生端同步）
        // 播放模式時，步驟同步由 Recording Manager 的 SyncOrigamiStep 完全控制
        bool isInPlaybackMode = IsInPlaybackMode();
        
        // 如果進入播放模式，停止所有 coroutines 以避免干擾
        if (isInPlaybackMode && isPlayingStep)
        {
            StopAllCoroutines();
            isPlayingStep = false;
            isWaitingForTrigger = false;
        }
        
        // 檢測錄製狀態變化
        if (recordingManager != null)
        {
            bool isRecordingNow = IsRecording();
            
            // 開始錄製
            if (isRecordingNow && !wasRecording)
            {
                if (showDebugLogs)
                    Debug.Log("[OrigamiGuideSimple] 開始錄製 - 重置步驟並顯示指示");
                ResetToStart();
                ShowGuidelines();
                guidelinesCurrentlyVisible = true;
            }
            // 結束錄製
            else if (!isRecordingNow && wasRecording)
            {
                if (showDebugLogs)
                    Debug.Log("[OrigamiGuideSimple] 結束錄製 - 隱藏指示");
                HideGuidelines();
                guidelinesCurrentlyVisible = false;
            }
            // 錄製中，確保指示顯示
            else if (isRecordingNow && !guidelinesCurrentlyVisible)
            {
                ShowGuidelines();
                guidelinesCurrentlyVisible = true;
            }
            // 非錄製中（播放或待機），確保指示隱藏
            else if (!isRecordingNow && guidelinesCurrentlyVisible)
            {
                HideGuidelines();
                guidelinesCurrentlyVisible = false;
            }
            
            wasRecording = isRecordingNow;
        }
        
        // 更新 Alembic 動畫播放（只在非播放模式下）
        if (!isInPlaybackMode && isPlayingStep && currentStepIndex >= 0 && currentStepIndex < steps.Count)
        {
            UpdateAlembicPlayback();
        }
        
        // 手動控制
        if (enableManualControl && Input.GetKeyDown(KeyCode.N))
        {
            if (isWaitingForTrigger)
            {
                // 如果 currentStepIndex == -1，表示所有步驟完成，重新開始
                if (currentStepIndex == -1)
                {
                    if (showDebugLogs)
                        Debug.Log("[OrigamiGuideSimple] 重新開始所有步驟");
                    ResetToStart();
                }
                else
                {
                    if (showDebugLogs)
                        Debug.Log("[OrigamiGuideSimple] 手動觸發步驟（N 鍵）");
                    isWaitingForTrigger = false;
                }
            }
        }
        
        // 手勢偵測（只在非播放模式下）
        if (!isInPlaybackMode && isWaitingForTrigger && currentStepIndex >= 0 && currentStepIndex < steps.Count)
        {
            UpdateHandTracking();
            CheckTriggers();
        }
    }

    void UpdateHandTracking()
    {
        leftHandTracked = false;
        rightHandTracked = false;
        
        // 使用 OVRPlugin 直接獲取手部狀態
        OVRPlugin.HandState leftHandState = default;
        if (OVRPlugin.GetHandState(OVRPlugin.Step.Render, OVRPlugin.Hand.HandLeft, ref leftHandState))
        {
            // 左手位置（將 OVR 座標轉換為 Unity 座標）
            leftHandIndexTip = leftHandState.PointerPose.Position.FromFlippedZVector3f();
            leftHandTracked = true;
            
            if (showDebugLogs && Time.frameCount % 60 == 0)
                Debug.Log($"[OrigamiGuideSimple] 左手追蹤: {leftHandIndexTip}");
        }
        
        OVRPlugin.HandState rightHandState = default;
        if (OVRPlugin.GetHandState(OVRPlugin.Step.Render, OVRPlugin.Hand.HandRight, ref rightHandState))
        {
            // 右手位置
            rightHandIndexTip = rightHandState.PointerPose.Position.FromFlippedZVector3f();
            rightHandTracked = true;
            
            if (showDebugLogs && Time.frameCount % 60 == 0)
                Debug.Log($"[OrigamiGuideSimple] 右手追蹤: {rightHandIndexTip}");
        }
    }

    void CheckTriggers()
    {
        FoldingStep step = steps[currentStepIndex];
        
        if (showDebugLogs && Time.frameCount % 120 == 0)
            Debug.Log($"[OrigamiGuideSimple] 檢查觸發: 步驟 {currentStepIndex}, 左手={leftHandTracked}, 右手={rightHandTracked}");
        
        for (int i = 0; i < step.startPoints.Count; i++)
        {
            if (triggeredStartPoints.Contains(i))
                continue;
            
            Vector3 worldPos = transform.TransformPoint(step.startPoints[i]);
            
            // 檢查左手
            if (leftHandTracked && IsHandTriggering(true, leftHandIndexTip, worldPos))
            {
                triggeredStartPoints.Add(i);
                OnStartPointTriggered(i);
                if (showDebugLogs)
                    Debug.Log($"[OrigamiGuideSimple] ✓ 左手觸發起始點 {i}");
            }
            // 檢查右手
            else if (rightHandTracked && IsHandTriggering(false, rightHandIndexTip, worldPos))
            {
                triggeredStartPoints.Add(i);
                OnStartPointTriggered(i);
                if (showDebugLogs)
                    Debug.Log($"[OrigamiGuideSimple] ✓ 右手觸發起始點 {i}");
            }
        }
        
        // 檢查是否所有起始點都被觸發
        if (triggeredStartPoints.Count >= step.startPoints.Count)
        {
            isWaitingForTrigger = false;
            if (showDebugLogs)
                Debug.Log($"[OrigamiGuideSimple] ✓ 所有起始點已觸發，進入步驟執行");
        }
    }

    bool IsHandTriggering(bool isLeftHand, Vector3 handPos, Vector3 targetPos)
    {
        float distance = Vector3.Distance(handPos, targetPos);
        
        if (distance > triggerDistance)
            return false;
        
        if (usePinchGesture)
        {
            // 使用 OVRPlugin 檢查 Pinch 手勢
            OVRPlugin.Hand handType = isLeftHand ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
            
            OVRPlugin.HandState handState = default;
            if (OVRPlugin.GetHandState(OVRPlugin.Step.Render, handType, ref handState))
            {
                float pinchStrength = handState.PinchStrength[(int)OVRPlugin.HandFinger.Index];
                bool isPinching = handState.Pinches.HasFlag(OVRPlugin.HandFingerPinch.Index);
                
                if (showDebugLogs && distance <= triggerDistance && Time.frameCount % 30 == 0)
                    Debug.Log($"[OrigamiGuideSimple] 手在範圍內: {(isLeftHand ? "左" : "右")}手, 距離={distance:F3}, Pinch={isPinching}, 強度={pinchStrength:F2}");
                
                return isPinching && pinchStrength >= pinchThreshold;
            }
            
            return false;
        }
        else
        {
            // 簡單碰觸檢測
            if (showDebugLogs && Time.frameCount % 30 == 0)
                Debug.Log($"[OrigamiGuideSimple] ✓ {(isLeftHand ? "左" : "右")}手碰觸觸發: 距離={distance:F3}");
            return true;
        }
    }

    void OnStartPointTriggered(int index)
    {
        // 改變球體為半透明
        if (index < currentStartSpheres.Count)
        {
            var sphere = currentStartSpheres[index];
            if (sphere != null)
            {
                var renderer = sphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color c = renderer.material.color;
                    c.a = 0.3f;
                    renderer.material.color = c;
                }
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"[OrigamiGuideSimple] ✓ 起始點 {index + 1}/{steps[currentStepIndex].startPoints.Count} 已觸發");
    }

    void ShowStepGuide(FoldingStep step, bool showStartPoints = true)
    {
        // 只在錄製時顯示指示
        if (!IsRecording())
        {
            if (showDebugLogs)
                Debug.Log("[OrigamiGuideSimple] 非錄製狀態，不顯示指示");
            return;
        }
        
        // 清除舊的視覺元素
        HideStepGuide();
        
        // 顯示起始點（綠色球）
        if (showStartPoints)
        {
            foreach (var pos in step.startPoints)
            {
                GameObject sphere = CreateSphere(pos, startPointColor);
                currentStartSpheres.Add(sphere);
            }
        }
        
        // 顯示終點（紅色球）
        foreach (var pos in step.endPoints)
        {
            GameObject sphere = CreateSphere(pos, endPointColor);
            currentEndSpheres.Add(sphere);
        }
        
        // 顯示摺痕線（黃色虛線）
        UpdateFoldLine(step.foldLinePoint1, step.foldLinePoint2);
        foldLineObject.SetActive(true);
    }

    void HideStartPoints()
    {
        foreach (var sphere in currentStartSpheres)
        {
            if (sphere != null)
                Destroy(sphere);
        }
        currentStartSpheres.Clear();
    }

    void HideStepGuide()
    {
        // 清除起始點
        HideStartPoints();
        
        // 清除終點
        foreach (var sphere in currentEndSpheres)
        {
            if (sphere != null)
                Destroy(sphere);
        }
        currentEndSpheres.Clear();
        
        // 隱藏摺痕線
        if (foldLineObject != null)
            foldLineObject.SetActive(false);
    }

    GameObject CreateSphere(Vector3 localPos, Color color)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(transform);
        sphere.transform.localPosition = localPos;
        sphere.transform.localScale = Vector3.one * sphereRadius * 2f;
        
        // 設定材質
        Renderer renderer = sphere.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = color;
        mat.renderQueue = 3000; // Transparent queue
        renderer.material = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        // 移除 Collider（不需要物理碰撞）
        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        
        return sphere;
    }

    void UpdateFoldLine(Vector3 point1, Vector3 point2)
    {
        if (foldLineRenderer == null) return;
        
        // 創建虛線
        Vector3 direction = (point2 - point1).normalized;
        float totalLength = Vector3.Distance(point1, point2);
        
        List<Vector3> dashPoints = new List<Vector3>();
        float currentLength = 0f;
        bool isDash = true;
        
        while (currentLength < totalLength)
        {
            if (isDash)
            {
                dashPoints.Add(point1 + direction * currentLength);
                currentLength += dashLength;
                dashPoints.Add(point1 + direction * Mathf.Min(currentLength, totalLength));
            }
            else
            {
                currentLength += dashGap;
            }
            isDash = !isDash;
        }
        
        foldLineRenderer.positionCount = dashPoints.Count;
        foldLineRenderer.SetPositions(dashPoints.ToArray());
        
        // 確保顏色正確設定（不只是 LineRenderer 的 Color，還要設定 Material 的 Color）
        foldLineRenderer.startColor = foldLineColor;
        foldLineRenderer.endColor = foldLineColor;
        if (foldLineRenderer.material != null)
        {
            foldLineRenderer.material.color = foldLineColor;
        }
    }

    void UpdateAlembicForStep(FoldingStep step)
    {
        if (syncController == null || syncController.alembicPlayer == null)
            return;
        
        // 設定動畫起始位置
        float targetProgress = step.progressStart;
        syncController.alembicPlayer.CurrentTime = targetProgress * syncController.alembicPlayer.Duration;
        
        if (showDebugLogs)
            Debug.Log($"[OrigamiGuideSimple] 設定 Alembic 起始進度: {targetProgress:F2} ({syncController.alembicPlayer.CurrentTime:F2}s)");
    }
    
    void UpdateAlembicPlayback()
    {
        if (syncController == null || syncController.alembicPlayer == null)
            return;
        
        FoldingStep step = steps[currentStepIndex];
        
        // 計算當前步驟經過的時間
        float elapsedTime = Time.time - stepStartTime;
        
        // 將步驟時間映射到 Alembic 動畫進度
        float stepProgress = Mathf.Clamp01(elapsedTime / step.duration);
        float targetProgress = Mathf.Lerp(step.progressStart, step.progressEnd, stepProgress);
        float targetTime = targetProgress * syncController.alembicPlayer.Duration;
        
        // 更新 Alembic 當前時間
        syncController.alembicPlayer.CurrentTime = targetTime;
        
        // 除錯資訊（每 30 幀顯示一次）
        if (showDebugLogs && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[OrigamiGuideSimple] Alembic 播放: 步驟進度={stepProgress:F2}, 動畫時間={targetTime:F2}s/{syncController.alembicPlayer.Duration:F2}s");
        }
    }

    void RecordStepEvent()
    {
        if (recordingManager != null && IsRecording() && currentStepIndex >= 0)
        {
            RecordStepEvent(currentStepIndex, steps[currentStepIndex].stepName);
            if (showDebugLogs)
                Debug.Log($"[OrigamiGuideSimple] 記錄步驟事件: {currentStepIndex}");
        }
    }

    // 供外部調用
    public void JumpToStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= steps.Count) return;
        
        StopAllCoroutines();
        currentStepIndex = stepIndex;
        StartCoroutine(StepSequence());
    }

    public void TriggerNextStep()
    {
        if (isWaitingForTrigger)
        {
            if (showDebugLogs)
                Debug.Log("[OrigamiGuideSimple] 手動觸發步驟（外部調用）");
            isWaitingForTrigger = false;
        }
    }
    
    public void ResetToStart()
    {
        // 停止所有 Coroutines
        StopAllCoroutines();
        
        // 清除所有視覺元素
        HideStepGuide();
        
        // 重置狀態
        currentStepIndex = -1;
        isWaitingForTrigger = false;
        isPlayingStep = false;
        triggeredStartPoints.Clear();
        
        // 重置 Alembic 動畫到開頭
        if (syncController != null && syncController.alembicPlayer != null)
        {
            syncController.alembicPlayer.CurrentTime = 0f;
        }
        
        // 重新開始步驟序列
        StartCoroutine(StepSequence());
        
        if (showDebugLogs)
            Debug.Log("[OrigamiGuideSimple] 已重置到第一步驟");
    }
    
    // ==================== Scene 視圖 Gizmos ====================
    
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmosInEditor || steps == null || steps.Count == 0)
            return;
        
        // 決定要顯示哪些步驟
        int startIdx = previewStepIndex >= 0 ? previewStepIndex : 0;
        int endIdx = previewStepIndex >= 0 ? previewStepIndex : steps.Count - 1;
        
        // 確保索引有效
        startIdx = Mathf.Clamp(startIdx, 0, steps.Count - 1);
        endIdx = Mathf.Clamp(endIdx, 0, steps.Count - 1);
        
        for (int i = startIdx; i <= endIdx; i++)
        {
            DrawStepGizmos(steps[i], i);
        }
    }
    
    void DrawStepGizmos(FoldingStep step, int stepIndex)
    {
        // 設定 Gizmos 矩陣為本物件的 Transform
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        // 繪製起始點（綠色球）
        Gizmos.color = startPointColor;
        foreach (var pos in step.startPoints)
        {
            Gizmos.DrawSphere(pos, sphereRadius);
            // 繪製標籤（稍微偏移以避免重疊）
            Vector3 labelPos = pos + Vector3.up * (sphereRadius + 0.01f);
            UnityEditor.Handles.matrix = transform.localToWorldMatrix;
            UnityEditor.Handles.Label(labelPos, $"起 {stepIndex + 1}", new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = startPointColor },
                fontSize = 12,
                fontStyle = FontStyle.Bold
            });
        }
        
        // 繪製終點（紅色球）
        Gizmos.color = endPointColor;
        foreach (var pos in step.endPoints)
        {
            Gizmos.DrawSphere(pos, sphereRadius);
            // 繪製標籤
            Vector3 labelPos = pos + Vector3.up * (sphereRadius + 0.01f);
            UnityEditor.Handles.matrix = transform.localToWorldMatrix;
            UnityEditor.Handles.Label(labelPos, $"終 {stepIndex + 1}", new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = endPointColor },
                fontSize = 12,
                fontStyle = FontStyle.Bold
            });
        }
        
        // 繪製摺痕線（黃色虛線）
        DrawDashedLine(step.foldLinePoint1, step.foldLinePoint2, foldLineColor);
        
        // 繪製步驟名稱（在中心位置）
        Vector3 centerPos = (step.foldLinePoint1 + step.foldLinePoint2) / 2f;
        centerPos += Vector3.up * 0.05f;
        UnityEditor.Handles.matrix = transform.localToWorldMatrix;
        UnityEditor.Handles.Label(centerPos, $"步驟 {stepIndex + 1}: {step.stepName}", new GUIStyle()
        {
            normal = new GUIStyleState() { textColor = Color.white },
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        });
        
        // 恢復原始矩陣
        Gizmos.matrix = originalMatrix;
    }
    
    void DrawDashedLine(Vector3 start, Vector3 end, Color color)
    {
        Gizmos.color = color;
        
        Vector3 direction = end - start;
        float totalLength = direction.magnitude;
        Vector3 normalizedDir = direction.normalized;
        
        float currentLength = 0f;
        bool drawDash = true;
        
        while (currentLength < totalLength)
        {
            float segmentLength = drawDash ? dashLength : dashGap;
            float remainingLength = totalLength - currentLength;
            segmentLength = Mathf.Min(segmentLength, remainingLength);
            
            if (drawDash)
            {
                Vector3 segmentStart = start + normalizedDir * currentLength;
                Vector3 segmentEnd = start + normalizedDir * (currentLength + segmentLength);
                Gizmos.DrawLine(segmentStart, segmentEnd);
            }
            
            currentLength += segmentLength;
            drawDash = !drawDash;
        }
    }
#endif
    
    // ==================== 輔助方法 ====================
    
    bool IsInPlaybackMode()
    {
        if (recordingManager == null) return false;
        
        if (recordingManager is AvatarRecordingManager)
            return ((AvatarRecordingManager)recordingManager).IsPlaying;
        
        // TeacherRecordingManager 沒有播放功能
        return false;
    }
    
    bool IsRecording()
    {
        if (recordingManager == null) return false;
        
        if (recordingManager is AvatarRecordingManager)
            return ((AvatarRecordingManager)recordingManager).IsRecording;
        else if (recordingManager is TeacherRecordingManager)
            return ((TeacherRecordingManager)recordingManager).IsRecording;
        
        return false;
    }
    
    void RecordStepEvent(int stepIndex, string stepName)
    {
        if (recordingManager == null) return;
        
        if (recordingManager is AvatarRecordingManager)
            ((AvatarRecordingManager)recordingManager).RecordOrigamiStepEvent(stepIndex, stepName);
        else if (recordingManager is TeacherRecordingManager)
            ((TeacherRecordingManager)recordingManager).RecordOrigamiStepEvent(stepIndex, stepName);
    }
    
    /// <summary>
    /// 隱藏所有摺紙指示（綠紅黃線條和球體）
    /// </summary>
    public void HideGuidelines()
    {
        // 隱藏起點球體
        foreach (var sphere in currentStartSpheres)
        {
            if (sphere != null)
                sphere.SetActive(false);
        }
        
        // 隱藏終點球體
        foreach (var sphere in currentEndSpheres)
        {
            if (sphere != null)
                sphere.SetActive(false);
        }
        
        // 隱藏摺線
        if (foldLineObject != null)
            foldLineObject.SetActive(false);
    }
    
    /// <summary>
    /// 顯示所有摺紙指示（綠紅黃線條和球體）
    /// </summary>
    public void ShowGuidelines()
    {
        // 顯示起點球體
        foreach (var sphere in currentStartSpheres)
        {
            if (sphere != null)
                sphere.SetActive(true);
        }
        
        // 顯示終點球體
        foreach (var sphere in currentEndSpheres)
        {
            if (sphere != null)
                sphere.SetActive(true);
        }
        
        // 顯示摺線（如果當前步驟需要且有正在播放的步驟）
        if (foldLineObject != null && isPlayingStep && currentStepIndex >= 0 && currentStepIndex < steps.Count)
        {
            foldLineObject.SetActive(true);
        }
    }
}
