using UnityEngine;
using Oculus.Avatar2;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using CAPI = Oculus.Avatar2.CAPI;

/// <summary>
/// Avatar 完整錄製管理器
/// 錄製動作 + 嘴型 + 音頻，並存檔供日後播放
/// </summary>
public class AvatarRecordingManager : MonoBehaviour
{
    [Header("Avatar 設定")]
    [Tooltip("本地 Avatar（錄製來源）")]
    public OvrAvatarEntity localAvatar;
    
    [Tooltip("遠端 Avatar（播放目標）")]
    public OvrAvatarEntity remoteAvatar;

    [Header("音頻設定")]
    [Tooltip("麥克風 AudioSource（來自 LipSyncInput）")]
    public AudioSource microphoneSource;
    
    [Tooltip("播放 AudioSource（遠端 Avatar 上）")]
    public AudioSource playbackSource;
    
    [Tooltip("麥克風設備名稱（留空使用默認）")]
    public string microphoneDevice = null;

    [Header("錄製設定")]
    [Tooltip("錄製品質等級")]
    public OvrAvatarEntity.StreamLOD streamLOD = OvrAvatarEntity.StreamLOD.High;
    
    [Tooltip("錄製幀率（每秒）")]
    [Range(10, 90)]
    public int recordingFPS = 30;
    
    [Tooltip("音頻採樣率")]
    public int audioSampleRate = 44100;
    
    [Tooltip("最大錄製時長（秒）")]
    public float maxRecordingDuration = 300f; // 5 分鐘

    [Header("存檔設定")]
    [Tooltip("錄製檔案存放路徑（相對於 Assets 資料夾）")]
    public string saveFolderPath = "Assets/Recordings";
    
    [Tooltip("自動生成檔名")]
    public bool autoGenerateFilename = true;
    
    [Tooltip("目標錄製檔名（空白 = 自動載入最新檔案）")]
    public string targetRecordingName = "";

    [Header("調試")]
    [Tooltip("顯示調試訊息")]
    public bool showDebugLogs = true;
    
    [Tooltip("在螢幕顯示錄製狀態")]
    public bool showRecordingUI = true;
    
    [Header("同步設定")]
    [Tooltip("播放時使用音頻時間同步動作（修正長時間錄製的漂移）")]
    public bool useAudioSync = true;
    
    [Tooltip("音頻同步容差（秒）")]
    [Range(0f, 0.5f)]
    public float audioSyncTolerance = 0.1f;
    
    [Header("步驟分組設定")]
    [Tooltip("啟用步驟分組播放（數字鍵播放分組而非單個步驟）")]
    public bool useStepGroups = false;
    
    [Tooltip("步驟分組定義（按 1-9 對應分組 1-9）")]
    public List<StepGroup> stepGroups = new List<StepGroup>();
    
    [Header("Avatar 播放位置設定")]
    [Tooltip("啟用指定 RemoteAvatar 位置")]
    public bool useCustomAvatarPosition = false;
    
    [Tooltip("RemoteAvatar 相對於相機的偏移\nZ=前後(正=前), X=左右(正=右), Y=上下(正=下)")]
    public Vector3 remoteAvatarOffset = new Vector3(0, 0, 1);
    
    [Tooltip("讓 RemoteAvatar 面向玩家（Camera）")]
    public bool facePlayer = true;
    
    [Tooltip("翻轉鏡像（讓左右手正確對應）")]
    public bool flipMirror = true;
    
    [Tooltip("播放時隱藏摺紙指示（綠紅黃線條）")]
    public bool hideOrigamiGuideInPlayback = true;
    
    [Tooltip("玩家相機（用於計算朝向）")]
    public Camera playerCamera;

    // === 私有變數 ===
    private AvatarRecordingData currentRecording;
    private bool isRecording = false;
    private bool isPlaying = false;
    private float recordingTimer = 0f;
    private float frameTimer = 0f;
    private float frameInterval;
    private int lastMicPosition = 0;
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
        frameInterval = 1f / recordingFPS;
        
        // 自動尋找組件
        FindComponents();
        
        // 檢查並創建存檔資料夾
        string savePath = GetSaveFolderPath();
        if (!Directory.Exists(savePath))
        {
            try
            {
                Directory.CreateDirectory(savePath);
                Debug.Log($"[RecordingManager] 創建錄製資料夾: {savePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RecordingManager] 創建資料夾失敗: {e.Message}");
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"[RecordingManager] 錄製資料夾: {savePath}");
    }

