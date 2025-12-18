using UnityEngine;
using Oculus.Avatar2;
using System.Collections.Generic;
using CAPI = Oculus.Avatar2.CAPI;

/// <summary>
/// Avatar 動作與音頻錄製與播放系統
/// 用於錄製第一人稱 Avatar 動作和聲音並在第三人稱 Avatar 上播放
/// 適用於異步學習、動作示範等場景
/// </summary>
public class AvatarRecordingPlayback : MonoBehaviour
{
    [Header("Avatar 設定")]
    [Tooltip("本地 Avatar - 用於錄製（第一人稱）")]
    public OvrAvatarEntity localAvatar;
    
    [Tooltip("遠端 Avatar - 用於播放（第三人稱）")]
    public OvrAvatarEntity remoteAvatar;

    [Header("音頻設定")]
    [Tooltip("本地 Avatar 的麥克風 AudioSource（通常在 LipSyncMicInput 組件上）")]
    public AudioSource localAudioSource;
    
    [Tooltip("遠端 Avatar 的音頻輸出組件")]
    public AudioSource remoteAudioSource;
    
    [Tooltip("是否錄製音頻（嘴唇同步和聲音）")]
    public bool recordAudio = true;
    
    [Tooltip("麥克風設備名稱（留空使用默認麥克風）")]
    public string microphoneDevice = null;

    [Header("錄製設定")]
    [Tooltip("Stream LOD 等級 - Full(最高品質), High, Medium, Low(最小數據)")]
    public OvrAvatarEntity.StreamLOD streamLOD = OvrAvatarEntity.StreamLOD.High;
    
    [Tooltip("是否自動開始錄製")]
    public bool autoStartRecording = true;
    
    [Tooltip("最大緩衝幀數（避免記憶體溢出）")]
    public int maxBufferFrames = 1000;

    [Header("播放設定")]
    [Tooltip("播放速度倍率（1.0 = 正常速度）")]
    [Range(0.1f, 2.0f)]
    public float playbackSpeed = 1.0f;

    [Header("調試資訊")]
    [Tooltip("顯示調試日誌")]
    public bool showDebugLogs = true;

    // 儲存錄製數據的緩衝區
    private Queue<FrameData> recordedDataQueue = new Queue<FrameData>();
    private bool isRecording = false;
    private bool isPlaying = false;
    private float playbackTimer = 0f;

    // 音頻相關
    private OvrAvatarLipSyncContext localLipSyncContext;
    private OvrAvatarLipSyncContext remoteLipSyncContext;
    private List<float> audioBuffer = new List<float>();
    
    /// <summary>
    /// 單幀數據結構（包含動作和音頻）
    /// </summary>
    [System.Serializable]
    private class FrameData
    {
        public byte[] motionData;      // 動作數據
        public float[] audioSamples;   // 音頻樣本
        public int audioChannels;      // 音頻通道數
        
        public FrameData(byte[] motion, float[] audio = null, int channels = 0)
        {
            motionData = motion;
            audioSamples = audio;
            audioChannels = channels;
        }
    }

    void Start()
    {
        // 確保 Avatar 設定正確
        SetupAvatars();

        if (autoStartRecording)
        {
            StartRecording();
        }
    }

    void Update()
    {
        // 錄製模式
        if (isRecording && localAvatar != null && localAvatar.IsCreated)
        {
            RecordFrame();
        }

        // 播放模式（考慮播放速度）
        if (isPlaying && remoteAvatar != null && remoteAvatar.IsCreated)
        {
            playbackTimer += Time.deltaTime * playbackSpeed;
            
            // 假設 60 FPS
            float frameTime = 1f / 60f;
            if (playbackTimer >= frameTime)
            {
                playbackTimer -= frameTime;
                PlaybackFrame();
            }
        }
    }

    #region 初始化

    /// <summary>
    /// 設置 Avatar 配置
    /// </summary>
    private void SetupAvatars()
    {
        if (localAvatar != null)
        {
            localAvatar.SetIsLocal(true);  // 設為本地 Avatar
            Log("本地 Avatar 已設置為錄製模式");
            
            // 設置本地音頻
            if (recordAudio)
            {
                SetupLocalAudio();
            }
        }
        else
        {
            LogWarning("未指定本地 Avatar！");
        }

        if (remoteAvatar != null)
        {
            remoteAvatar.SetIsLocal(false); // 設為遠端 Avatar
            Log("遠端 Avatar 已設置為播放模式");
            
            // 設置遠端音頻
            if (recordAudio)
            {
                SetupRemoteAudio();
            }
        }
        else
        {
            LogWarning("未指定遠端 Avatar！");
        }
    }

