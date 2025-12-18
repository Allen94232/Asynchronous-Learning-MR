using UnityEngine;
using Oculus.Avatar2;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using CAPI = Oculus.Avatar2.CAPI;

/// <summary>
/// 學生播放管理器
/// 用於學生端場景，負責載入和播放教師錄製的課程
/// </summary>
public class StudentPlaybackManager : MonoBehaviour
{
    [Header("Avatar 設定")]
    [Tooltip("教師 Avatar（播放錄製動作）")]
    public OvrAvatarEntity teacherAvatar;
    
    [Tooltip("播放 AudioSource（教師聲音）")]
    public AudioSource teacherAudioSource;

    [Header("播放設定")]
    [Tooltip("自動載入最新課程")]
    public bool autoLoadLatest = false;
    
    [Tooltip("目標錄製檔名（空白 = 自動載入最新檔案）")]
    public string targetRecordingName = "";
    
    [Tooltip("播放速度倍率")]
    [Range(0.5f, 2f)]
    public float playbackSpeed = 1f;
    
    [Tooltip("使用音頻時間同步動作（修正長時間錄製的漂移）")]
    public bool useAudioSync = true;

    [Header("存檔設定")]
    [Tooltip("錄製檔案路徑")]
    public string recordingsFolderPath = "Assets/Recordings";

    [Header("UI 設定")]
    [Tooltip("顯示調試訊息")]
    public bool showDebugLogs = true;
    
    [Tooltip("在螢幕顯示播放狀態")]
    public bool showPlaybackUI = true;
    
    [Header("步驟分組設定")]
    [Tooltip("啟用步驟分組播放（數字鍵播放分組而非單個步驟）")]
    public bool useStepGroups = false;
    
    [Tooltip("步驟分組定義（按 1-9 對應分組 1-9）")]
    public List<StepGroup> stepGroups = new List<StepGroup>();
    
    [Header("Avatar 播放位置設定")]
    [Tooltip("啟用指定 TeacherAvatar 位置")]
    public bool useCustomAvatarPosition = false;
    
    [Tooltip("TeacherAvatar 相對於相機的偏移\nZ=前後(正=前), X=左右(正=右), Y=上下(正=下)")]
    public Vector3 teacherAvatarOffset = new Vector3(0, 0, 1);
    
    [Tooltip("讓 TeacherAvatar 面向學生（Camera）")]
    public bool faceStudent = true;
    
    [Tooltip("翻轉鏡像（讓左右手正確對應）")]
    public bool flipMirror = true;
    
    [Tooltip("播放時隱藏摺紙指示（綠紅黃線條）")]
    public bool hideOrigamiGuideInPlayback = true;
    
    [Tooltip("禁用 Origami 位置同步（保持場景中的相對位置）")]
    public bool disableOrigamiPositionSync = true;
    
    [Header("Joystick 控制設定")]
    [Tooltip("啟用 Joystick 手動控制 Avatar 位置和旋轉")]
    public bool enableJoystickControl = true;
    
    [Tooltip("左手控制器（用於控制位置）")]
    public OVRInput.Controller leftController = OVRInput.Controller.LTouch;
    
    [Tooltip("右手控制器（用於控制旋轉）")]
    public OVRInput.Controller rightController = OVRInput.Controller.RTouch;
    
    [Tooltip("位置移動速度（米/秒）")]
    public float positionMoveSpeed = 0.5f;
    
    [Tooltip("旋轉速度（度/秒）")]
    public float rotationSpeed = 60f;
    
    [Tooltip("學生相機（用於計算朝向）")]
    public Camera studentCamera;
    
    [Header("學生 Avatar 可見性設定")]
    [Tooltip("隱藏學生自己的 Avatar（MR 場景只看自己的手）")]
    public bool hideLocalAvatar = true;
    
    [Tooltip("等待 Avatar 初始化後再隱藏（秒）")]
    public float hideLocalAvatarDelay = 3f;

    [Header("形狀偵測設定")]
    [Tooltip("形狀偵測器（用於驗證步驟）")]
    public ShapeDetector shapeDetector;
    
    [Tooltip("驗證時使用的相機（用於截取摺紙畫面）")]
    public Camera verificationCamera;
    
    [Header("UI 按鈕")]
    [Tooltip("讀取檔案按鈕")]
    public GameObject loadButton;
    
    [Tooltip("播放按鈕")]
    public GameObject playButton;
    
    [Tooltip("暫停按鈕")]
    public GameObject pauseButton;
    
    [Tooltip("繼續播放按鈕")]
    public GameObject resumeButton;
    
    [Tooltip("離開播放按鈕")]
    public GameObject exitButton;
    
    [Tooltip("驗證步驟按鈕")]
    public GameObject verifyButton;
    
    [Tooltip("上一步驟按鈕")]
    public GameObject previousButton;
    
    [Tooltip("重播按鈕")]
    public GameObject replayButton;
    
    [Tooltip("下一步驟按鈕")]
    public GameObject nextButton;

    // === 私有變數 ===
    private OvrAvatarEntity localAvatar; // 學生的本地 Avatar
    private AvatarRecordingData currentRecording;
    private bool isPlaying = false;
    private int playbackFrameIndex = 0;
    private float playbackTimer = 0f;
    private MonoBehaviour loopbackManager;
    
    // 單步驟播放相關
    private bool isPlayingSingleStep = false;
    private int singleStepIndex = -1;
    private float singleStepEndTime = -1f;
    private int currentPlayingGroupIndex = -1; // 當前播放的分組索引

    // === 錄製數據結構（使用共享類別）===

    void Start()
    {
        // 自動尋找組件
        FindComponents();
        
        // 立即禁用 OrigamiSyncController 防止它在啟動時移動位置
        if (disableOrigamiPositionSync)
        {
            var syncController = FindObjectOfType<OrigamiSyncController>();
            if (syncController != null)
            {
                syncController.enabled = false;
                if (showDebugLogs)
                    Debug.Log("[StudentPlayback] Start: 已禁用 OrigamiSyncController");
            }
        }
        
        // 自動載入最新課程
        if (autoLoadLatest)
        {
            string[] recordings = ListAvailableRecordings();
            if (recordings.Length > 0)
            {
                LoadRecording(recordings[recordings.Length - 1]);
            }
        }
        
        // 延遲停用 NetworkLoopbackManager，等 Avatar 初始化完成
        if (loopbackManager != null)
        {
            StartCoroutine(DisableLoopbackAfterInit());
        }
        
        // 延遲隱藏學生的 LocalAvatar（MR 場景只看自己的手）
        if (hideLocalAvatar)
        {
            StartCoroutine(HideLocalAvatarAfterInit());
        }
    }
    