    void FindComponents()
    {
        if (localAvatar == null)
            localAvatar = GameObject.Find("LocalAvatar")?.GetComponent<OvrAvatarEntity>();
        
        if (remoteAvatar == null)
            remoteAvatar = GameObject.Find("RemoteLoopbackAvatar")?.GetComponent<OvrAvatarEntity>();
        
        if (microphoneSource == null)
        {
            var lipSyncInput = GameObject.Find("LipSyncInput");
            if (lipSyncInput != null)
            {
                microphoneSource = lipSyncInput.GetComponent<AudioSource>();
                
                // 確保麥克風 AudioClip 使用正確的採樣率
                if (microphoneSource != null && microphoneSource.clip != null)
                {
                    audioSampleRate = microphoneSource.clip.frequency;
                    if (showDebugLogs)
                        Debug.Log($"[RecordingManager] 麥克風採樣率: {audioSampleRate} Hz");
                }
            }
        }
        
        if (playbackSource == null && remoteAvatar != null)
        {
            playbackSource = remoteAvatar.GetComponent<AudioSource>();
            if (playbackSource == null)
                playbackSource = remoteAvatar.gameObject.AddComponent<AudioSource>();
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
        
        if (isRecording)
        {
            recordingTimer += Time.deltaTime;
            frameTimer += Time.deltaTime;
            
            // 檢查最大錄製時長
            if (recordingTimer >= maxRecordingDuration)
            {
                Debug.LogWarning($"[RecordingManager] 達到最大錄製時長 {maxRecordingDuration} 秒，自動停止");
                StopRecording();
                return;
            }
            
            // 按幀率錄製
            if (frameTimer >= frameInterval)
            {
                RecordFrame();
                frameTimer = 0f;
            }
        }
        
        if (isPlaying)
        {
            PlaybackFrame();
        }
    }
    
    /// <summary>
    /// 處理鍵盤快捷鍵
    /// </summary>
    void HandleKeyboardInput()
    {
        // R 鍵：開始/停止錄製
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (isRecording)
            {
                StopRecording();
            }
            else if (!isPlaying)
            {
                StartRecording();
            }
        }

        // S 鍵：儲存
        if (Input.GetKeyDown(KeyCode.S) && !isRecording && currentRecording != null && currentRecording.frames.Count > 0)
        {
            SaveRecording();
        }

        // L 鍵：載入
        if (Input.GetKeyDown(KeyCode.L) && !isRecording && !isPlaying)
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
            else if (!isRecording)
            {
                // 先清空之前的播放狀態（相當於按 C）
                CancelPlayback();
                
                // 然後載入錄製（相當於按 L）
                if (showDebugLogs)
                    Debug.Log("[RecordingManager] 自動載入錄製...");
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
            if (isPlaying || isPlayingSingleStep || (remoteAvatar != null && !remoteAvatar.IsLocal))
            {
                CancelPlayback();
            }
        }
        
        // 數字鍵 1-9：播放指定步驟或步驟組
        if (!isRecording && currentRecording != null && currentRecording.origamiStepEvents.Count > 0)
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
    
    /// <summary>
    /// 播放指定的單個步驟
    /// </summary>
    public void PlaySingleStep(int stepIndex)
    {
        // 先清空之前的播放狀態（相當於按 C）
        CancelPlayback();
        
        // 然後載入錄製（相當於按 L）
        if (showDebugLogs)
            Debug.Log("[RecordingManager] 自動載入錄製...");
        LoadLatestRecording();
        
        // 確認載入成功
        if (currentRecording == null || currentRecording.origamiStepEvents.Count == 0)
        {
            Debug.LogError("[RecordingManager] 沒有可播放的錄製數據或步驟事件");
            return;
        }
        
        if (stepIndex < 0 || stepIndex >= currentRecording.origamiStepEvents.Count)
        {
            Debug.LogError($"[RecordingManager] 步驟索引超出範圍: {stepIndex} (共 {currentRecording.origamiStepEvents.Count} 個步驟)");
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
            Debug.LogWarning($"[RecordingManager] 找不到 OrigamiStepGuideSimple，使用預設持續時間 {stepDuration}s");
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
            Debug.Log($"[RecordingManager] 播放步驟 {stepIndex + 1}: {stepStartTime:F2}s - {stepEndTime:F2}s (持續 {stepDuration:F2}s)");
        }
        
        // 設置單步驟播放標記
        isPlayingSingleStep = true;
        singleStepIndex = stepIndex;
        singleStepEndTime = stepEndTime;
        
        // 如果正在播放，先停止（但不重置狀態）
        bool wasPlaying = isPlaying;
        if (isPlaying)
        {
            isPlaying = false; // 臨時停止以避免 StartPlayback 衝突
        }
        
        // 恢復播放狀態（需要在 JumpToTime 之前設置，因為 JumpToTime 檢查 isPlaying）
        isPlaying = true;
        
        // 初始化播放環境（無論之前是否播放）
        if (remoteAvatar == null || !remoteAvatar.IsCreated)
        {
            Debug.LogError("[RecordingManager] RemoteAvatar 未準備好");
            isPlaying = false;
            return;
        }
        
        // 如果 Avatar 在遠端模式但剛播完步驟（wasPlaying=false），需要重置以清除舊姿勢
        if (!wasPlaying && !remoteAvatar.IsLocal)
        {
            // 先切回本地模式清除狀態，再切回遠端模式
            remoteAvatar.SetIsLocal(true);
            if (showDebugLogs)
                Debug.Log("[RecordingManager] 重置 Avatar：本地模式 → 遠端模式");
        }
        
        // 設置為遠端模式並確保 Avatar 準備好接收新數據
        remoteAvatar.SetIsLocal(false);
        
        // 停用即時同步
        if (loopbackManager != null)
        {
            loopbackManager.enabled = false;
        }
        
        // 設置播放音頻
        if (playbackSource != null && currentRecording.audioSamples.Count > 0)
        {
            if (playbackSource.clip == null)
            {
                int sampleCount = currentRecording.audioSamples.Count / currentRecording.audioChannels;
                AudioClip audioClip = AudioClip.Create(
                    "RecordedAudio",
                    sampleCount,
                    currentRecording.audioChannels,
                    currentRecording.audioSampleRate,
                    false
                );
                audioClip.SetData(currentRecording.audioSamples.ToArray(), 0);
                playbackSource.clip = audioClip;
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
                    Debug.Log($"[RecordingManager] 強制應用起始幀 {startFrameIndex} 的 Avatar 數據");
            }
        }
        
        // 確保音頻正在播放
        if (playbackSource != null && playbackSource.clip != null)
        {
            if (!playbackSource.isPlaying)
            {
                playbackSource.time = stepStartTime;
                playbackSource.Play();
            }
        }
        
        // 設定 RemoteAvatar 位置和朝向
        SetRemoteAvatarPositionAndRotation();
        
        // 處理摺紙指示的顯示/隱藏
        if (hideOrigamiGuideInPlayback && stepGuide != null)
        {
            stepGuide.HideGuidelines();
            if (showDebugLogs)
                Debug.Log("[RecordingManager] 已隱藏摺紙指示");
        }
    }
    
    /// <summary>
    /// 播放步驟組（連續播放多個步驟）
    /// </summary>
    public void PlayStepGroup(int groupIndex)
    {
        // 先清空之前的播放狀態（相當於按 C）
        CancelPlayback();
        
        // 然後載入錄製（相當於按 L）
        if (showDebugLogs)
            Debug.Log("[RecordingManager] 自動載入錄製...");
        LoadLatestRecording();
        
        // 確認載入成功
        if (currentRecording == null || currentRecording.origamiStepEvents.Count == 0)
        {
            Debug.LogError("[RecordingManager] 沒有可播放的錄製數據或步驟事件");
            return;
        }
        
        if (groupIndex < 0 || groupIndex >= stepGroups.Count)
        {
            Debug.LogError($"[RecordingManager] 分組索引超出範圍: {groupIndex} (共 {stepGroups.Count} 個分組)");
            return;
        }
        
        StepGroup group = stepGroups[groupIndex];
        
        if (group.stepIndices == null || group.stepIndices.Count == 0)
        {
            Debug.LogError($"[RecordingManager] 分組 '{group.groupName}' 沒有包含任何步驟");
            return;
        }
        
        // 驗證所有步驟索引
        foreach (int stepIdx in group.stepIndices)
        {
            if (stepIdx < 0 || stepIdx >= currentRecording.origamiStepEvents.Count)
            {
                Debug.LogError($"[RecordingManager] 分組 '{group.groupName}' 包含無效步驟索引: {stepIdx}");
                return;
            }
        }
        
        // 獲取 OrigamiStepGuideSimple（可選）
        var stepGuide = FindObjectOfType<OrigamiStepGuideSimple>();
        if (stepGuide == null && showDebugLogs)
        {
            Debug.LogWarning("[RecordingManager] 找不到 OrigamiStepGuideSimple，將使用錄製數據中的時間戳計算分組範圍");
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
            Debug.Log($"[RecordingManager] 播放分組 '{group.groupName}' (步驟 {stepList}): {groupStartTime:F2}s - {groupEndTime:F2}s");
        }
        
        // 設置單步驟播放標記（實際上是分組播放）
        isPlayingSingleStep = true;
        singleStepIndex = firstStepIdx; // 記錄第一個步驟索引
        singleStepEndTime = groupEndTime;
        currentPlayingGroupIndex = groupIndex; // 記錄當前分組
        
        // 如果正在播放，先停止
        bool wasPlaying = isPlaying;
        if (isPlaying)
        {
            isPlaying = false;
        }
        
        // 恢復播放狀態
        isPlaying = true;
        
        // 初始化播放環境
        if (remoteAvatar == null || !remoteAvatar.IsCreated)
        {
            Debug.LogError("[RecordingManager] RemoteAvatar 未準備好");
            isPlaying = false;
            return;
        }
        
        // 如果 Avatar 在遠端模式但剛播完步驟，需要重置以清除舊姿勢
        if (!wasPlaying && !remoteAvatar.IsLocal)
        {
            remoteAvatar.SetIsLocal(true);
            if (showDebugLogs)
                Debug.Log("[RecordingManager] 重置 Avatar：本地模式 → 遠端模式");
        }
        
        // 設置為遠端模式
        remoteAvatar.SetIsLocal(false);
        
        // 停用即時同步
        if (loopbackManager != null)
        {
            loopbackManager.enabled = false;
        }
        
        // 設置播放音頻
        if (playbackSource != null && currentRecording.audioSamples.Count > 0)
        {
            if (playbackSource.clip == null)
            {
                int sampleCount = currentRecording.audioSamples.Count / currentRecording.audioChannels;
                AudioClip audioClip = AudioClip.Create(
                    "RecordedAudio",
                    sampleCount,
                    currentRecording.audioChannels,
                    currentRecording.audioSampleRate,
                    false
                );
                audioClip.SetData(currentRecording.audioSamples.ToArray(), 0);
                playbackSource.clip = audioClip;
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
                    Debug.Log($"[RecordingManager] 強制應用起始幀 {startFrameIndex} 的 Avatar 數據");
            }
        }
        
        // 確保音頻正在播放
        if (playbackSource != null && playbackSource.clip != null)
        {
            if (!playbackSource.isPlaying)
            {
                playbackSource.time = groupStartTime;
                playbackSource.Play();
            }
        }
        
        // 設定 RemoteAvatar 位置和朝向
        SetRemoteAvatarPositionAndRotation();
        
        // 處理摺紙指示的顯示/隱藏
        if (hideOrigamiGuideInPlayback && stepGuide != null)
        {
            stepGuide.HideGuidelines();
            if (showDebugLogs)
                Debug.Log("[RecordingManager] 已隱藏摺紙指示");
        }
    }
    
    /// <summary>
    /// 播放上一個步驟組
    /// </summary>
    public void PlayPreviousStepGroup()
    {
        if (!useStepGroups || stepGroups.Count == 0)
        {
            Debug.LogWarning("[RecordingManager] 步驟分組未啟用或沒有分組");
            return;
        }
        
        int targetGroupIndex = currentPlayingGroupIndex - 1;
        if (targetGroupIndex < 0)
            targetGroupIndex = stepGroups.Count - 1; // 循環到最後一個
        
        if (showDebugLogs)
            Debug.Log($"[RecordingManager] 播放上一個分組: {targetGroupIndex + 1}");
        
        PlayStepGroup(targetGroupIndex);
    }
    
    /// <summary>
    /// 重播當前步驟組
    /// </summary>
    public void ReplayCurrentStepGroup()
    {
        if (!useStepGroups || stepGroups.Count == 0)
        {
            Debug.LogWarning("[RecordingManager] 步驟分組未啟用或沒有分組");
            return;
        }
        
        if (currentPlayingGroupIndex < 0)
        {
            // 如果還沒有播放過，播放第一個
            currentPlayingGroupIndex = 0;
        }
        
        if (showDebugLogs)
            Debug.Log($"[RecordingManager] 重播當前分組: {currentPlayingGroupIndex + 1}");
        
        PlayStepGroup(currentPlayingGroupIndex);
    }
    
    /// <summary>
    /// 播放下一個步驟組
    /// </summary>
    public void PlayNextStepGroup()
    {
        if (!useStepGroups || stepGroups.Count == 0)
        {
            Debug.LogWarning("[RecordingManager] 步驟分組未啟用或沒有分組");
            return;
        }
        
        int targetGroupIndex = currentPlayingGroupIndex + 1;
        if (targetGroupIndex >= stepGroups.Count)
            targetGroupIndex = 0; // 循環到第一個
        
        if (showDebugLogs)
            Debug.Log($"[RecordingManager] 播放下一個分組: {targetGroupIndex + 1}");
        
        PlayStepGroup(targetGroupIndex);
    }
    
    /// <summary>
    /// 設定 RemoteAvatar 的位置和旋轉（相對於相機）
    /// </summary>
    void SetRemoteAvatarPositionAndRotation()
    {
        if (!useCustomAvatarPosition || remoteAvatar == null)
            return;
        
        // 確保有相機參考
        if (playerCamera == null)
            playerCamera = Camera.main;
        
        if (playerCamera == null)
        {
            Debug.LogWarning("[RecordingManager] 找不到玩家相機，無法設定 RemoteAvatar 位置");
            return;
        }
        
        // 計算相對於相機的世界位置（與 OrigamiSyncController 相同邏輯）
        // remoteAvatarOffset.z = 前後（正值 = 前方）
        // remoteAvatarOffset.x = 左右（正值 = 右方）
        // remoteAvatarOffset.y = 上下（正值 = 下方，因為使用 TransformDirection(Vector3.down)）
        Vector3 worldPosition = playerCamera.transform.position + 
                               playerCamera.transform.forward * remoteAvatarOffset.z +
                               playerCamera.transform.right * remoteAvatarOffset.x +
                               playerCamera.transform.TransformDirection(Vector3.down) * remoteAvatarOffset.y;
        
        remoteAvatar.transform.position = worldPosition;
        
        // 設定旋轉（面向玩家）
        if (facePlayer)
        {
            Vector3 directionToPlayer = playerCamera.transform.position - remoteAvatar.transform.position;
            directionToPlayer.y = 0; // 只在水平面旋轉
            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(directionToPlayer);
                remoteAvatar.transform.rotation = lookRotation;
            }
        }
        
        // 翻轉鏡像（修正左右手對應）
        if (flipMirror)
        {
            // 翻轉 X 軸 scale，這樣左右手會正確對應
            Vector3 scale = remoteAvatar.transform.localScale;
            scale.x = -Mathf.Abs(scale.x); // 確保 X 是負數
            remoteAvatar.transform.localScale = scale;
            
            // 因為 scale.x 是負數（鏡像），LookRotation 的方向會顛倒
            // 需要旋轉 180 度來修正
            if (facePlayer)
            {
                remoteAvatar.transform.Rotate(0, 180f, 0);
            }
        }
        else
        {
            // 恢復正常 scale
            Vector3 scale = remoteAvatar.transform.localScale;
            scale.x = Mathf.Abs(scale.x); // 確保 X 是正數
            remoteAvatar.transform.localScale = scale;
        }
        
        if (showDebugLogs)
            Debug.Log($"[RecordingManager] RemoteAvatar 位置: {remoteAvatar.transform.position} (相機偏移: {remoteAvatarOffset}), 旋轉: {remoteAvatar.transform.eulerAngles}, 鏡像翻轉: {flipMirror}");
    }
    