    /// <summary>
    /// 設置本地 Avatar 的音頻捕捉
    /// </summary>
    private void SetupLocalAudio()
    {
        // 獲取 LipSync Context
        localLipSyncContext = localAvatar.GetComponentInChildren<OvrAvatarLipSyncContext>();
        
        if (localLipSyncContext == null)
        {
            LogWarning("本地 Avatar 沒有 OvrAvatarLipSyncContext 組件！");
        }
        else
        {
            Log("✓ 本地 Avatar 音頻系統已就緒");
        }

        // 檢查本地音頻源（麥克風）
        if (localAudioSource == null)
        {
            // 嘗試自動找到 AudioSource
            AudioSource[] audioSources = localAvatar.GetComponentsInChildren<AudioSource>();
            foreach (var source in audioSources)
            {
                if (source.clip != null && Microphone.IsRecording(microphoneDevice))
                {
                    localAudioSource = source;
                    break;
                }
            }
            
            if (localAudioSource == null && audioSources.Length > 0)
            {
                localAudioSource = audioSources[0];
            }
            
            if (localAudioSource != null)
            {
                Log($"✓ 找到本地音頻源: {localAudioSource.gameObject.name}");
            }
            else
            {
                LogWarning("未找到本地 AudioSource！音頻錄製可能無法工作。");
            }
        }
        
        // 檢查並警告虛擬音頻設備
        if (!string.IsNullOrEmpty(microphoneDevice))
        {
            if (microphoneDevice.Contains("Oculus") || microphoneDevice.Contains("Virtual"))
            {
                LogWarning("⚠ 檢測到虛擬音頻設備（Oculus Virtual Audio Device）");
                LogWarning("在 Unity Editor 中，虛擬設備無法捕捉音頻！");
                LogWarning("建議：1) 使用 PC 的實體麥克風，或 2) Build 到 Quest 設備測試");
            }
        }
    }

    /// <summary>
    /// 設置遠端 Avatar 的音頻播放
    /// </summary>
    private void SetupRemoteAudio()
    {
        // 獲取或創建 AudioSource
        if (remoteAudioSource == null)
        {
            remoteAudioSource = remoteAvatar.GetComponentInChildren<AudioSource>();
            if (remoteAudioSource == null)
            {
                remoteAudioSource = remoteAvatar.gameObject.AddComponent<AudioSource>();
                remoteAudioSource.spatialBlend = 1.0f; // 3D 音效
                remoteAudioSource.loop = false;
            }
        }

        // 獲取或創建 LipSync Context
        remoteLipSyncContext = remoteAvatar.GetComponentInChildren<OvrAvatarLipSyncContext>();
        
        if (remoteLipSyncContext == null)
        {
            // 創建 LipSync Context
            var lipSyncObj = new GameObject("RemoteLipSyncContext");
            lipSyncObj.transform.SetParent(remoteAvatar.transform);
            remoteLipSyncContext = lipSyncObj.AddComponent<OvrAvatarLipSyncContext>();
            
            // 設置為手動模式（我們會手動提供音頻數據）
            remoteLipSyncContext.CaptureAudio = false;
            
            // 關聯到遠端 Avatar
            remoteAvatar.SetLipSync(remoteLipSyncContext);
            
            Log("✓ 已為遠端 Avatar 創建 LipSync Context");
        }
        else
        {
            Log("✓ 遠端 Avatar 音頻系統已就緒");
        }
    }

    #endregion

    #region 錄製功能