    /// <summary>
    /// 延遲停用 loopback 以等待 Avatar 初始化
    /// </summary>
    System.Collections.IEnumerator DisableLoopbackAfterInit()
    {
        // 等待 3 秒讓 Avatar 充分初始化
        yield return new WaitForSeconds(3f);
        
        // 確認 Avatar 已創建
        if (teacherAvatar != null && teacherAvatar.IsCreated)
        {
            loopbackManager.enabled = false;
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] Avatar 初始化完成，已停用 loopback");
        }
        else
        {
            // 如果還未初始化，再等 2 秒
            yield return new WaitForSeconds(2f);
            if (loopbackManager != null)
            {
                loopbackManager.enabled = false;
                if (showDebugLogs)
                    Debug.Log("[StudentPlayback] 強制停用 loopback（延遲 5 秒）");
            }
        }
    }
    
    /// <summary>
    /// 延遲隱藏 LocalAvatar（等待 Meta Avatar 系統初始化完成）
    /// 使用 OvrAvatarEntity.Hidden 屬性，不會破壞 Avatar 系統運作
    /// </summary>
    System.Collections.IEnumerator HideLocalAvatarAfterInit()
    {
        // 等待指定時間讓 Avatar 充分初始化
        yield return new WaitForSeconds(hideLocalAvatarDelay);
        
        // 尋找 LocalAvatar
        if (localAvatar == null)
        {
            localAvatar = GameObject.Find("LocalAvatar")?.GetComponent<OvrAvatarEntity>();
        }
        
        if (localAvatar != null && localAvatar.IsCreated)
        {
            // 使用 Meta Avatar SDK 的內建 Hidden 屬性
            // 這會呼叫 SetActiveView(None)，正確隱藏 Avatar 而不影響系統運作
            localAvatar.Hidden = true;
            
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] ✓ LocalAvatar 已隱藏（使用 OvrAvatarEntity.Hidden）");
        }
        else if (localAvatar != null)
        {
            // 如果 Avatar 尚未創建，再等一下
            yield return new WaitForSeconds(2f);
            
            if (localAvatar.IsCreated)
            {
                localAvatar.Hidden = true;
                if (showDebugLogs)
                    Debug.Log("[StudentPlayback] ✓ LocalAvatar 已隱藏（延遲後）");
            }
            else
            {
                if (showDebugLogs)
                    Debug.LogWarning("[StudentPlayback] ⚠ LocalAvatar 初始化失敗，無法隱藏");
            }
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning("[StudentPlayback] ⚠ 找不到 LocalAvatar");
        }
    }


    void FindComponents()
    {
        if (teacherAvatar == null)
        {
            // 在學生場景中，TeacherAvatar 可能叫 RemoteLoopbackAvatar 或 TeacherAvatar
            teacherAvatar = GameObject.Find("TeacherAvatar")?.GetComponent<OvrAvatarEntity>();
            if (teacherAvatar == null)
                teacherAvatar = GameObject.Find("RemoteLoopbackAvatar")?.GetComponent<OvrAvatarEntity>();
        }
        
        if (teacherAudioSource == null && teacherAvatar != null)
        {
            teacherAudioSource = teacherAvatar.GetComponent<AudioSource>();
            if (teacherAudioSource == null)
            {
                teacherAudioSource = teacherAvatar.gameObject.AddComponent<AudioSource>();
                teacherAudioSource.spatialBlend = 1.0f; // 3D 音效
            }
        }
        
        // 尋找 NetworkLoopbackManager（用於控制即時同步）
        var loopbackObj = GameObject.Find("NetworkLoopbackManager");
        if (loopbackObj != null)
        {
            loopbackManager = loopbackObj.GetComponent<MonoBehaviour>();
        }
    }

    void Update()
    {
        // 處理鍵盤快捷鍵
        HandleKeyboardInput();
        
        if (isPlaying)
        {
            PlaybackFrame();
        }
        
        // Joystick 手動控制 Avatar 位置和旋轉
        if (enableJoystickControl && teacherAvatar != null)
        {
            HandleJoystickControl();
        }
    }
    
    void HandleKeyboardInput()
    {
        // L 鍵：載入最新課程
        if (Input.GetKeyDown(KeyCode.L) && !isPlaying)
        {
            LoadLatestRecording();
        }

        // P 鍵：開始/暫停/繼續播放
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (isPlaying)
            {
                // 暫停播放
                StopPlayback();
            }
            else
            {
                // 先清空之前的播放狀態（相當於按 C）
                CancelPlayback();
                
                // 然後載入錄製（相當於按 L）
                if (showDebugLogs)
                    Debug.Log("[StudentPlayback] 自動載入錄製...");
                LoadLatestRecording();
                
                // 確認載入成功後才播放
                if (currentRecording != null && currentRecording.frames.Count > 0)
                {
                    StartPlayback();
                }
            }
        }
        
        // C 鍵：取消播放並清空狀態（即使步驟播放完畢也允許取消）
        if (Input.GetKeyDown(KeyCode.C))
        {
            // 檢查是否有需要清理的播放狀態
            if (isPlaying || isPlayingSingleStep || (teacherAvatar != null && !teacherAvatar.IsLocal))
            {
                CancelPlayback();
            }
        }
        
        // 數字鍵 1-9：播放指定步驟或步驟組
        if (currentRecording != null && currentRecording.origamiStepEvents.Count > 0)
        {
            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                {
                    if (useStepGroups && stepGroups.Count >= i)
                    {
                        // 播放步驟組
                        PlayStepGroup(i - 1); // 分組索引從 0 開始
                    }
                    else
                    {
                        // 播放單個步驟
                        PlaySingleStep(i - 1); // 步驟索引從 0 開始
                    }
                    break;
                }
            }
        }
    }

    // ==================== 播放功能 ====================
    
    public void StartPlayback()
    {
        // **修正：與 AvatarRecordingManager 一致，不要在這裡呼叫 CancelPlayback**
        // CancelPlayback 會破壞 Avatar 狀態，應該由用戶明確按 C 鍵呼叫
        
        if (currentRecording == null || currentRecording.frames.Count == 0)
        {
            Debug.LogError("[StudentPlayback] 沒有可播放的課程");
            return;
        }
        
        if (teacherAvatar == null || !teacherAvatar.IsCreated)
        {
            Debug.LogError("[StudentPlayback] TeacherAvatar 未準備好");
            return;
        }
        
        // **關鍵修正：先應用一幀數據初始化 Avatar，再停用同步**
        if (currentRecording.frames.Count > 0 && currentRecording.frames[0].avatarStreamData != null)
        {
            // 先設置為遠端模式
            teacherAvatar.SetIsLocal(false);
            
            // 立即應用第一幀數據，初始化 Avatar LOD 和渲染狀態
            NativeArray<byte> initData = new NativeArray<byte>(currentRecording.frames[0].avatarStreamData, Allocator.Temp);
            try
            {
                teacherAvatar.ApplyStreamData(initData);
                if (showDebugLogs)
                    Debug.Log("[StudentPlayback] ✓ TeacherAvatar 初始化完成");
            }
            finally
            {
                initData.Dispose();
            }
        }
        
        // 現在可以安全地停用即時同步
        if (loopbackManager != null)
        {
            loopbackManager.enabled = false;
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] 已停止即時同步");
        }
        
        // 設置播放音頻
        if (teacherAudioSource != null && currentRecording.audioSamples.Count > 0)
        {
            // 停止之前的音頻（如果有）
            if (teacherAudioSource.isPlaying)
            {
                teacherAudioSource.Stop();
            }
            
            // 重新創建 AudioClip 確保從頭開始播放
            int sampleCount = currentRecording.audioSamples.Count / currentRecording.audioChannels;
            AudioClip audioClip = AudioClip.Create(
                "TeacherVoice",
                sampleCount,
                currentRecording.audioChannels,
                currentRecording.audioSampleRate,
                false
            );
            audioClip.SetData(currentRecording.audioSamples.ToArray(), 0);
            teacherAudioSource.clip = audioClip;
            teacherAudioSource.time = 0f;  // 確保從頭開始
            teacherAudioSource.Play();
            
            if (showDebugLogs)
                Debug.Log($"[StudentPlayback] ✓ 音頻已設置: {sampleCount} 樣本, {currentRecording.audioChannels} 聲道, {currentRecording.audioSampleRate} Hz");
        }
        
        isPlaying = true;
        playbackFrameIndex = 0;
        
        // **關鍵修正：將第一幀的 timestamp 作為起點（歸零）**
        playbackTimer = currentRecording.frames[0].timestamp;
        
        // **同步音頻播放位置與第一幀時間戳**
        if (teacherAudioSource != null && teacherAudioSource.clip != null)
        {
            teacherAudioSource.time = currentRecording.frames[0].timestamp;
            if (showDebugLogs)
                Debug.Log($"[StudentPlayback] 音頻播放位置設定為: {teacherAudioSource.time:F3}s");
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[StudentPlayback] ✓ 開始播放課程: {currentRecording.recordingName}");
            Debug.Log($"[StudentPlayback] 總幀數: {currentRecording.frames.Count}, 時長: {currentRecording.duration:F2}s");
            Debug.Log($"[StudentPlayback] playbackTimer 起點: {playbackTimer:F3}s");
        }
    }
    
    public void StopPlayback()
    {
        if (!isPlaying)
            return;
        
        isPlaying = false;
        
        // 暫停音頻播放（保留位置和 clip）
        if (teacherAudioSource != null && teacherAudioSource.isPlaying)
        {
            teacherAudioSource.Pause();
        }
        
        // 保持 Avatar 在遠端模式，這樣下次播放時可以繼續接收數據
        // 不要在這裡恢復本地模式，否則下次播放會卡住
        
        if (showDebugLogs)
            Debug.Log($"[StudentPlayback] ⏸ 暫停播放 (幀: {playbackFrameIndex}/{currentRecording.frames.Count})");
    }
    
    /// <summary>
    /// 完全停止播放並恢復即時同步
    /// </summary>
    void CompletelyStopPlayback()
    {
        isPlaying = false;
        
        // **修正：添加單步驟播放標記重置（與 AvatarRecordingManager 一致）**
        isPlayingSingleStep = false;
        singleStepIndex = -1;
        singleStepEndTime = -1f;
        
        // 停止音頻播放
        if (teacherAudioSource != null)
        {
            teacherAudioSource.Stop();
            teacherAudioSource.clip = null;
        }
        
        // **重要**：先停用 loopbackManager
        if (loopbackManager != null)
        {
            loopbackManager.enabled = false;
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] 已停用 loopbackManager");
        }
        
        // **重置 Avatar 播放狀態**：清除動作緩衝區
        if (teacherAvatar != null && teacherAvatar.IsCreated && !teacherAvatar.IsLocal)
        {
            teacherAvatar.SetIsLocal(true);
            teacherAvatar.SetIsLocal(false);
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] 已重置 Avatar 播放狀態");
        }
        
        if (showDebugLogs)
            Debug.Log("[StudentPlayback] 完全停止播放");
    }
    
    public void RestartPlayback()
    {
        // 先停止當前播放
        if (isPlaying)
        {
            isPlaying = false;
            if (teacherAudioSource != null)
            {
                teacherAudioSource.Stop();
            }
        }
        
        // 重置播放狀態
        playbackFrameIndex = 0;
        playbackTimer = currentRecording.frames[0].timestamp;
        
        // 重新開始播放
        StartPlayback();
        
        if (showDebugLogs)
            Debug.Log("[StudentPlayback] 🔄 重新播放課程");
    }
    
    /// <summary>
    /// 播放指定的單個步驟
    /// </summary>
    public void PlaySingleStep(int stepIndex)
    {
        // 立即禁用 OrigamiSyncController 防止它在初始化時移動位置
        if (disableOrigamiPositionSync)
        {
            var syncController = FindObjectOfType<OrigamiSyncController>();
            if (syncController != null)
            {
                syncController.enabled = false;
                if (showDebugLogs)
                    Debug.Log("[StudentPlayback] 提前禁用 OrigamiSyncController");
            }
        }
        
        // 先清空之前的播放狀態（相當於按 C）
        CancelPlayback();
        
        // 然後載入錄製（相當於按 L）
        if (showDebugLogs)
            Debug.Log("[StudentPlayback] 自動載入錄製...");
        LoadLatestRecording();
        
        // 確認有可播放的數據
        if (currentRecording == null || currentRecording.origamiStepEvents.Count == 0)
        {
            Debug.LogError("[StudentPlayback] 沒有可播放的錄製數據或步驟事件");
            return;
        }
        
        if (stepIndex < 0 || stepIndex >= currentRecording.origamiStepEvents.Count)
        {
            Debug.LogError($"[StudentPlayback] 步驟索引超出範圍: {stepIndex} (共 {currentRecording.origamiStepEvents.Count} 個步驟)");
            return;
        }
        
        // 獲取 OrigamiStepGuideSimple 來取得步驟的 duration（可選）
        var stepGuide = FindObjectOfType<OrigamiStepGuideSimple>();
        float stepDuration = 10f; // 預設持續時間
        
        if (stepGuide != null && stepGuide.steps.Count > stepIndex)
        {
            stepDuration = stepGuide.steps[stepIndex].duration;
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning($"[StudentPlayback] 找不到 OrigamiStepGuideSimple，使用預設持續時間 {stepDuration}s");
        }
        
        // 計算步驟開始時間
        float stepStartTime;
        if (stepIndex == 0)
        {
            // 第一個步驟從 0 秒開始
            stepStartTime = 0f;
        }
        else
        {
            // 其他步驟從前一個步驟結束時開始
            // 前一個步驟結束時間 = 前一個步驟開始時間 + 前一個步驟持續時間
            float prevStepStartTime = currentRecording.origamiStepEvents[stepIndex - 1].timestamp;
            float prevStepDuration = (stepGuide != null && stepGuide.steps.Count > stepIndex - 1) 
                ? stepGuide.steps[stepIndex - 1].duration 
                : stepDuration; // 使用相同的預設持續時間
            stepStartTime = prevStepStartTime + prevStepDuration;
        }
        
        // 步驟結束時間
        float stepEndTime;
        if (stepIndex + 1 >= currentRecording.origamiStepEvents.Count)
        {
            // 播到整個錄製結束
            stepEndTime = currentRecording.duration;
        }
        else
        {
            // 結束時間 = 該步驟開始時間 + 步驟持續時間
            stepEndTime = currentRecording.origamiStepEvents[stepIndex].timestamp + stepDuration;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[StudentPlayback] 播放步驟 {stepIndex + 1}: {stepStartTime:F2}s - {stepEndTime:F2}s (持續 {stepDuration:F2}s)");
        }
        
        // 設置單步驟播放標記
        isPlayingSingleStep = true;
        singleStepIndex = stepIndex;
        singleStepEndTime = stepEndTime;
        
        // 初始化播放環境（無論之前是否播放）
        if (teacherAvatar == null || !teacherAvatar.IsCreated)
        {
            Debug.LogError($"[StudentPlayback] TeacherAvatar 未準備好 - teacherAvatar: {(teacherAvatar == null ? "null" : "exists")}, IsCreated: {(teacherAvatar != null ? teacherAvatar.IsCreated.ToString() : "N/A")}");
            return;
        }
        
        // **重要**：先停用 loopbackManager，再設置 Avatar 模式
        if (loopbackManager != null)
        {
            loopbackManager.enabled = false;
        }
        
        // 確保 Avatar 在遠端模式（只在第一次播放時切換）
        if (teacherAvatar.IsLocal)
        {
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] Avatar 當前是本地模式，切換為遠端模式...");
            teacherAvatar.SetIsLocal(false);
        }
        
        // 設置播放狀態（必須在 JumpToTime 之前）
        isPlaying = true;
        
        if (showDebugLogs)
            Debug.Log($"[StudentPlayback] Avatar 狀態: IsLocal={teacherAvatar.IsLocal}, IsCreated={teacherAvatar.IsCreated}");
        
        // 設置播放音頻
        if (teacherAudioSource != null && currentRecording.audioSamples.Count > 0)
        {
            if (teacherAudioSource.clip == null)
            {
                int sampleCount = currentRecording.audioSamples.Count / currentRecording.audioChannels;
                AudioClip audioClip = AudioClip.Create(
                    "TeacherVoice",
                    sampleCount,
                    currentRecording.audioChannels,
                    currentRecording.audioSampleRate,
                    false
                );
                audioClip.SetData(currentRecording.audioSamples.ToArray(), 0);
                teacherAudioSource.clip = audioClip;
            }
        }
        
        // 跳轉到步驟開始時間
        JumpToTime(stepStartTime);
        
        // 強制應用起始幀的 Avatar 數據以確保 Avatar 不會卡在之前的姿勢
        int startFrameIndex = FindFrameByTime(stepStartTime);
        if (startFrameIndex >= 0 && startFrameIndex < currentRecording.frames.Count)
        {
            AvatarFrameData startFrame = currentRecording.frames[startFrameIndex];
            if (startFrame.avatarStreamData != null && startFrame.avatarStreamData.Length > 0)
            {
                ApplyAvatarStream(startFrame.avatarStreamData);
                if (showDebugLogs)
                    Debug.Log($"[StudentPlayback] 強制應用起始幀 {startFrameIndex} 的 Avatar 數據");
            }
        }
        
        // 確保音頻正在播放
        if (teacherAudioSource != null && teacherAudioSource.clip != null)
        {
            if (!teacherAudioSource.isPlaying)
            {
                teacherAudioSource.time = stepStartTime;
                teacherAudioSource.Play();
            }
        }
        
        // 設定 TeacherAvatar 位置和朝向
        SetTeacherAvatarPositionAndRotation();
        
        // 處理摺紙指示的顯示/隱藏
        if (hideOrigamiGuideInPlayback && stepGuide != null)
        {
            stepGuide.HideGuidelines();
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] 已隱藏摺紙指示");
        }
    }
    
    /// <summary>
    /// 取消播放並清空狀態（初始化動畫）
    /// </summary>
    public void CancelPlayback()
    {
        // 即使 isPlaying 為 false 也允許取消，因為可能剛播放完步驟停在那裡
        if (!isPlaying && showDebugLogs)
        {
            Debug.Log("[StudentPlayback] isPlaying=false，但仍執行清理以恢復即時同步");
        }
        
        // 停止播放
        isPlaying = false;
        
        // 停止音頻
        if (teacherAudioSource != null)
        {
            teacherAudioSource.Stop();
            teacherAudioSource.clip = null;
        }
        
        // **重要**：先停用 loopbackManager，確保它不會在 Avatar 狀態切換時發送數據
        if (loopbackManager != null)
        {
            loopbackManager.enabled = false;
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] 已停用 loopbackManager");
        }
        
        // **重置 Avatar 播放狀態**：通過 SetIsLocal(true) → SetIsLocal(false) 清除播放緩衝區
        // 這會調用 PlaybackStop 然後 PlaybackStart，清除舊的動作數據
        if (teacherAvatar != null && teacherAvatar.IsCreated)
        {
            if (!teacherAvatar.IsLocal)
            {
                // 先切到本地模式（停止播放，清除緩衝區）
                teacherAvatar.SetIsLocal(true);
                // 再切回遠端模式（重新開始播放）
                teacherAvatar.SetIsLocal(false);
                if (showDebugLogs)
                    Debug.Log("[StudentPlayback] 已重置 Avatar 播放狀態（清除動作緩衝區）");
            }
        }
        
        // 不重置紙張動畫，保持在當前位置
        // 用戶希望回播時紙張不跳回原點
        
        // 重置內部狀態
        playbackFrameIndex = 0;
        playbackTimer = 0f;
        lastSyncedStep = -1;
        
        // 重置單步驟播放標記
        isPlayingSingleStep = false;
        singleStepIndex = -1;
        singleStepEndTime = -1f;
        
        // 不在這裡恢復摺紙指示（由 UI_ExitPlayback 負責）
        // 避免在切換步驟時誤顯示指示線
        
        if (showDebugLogs)
            Debug.Log("[StudentPlayback] ✓ 已取消播放並重置所有狀態");
    }
    
    /// <summary>
    /// 播放步驟組（連續播放多個步驟）
    /// </summary>
    public void PlayStepGroup(int groupIndex)
    {
        // 立即禁用 OrigamiSyncController 防止它在初始化時移動位置
        if (disableOrigamiPositionSync)
        {
            var syncController = FindObjectOfType<OrigamiSyncController>();
            if (syncController != null)
            {
                syncController.enabled = false;
                if (showDebugLogs)
                    Debug.Log("[StudentPlayback] 提前禁用 OrigamiSyncController");
            }
        }
        
        // 先清空之前的播放狀態（相當於按 C）
        CancelPlayback();
        
        // 然後載入錄製（相當於按 L）
        if (showDebugLogs)
            Debug.Log("[StudentPlayback] 自動載入錄製...");
        LoadLatestRecording();
        
        // 確認有可播放的數據
        if (currentRecording == null || currentRecording.origamiStepEvents.Count == 0)
        {
            Debug.LogError("[StudentPlayback] 沒有可播放的錄製數據或步驟事件");
            return;
        }
        
        if (groupIndex < 0 || groupIndex >= stepGroups.Count)
        {
            Debug.LogError($"[StudentPlayback] 分組索引超出範圍: {groupIndex} (共 {stepGroups.Count} 個分組)");
            return;
        }
        
        StepGroup group = stepGroups[groupIndex];
        
        if (group.stepIndices == null || group.stepIndices.Count == 0)
        {
            Debug.LogError($"[StudentPlayback] 分組 '{group.groupName}' 沒有包含任何步驟");
            return;
        }
        
        // 驗證所有步驟索引
        foreach (int stepIdx in group.stepIndices)
        {
            if (stepIdx < 0 || stepIdx >= currentRecording.origamiStepEvents.Count)
            {
                Debug.LogError($"[StudentPlayback] 分組 '{group.groupName}' 包含無效步驟索引: {stepIdx}");
                return;
            }
        }
        
        // 獲取 OrigamiStepGuideSimple（可選）
        var stepGuide = FindObjectOfType<OrigamiStepGuideSimple>();
        if (stepGuide == null && showDebugLogs)
        {
            Debug.LogWarning("[StudentPlayback] 找不到 OrigamiStepGuideSimple，將使用錄製數據中的時間戳計算分組範圍");
        }
        
        // 計算分組的開始和結束時間
        int firstStepIdx = group.stepIndices[0];
        int lastStepIdx = group.stepIndices[group.stepIndices.Count - 1];
        
        // 第一個步驟的開始時間
        float groupStartTime;
        if (firstStepIdx == 0)
        {
            groupStartTime = 0f;
        }
        else
        {
            float prevStepStartTime = currentRecording.origamiStepEvents[firstStepIdx - 1].timestamp;
            float prevStepDuration = (stepGuide != null && stepGuide.steps.Count > firstStepIdx - 1) 
                ? stepGuide.steps[firstStepIdx - 1].duration 
                : 10f; // 預設持續時間
            groupStartTime = prevStepStartTime + prevStepDuration;
        }
        
        // 最後一個步驟的結束時間
        float groupEndTime;
        if (lastStepIdx + 1 >= currentRecording.origamiStepEvents.Count)
        {
            groupEndTime = currentRecording.duration;
        }
        else
        {
            float lastStepStartTime = currentRecording.origamiStepEvents[lastStepIdx].timestamp;
            float lastStepDuration = (stepGuide != null && stepGuide.steps.Count > lastStepIdx) 
                ? stepGuide.steps[lastStepIdx].duration 
                : 10f; // 預設持續時間
            groupEndTime = lastStepStartTime + lastStepDuration;
        }
        
        if (showDebugLogs)
        {
            string stepList = string.Join(", ", group.stepIndices.ConvertAll(x => (x + 1).ToString()));
            Debug.Log($"[StudentPlayback] 播放分組 '{group.groupName}' (步驟 {stepList}): {groupStartTime:F2}s - {groupEndTime:F2}s");
        }
        
        // 設置單步驟播放標記（實際上是分組播放）
        isPlayingSingleStep = true;
        singleStepIndex = firstStepIdx; // 記錄第一個步驟索引
        singleStepEndTime = groupEndTime;
        currentPlayingGroupIndex = groupIndex; // 記錄當前分組
        
        // 初始化播放環境
        if (teacherAvatar == null || !teacherAvatar.IsCreated)
        {
            Debug.LogError($"[StudentPlayback] TeacherAvatar 未準備好 - teacherAvatar: {(teacherAvatar == null ? "null" : "exists")}, IsCreated: {(teacherAvatar != null ? teacherAvatar.IsCreated.ToString() : "N/A")}");
            return;
        }
        
        // **重要**：先停用 loopbackManager，再設置 Avatar 模式
        if (loopbackManager != null)
        {
            loopbackManager.enabled = false;
        }
        
        // 確保 Avatar 在遠端模式（只在第一次播放時切換）
        if (teacherAvatar.IsLocal)
        {
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] Avatar 當前是本地模式，切換為遠端模式...");
            teacherAvatar.SetIsLocal(false);
        }
        
        // 設置播放狀態（必須在 JumpToTime 之前）
        isPlaying = true;
        
        if (showDebugLogs)
            Debug.Log($"[StudentPlayback] Avatar 狀態: IsLocal={teacherAvatar.IsLocal}, IsCreated={teacherAvatar.IsCreated}");
        
        // 設置播放音頻
        if (teacherAudioSource != null && currentRecording.audioSamples.Count > 0)
        {
            if (teacherAudioSource.clip == null)
            {
                int sampleCount = currentRecording.audioSamples.Count / currentRecording.audioChannels;
                AudioClip audioClip = AudioClip.Create(
                    "TeacherVoice",
                    sampleCount,
                    currentRecording.audioChannels,
                    currentRecording.audioSampleRate,
                    false
                );
                audioClip.SetData(currentRecording.audioSamples.ToArray(), 0);
                teacherAudioSource.clip = audioClip;
            }
        }
        
        // 跳轉到分組開始時間
        JumpToTime(groupStartTime);
        
        // 強制應用起始幀的 Avatar 數據
        int startFrameIndex = FindFrameByTime(groupStartTime);
        if (startFrameIndex >= 0 && startFrameIndex < currentRecording.frames.Count)
        {
            AvatarFrameData startFrame = currentRecording.frames[startFrameIndex];
            if (startFrame.avatarStreamData != null && startFrame.avatarStreamData.Length > 0)
            {
                ApplyAvatarStream(startFrame.avatarStreamData);
                if (showDebugLogs)
                    Debug.Log($"[StudentPlayback] 強制應用起始幀 {startFrameIndex} 的 Avatar 數據");
            }
        }
        
        // 確保音頻正在播放
        if (teacherAudioSource != null && teacherAudioSource.clip != null)
        {
            if (!teacherAudioSource.isPlaying)
            {
                teacherAudioSource.time = groupStartTime;
                teacherAudioSource.Play();
            }
        }
        
        // 設定 TeacherAvatar 位置和朝向
        SetTeacherAvatarPositionAndRotation();
        
        // 處理摺紙指示的顯示/隱藏
        if (hideOrigamiGuideInPlayback && stepGuide != null)
        {
            stepGuide.HideGuidelines();
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] 已隱藏摺紙指示");
        }
    }
    
    /// <summary>
    /// 播放上一個步驟組
    /// </summary>
    public void PlayPreviousStepGroup()
    {
        if (!useStepGroups || stepGroups.Count == 0)
        {
            Debug.LogWarning("[StudentPlayback] 步驟分組未啟用或沒有分組");
            return;
        }
        
        int targetGroupIndex = currentPlayingGroupIndex - 1;
        if (targetGroupIndex < 0)
            targetGroupIndex = stepGroups.Count - 1; // 循環到最後一個
        
        if (showDebugLogs)
            Debug.Log($"[StudentPlayback] 播放上一個分組: {targetGroupIndex + 1}");
        
        PlayStepGroup(targetGroupIndex);
    }
    
    /// <summary>
    /// 重播當前步驟組
    /// </summary>
    public void ReplayCurrentStepGroup()
    {
        if (!useStepGroups || stepGroups.Count == 0)
        {
            Debug.LogWarning("[StudentPlayback] 步驟分組未啟用或沒有分組");
            return;
        }
        
        if (currentPlayingGroupIndex < 0)
        {
            // 如果還沒有播放過，播放第一個
            currentPlayingGroupIndex = 0;
        }
        
        if (showDebugLogs)
            Debug.Log($"[StudentPlayback] 重播當前分組: {currentPlayingGroupIndex + 1}");
        
        PlayStepGroup(currentPlayingGroupIndex);
    }
    
    /// <summary>
    /// 播放下一個步驟組
    /// </summary>
    public void PlayNextStepGroup()
    {
        if (!useStepGroups || stepGroups.Count == 0)
        {
            Debug.LogWarning("[StudentPlayback] 步驟分組未啟用或沒有分組");
            return;
        }
        
        int targetGroupIndex = currentPlayingGroupIndex + 1;
        if (targetGroupIndex >= stepGroups.Count)
            targetGroupIndex = 0; // 循環到第一個
        
        if (showDebugLogs)
            Debug.Log($"[StudentPlayback] 播放下一個分組: {targetGroupIndex + 1}");
        
        PlayStepGroup(targetGroupIndex);
    }
    
    /// <summary>
    /// 處理 Joystick 控制 Avatar 位置和旋轉
    /// </summary>
    void HandleJoystickControl()
    {
        // 左手 Joystick 控制位置
        Vector2 leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, leftController);
        if (leftStick.sqrMagnitude > 0.01f)
        {
            // 相對於相機方向移動
            Vector3 moveDirection = Vector3.zero;
            
            if (studentCamera != null)
            {
                // X 軸：左右移動
                moveDirection += studentCamera.transform.right * leftStick.x;
                // Y 軸：前後移動
                moveDirection += studentCamera.transform.forward * leftStick.y;
                moveDirection.y = 0; // 保持在水平面
                moveDirection = moveDirection.normalized;
            }
            else
            {
                // 如果沒有相機，使用全局坐標
                moveDirection = new Vector3(leftStick.x, 0, leftStick.y);
            }
            
            teacherAvatar.transform.position += moveDirection * positionMoveSpeed * Time.deltaTime;
        }
        
        // 右手 Joystick 控制旋轉和高度
        Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, rightController);
        
        // 增加閾值並只響應主要方向，避免誤觸
        float deadzone = 0.3f;
        if (rightStick.sqrMagnitude > deadzone * deadzone)
        {
            // 判斷主要方向：水平或垂直
            if (Mathf.Abs(rightStick.x) > Mathf.Abs(rightStick.y))
            {
                // 水平方向為主：控制旋轉
                float rotationAmount = rightStick.x * rotationSpeed * Time.deltaTime;
                teacherAvatar.transform.Rotate(0, -rotationAmount, 0, Space.World);
            }
            else
            {
                // 垂直方向為主：控制高度
                Vector3 verticalMove = new Vector3(0, rightStick.y * positionMoveSpeed * Time.deltaTime, 0);
                teacherAvatar.transform.position += verticalMove;
            }
        }
    }
    
    /// <summary>
    /// 設定 TeacherAvatar 的位置和旋轉（相對於相機）
    /// </summary>
    void SetTeacherAvatarPositionAndRotation()
    {
        if (!useCustomAvatarPosition || teacherAvatar == null)
            return;
        
        // 確保有相機參考
        if (studentCamera == null)
            studentCamera = Camera.main;
        
        if (studentCamera == null)
        {
            Debug.LogWarning("[StudentPlayback] 找不到學生相機，無法設定 TeacherAvatar 位置");
            return;
        }
        
        // 計算相對於相機的世界位置（與 OrigamiSyncController 相同邏輯）
        // teacherAvatarOffset.z = 前後（正值 = 前方）
        // teacherAvatarOffset.x = 左右（正值 = 右方）
        // teacherAvatarOffset.y = 上下（正值 = 下方，因為使用 TransformDirection(Vector3.down)）
        Vector3 worldPosition = studentCamera.transform.position + 
                               studentCamera.transform.forward * teacherAvatarOffset.z +
                               studentCamera.transform.right * teacherAvatarOffset.x +
                               studentCamera.transform.TransformDirection(Vector3.down) * teacherAvatarOffset.y;
        
        teacherAvatar.transform.position = worldPosition;
        
        // 設定旋轉（面向學生）
        if (faceStudent)
        {
            Vector3 directionToStudent = studentCamera.transform.position - teacherAvatar.transform.position;
            directionToStudent.y = 0; // 只在水平面旋轉
            if (directionToStudent.sqrMagnitude > 0.001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(directionToStudent);
                teacherAvatar.transform.rotation = lookRotation;
            }
        }
        
        // 翻轉鏡像（修正左右手對應）
        if (flipMirror)
        {
            // 翻轉 X 軸 scale，這樣左右手會正確對應
            Vector3 scale = teacherAvatar.transform.localScale;
            scale.x = -Mathf.Abs(scale.x); // 確保 X 是負數
            teacherAvatar.transform.localScale = scale;
            
            // 因為 scale.x 是負數（鏡像），LookRotation 的方向會顛倒
            // 需要旋轉 180 度來修正
            if (faceStudent)
            {
                teacherAvatar.transform.Rotate(0, 180f, 0);
            }
        }
        else
        {
            // 恢復正常 scale
            Vector3 scale = teacherAvatar.transform.localScale;
            scale.x = Mathf.Abs(scale.x); // 確保 X 是正數
            teacherAvatar.transform.localScale = scale;
        }
        
        if (showDebugLogs)
            Debug.Log($"[StudentPlayback] TeacherAvatar 位置: {teacherAvatar.transform.position} (相機偏移: {teacherAvatarOffset}), 旋轉: {teacherAvatar.transform.eulerAngles}, 鏡像翻轉: {flipMirror}");
    }
    
    void PlaybackFrame()
    {
        if (playbackFrameIndex >= currentRecording.frames.Count)
        {
            // 播放完畢，完全停止並恢復即時同步
            CompletelyStopPlayback();
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] ✓ 播放完畢");
            return;
        }
        
        // 檢查單步驟播放是否結束
        if (isPlayingSingleStep)
        {
            float currentTime = useAudioSync && teacherAudioSource != null && teacherAudioSource.isPlaying
                ? teacherAudioSource.time
                : playbackTimer;
            
            if (currentTime >= singleStepEndTime)
            {
                if (showDebugLogs)
                    Debug.Log($"[StudentPlayback] ✓ 步驟 {singleStepIndex + 1} 播放完畢，停在 {currentTime:F2}s");
                
                // 停止播放但保持在當前位置（不重置狀態）
                StopPlayback();
                
                // 重置單步驟播放標記
                isPlayingSingleStep = false;
                singleStepIndex = -1;
                singleStepEndTime = -1f;
                
                // UI: 步驟播放完成，只顯示驗證按鈕和離開按鈕
                if (loadButton != null) loadButton.SetActive(false);
                if (playButton != null) playButton.SetActive(false);
                if (pauseButton != null) pauseButton.SetActive(false);
                if (resumeButton != null) resumeButton.SetActive(false);
                if (verifyButton != null) verifyButton.SetActive(true);
                if (previousButton != null) previousButton.SetActive(false);
                if (replayButton != null) replayButton.SetActive(false);
                if (nextButton != null) nextButton.SetActive(false);
                if (exitButton != null) exitButton.SetActive(true);
                
                return;
            }
        }
        
        // 使用音頻同步模式
        if (useAudioSync && teacherAudioSource != null && teacherAudioSource.isPlaying)
        {
            // 使用音頻的實際播放時間作為基準
            float audioTime = teacherAudioSource.time;
            
            // 查找最接近當前音頻時間的幀
            int targetFrameIndex = FindFrameByTime(audioTime);
            
            // 如果找到有效的幀索引
            if (targetFrameIndex >= 0 && targetFrameIndex < currentRecording.frames.Count)
            {
                // 更新幀索引
                playbackFrameIndex = targetFrameIndex;
                
                // 應用 Avatar 數據（每幀都應用以支持向前/向後跳轉）
                AvatarFrameData frame = currentRecording.frames[playbackFrameIndex];
                if (frame.avatarStreamData != null && frame.avatarStreamData.Length > 0)
                {
                    ApplyAvatarStream(frame.avatarStreamData);
                }
                
                // 同步摺紙步驟（每幀都同步以確保 Alembic 持續更新）
                SyncOrigamiStep(audioTime);
                
                // 每 60 幀顯示一次同步狀態
                if (showDebugLogs && playbackFrameIndex % 60 == 0)
                {
                    Debug.Log($"[StudentPlayback] 音頻同步: 音頻時間 {audioTime:F3}s → 幀 {playbackFrameIndex}/{currentRecording.frames.Count}");
                }
            }
        }
        else
        {
            // 傳統模式：使用 playbackTimer
            AvatarFrameData frame = currentRecording.frames[playbackFrameIndex];
            
            // 等待正確的時間點
            if (playbackTimer < frame.timestamp)
            {
                playbackTimer += Time.deltaTime;
                return;
            }
            
            // 應用 Avatar 串流數據
            if (frame.avatarStreamData != null && frame.avatarStreamData.Length > 0)
            {
                ApplyAvatarStream(frame.avatarStreamData);
            }
            else if (showDebugLogs && playbackFrameIndex % 30 == 0)
            {
                Debug.LogWarning($"[StudentPlayback] 第 {playbackFrameIndex} 幀沒有動作數據");
            }
            
            // 同步摺紙步驟
            SyncOrigamiStep(frame.timestamp);
            
            playbackFrameIndex++;
            playbackTimer += Time.deltaTime;
            
            // 每 30 幀顯示一次進度
            if (showDebugLogs && playbackFrameIndex % 30 == 0)
            {
                Debug.Log($"[StudentPlayback] 播放進度: {playbackFrameIndex}/{currentRecording.frames.Count}");
            }
        }
    }
    
    void ApplyAvatarStream(byte[] streamData)
    {
        if (streamData == null || streamData.Length == 0)
        {
            if (showDebugLogs)
                Debug.LogWarning("[StudentPlayback] 串流數據為空");
            return;
        }
        
        if (teacherAvatar == null || !teacherAvatar.IsCreated)
        {
            if (showDebugLogs)
                Debug.LogWarning("[StudentPlayback] TeacherAvatar 未創建");
            return;
        }
        
        // 使用 Meta Avatar SDK 應用串流數據
        // 需要將 byte[] 轉換為 NativeArray
        NativeArray<byte> nativeData = new NativeArray<byte>(streamData, Allocator.Temp);
        
        try
        {
            bool success = teacherAvatar.ApplyStreamData(nativeData);
            
            if (showDebugLogs)
            {
                if (success)
                {
                    Debug.Log($"[StudentPlayback] ✓ 應用串流數據成功 ({streamData.Length} bytes)");
                }
                else
                {
                    Debug.LogWarning($"[StudentPlayback] ✗ 應用串流數據失敗");
                }
            }
        }
        finally
        {
            nativeData.Dispose();
        }
    }
    
    // 音頻播放已改為在 StartPlayback 時設置完整音頻流
    
    /// <summary>
    /// 根據時間查找最接近的幀索引（用於音頻同步）
    /// </summary>
    int FindFrameByTime(float targetTime)
    {
        if (currentRecording == null || currentRecording.frames.Count == 0)
            return -1;
        
        // 二分搜尋找到最接近的幀
        int left = 0;
        int right = currentRecording.frames.Count - 1;
        int closestIndex = 0;
        float closestDiff = Mathf.Abs(currentRecording.frames[0].timestamp - targetTime);
        
        while (left <= right)
        {
            int mid = (left + right) / 2;
            float frameDiff = Mathf.Abs(currentRecording.frames[mid].timestamp - targetTime);
            
            // 更新最接近的幀
            if (frameDiff < closestDiff)
            {
                closestDiff = frameDiff;
                closestIndex = mid;
            }
            
            // 繼續搜尋
            if (currentRecording.frames[mid].timestamp < targetTime)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        
        return closestIndex;
    }

    // ==================== 載入功能 ====================
    
    public bool LoadRecording(string filename)
    {
        string loadPath = GetRecordingFilePath(filename);
        
        if (!File.Exists(loadPath))
        {
            Debug.LogError($"[StudentPlayback] 課程檔案不存在: {loadPath}");
            return false;
        }
        
        try
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream stream = new FileStream(loadPath, FileMode.Open))
            {
                currentRecording = (AvatarRecordingData)formatter.Deserialize(stream);
            }
            
            // 驗證數據完整性
            if (currentRecording.audioSamples == null)
            {
                Debug.LogWarning("[StudentPlayback] ⚠ 舊格式檔案，缺少連續音頻數據");
                currentRecording.audioSamples = new List<float>();
            }
            
            // 重置播放狀態
            playbackFrameIndex = 0;
            playbackTimer = 0f;
            isPlaying = false;
            
            if (showDebugLogs)
            {
                Debug.Log($"[StudentPlayback] ✓ 課程已載入: {currentRecording.recordingName}");
                Debug.Log($"[StudentPlayback] 錄製日期: {currentRecording.recordingDate}");
                Debug.Log($"[StudentPlayback] 時長: {currentRecording.duration:F1}s, 幀數: {currentRecording.frames.Count}");
                Debug.Log($"[StudentPlayback] 音頻樣本: {currentRecording.audioSamples.Count / (currentRecording.audioChannels > 0 ? currentRecording.audioChannels : 1)} 個");
            }
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StudentPlayback] 載入失敗: {e.Message}");
            Debug.LogError($"[StudentPlayback] 這可能是舊格式的錄製檔案，請使用 TeacherRecordingManager 重新錄製");
            Debug.LogError($"[StudentPlayback] 詳細錯誤: {e.StackTrace}");
            return false;
        }
    }
    
    public void LoadLatestRecording()
    {
        string[] recordings = ListAvailableRecordings();
        
        if (recordings.Length > 0)
        {
            string fileToLoad = null;
            
            // 先嘗試載入指定的檔案
            if (!string.IsNullOrEmpty(targetRecordingName))
            {
                foreach (string recording in recordings)
                {
                    if (recording.Equals(targetRecordingName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        fileToLoad = recording;
                        if (showDebugLogs)
                            Debug.Log($"[StudentPlayback] 找到指定檔案: {fileToLoad}");
                        break;
                    }
                }
                
                if (fileToLoad == null && showDebugLogs)
                {
                    Debug.LogWarning($"[StudentPlayback] 找不到指定檔案 '{targetRecordingName}'，將載入最新檔案");
                }
            }
            
            // 如果沒有指定或找不到，載入最新的檔案
            if (fileToLoad == null)
            {
                fileToLoad = recordings[recordings.Length - 1];
                if (showDebugLogs)
                    Debug.Log($"[StudentPlayback] 載入最新檔案: {fileToLoad}");
            }
            
            LoadRecording(fileToLoad);
        }
        else
        {
            Debug.LogWarning("[StudentPlayback] 找不到任何課程檔案");
        }
    }
    
    public string[] ListAvailableRecordings()
    {
        string folderPath = GetRecordingsFolderPath();
        
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[StudentPlayback] 課程資料夾不存在: {folderPath}");
            return new string[0];
        }
        
        string[] files = Directory.GetFiles(folderPath, "*.recording");
        
        for (int i = 0; i < files.Length; i++)
        {
            files[i] = Path.GetFileNameWithoutExtension(files[i]);
        }
        
        if (showDebugLogs)
            Debug.Log($"[StudentPlayback] 找到 {files.Length} 個課程檔案");
        
        return files;
    }

    string GetRecordingsFolderPath()
    {
        return Path.Combine(Application.dataPath, "Recordings");
    }
    
    string GetRecordingFilePath(string filename)
    {
        return Path.Combine(GetRecordingsFolderPath(), filename + ".recording");
    }

    // ==================== UI 顯示 ====================
    
    void OnGUI()
    {
        if (!showPlaybackUI)
            return;
        
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(15, 15, 10, 10);
        
        float width = 450f;
        float height = 150f;
        float xPos = Screen.width - width - 20f;
        float yPos = 20f;
        
        GUI.Box(new Rect(xPos, yPos, width, height), "", style);
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 18;
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontStyle = FontStyle.Bold;
        
        float yOffset = yPos + 15f;
        
        if (currentRecording == null)
        {
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 35f),
                "📚 學生播放端 - 等待載入課程", labelStyle);
            yOffset += 40f;
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 30f),
                "按 L 鍵載入最新課程", labelStyle);
        }
        else if (isPlaying)
        {
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 35f),
                "▶ 播放中...", labelStyle);
            yOffset += 35f;
            
            // 使用音頻時間或 playbackTimer
            float currentTime = useAudioSync && teacherAudioSource != null && teacherAudioSource.isPlaying
                ? teacherAudioSource.time
                : playbackTimer;
            float progress = currentRecording.duration > 0 ? currentTime / currentRecording.duration : 0f;
            
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 30f),
                $"時間: {currentTime:F1}s / {currentRecording.duration:F1}s", labelStyle);
            yOffset += 30f;
            
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                $"幀: {playbackFrameIndex}/{currentRecording.frames.Count}", labelStyle);
            yOffset += 30f;
            
            // 進度條
            DrawProgressBar(new Rect(xPos + 15f, yOffset, width - 30f, 20f), progress);
        }
        else
        {
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 35f),
                "⏸ 已載入課程", labelStyle);
            yOffset += 35f;
            
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                $"課程: {currentRecording.recordingName}", labelStyle);
            yOffset += 25f;
            
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                $"時長: {currentRecording.duration:F1}s", labelStyle);
            yOffset += 30f;
            
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                "按 P 或 Space 開始播放", labelStyle);
        }
    }
    
    void DrawProgressBar(Rect rect, float progress)
    {
        // 背景
        GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        
        // 進度條
        GUI.color = Color.Lerp(Color.green, Color.blue, progress);
        Rect progressRect = new Rect(rect.x, rect.y, rect.width * progress, rect.height);
        GUI.DrawTexture(progressRect, Texture2D.whiteTexture);
        
        GUI.color = Color.white;
    }

    // ==================== 摺紙步驟播放 ====================
    
    private int lastSyncedStep = -1;
    
    /// <summary>
    /// 同步摺紙步驟和 Alembic 動畫（在播放時調用）
    /// </summary>
    void SyncOrigamiStep(float currentTime)
    {
        int targetStep = GetCurrentOrigamiStep(currentTime);
        
        if (targetStep >= 0)
        {
            var stepGuide = FindObjectOfType<OrigamiStepGuideSimple>();
            if (stepGuide != null)
            {
                // 在播放模式下，不要調用 JumpToStep（它會重置 Alembic 造成跳躍）
                // 只記錄步驟變化用於調試
                if (targetStep != lastSyncedStep)
                {
                    lastSyncedStep = targetStep;
                    
                    if (showDebugLogs)
                        Debug.Log($"[StudentPlayback] 切換到步驟 {targetStep}");
                }
                
                // 持續更新 Alembic 動畫（每幀都更新，確保動畫流暢）
                // 直接查找 AlembicStreamPlayer，不通過 OrigamiSyncController
                var alembicPlayer = FindObjectOfType<UnityEngine.Formats.Alembic.Importer.AlembicStreamPlayer>();
                if (alembicPlayer != null)
                {
                    // 找到該步驟開始的時間戳
                    float stepStartTime = 0f;
                    foreach (var stepEvent in currentRecording.origamiStepEvents)
                    {
                        if (stepEvent.stepIndex == targetStep)
                        {
                            stepStartTime = stepEvent.timestamp;
                            break;
                        }
                    }
                    
                    // 計算步驟內的相對時間
                    float elapsedInStep = currentTime - stepStartTime;
                    var step = stepGuide.steps[targetStep];
                    float stepProgress = Mathf.Clamp01(elapsedInStep / step.duration);
                    
                    // 映射到 Alembic 動畫進度
                    float targetProgress = Mathf.Lerp(step.progressStart, step.progressEnd, stepProgress);
                    float alembicTime = targetProgress * alembicPlayer.Duration;
                    
                    alembicPlayer.CurrentTime = alembicTime;
                    
                    if (showDebugLogs && Time.frameCount % 30 == 0)
                        Debug.Log($"[StudentPlayback] Alembic 播放: 步驟 {targetStep}, 進度 {stepProgress:F2}, 時間 {alembicTime:F2}s");
                }
            }
        }
    }
    
    /// <summary>
    /// 獲取當前時間應該顯示的摺紙步驟（用於播放）
    /// </summary>
    public int GetCurrentOrigamiStep(float currentTime)
    {
        if (currentRecording == null || currentRecording.origamiStepEvents.Count == 0)
            return -1;
        
        // 找到最後一個時間戳 <= currentTime 的步驟
        int currentStep = -1;
        foreach (var stepEvent in currentRecording.origamiStepEvents)
        {
            if (stepEvent.timestamp <= currentTime)
            {
                currentStep = stepEvent.stepIndex;
            }
            else
            {
                break;
            }
        }
        
        return currentStep;
    }

    // ==================== 公開屬性 ====================
    
    public bool IsPlaying => isPlaying;
    public bool HasRecording => currentRecording != null && currentRecording.frames.Count > 0;
    public string CurrentRecordingName => currentRecording?.recordingName ?? "";
    public float PlaybackProgress => currentRecording != null && currentRecording.duration > 0 
        ? playbackTimer / currentRecording.duration : 0f;
    public AvatarRecordingData CurrentRecording => currentRecording;
    
    /// <summary>
    /// 跳轉到指定時間點（秒）
    /// 同時同步 Avatar、音頻、摺紙步驟
    /// </summary>
    public void JumpToTime(float targetTime)
    {
        if (currentRecording == null || !isPlaying)
        {
            Debug.LogWarning("[StudentPlayback] 無法跳轉：沒有正在播放的錄製檔案");
            return;
        }
        
        // 1. 找到對應的 Avatar 幀
        int targetFrameIndex = -1;
        for (int i = 0; i < currentRecording.frames.Count; i++)
        {
            if (currentRecording.frames[i].timestamp >= targetTime)
            {
                targetFrameIndex = i;
                break;
            }
        }
        
        if (targetFrameIndex < 0 && currentRecording.frames.Count > 0)
        {
            // 如果超過最後一幀，使用最後一幀
            targetFrameIndex = currentRecording.frames.Count - 1;
        }
        
        if (targetFrameIndex >= 0)
        {
            // 2. 應用該幀的 Avatar 數據
            if (teacherAvatar != null)
            {
                AvatarFrameData frame = currentRecording.frames[targetFrameIndex];
                if (frame.avatarStreamData != null && frame.avatarStreamData.Length > 0)
                {
                    ApplyAvatarStream(frame.avatarStreamData);
                }
                if (showDebugLogs)
                    Debug.Log($"[StudentPlayback] 跳轉 Avatar 到第 {targetFrameIndex} 幀");
            }
            
            // 3. 跳轉音頻播放位置
            if (teacherAudioSource != null && teacherAudioSource.clip != null)
            {
                int audioSamplePosition = (int)(targetTime * currentRecording.audioSampleRate * currentRecording.audioChannels);
                audioSamplePosition = Mathf.Clamp(audioSamplePosition, 0, currentRecording.audioSamples.Count - 1);
                teacherAudioSource.timeSamples = audioSamplePosition;
                
                if (showDebugLogs)
                    Debug.Log($"[StudentPlayback] 跳轉音頻到 {audioSamplePosition} samples");
            }
            
            // 4. 直接更新 Alembic 動畫（不調用 JumpToStep 避免重置）
            int targetStep = GetCurrentOrigamiStep(targetTime);
            if (targetStep >= 0)
            {
                var stepGuideSimple = FindObjectOfType<OrigamiStepGuideSimple>();
                var alembicPlayer = FindObjectOfType<UnityEngine.Formats.Alembic.Importer.AlembicStreamPlayer>();
                
                if (alembicPlayer != null && stepGuideSimple != null)
                {
                    // 計算該步驟在目標時間的 Alembic 進度
                    var step = stepGuideSimple.steps[targetStep];
                    
                    // 找到該步驟開始的時間戳
                    float stepStartTime = 0f;
                    foreach (var stepEvent in currentRecording.origamiStepEvents)
                    {
                        if (stepEvent.stepIndex == targetStep)
                        {
                            stepStartTime = stepEvent.timestamp;
                            break;
                        }
                    }
                    
                    // 計算步驟內的相對時間
                    float elapsedInStep = targetTime - stepStartTime;
                    float stepProgress = Mathf.Clamp01(elapsedInStep / step.duration);
                    
                    // 映射到 Alembic 動畫進度
                    float targetProgress = Mathf.Lerp(step.progressStart, step.progressEnd, stepProgress);
                    float alembicTime = targetProgress * alembicPlayer.Duration;
                    
                    alembicPlayer.CurrentTime = alembicTime;
                    
                    if (showDebugLogs)
                        Debug.Log($"[StudentPlayback] 跳轉 Alembic 到 {alembicTime:F2}s (進度: {targetProgress:F2})");
                }
            }
            
            // 更新內部播放計時器和幀索引（支持向前/向後跳轉）
            playbackFrameIndex = targetFrameIndex;
            playbackTimer = targetTime;
            
            // 設置同步步驟記錄為目標步驟，避免 SyncOrigamiStep 在下一幀重新計算導致跳回 t=0
            lastSyncedStep = targetStep;
            
            if (showDebugLogs)
                Debug.Log($"[StudentPlayback] ✓ 已跳轉到 {targetTime:F2} 秒");
        }
        else
        {
            Debug.LogWarning($"[StudentPlayback] 找不到對應時間的幀：{targetTime:F2}s");
        }
    }
    
    // ==================== UI 控制函數 ====================
    
    /// <summary>
    /// UI：載入最新錄製檔案（按鈕呼叫）
    /// </summary>
    public void UI_LoadRecording()
    {
        LoadLatestRecording();
        
        // UI: 顯示播放按鈕，隱藏其他按鈕
        if (loadButton != null) loadButton.SetActive(true);
        if (playButton != null) playButton.SetActive(true);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (resumeButton != null) resumeButton.SetActive(false);
        if (exitButton != null) exitButton.SetActive(false);
        if (verifyButton != null) verifyButton.SetActive(false);
        if (previousButton != null) previousButton.SetActive(false);
        if (replayButton != null) replayButton.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false);
    }
    
    /// <summary>
    /// UI：播放第一個步驟組（按鈕呼叫）
    /// </summary>
    public void UI_PlayFirstStep()
    {
        if (currentRecording == null || currentRecording.origamiStepEvents.Count == 0)
        {
            // 如果沒有載入錄製，先自動載入
            LoadLatestRecording();
            
            if (currentRecording == null || currentRecording.origamiStepEvents.Count == 0)
            {
                Debug.LogError("[StudentPlayback] 沒有可播放的錄製數據");
                return;
            }
        }
        
        // 播放第一個步驟組
        PlayStepGroup(0);
        
        // UI: 顯示暫停和離開按鈕，隱藏其他按鈕
        if (loadButton != null) loadButton.SetActive(false);
        if (playButton != null) playButton.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);
        if (resumeButton != null) resumeButton.SetActive(false);
        if (exitButton != null) exitButton.SetActive(true);
        if (verifyButton != null) verifyButton.SetActive(false);
        if (previousButton != null) previousButton.SetActive(false);
        if (replayButton != null) replayButton.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false);
    }
    
    /// <summary>
    /// UI：暫停播放（按鈕呼叫）
    /// </summary>
    public void UI_PausePlayback()
    {
        if (isPlaying)
        {
            StopPlayback();
            
            // UI: 暫停時，隱藏暫停按鈕，顯示繼續按鈕
            if (pauseButton != null) pauseButton.SetActive(false);
            if (resumeButton != null) resumeButton.SetActive(true);
            
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] UI: Playback paused");
        }
    }
    
    /// <summary>
    /// UI：繼續播放（按鈕呼叫）
    /// </summary>
    public void UI_ResumePlayback()
    {
        if (!isPlaying && currentRecording != null)
        {
            // 從當前位置繼續播放（不重置到開頭）
            isPlaying = true;
            
            // 繼續音頻播放（從當前位置）
            if (teacherAudioSource != null && teacherAudioSource.clip != null)
            {
                if (!teacherAudioSource.isPlaying)
                {
                    teacherAudioSource.Play();
                }
            }
            
            // UI: 繼續時，隱藏繼續按鈕，顯示暫停按鈕
            if (resumeButton != null) resumeButton.SetActive(false);
            if (pauseButton != null) pauseButton.SetActive(true);
            
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] UI: Resume playback from current position");
        }
    }
    
    /// <summary>
    /// UI: Verify current step (called by button)
    /// </summary>
    public void UI_VerifyStep()
    {
        if (showDebugLogs)
            Debug.Log("[StudentPlayback] UI: Starting verification");
        
        // Get current step number (1-based)
        int currentStep = GetCurrentStepGroupIndex() + 1;
        
        if (currentStep < 1 || currentStep > 3)
        {
            Debug.LogWarning($"[StudentPlayback] Invalid step number: {currentStep} (only 1-3 supported)");
            OnVerificationFailed("Step number out of range");
            return;
        }
        
        // 步驟映射：步驟一和步驟二都檢測 shape_2
        int expectedShapeStep = currentStep;
        if (currentStep == 1 || currentStep == 2)
        {
            expectedShapeStep = 2; // 步驟一和步驟二都驗證 shape_2
            if (showDebugLogs)
                Debug.Log($"[StudentPlayback] Step {currentStep} mapped to verify shape_2");
        }
        
        // UI: During verification, only show EXIT button
        if (loadButton != null) loadButton.SetActive(false);
        if (playButton != null) playButton.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (resumeButton != null) resumeButton.SetActive(false);
        if (verifyButton != null) verifyButton.SetActive(false);
        if (previousButton != null) previousButton.SetActive(false);
        if (replayButton != null) replayButton.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false);
        if (exitButton != null) exitButton.SetActive(true);
        
        // Show "Verifying..." message in runtime text (yellow)
        if (shapeDetector != null)
        {
            shapeDetector.ShowVerifyingMessage();
        }
        
        // Use ShapeDetector for verification
        if (shapeDetector != null)
        {
            StartCoroutine(VerifyStepCoroutine(expectedShapeStep));
        }
        else
        {
            Debug.LogWarning("[StudentPlayback] ShapeDetector not set, skipping verification");
            // No detector, pass directly (development mode)
            OnVerificationSuccess(expectedShapeStep, 1.0f);
        }
        
    }
    
    /// <summary>
    /// Verification coroutine
    /// </summary>
    private System.Collections.IEnumerator VerifyStepCoroutine(int expectedStep)
    {
        if (showDebugLogs)
            Debug.Log($"[StudentPlayback] Verifying step {expectedStep}...");
        
        // Always use ShapeDetector's screenshot method (supports WebCamera, RealSense, etc.)
        // Let ShapeDetector handle the capture based on its configured mode
        var verifyTask = shapeDetector.VerifyStepAsync(expectedStep, null);
        
        while (!verifyTask.IsCompleted)
        {
            yield return null;
        }
        
        if (verifyTask.IsFaulted)
        {
            Debug.LogError($"[StudentPlayback] Verification failed: {verifyTask.Exception?.Message}");
            OnVerificationFailed(verifyTask.Exception?.Message ?? "Unknown error");
            
            // Show control buttons after failure
            ShowVerificationResultUI();
            yield break;
        }
        
        var result = verifyTask.Result;
        
        if (!string.IsNullOrEmpty(result.error))
        {
            Debug.LogError($"[StudentPlayback] Verification error: {result.error}");
            OnVerificationFailed(result.error);
        }
        else
        {
            // 多檢測驗證：檢查所有檢測結果中是否有符合預期步驟的形狀
            bool hasMatchingShape = result.HasMatchingShape(expectedStep, shapeDetector.confidenceThreshold);
            
            if (hasMatchingShape)
            {
                // 找到符合的檢測結果
                var matchingDetection = result.GetBestMatchingDetection(expectedStep);
                float matchingConfidence = matchingDetection != null ? matchingDetection.confidence : result.confidence;
                
                if (showDebugLogs)
                {
                    Debug.Log($"[StudentPlayback] ✓ Multi-detection verification success!");
                    Debug.Log($"[StudentPlayback]   Expected: shape_{expectedStep}");
                    Debug.Log($"[StudentPlayback]   Found matching detection with confidence: {matchingConfidence:P1}");
                    if (result.all_detections != null && result.all_detections.Length > 1)
                    {
                        Debug.Log($"[StudentPlayback]   Total detections: {result.all_detections.Length}");
                    }
                }
                
                OnVerificationSuccess(expectedStep, matchingConfidence);
            }
            else if (result.success)
            {
                // 標準驗證成功（最佳檢測符合預期）
                if (showDebugLogs)
                    Debug.Log($"[StudentPlayback] ✓ Verification success! {result.message}");
                OnVerificationSuccess(expectedStep, result.confidence);
            }
            else
            {
                // 驗證失敗
                if (showDebugLogs)
                    Debug.Log($"[StudentPlayback] ✗ Verification failed: {result.message}");
                OnVerificationFailed(result.message);
            }
        }
        
        // Show control buttons after verification completes
        ShowVerificationResultUI();
    }
    
    /// <summary>
    /// Show UI buttons after verification completes
    /// </summary>
    private void ShowVerificationResultUI()
    {
        int currentStepIndex = GetCurrentStepGroupIndex();
        int totalSteps = GetTotalStepGroups();
        
        // Hide all other buttons
        if (loadButton != null) loadButton.SetActive(false);
        if (playButton != null) playButton.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (resumeButton != null) resumeButton.SetActive(false);
        if (verifyButton != null) verifyButton.SetActive(false);
        
        // Show Previous button (unless first step)
        if (previousButton != null)
            previousButton.SetActive(currentStepIndex > 0);
        
        // Show Replay button
        if (replayButton != null) replayButton.SetActive(true);
        
        // Show Next button (unless last step)
        if (nextButton != null)
            nextButton.SetActive(currentStepIndex < totalSteps - 1);
        
        // Show EXIT button
        if (exitButton != null)
            exitButton.SetActive(true);
    }
    
    /// <summary>
    /// 截取驗證用圖片
    /// </summary>
    private string CaptureVerificationImage()
    {
        try
        {
            Camera cam = verificationCamera != null ? verificationCamera : Camera.main;
            if (cam == null) return null;
            
            int width = 640;
            int height = 640;
            
            RenderTexture rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            cam.Render();
            
            RenderTexture.active = rt;
            screenshot.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            
            cam.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);
            
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "verify_capture.png");
            System.IO.File.WriteAllBytes(path, screenshot.EncodeToPNG());
            Destroy(screenshot);
            
            if (showDebugLogs)
                Debug.Log($"[StudentPlayback] 驗證圖片已儲存: {path}");
            
            return path;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StudentPlayback] 截圖失敗: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Callback when verification succeeds
    /// </summary>
    protected virtual void OnVerificationSuccess(int step, float confidence)
    {
        Debug.Log($"<color=green>Step {step} verification success! Confidence: {confidence:P0}</color>");
        
        // Show success message in runtime text (green, disappears after 3 seconds)
        if (shapeDetector != null)
        {
            shapeDetector.ShowSuccessMessage($"Success!");
        }
        
        // Notify UI to update status
        // StudentPlaybackUI will automatically detect and switch to Verified state
    }
    
    /// <summary>
    /// Callback when verification fails
    /// </summary>
    protected virtual void OnVerificationFailed(string reason)
    {
        Debug.Log($"<color=red>Verification failed");
        
        // Show failure message in runtime text (red, disappears after 3 seconds)
        if (shapeDetector != null)
        {
            shapeDetector.ShowFailureMessage($"Verification failed");
        }
        
        // Can add UI prompts, sound effects, etc. here
    }
    
    /// <summary>
    /// Last verification result (for UI query)
    /// </summary>
    private bool? lastVerificationResult = null;
    private float lastVerificationTime = 0f;
    
    /// <summary>
    /// Check if last verification was successful (for UI use)
    /// </summary>
    public bool IsLastVerificationSuccessful()
    {
        return lastVerificationResult == true;
    }
    
    /// <summary>
    /// UI：播放上一個步驟組（按鈕呼叫）
    /// </summary>
    public void UI_PreviousStep()
    {
        PlayPreviousStepGroup();
        
        // UI: 回到播放狀態，顯示暫停和離開按鈕
        if (loadButton != null) loadButton.SetActive(false);
        if (playButton != null) playButton.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);
        if (resumeButton != null) resumeButton.SetActive(false);
        if (exitButton != null) exitButton.SetActive(true);
        if (verifyButton != null) verifyButton.SetActive(false);
        if (previousButton != null) previousButton.SetActive(false);
        if (replayButton != null) replayButton.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false);
    }
    
    /// <summary>
    /// UI：重播當前步驟組（按鈕呼叫）
    /// </summary>
    public void UI_ReplayStep()
    {
        ReplayCurrentStepGroup();
        
        // UI: 回到播放狀態，顯示暫停和離開按鈕
        if (loadButton != null) loadButton.SetActive(false);
        if (playButton != null) playButton.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);
        if (resumeButton != null) resumeButton.SetActive(false);
        if (exitButton != null) exitButton.SetActive(true);
        if (verifyButton != null) verifyButton.SetActive(false);
        if (previousButton != null) previousButton.SetActive(false);
        if (replayButton != null) replayButton.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false);
    }
    
    /// <summary>
    /// UI：播放下一個步驟組（按鈕呼叫）
    /// </summary>
    public void UI_NextStep()
    {
        PlayNextStepGroup();
        
        // UI: 回到播放狀態，顯示暫停和離開按鈕
        if (loadButton != null) loadButton.SetActive(false);
        if (playButton != null) playButton.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);
        if (resumeButton != null) resumeButton.SetActive(false);
        if (exitButton != null) exitButton.SetActive(true);
        if (verifyButton != null) verifyButton.SetActive(false);
        if (previousButton != null) previousButton.SetActive(false);
        if (replayButton != null) replayButton.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false);
    }
    
    /// <summary>
    /// UI：離開播放，回到初始狀態（按鈕呼叫）
    /// </summary>
    public void UI_ExitPlayback()
    {
        CancelPlayback();
        
        // 重置紙張動畫到初始狀態 (t=0)
        var syncController = FindObjectOfType<OrigamiSyncController>();
        if (syncController != null && syncController.alembicPlayer != null)
        {
            syncController.alembicPlayer.CurrentTime = 0f;
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] 已重置紙張動畫到初始狀態 (t=0)");
        }
        
        // 恢復摺紙指示
        var stepGuide = FindObjectOfType<OrigamiStepGuideSimple>();
        if (stepGuide != null)
        {
            stepGuide.ShowGuidelines();
            if (showDebugLogs)
                Debug.Log("[StudentPlayback] 已恢復摺紙指示");
        }
        
        // UI: 回到初始狀態，顯示讀取和播放按鈕
        if (loadButton != null) loadButton.SetActive(true);
        if (playButton != null) playButton.SetActive(true);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (resumeButton != null) resumeButton.SetActive(false);
        if (exitButton != null) exitButton.SetActive(false);
        if (verifyButton != null) verifyButton.SetActive(false);
        if (previousButton != null) previousButton.SetActive(false);
        if (replayButton != null) replayButton.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false);
        
        if (showDebugLogs)
            Debug.Log("[StudentPlayback] UI: Exited playback");
    }
    
    /// <summary>
    /// 獲取當前播放的步驟組索引
    /// </summary>
    public int GetCurrentStepGroupIndex()
    {
        return currentPlayingGroupIndex;
    }
    
    /// <summary>
    /// 獲取總步驟組數量
    /// </summary>
    public int GetTotalStepGroups()
    {
        return stepGroups.Count;
    }
    
    /// <summary>
    /// 檢查是否有錄製數據已載入
    /// </summary>
    public bool HasRecordingLoaded()
    {
        return currentRecording != null && currentRecording.frames.Count > 0;
    }
}