    /// <summary>
    /// 載入最新的錄製檔案（或指定檔名）
    /// </summary>
    void LoadLatestRecording()
    {
        string[] recordings = ListSavedRecordings();
        
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
                            Debug.Log($"[RecordingManager] 找到指定檔案: {fileToLoad}");
                        break;
                    }
                }
                
                if (fileToLoad == null && showDebugLogs)
                {
                    Debug.LogWarning($"[RecordingManager] 找不到指定檔案 '{targetRecordingName}'，將載入最新檔案");
                }
            }
            
            // 如果沒有指定或找不到，載入最新的檔案
            if (fileToLoad == null)
            {
                fileToLoad = recordings[recordings.Length - 1];
                if (showDebugLogs)
                    Debug.Log($"[RecordingManager] 載入最新檔案: {fileToLoad}");
            }
            
            LoadRecording(fileToLoad);
        }
        else
        {
            Debug.LogWarning("[RecordingManager] 找不到任何錄製檔案");
        }
    }

    // ==================== 錄製功能 ====================
    
    /// <summary>
    /// 開始錄製
    /// </summary>
    public void StartRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning("[RecordingManager] 已經在錄製中");
            return;
        }
        
        if (localAvatar == null || !localAvatar.IsCreated)
        {
            Debug.LogError("[RecordingManager] LocalAvatar 未準備好");
            return;
        }
        
        if (microphoneSource == null || microphoneSource.clip == null)
        {
            Debug.LogError("[RecordingManager] 麥克風未準備好");
            return;
        }
        
        // 創建新錄製
        string recordingName = autoGenerateFilename 
            ? $"Recording_{System.DateTime.Now:yyyyMMdd_HHmmss}" 
            : "Recording";
        
        int channels = microphoneSource.clip.channels;
        currentRecording = new AvatarRecordingData(recordingName, recordingFPS, audioSampleRate, channels);
        
        isRecording = true;
        recordingTimer = 0f;  // 確保從 0 開始
        frameTimer = 0f;      // 確保從 0 開始
        lastMicPosition = Microphone.GetPosition(microphoneDevice);
        
        if (showDebugLogs)
        {
            Debug.Log($"[RecordingManager] ✓ 開始錄製: {recordingName}");
            Debug.Log($"[RecordingManager] FPS: {recordingFPS}, 音頻: {audioSampleRate} Hz");
        }
    }
    
    /// <summary>
    /// 停止錄製
    /// </summary>
    public void StopRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning("[RecordingManager] 沒有在錄製");
            return;
        }
        
        isRecording = false;
        currentRecording.duration = recordingTimer;
        
        if (showDebugLogs)
        {
            Debug.Log($"[RecordingManager] ✓ 停止錄製");
            Debug.Log($"[RecordingManager] 錄製時長: {recordingTimer:F2} 秒");
            Debug.Log($"[RecordingManager] 總幀數: {currentRecording.frames.Count}");
        }
    }
    
    /// <summary>
    /// 錄製單幀數據（僅動作）
    /// </summary>
    void RecordFrame()
    {
        if (localAvatar == null || !localAvatar.IsCreated)
            return;
        
        AvatarFrameData frame = new AvatarFrameData();
        
        // **關鍵修正：使用音頻樣本數計算精確時間戳**
        // 這樣 frame timestamp 就會和音頻完美對齊
        int totalAudioSamples = currentRecording.audioSamples.Count / currentRecording.audioChannels;
        frame.timestamp = (float)totalAudioSamples / currentRecording.audioSampleRate;
        
        // 錄製 Avatar 串流數據（動作 + 嘴型）
        frame.avatarStreamData = RecordAvatarStream();
        
        currentRecording.frames.Add(frame);
        
        // 同時持續錄製音頻
        RecordAudioSamples();
    }
    
    /// <summary>
    /// 錄製 Avatar 串流數據（包含動作和嘴型）
    /// </summary>
    byte[] RecordAvatarStream()
    {
        // 使用 Meta Avatar SDK 的串流功能（新版 API）
        NativeArray<byte> nativeBuffer = default;
        
        try
        {
            uint bytesWritten = localAvatar.RecordStreamData_AutoBuffer(
                streamLOD,
                ref nativeBuffer
            );
            
            if (bytesWritten > 0 && nativeBuffer.IsCreated)
            {
                byte[] streamData = new byte[bytesWritten];
                NativeArray<byte>.Copy(nativeBuffer, streamData, (int)bytesWritten);
                
                if (showDebugLogs && currentRecording.frames.Count % 30 == 0)
                {
                    Debug.Log($"[RecordingManager] 錄製第 {currentRecording.frames.Count} 幀 - 動作數據: {bytesWritten} bytes");
                }
                
                return streamData;
            }
            else
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[RecordingManager] 錄製動作數據失敗: bytesWritten={bytesWritten}");
            }
        }
        finally
        {
            if (nativeBuffer.IsCreated)
                nativeBuffer.Dispose();
        }
        
        return null;
    }
    
    /// <summary>
    /// 連續錄製音頻樣本
    /// </summary>
    void RecordAudioSamples()
    {
        if (microphoneSource == null || microphoneSource.clip == null)
            return;
        
        int currentPosition = Microphone.GetPosition(microphoneDevice);
        if (currentPosition < 0 || currentPosition == lastMicPosition)
            return;
        
        int channels = currentRecording.audioChannels;
        int totalSamples = microphoneSource.clip.samples;
        
        // 計算新樣本數量
        int samplesAvailable;
        if (currentPosition < lastMicPosition)
        {
            // 循環緩衝：從 lastMicPosition 到結尾 + 從開頭到 currentPosition
            samplesAvailable = (totalSamples - lastMicPosition) + currentPosition;
        }
        else
        {
            samplesAvailable = currentPosition - lastMicPosition;
        }
        
        if (samplesAvailable > 0 && samplesAvailable < totalSamples)
        {
            float[] samples = new float[samplesAvailable * channels];
            
            try
            {
                // 正確處理循環緩衝
                if (currentPosition < lastMicPosition)
                {
                    // 分兩段讀取
                    int firstPartSamples = totalSamples - lastMicPosition;
                    int secondPartSamples = currentPosition;
                    
                    float[] firstPart = new float[firstPartSamples * channels];
                    float[] secondPart = new float[secondPartSamples * channels];
                    
                    microphoneSource.clip.GetData(firstPart, lastMicPosition);
                    microphoneSource.clip.GetData(secondPart, 0);
                    
                    // 合併兩段
                    System.Array.Copy(firstPart, 0, samples, 0, firstPart.Length);
                    System.Array.Copy(secondPart, 0, samples, firstPart.Length, secondPart.Length);
                }
                else
                {
                    // 一次讀取
                    microphoneSource.clip.GetData(samples, lastMicPosition);
                }
                
                // 添加到連續音頻流
                currentRecording.audioSamples.AddRange(samples);
                
                if (showDebugLogs && currentRecording.frames.Count % 30 == 0)
                {
                    Debug.Log($"[RecordingManager] 錄製音頻: {samplesAvailable} 樣本，總計 {currentRecording.audioSamples.Count / channels} 樣本");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RecordingManager] 讀取音頻失敗: {e.Message}");
            }
        }
        
        lastMicPosition = currentPosition;
    }

    // ==================== 播放功能 ====================
    
    /// <summary>
    /// 開始播放錄製
    /// </summary>
    public void StartPlayback()
    {
        if (currentRecording == null || currentRecording.frames.Count == 0)
        {
            Debug.LogError("[RecordingManager] 沒有可播放的錄製數據");
            return;
        }
        
        if (remoteAvatar == null || !remoteAvatar.IsCreated)
        {
            Debug.LogError("[RecordingManager] RemoteAvatar 未準備好");
            return;
        }
        
        // **關鍵修正：先應用一幀數據初始化 Avatar，再停用同步**
        if (currentRecording.frames.Count > 0 && currentRecording.frames[0].avatarStreamData != null)
        {
            // 先設置為遠端模式
            remoteAvatar.SetIsLocal(false);
            
            // 立即應用第一幀數據，初始化 Avatar LOD 和渲染狀態
            NativeArray<byte> initData = new NativeArray<byte>(currentRecording.frames[0].avatarStreamData, Allocator.Temp);
            try
            {
                remoteAvatar.ApplyStreamData(initData);
                if (showDebugLogs)
                    Debug.Log("[RecordingManager] ✓ Remote Avatar 初始化完成");
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
                Debug.Log("[RecordingManager] 已停止即時同步");
        }
        
        // 設置播放音頻
        if (playbackSource != null && currentRecording.audioSamples.Count > 0)
        {
            // 停止之前的音頻（如果有）
            if (playbackSource.isPlaying)
            {
                playbackSource.Stop();
            }
            
            // 重新創建 AudioClip 確保從頭開始播放
            int sampleCount = currentRecording.audioSamples.Count / currentRecording.audioChannels;
            AudioClip audioClip = AudioClip.Create(
                "RecordedAudio",
                sampleCount,
                currentRecording.audioChannels,
                currentRecording.audioSampleRate,
                false
            );
            audioClip.SetData(currentRecording.audioSamples.ToArray(), 0);
            playbackSource.clip = audioClip;
            playbackSource.time = 0f;  // 確保從頭開始
            playbackSource.Play();
            
            if (showDebugLogs)
                Debug.Log($"[RecordingManager] ✓ 音頻已設置: {sampleCount} 樣本, {currentRecording.audioChannels} 聲道, {currentRecording.audioSampleRate} Hz");
        }
        
        isPlaying = true;
        playbackFrameIndex = 0;
        
        // **關鍵修正：將第一幀的 timestamp 作為起點（歸零）**
        playbackTimer = currentRecording.frames[0].timestamp;
        
        // **同步音頻播放位置與第一幀時間戳**
        if (playbackSource != null && playbackSource.clip != null)
        {
            playbackSource.time = currentRecording.frames[0].timestamp;
            if (showDebugLogs)
                Debug.Log($"[RecordingManager] 音頻播放位置設定為: {playbackSource.time:F3}s");
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[RecordingManager] ✓ 開始播放: {currentRecording.recordingName}");
            Debug.Log($"[RecordingManager] 總幀數: {currentRecording.frames.Count}, 時長: {currentRecording.duration:F2}s");
            Debug.Log($"[RecordingManager] playbackTimer 起點: {playbackTimer:F3}s");
        }
    }
    
    /// <summary>
    /// 停止播放
    /// </summary>
    public void StopPlayback()
    {
        if (!isPlaying)
            return;
        
        isPlaying = false;
        
        // 暫停音頻播放（保留位置和 clip）
        if (playbackSource != null && playbackSource.isPlaying)
        {
            playbackSource.Pause();
        }
        
        // 保持 Avatar 在遠端模式，這樣下次播放時可以繼續接收數據
        // 不要在這裡恢復本地模式，否則下次播放會卡住
        
        if (showDebugLogs)
            Debug.Log($"[RecordingManager] ⏸ 暫停播放 (幀: {playbackFrameIndex}/{currentRecording.frames.Count})");
    }
    
    /// <summary>
    /// 取消播放並清空狀態（初始化動畫）
    /// </summary>
    public void CancelPlayback()
    {
        // 即使 isPlaying 為 false 也允許取消，因為可能剛播放完步驟停在那裡
        if (!isPlaying && showDebugLogs)
        {
            Debug.Log("[RecordingManager] isPlaying=false，但仍執行清理以恢復即時同步");
        }
        
        // 停止播放
        isPlaying = false;
        
        // 停止音頻
        if (playbackSource != null)
        {
            playbackSource.Stop();
            playbackSource.clip = null;
        }
        
        // 重置 Avatar 為本地模式
        if (remoteAvatar != null)
        {
            remoteAvatar.SetIsLocal(true);
        }
        
        // 恢復即時同步
        if (loopbackManager != null)
        {
            loopbackManager.enabled = true;
        }
        
        // 不重置紙張動畫，保持在當前位置
        // 用戶希望回播時紙張不跳回原點
        // var stepGuide = FindObjectOfType<OrigamiStepGuideSimple>();
        // if (stepGuide != null)
        // {
        //     stepGuide.ResetToStart();
        // }
        // 
        // var syncController = FindObjectOfType<OrigamiSyncController>();
        // if (syncController != null && syncController.alembicPlayer != null)
        // {
        //     syncController.alembicPlayer.CurrentTime = 0f;
        // }
        
        // 重置內部狀態
        playbackFrameIndex = 0;
        playbackTimer = 0f;
        lastSyncedStep = -1;
        
        // 重置單步驟播放標記
        isPlayingSingleStep = false;
        singleStepIndex = -1;
        singleStepEndTime = -1f;
        
        // 恢復摺紙指示
        if (hideOrigamiGuideInPlayback)
        {
            var stepGuide = FindObjectOfType<OrigamiStepGuideSimple>();
            if (stepGuide != null)
            {
                stepGuide.ShowGuidelines();
                if (showDebugLogs)
                    Debug.Log("[RecordingManager] 已恢復摺紙指示");
            }
        }
        
        if (showDebugLogs)
            Debug.Log("[RecordingManager] ✓ 已取消播放並重置所有狀態");
    }
    
    /// <summary>
    /// 完全停止播放並恢復即時同步
    /// </summary>
    void CompletelyStopPlayback()
    {
        isPlaying = false;
        
        // 重置單步驟播放標記
        isPlayingSingleStep = false;
        singleStepIndex = -1;
        singleStepEndTime = -1f;
        
        // 停止音頻播放
        if (playbackSource != null)
        {
            playbackSource.Stop();
            playbackSource.clip = null;
        }
        
        // **恢復 RemoteAvatar 為本地模式（接收即時同步）**
        if (remoteAvatar != null)
        {
            remoteAvatar.SetIsLocal(true);
            if (showDebugLogs)
                Debug.Log("[RecordingManager] RemoteAvatar 恢復為本地模式 (IsLocal=true)");
        }
        
        // 恢復即時同步
        if (loopbackManager != null)
        {
            loopbackManager.enabled = true;
            if (showDebugLogs)
                Debug.Log("[RecordingManager] 已恢復即時同步");
        }
    }
    
    /// <summary>
    /// 播放幀數據
    /// </summary>
    void PlaybackFrame()
    {
        if (playbackFrameIndex >= currentRecording.frames.Count)
        {
            // 播放完畢，完全停止並恢復即時同步
            CompletelyStopPlayback();
            if (showDebugLogs)
                Debug.Log("[RecordingManager] ✓ 播放完畢");
            return;
        }
        
        // 檢查單步驟播放是否結束
        if (isPlayingSingleStep)
        {
            float currentTime = useAudioSync && playbackSource != null && playbackSource.isPlaying
                ? playbackSource.time
                : playbackTimer;
            
            if (currentTime >= singleStepEndTime)
            {
                if (showDebugLogs)
                    Debug.Log($"[RecordingManager] ✓ 步驟 {singleStepIndex + 1} 播放完畢，停在 {currentTime:F2}s");
                
                // 停止播放但保持在當前位置（不重置狀態）
                StopPlayback();
                
                // 重置單步驟播放標記
                isPlayingSingleStep = false;
                singleStepIndex = -1;
                singleStepEndTime = -1f;
                
                return;
            }
        }
        
        // 使用音頻同步模式
        if (useAudioSync && playbackSource != null && playbackSource.isPlaying)
        {
            // 使用音頻的實際播放時間作為基準
            float audioTime = playbackSource.time;
            
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
                    Debug.Log($"[RecordingManager] 音頻同步: 音頻時間 {audioTime:F3}s → 幀 {playbackFrameIndex}/{currentRecording.frames.Count}");
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
                Debug.LogWarning($"[RecordingManager] 第 {playbackFrameIndex} 幀沒有動作數據");
            }
            
            // 同步摺紙步驟
            SyncOrigamiStep(frame.timestamp);
            
            playbackFrameIndex++;
            playbackTimer += Time.deltaTime;
            
            // 每 30 幀顯示一次進度
            if (showDebugLogs && playbackFrameIndex % 30 == 0)
            {
                Debug.Log($"[RecordingManager] 播放進度: {playbackFrameIndex}/{currentRecording.frames.Count}");
            }
        }
    }
    
    /// <summary>
    /// 應用 Avatar 串流數據到遠端 Avatar
    /// </summary>
    void ApplyAvatarStream(byte[] streamData)
    {
        if (streamData == null || streamData.Length == 0)
        {
            if (showDebugLogs)
                Debug.LogWarning("[RecordingManager] 串流數據為空");
            return;
        }
        
        if (remoteAvatar == null || !remoteAvatar.IsCreated)
        {
            if (showDebugLogs)
                Debug.LogWarning("[RecordingManager] RemoteAvatar 未創建");
            return;
        }
        
        // 使用 Meta Avatar SDK 應用串流數據
        // 需要將 byte[] 轉換為 NativeArray
        NativeArray<byte> nativeData = new NativeArray<byte>(streamData, Allocator.Temp);
        
        try
        {
            bool success = remoteAvatar.ApplyStreamData(nativeData);
            
            if (showDebugLogs)
            {
                if (success)
                {
                    Debug.Log($"[RecordingManager] ✓ 應用串流數據成功 ({streamData.Length} bytes)");
                }
                else
                {
                    Debug.LogWarning($"[RecordingManager] ✗ 應用串流數據失敗");
                }
            }
        }
        finally
        {
            nativeData.Dispose();
        }
    }
    
    // 音頻播放已改為在 StartPlayback 時設置完整音頻流

    // ==================== 存檔功能 ====================
    
    /// <summary>
    /// 儲存錄製到檔案
    /// </summary>
    public void SaveRecording(string filename = null)
    {
        if (currentRecording == null || currentRecording.frames.Count == 0)
        {
            Debug.LogError("[RecordingManager] 沒有可儲存的錄製數據");
            return;
        }
        
        if (isRecording)
        {
            Debug.LogWarning("[RecordingManager] 請先停止錄製");
            return;
        }
        
        string savePath = GetSaveFilePath(filename ?? currentRecording.recordingName);
        
        try
        {
            // 確保資料夾存在
            string directory = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log($"[RecordingManager] 創建目錄: {directory}");
            }
            
            // 序列化並儲存
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream stream = new FileStream(savePath, FileMode.Create))
            {
                formatter.Serialize(stream, currentRecording);
                stream.Flush();
            }
            
            // 驗證檔案是否存在
            if (File.Exists(savePath))
            {
                FileInfo fileInfo = new FileInfo(savePath);
                Debug.Log($"[RecordingManager] ✓ 錄製已儲存: {savePath}");
                Debug.Log($"[RecordingManager] 檔案大小: {fileInfo.Length / 1024f:F1} KB");
                Debug.Log($"[RecordingManager] 完整路徑: {Path.GetFullPath(savePath)}");
            }
            else
            {
                Debug.LogError($"[RecordingManager] 檔案儲存後未找到: {savePath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RecordingManager] 儲存失敗: {e.Message}");
            Debug.LogError($"[RecordingManager] 錯誤堆疊: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// 從檔案載入錄製
    /// </summary>
    public bool LoadRecording(string filename)
    {
        string loadPath = GetSaveFilePath(filename);
        
        if (!File.Exists(loadPath))
        {
            Debug.LogError($"[RecordingManager] 檔案不存在: {loadPath}");
            return false;
        }
        
        try
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream stream = new FileStream(loadPath, FileMode.Open))
            {
                currentRecording = (AvatarRecordingData)formatter.Deserialize(stream);
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"[RecordingManager] ✓ 錄製已載入: {loadPath}");
                Debug.Log($"[RecordingManager] 時長: {currentRecording.duration:F2} 秒");
                Debug.Log($"[RecordingManager] 幀數: {currentRecording.frames.Count}");
            }
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RecordingManager] 載入失敗: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 列出所有已儲存的錄製檔案
    /// </summary>
    public string[] ListSavedRecordings()
    {
        string folderPath = GetSaveFolderPath();
        
        if (!Directory.Exists(folderPath))
            return new string[0];
        
        string[] files = Directory.GetFiles(folderPath, "*.recording");
        
        // 去除路徑和副檔名
        for (int i = 0; i < files.Length; i++)
        {
            files[i] = Path.GetFileNameWithoutExtension(files[i]);
        }
        
        return files;
    }

    // ==================== 輔助方法 ====================
    
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
        
        // 不再需要容差檢查，因為錄製時已經用音頻樣本數計算時間戳
        // frame timestamp 和 audio time 現在完全對齊
        
        return closestIndex;
    }
    
    string GetSaveFolderPath()
    {
        // 使用 Application.dataPath 來取得 Assets 資料夾的完整路徑
        // Application.dataPath 指向專案的 Assets 資料夾
        string fullPath = Path.Combine(Application.dataPath, "Recordings");
        return fullPath;
    }
    
    string GetSaveFilePath(string filename)
    {
        return Path.Combine(GetSaveFolderPath(), filename + ".recording");
    }

    // ==================== UI 顯示 ====================
    
    void OnGUI()
    {
        if (!showRecordingUI)
            return;
        
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 18;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(15, 15, 10, 10);
        
        float width = 350f;
        float height = 150f;
        float xPos = Screen.width - width - 20f;
        float yPos = 20f;
        
        GUI.Box(new Rect(xPos, yPos, width, height), "", style);
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 16;
        labelStyle.normal.textColor = Color.white;
        
        float yOffset = yPos + 15f;
        
        if (isRecording)
        {
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 30f),
                $"🔴 錄製中: {recordingTimer:F1}s", labelStyle);
            yOffset += 30f;
            
            if (currentRecording != null)
            {
                GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                    $"幀數: {currentRecording.frames.Count}", labelStyle);
                yOffset += 25f;
                
                float progress = recordingTimer / maxRecordingDuration;
                DrawProgressBar(new Rect(xPos + 15f, yOffset, width - 30f, 20f), progress);
            }
        }
        else if (isPlaying)
        {
            // 使用音頻的實際時間顯示
            float displayTime = (useAudioSync && playbackSource != null && playbackSource.isPlaying) 
                ? playbackSource.time 
                : playbackTimer;
            
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 30f),
                $"▶ 播放中: {displayTime:F1}s / {currentRecording.duration:F1}s", labelStyle);
            yOffset += 30f;
            
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                $"幀: {playbackFrameIndex}/{currentRecording.frames.Count}", labelStyle);
            yOffset += 25f;
            
            float progress = displayTime / currentRecording.duration;
            DrawProgressBar(new Rect(xPos + 15f, yOffset, width - 30f, 20f), progress);
        }
        else
        {
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 30f),
                "⏸ 就緒", labelStyle);
            
            if (currentRecording != null)
            {
                yOffset += 35f;
                GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                    $"已載入: {currentRecording.recordingName}", labelStyle);
                yOffset += 25f;
                GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                    $"時長: {currentRecording.duration:F1}s, {currentRecording.frames.Count} 幀", labelStyle);
            }
        }
    }
    
    void DrawProgressBar(Rect rect, float progress)
    {
        // 背景
        GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        
        // 進度條
        GUI.color = Color.Lerp(Color.green, Color.red, progress);
        Rect progressRect = new Rect(rect.x, rect.y, rect.width * progress, rect.height);
        GUI.DrawTexture(progressRect, Texture2D.whiteTexture);
        
        GUI.color = Color.white;
    }

    // ==================== 摺紙步驟記錄與播放 ====================
    
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
                // 在播放模式下，不要調用 JumpToStep（它會重置 Alembic）
                // 只記錄步驟變化用於調試
                if (targetStep != lastSyncedStep)
                {
                    lastSyncedStep = targetStep;
                    
                    if (showDebugLogs)
                        Debug.Log($"[RecordingManager] 切換到步驟 {targetStep}");
                }
                
                // 持續更新 Alembic 動畫（每幀都更新）
                var syncController = FindObjectOfType<OrigamiSyncController>();
                if (syncController != null && syncController.alembicPlayer != null)
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
                    float alembicTime = targetProgress * syncController.alembicPlayer.Duration;
                    
                    syncController.alembicPlayer.CurrentTime = alembicTime;
                    
                    if (showDebugLogs && Time.frameCount % 30 == 0)
                        Debug.Log($"[RecordingManager] Alembic 播放: 步驟 {targetStep}, 進度 {stepProgress:F2}, 時間 {alembicTime:F2}s");
                }
            }
        }
    }
    
    /// <summary>
    /// 記錄摺紙步驟切換事件（由 OrigamiStepGuide 呼叫）
    /// </summary>
    public void RecordOrigamiStepEvent(int stepIndex, string stepName)
    {
        if (!isRecording || currentRecording == null)
        {
            Debug.LogWarning("[RecordingManager] 沒有在錄製，無法記錄摺紙步驟事件");
            return;
        }
        
        // 使用音頻樣本數計算精確時間戳（與 frame timestamp 一致）
        int totalAudioSamples = currentRecording.audioSamples.Count / currentRecording.audioChannels;
        float timestamp = (float)totalAudioSamples / currentRecording.audioSampleRate;
        
        OrigamiStepEvent stepEvent = new OrigamiStepEvent(timestamp, stepIndex, stepName);
        currentRecording.origamiStepEvents.Add(stepEvent);
        
        if (showDebugLogs)
        {
            Debug.Log($"[RecordingManager] ✓ 記錄摺紙步驟事件: 步驟 {stepIndex} '{stepName}' 於 {timestamp:F3}s");
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
                break; // 已經超過當前時間
            }
        }
        
        return currentStep;
    }

    // ==================== 公開方法 ====================
    
    public bool IsRecording => isRecording;
    public bool IsPlaying => isPlaying;
    public float RecordingDuration => recordingTimer;
    public int RecordedFrames => currentRecording?.frames.Count ?? 0;
    public AvatarRecordingData CurrentRecording => currentRecording;
    
    /// <summary>
    /// 跳轉到指定時間點（秒）
    /// 同時同步 Avatar、音頻、摺紙步驟
    /// </summary>
    public void JumpToTime(float targetTime)
    {
        if (currentRecording == null || !isPlaying)
        {
            Debug.LogWarning("[RecordingManager] 無法跳轉：沒有正在播放的錄製檔案");
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
            if (remoteAvatar != null)
            {
                AvatarFrameData frame = currentRecording.frames[targetFrameIndex];
                if (frame.avatarStreamData != null && frame.avatarStreamData.Length > 0)
                {
                    ApplyAvatarStream(frame.avatarStreamData);
                }
                if (showDebugLogs)
                    Debug.Log($"[RecordingManager] 跳轉 Avatar 到第 {targetFrameIndex} 幀");
            }
            
            // 3. 跳轉音頻播放位置
            if (playbackSource != null && playbackSource.clip != null)
            {
                int audioSamplePosition = (int)(targetTime * currentRecording.audioSampleRate * currentRecording.audioChannels);
                audioSamplePosition = Mathf.Clamp(audioSamplePosition, 0, currentRecording.audioSamples.Count - 1);
                playbackSource.timeSamples = audioSamplePosition;
                
                if (showDebugLogs)
                    Debug.Log($"[RecordingManager] 跳轉音頻到 {audioSamplePosition} samples");
            }
            
            // 4. 直接更新 Alembic 動畫（不調用 JumpToStep 避免重置）
            int targetStep = GetCurrentOrigamiStep(targetTime);
            if (targetStep >= 0)
            {
                var stepGuideSimple = FindObjectOfType<OrigamiStepGuideSimple>();
                var syncController = FindObjectOfType<OrigamiSyncController>();
                
                if (syncController != null && syncController.alembicPlayer != null && stepGuideSimple != null)
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
                    float alembicTime = targetProgress * syncController.alembicPlayer.Duration;
                    
                    syncController.alembicPlayer.CurrentTime = alembicTime;
                    
                    if (showDebugLogs)
                        Debug.Log($"[RecordingManager] 跳轉 Alembic 到 {alembicTime:F2}s (進度: {targetProgress:F2})");
                }
            }
            
            // 更新內部播放計時器和幀索引（支持向前/向後跳轉）
            playbackFrameIndex = targetFrameIndex;
            playbackTimer = targetTime;
            recordingTimer = targetTime;
            
            // 重置同步步驟記錄，讓 SyncOrigamiStep 重新同步
            lastSyncedStep = -1;
            
            if (showDebugLogs)
                Debug.Log($"[RecordingManager] ✓ 已跳轉到 {targetTime:F2} 秒");
        }
        else
        {
            Debug.LogWarning($"[RecordingManager] 找不到對應時間的幀：{targetTime:F2}s");
        }
    }
}