    /// <summary>
    /// 開始錄製 Avatar 動作
    /// </summary>
    public void StartRecording()
    {
        if (localAvatar == null)
        {
            LogError("本地 Avatar 未指定！");
            return;
        }

        if (!localAvatar.IsCreated)
        {
            LogError("本地 Avatar 未準備好！請等待 Avatar 載入完成。");
            return;
        }

        // 清空舊數據
        recordedDataQueue.Clear();

        // 啟動錄製
        if (localAvatar.RecordStart())
        {
            isRecording = true;
            Log($"✓ 開始錄製 Avatar 動作（LOD: {streamLOD}）");
        }
        else
        {
            LogError("✗ 無法開始錄製");
        }
    }

    /// <summary>
    /// 停止錄製
    /// </summary>
    public void StopRecording()
    {
        if (localAvatar != null && localAvatar.IsCreated)
        {
            localAvatar.RecordStop();
            isRecording = false;
            Log($"✓ 停止錄製 - 共錄製 {recordedDataQueue.Count} 幀");
        }
    }

    /// <summary>
    /// 錄製當前幀的數據（動作 + 音頻）
    /// </summary>
    private void RecordFrame()
    {
        // 錄製當前幀的 Stream 數據（動作）
        byte[] motionData = localAvatar.RecordStreamData(streamLOD);
        
        if (motionData != null && motionData.Length > 0)
        {
            float[] audioSamples = null;
            int audioChannels = 0;
            
            // 錄製音頻（如果啟用）
            if (recordAudio && localAudioSource != null && localAudioSource.isPlaying)
            {
                // 從麥克風獲取音頻數據
                AudioClip clip = localAudioSource.clip;
                if (clip != null)
                {
                    int sampleCount = clip.frequency / 60; // 假設 60 FPS
                    audioSamples = new float[sampleCount * clip.channels];
                    audioChannels = clip.channels;
                    
                    int position = Microphone.GetPosition(microphoneDevice);
                    if (position > sampleCount)
                    {
                        clip.GetData(audioSamples, position - sampleCount);
                    }
                }
            }
            
            // 創建包含動作和音頻的幀數據
            FrameData frame = new FrameData(motionData, audioSamples, audioChannels);
            
            // 儲存數據到隊列
            recordedDataQueue.Enqueue(frame);
            
            // 限制隊列大小（避免記憶體溢出）
            if (recordedDataQueue.Count > maxBufferFrames)
            {
                recordedDataQueue.Dequeue();
            }
        }
    }

