using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

/// <summary>
/// 摺紙動畫同步控制器
/// 用於同步 Alembic 摺紙動畫與教師 Avatar 錄製/播放
/// 支援單獨使用 AvatarRecordingManager 或配合 StudentPlaybackManager
/// </summary>
public class OrigamiSyncController : MonoBehaviour
{
    [Header("Alembic 設定")]
    [Tooltip("Alembic Stream Player 組件")]
    public AlembicStreamPlayer alembicPlayer;
    
    [Header("錄製管理器")]
    [Tooltip("教師錄製管理器（用於同步錄製和播放）")]
    public MonoBehaviour recordingManager; // 可以是 TeacherRecordingManager 或 AvatarRecordingManager
    
    [Tooltip("學生播放管理器（可選，用於學生端播放）")]
    public StudentPlaybackManager playbackManager;

    [Header("同步設定")]
    [Tooltip("是否在錄製時顯示摺紙預覽（鬼影）")]
    public bool showPreviewDuringRecording = true;
    
    [Tooltip("預覽材質透明度")]
    [Range(0.1f, 1f)]
    public float previewAlpha = 0.5f;
    
    [Tooltip("摺紙動畫開始時間偏移（秒）")]
    public float timeOffset = 0f;
    
    [Header("視覺指示")]
    [Tooltip("預覽模式的材質顏色")]
    public Color previewColor = new Color(0.5f, 1f, 0.5f, 0.5f);
    
    [Tooltip("正常播放的材質顏色")]
    public Color playbackColor = Color.white;
    
    [Header("調試")]
    [Tooltip("顯示調試訊息")]
    public bool showDebugLogs = true;
    
    [Header("位置設定")]
    [Tooltip("摺紙在攝影機前方的距離（米）")]
    public float forwardDistance = 0.5f;
    
    [Tooltip("摺紙在攝影機下方的距離（米）")]
    public float downwardDistance = 0.3f;
    
    [Tooltip("摺紙的初始旋轉（Euler 角度）")]
    public Vector3 paperRotation = Vector3.zero;
    
    [Tooltip("播放開始時更新摺紙位置到相機前方")]
    public bool updatePositionOnPlayback = true;
    
    [Tooltip("禁用自動位置更新（保持場景中預設的位置）")]
    public bool disableAutoPositioning = false;

    // 私有變數
    private Material[] originalMaterials;
    private Material[] previewMaterials;
    private Renderer[] origamiRenderers;
    private bool isRecording = false;
    private bool isPlaying = false;
    private float syncTimer = 0f;
    
    // 用反射獲取錄製/播放狀態
    private System.Reflection.PropertyInfo isRecordingProperty;
    private System.Reflection.PropertyInfo recordingDurationProperty;
    private System.Reflection.PropertyInfo isPlayingProperty;

    void Start()
    {
        // 自動尋找組件
        FindComponents();
        
        // 初始化材質
        InitializeMaterials();
        
        // 設置反射 - 錄製管理器
        if (recordingManager != null)
        {
            var managerType = recordingManager.GetType();
            isRecordingProperty = managerType.GetProperty("IsRecording");
            recordingDurationProperty = managerType.GetProperty("RecordingDuration");
            
            // AvatarRecordingManager 也支援播放
            isPlayingProperty = managerType.GetProperty("IsPlaying");
        }
        
        // 初始狀態：顯示摺紙（第一幀）
        if (alembicPlayer != null)
        {
            alembicPlayer.CurrentTime = 0f;
            SetOrigamiVisibility(true);
        }
        
        // 延遲 1 秒後初始化位置（僅在未禁用自動定位時）
        if (!disableAutoPositioning)
        {
            Invoke(nameof(PositionOrigamiInFrontOfCamera), 1f);
        }
    }

    void FindComponents()
    {
        // 尋找 AlembicStreamPlayer
        if (alembicPlayer == null)
        {
            alembicPlayer = GetComponent<AlembicStreamPlayer>();
            if (alembicPlayer == null)
            {
                alembicPlayer = FindObjectOfType<AlembicStreamPlayer>();
            }
        }
        
        // 尋找錄製管理器
        if (recordingManager == null)
        {
            // 嘗試找 TeacherRecordingManager
            var teacher = FindObjectOfType<TeacherRecordingManager>();
            if (teacher != null)
            {
                recordingManager = teacher;
            }
            else
            {
                // 或 AvatarRecordingManager
                var avatar = FindObjectOfType<AvatarRecordingManager>();
                if (avatar != null)
                {
                    recordingManager = avatar;
                }
            }
        }
        
        // 尋找播放管理器（可選）
        if (playbackManager == null)
        {
            playbackManager = FindObjectOfType<StudentPlaybackManager>();
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[OrigamiSync] AlembicPlayer: {alembicPlayer != null}");
            Debug.Log($"[OrigamiSync] RecordingManager: {recordingManager != null} ({recordingManager?.GetType().Name})");
            Debug.Log($"[OrigamiSync] PlaybackManager: {playbackManager != null} (可選)");
        }
    }