    /// <summary>
    /// 切換錄製狀態
    /// </summary>
    public void ToggleRecording()
    {
        if (isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    #endregion

    #region 播放功能

    /// <summary>
    /// 開始播放錄製的動作
    /// </summary>
    public void StartPlayback()
    {
        if (remoteAvatar == null)
        {
            LogError("遠端 Avatar 未指定！");
            return;
        }

        if (!remoteAvatar.IsCreated)
        {
            LogError("遠端 Avatar 未準備好！");
            return;
        }

        if (recordedDataQueue.Count == 0)
        {
            LogWarning("沒有錄製的數據可播放！請先錄製動作。");
            return;
        }

        isPlaying = true;
        playbackTimer = 0f;
        Log($"✓ 開始播放 - 共 {recordedDataQueue.Count} 幀（速度: {playbackSpeed}x）");
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    public void StopPlayback()
    {
        isPlaying = false;
        Log("✓ 停止播放");
    }

    /// <summary>
    /// 播放下一幀數據（動作 + 音頻）
    /// </summary>
    private void PlaybackFrame()
    {
        if (recordedDataQueue.Count > 0)
        {
            FrameData frame = recordedDataQueue.Dequeue();
            
            // 應用動作數據到遠端 Avatar
            if (remoteAvatar.ApplyStreamData(frame.motionData))
            {
                // 成功應用動作數據
            }
            else
            {
                LogWarning("無法應用 Stream 數據到遠端 Avatar");
            }
            
            // 播放音頻數據（如果有）
            if (recordAudio && frame.audioSamples != null && frame.audioSamples.Length > 0)
            {
                PlayAudioFrame(frame.audioSamples, frame.audioChannels);
            }
        }
        else
        {
            // 播放完畢
            isPlaying = false;
            Log("✓ 播放完成");
        }
    }

    /// <summary>
    /// 播放音頻幀到遠端 Avatar
    /// </summary>
    private void PlayAudioFrame(float[] audioSamples, int channels)
    {
        if (remoteLipSyncContext != null)
        {
            // 將音頻樣本發送到 LipSync 系統進行嘴唇同步
            remoteLipSyncContext.ProcessAudioSamples(audioSamples, channels);
        }
        
        // 播放音頻到 AudioSource（可選）
        if (remoteAudioSource != null && remoteAudioSource.enabled)
        {
            // 創建臨時 AudioClip 來播放這幀音頻
            int sampleRate = 48000; // 標準採樣率
            AudioClip tempClip = AudioClip.Create("TempAudio", audioSamples.Length / channels, 
                                                   channels, sampleRate, false);
            tempClip.SetData(audioSamples, 0);
            
            // 播放音頻片段
            remoteAudioSource.PlayOneShot(tempClip);
        }
    }

    /// <summary>
    /// 切換播放狀態
    /// </summary>
    public void TogglePlayback()
    {
        if (isPlaying)
        {
            StopPlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    #endregion

    #region 實時循環播放

    /// <summary>
    /// 啟用實時循環模式 - 邊錄邊播（類似 NetworkLoopback）
    /// </summary>
    public void EnableRealtimeLoopback()
    {
        if (!isRecording)
        {
            StartRecording();
        }
        
        isPlaying = true;
        playbackTimer = 0f;
        Log("✓ 啟用實時循環模式（邊錄邊播）");
    }

    /// <summary>
    /// 禁用實時循環模式
    /// </summary>
    public void DisableRealtimeLoopback()
    {
        StopRecording();
        StopPlayback();
        Log("✓ 禁用實時循環模式");
    }

    #endregion

    #region 數據管理

    /// <summary>
    /// 清空錄製數據
    /// </summary>
    public void ClearRecordedData()
    {
        recordedDataQueue.Clear();
        Log("✓ 已清空所有錄製數據");
    }

    /// <summary>
    /// 獲取當前錄製的幀數
    /// </summary>
    public int GetRecordedFrameCount()
    {
        return recordedDataQueue.Count;
    }

    /// <summary>
    /// 獲取預估的錄製時長（秒）
    /// </summary>
    public float GetRecordedDuration()
    {
        return recordedDataQueue.Count / 60f; // 假設 60 FPS
    }

    #endregion

    #region 統計資訊

    /// <summary>
    /// 顯示當前狀態資訊
    /// </summary>
    public void ShowStats()
    {
        Log("=== Avatar 錄製/播放統計 ===");
        Log($"正在錄製: {isRecording}");
        Log($"正在播放: {isPlaying}");
        Log($"已錄製幀數: {recordedDataQueue.Count}");
        Log($"預估時長: {GetRecordedDuration():F2} 秒");
        Log($"Stream LOD: {streamLOD}");
        Log($"播放速度: {playbackSpeed}x");
        Log($"本地 Avatar 狀態: {(localAvatar != null && localAvatar.IsCreated ? "就緒" : "未就緒")}");
        Log($"遠端 Avatar 狀態: {(remoteAvatar != null && remoteAvatar.IsCreated ? "就緒" : "未就緒")}");
        Log($"===========================");
    }

    #endregion

    #region 調試輔助

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[AvatarRecording] {message}");
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[AvatarRecording] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[AvatarRecording] {message}");
    }

    #endregion

    #region Unity Editor 調試按鈕

#if UNITY_EDITOR
    [ContextMenu("開始錄製")]
    private void EditorStartRecording()
    {
        StartRecording();
    }

    [ContextMenu("停止錄製")]
    private void EditorStopRecording()
    {
        StopRecording();
    }

    [ContextMenu("開始播放")]
    private void EditorStartPlayback()
    {
        StartPlayback();
    }

    [ContextMenu("停止播放")]
    private void EditorStopPlayback()
    {
        StopPlayback();
    }

    [ContextMenu("啟用實時循環")]
    private void EditorEnableLoopback()
    {
        EnableRealtimeLoopback();
    }

    [ContextMenu("禁用實時循環")]
    private void EditorDisableLoopback()
    {
        DisableRealtimeLoopback();
    }

    [ContextMenu("清空錄製數據")]
    private void EditorClearData()
    {
        ClearRecordedData();
    }

    [ContextMenu("顯示統計資訊")]
    private void EditorShowStats()
    {
        ShowStats();
    }
#endif

    #endregion
}