    void InitializeMaterials()
    {
        if (alembicPlayer == null) return;
        
        // 獲取所有 Renderer
        origamiRenderers = alembicPlayer.GetComponentsInChildren<Renderer>();
        
        if (origamiRenderers.Length == 0)
        {
            Debug.LogWarning("[OrigamiSync] 找不到摺紙的 Renderer 組件");
            return;
        }
        
        // 保存原始材質
        originalMaterials = new Material[origamiRenderers.Length];
        previewMaterials = new Material[origamiRenderers.Length];
        
        for (int i = 0; i < origamiRenderers.Length; i++)
        {
            if (origamiRenderers[i].sharedMaterial != null)
            {
                originalMaterials[i] = origamiRenderers[i].sharedMaterial;
                
                // 創建預覽材質（半透明）
                previewMaterials[i] = new Material(originalMaterials[i]);
                
                // 設置為透明模式
                if (previewMaterials[i].HasProperty("_Mode"))
                {
                    previewMaterials[i].SetFloat("_Mode", 3); // Transparent mode
                }
                if (previewMaterials[i].HasProperty("_SrcBlend"))
                {
                    previewMaterials[i].SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                }
                if (previewMaterials[i].HasProperty("_DstBlend"))
                {
                    previewMaterials[i].SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }
                if (previewMaterials[i].HasProperty("_ZWrite"))
                {
                    previewMaterials[i].SetFloat("_ZWrite", 0);
                }
                
                previewMaterials[i].EnableKeyword("_ALPHABLEND_ON");
                previewMaterials[i].renderQueue = 3000;
                
                // 設置顏色和透明度
                if (previewMaterials[i].HasProperty("_Color"))
                {
                    Color col = previewColor;
                    col.a = previewAlpha;
                    previewMaterials[i].SetColor("_Color", col);
                }
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"[OrigamiSync] 已初始化 {origamiRenderers.Length} 個材質");
    }

    void Update()
    {
        // 檢查錄製狀態
        bool currentlyRecording = GetIsRecording();
        if (currentlyRecording != isRecording)
        {
            isRecording = currentlyRecording;
            OnRecordingStateChanged(isRecording);
        }
        
        // 檢查播放狀態（支援兩種管理器）
        bool currentlyPlaying = false;
        
        // 優先使用 StudentPlaybackManager
        if (playbackManager != null)
        {
            currentlyPlaying = playbackManager.IsPlaying;
        }
        // 如果沒有 PlaybackManager，檢查 RecordingManager 是否支援播放
        else if (recordingManager != null && isPlayingProperty != null)
        {
            currentlyPlaying = GetIsPlaying();
        }
        
        if (currentlyPlaying != isPlaying)
        {
            isPlaying = currentlyPlaying;
            OnPlaybackStateChanged(isPlaying);
        }
        
        // 注意：不再自動同步時間，由 OrigamiStepGuide 控制
    }

    /// <summary>
    /// 獲取錄製狀態
    /// </summary>
    bool GetIsRecording()
    {
        if (recordingManager == null || isRecordingProperty == null)
            return false;
        
        try
        {
            return (bool)isRecordingProperty.GetValue(recordingManager);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 獲取播放狀態（用於 AvatarRecordingManager）
    /// </summary>
    bool GetIsPlaying()
    {
        if (recordingManager == null || isPlayingProperty == null)
            return false;
        
        try
        {
            return (bool)isPlayingProperty.GetValue(recordingManager);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 獲取錄製時長
    /// </summary>
    float GetRecordingDuration()
    {
        if (recordingManager == null || recordingDurationProperty == null)
            return 0f;
        
        try
        {
            return (float)recordingDurationProperty.GetValue(recordingManager);
        }
        catch
        {
            return 0f;
        }
    }
    
    /// <summary>
    /// 獲取播放時間（支援兩種管理器）
    /// </summary>
    float GetPlaybackTime()
    {
        // 優先使用 StudentPlaybackManager
        if (playbackManager != null && playbackManager.HasRecording)
        {
            // 通過播放進度計算時間
            if (alembicPlayer != null)
            {
                return playbackManager.PlaybackProgress * alembicPlayer.Duration;
            }
        }
        
        // 使用 AvatarRecordingManager 的 RecordingDuration（播放時當作計時器）
        if (recordingManager != null && recordingDurationProperty != null)
        {
            try
            {
                return (float)recordingDurationProperty.GetValue(recordingManager);
            }
            catch { }
        }
        
        return 0f;
    }

    /// <summary>
    /// 錄製狀態改變
    /// </summary>
    void OnRecordingStateChanged(bool recording)
    {
        if (recording)
        {
            if (showDebugLogs)
                Debug.Log("[OrigamiSync] 開始錄製 - 顯示摺紙預覽");
            
            // 顯示摺紙預覽（半透明）
            if (showPreviewDuringRecording)
            {
                ApplyPreviewMaterials();
                SetOrigamiVisibility(true);
                
                // 重置時間
                syncTimer = 0f;
                
                // 重新初始化紙張位置
                PositionOrigamiInFrontOfCamera();
                
                // 啟動摺紙步驟指引的第一步（由 StepGuide 控制 Alembic 時間）
                var stepGuide = FindObjectOfType<OrigamiStepGuide>();
                if (stepGuide != null)
                {
                    stepGuide.StartFirstStep();
                }
                else
                {
                    Debug.LogWarning("[OrigamiSync] 找不到 OrigamiStepGuide，無法啟動步驟控制");
                }
            }
            else
            {
                SetOrigamiVisibility(false);
            }
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("[OrigamiSync] 停止錄製 - 顯示原始摺紙");
            
            // 保持摺紙可見，恢復原始材質
            ApplyOriginalMaterials();
            SetOrigamiVisibility(true);
            
            // 重置 Alembic 時間到起點並暫停
            if (alembicPlayer != null)
            {
                alembicPlayer.CurrentTime = alembicPlayer.StartTime;
                alembicPlayer.UpdateImmediately(alembicPlayer.StartTime);
                Debug.Log($"[OrigamiSync] 重置動畫時間到 {alembicPlayer.StartTime}");
            }
            
            // 重置 OrigamiStepGuide
            var stepGuide = FindObjectOfType<OrigamiStepGuide>();
            if (stepGuide != null)
            {
                stepGuide.ResetToStart();
            }
        }
    }

    /// <summary>
    /// 播放狀態改變
    /// </summary>
    void OnPlaybackStateChanged(bool playing)
    {
        if (playing)
        {
            if (showDebugLogs)
                Debug.Log("[OrigamiSync] 開始播放 - 顯示摺紙動畫");
            
            // 顯示摺紙（不透明）
            ApplyOriginalMaterials();
            SetOrigamiVisibility(true);
            
            // 根據選項決定是否重新定位紙張
            if (updatePositionOnPlayback)
            {
                PositionOrigamiInFrontOfCamera();
            }
            
            // 重置時間
            syncTimer = 0f;
            if (alembicPlayer != null)
            {
                alembicPlayer.CurrentTime = timeOffset;
            }
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("[OrigamiSync] 停止播放");
            
            // 保持顯示，但停止更新時間
        }
    }

    /// <summary>
    /// 與錄製同步
    /// </summary>
    void SyncWithRecording()
    {
        if (alembicPlayer == null) return;
        
        float recordingTime = GetRecordingDuration();
        float targetTime = recordingTime + timeOffset;
        
        // 更新 Alembic 時間
        alembicPlayer.CurrentTime = targetTime;
        
        syncTimer = recordingTime;
    }

    /// <summary>
    /// 與播放同步
    /// </summary>
    void SyncWithPlayback()
    {
        if (alembicPlayer == null) return;
        
        float targetTime = 0f;
        
        // 使用 StudentPlaybackManager
        if (playbackManager != null && playbackManager.HasRecording)
        {
            // 使用播放進度（0-1）來計算時間
            float progress = playbackManager.PlaybackProgress;
            float duration = alembicPlayer.Duration;
            targetTime = (progress * duration) + timeOffset;
        }
        // 使用 AvatarRecordingManager
        else if (recordingManager != null)
        {
            // 直接使用播放時間
            float playbackTime = GetPlaybackTime();
            targetTime = playbackTime + timeOffset;
        }
        
        // 更新 Alembic 時間
        alembicPlayer.CurrentTime = targetTime;
    }

    /// <summary>
    /// 應用預覽材質
    /// </summary>
    void ApplyPreviewMaterials()
    {
        if (origamiRenderers == null || previewMaterials == null) return;
        
        for (int i = 0; i < origamiRenderers.Length; i++)
        {
            if (origamiRenderers[i] != null && previewMaterials[i] != null)
            {
                origamiRenderers[i].material = previewMaterials[i];
            }
        }
    }

    /// <summary>
    /// 應用原始材質
    /// </summary>
    void ApplyOriginalMaterials()
    {
        if (origamiRenderers == null || originalMaterials == null) return;
        
        for (int i = 0; i < origamiRenderers.Length; i++)
        {
            if (origamiRenderers[i] != null && originalMaterials[i] != null)
            {
                origamiRenderers[i].material = originalMaterials[i];
            }
        }
    }

    /// <summary>
    /// 設置摺紙可見性
    /// </summary>
    void SetOrigamiVisibility(bool visible)
    {
        if (origamiRenderers == null) return;
        
        foreach (var renderer in origamiRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"[OrigamiSync] 摺紙可見性: {visible}");
    }
    
    /// <summary>
    /// 將摺紙放到攝影機前方下面
    /// </summary>
    void PositionOrigamiInFrontOfCamera()
    {
        // 如果禁用自動定位，則不更新位置
        if (disableAutoPositioning)
        {
            if (showDebugLogs)
                Debug.Log("[OrigamiSync] 自動定位已禁用，保持當前位置");
            return;
        }
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[OrigamiSync] 找不到主攝影機");
            return;
        }
        
        // 計算攝影機前方下方的位置
        Vector3 targetPosition = mainCamera.transform.position + 
                                mainCamera.transform.forward * forwardDistance + 
                                mainCamera.transform.TransformDirection(Vector3.down) * downwardDistance;
        
        transform.position = targetPosition;
        
        // 設置紙張旋轉（使用自定義旋轉）
        transform.rotation = Quaternion.Euler(paperRotation);
        
        if (showDebugLogs)
            Debug.Log($"[OrigamiSync] 摺紙已移到攝影機前方: {targetPosition}");
    }

    /// <summary>
    /// 手動設置時間（用於測試）
    /// </summary>
    public void SetTime(float time)
    {
        if (alembicPlayer != null)
        {
            alembicPlayer.CurrentTime = time + timeOffset;
        }
    }

    /// <summary>
    /// 重置摺紙動畫
    /// </summary>
    public void ResetAnimation()
    {
        if (alembicPlayer != null)
        {
            alembicPlayer.CurrentTime = timeOffset;
        }
        syncTimer = 0f;
    }

    void OnGUI()
    {
        if (!showDebugLogs) return;
        
        // 顯示調試信息
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(10, 10, 5, 5);
        
        float width = 320f;
        float height = 140f;
        float xPos = 20f;
        float yPos = Screen.height - height - 20f;
        
        GUI.Box(new Rect(xPos, yPos, width, height), "", style);
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 12;
        labelStyle.normal.textColor = Color.white;
        
        float yOffset = yPos + 10f;
        
        GUI.Label(new Rect(xPos + 10f, yOffset, width, 20f),
            "摺紙同步控制器", labelStyle);
        yOffset += 25f;
        
        string status = isRecording ? "🔴 錄製中（預覽）" : isPlaying ? "▶ 播放中" : "⏸ 待機";
        GUI.Label(new Rect(xPos + 10f, yOffset, width, 20f),
            $"狀態: {status}", labelStyle);
        yOffset += 20f;
        
        string mode = playbackManager != null ? "學生端模式" : "教師端模式";
        GUI.Label(new Rect(xPos + 10f, yOffset, width, 20f),
            $"模式: {mode}", labelStyle);
        yOffset += 20f;
        
        if (alembicPlayer != null)
        {
            GUI.Label(new Rect(xPos + 10f, yOffset, width, 20f),
                $"動畫: {alembicPlayer.CurrentTime:F2}s / {alembicPlayer.Duration:F2}s", labelStyle);
            yOffset += 20f;
            
            GUI.Label(new Rect(xPos + 10f, yOffset, width, 20f),
                $"同步計時: {syncTimer:F2}s", labelStyle);
        }
    }

    void OnDestroy()
    {
        // 清理預覽材質
        if (previewMaterials != null)
        {
            foreach (var mat in previewMaterials)
            {
                if (mat != null)
                {
                    Destroy(mat);
                }
            }
        }
    }
}
