using UnityEngine;
using Oculus.Avatar2;

/// <summary>
/// NetworkLoopback 場景音頻播放器
/// 直接將麥克風音頻播放到遠端 Avatar
/// 無緩衝、無延遲、與嘴型完全同步
/// </summary>
public class NetworkLoopbackAudioRecorder : MonoBehaviour
{
    [Header("Avatar 設定")]
    [Tooltip("本地 Avatar（錄製音頻來源）")]
    public OvrAvatarEntity localAvatar;
    
    [Tooltip("遠端 Avatar（播放音頻目標）")]
    public OvrAvatarEntity remoteAvatar;

    [Header("音頻設定")]
    [Tooltip("本地 Avatar 的 AudioSource（LipSyncInput 上的）")]
    public AudioSource localAudioSource;
    
    [Tooltip("遠端 Avatar 的 AudioSource（用於播放聲音）")]
    public AudioSource remoteAudioSource;
    
    [Tooltip("麥克風設備名稱（留空使用默認）")]
    public string microphoneDevice = null;

    [Header("錄製設定")]
    [Tooltip("自動開始播放")]
    public bool autoStartRecording = true;
    
    [Tooltip("錄製音頻品質（Hz）")]
    public int audioSampleRate = 44100;

    [Header("調試")]
    [Tooltip("顯示調試日誌")]
    public bool showDebugLogs = true;
    
    [Tooltip("在螢幕上顯示音量")]
    public bool showVolumeOnScreen = true;

    // === 私有變數 ===
    private bool isPlaying = false;
    private float currentVolume = 0f;
    private float peakVolume = 0f;

    void Start()
    {
        if (showDebugLogs)
            Debug.Log($"[AudioRecorder] 初始化音頻播放器");

        // 自動尋找組件
        if (localAvatar == null)
        {
            localAvatar = GameObject.Find("LocalAvatar")?.GetComponent<OvrAvatarEntity>();
        }

        if (remoteAvatar == null)
        {
            remoteAvatar = GameObject.Find("RemoteLoopbackAvatar")?.GetComponent<OvrAvatarEntity>();
        }

        // 尋找或創建 AudioSource
        SetupAudioSources();

        if (autoStartRecording)
        {
            // 延遲啟動，等待麥克風初始化
            Invoke(nameof(StartRecording), 0.5f);
        }
    }

    void SetupAudioSources()
    {
        // 本地 AudioSource（應該已經存在於 LipSyncInput）
        if (localAudioSource == null)
        {
            var lipSyncInput = GameObject.Find("LipSyncInput");
            if (lipSyncInput != null)
            {
                localAudioSource = lipSyncInput.GetComponent<AudioSource>();
                if (localAudioSource == null)
                {
                    Debug.LogError("[AudioRecorder] LipSyncInput 上沒有 AudioSource！");
                    return;
                }
            }
            else
            {
                Debug.LogError("[AudioRecorder] 找不到 LipSyncInput GameObject！");
                return;
            }
        }

        // 遠端 AudioSource（用於播放錄製的聲音）
        if (remoteAudioSource == null && remoteAvatar != null)
        {
            remoteAudioSource = remoteAvatar.gameObject.GetComponent<AudioSource>();
            if (remoteAudioSource == null)
            {
                remoteAudioSource = remoteAvatar.gameObject.AddComponent<AudioSource>();
                if (showDebugLogs)
                    Debug.Log("[AudioRecorder] 為 RemoteLoopbackAvatar 創建 AudioSource");
            }
        }

        // 配置遠端 AudioSource
        if (remoteAudioSource != null)
        {
            remoteAudioSource.loop = true;
            remoteAudioSource.playOnAwake = false;
            remoteAudioSource.spatialBlend = 0f; // 2D 音效
            remoteAudioSource.volume = 1.5f;
            
            if (showDebugLogs)
                Debug.Log("[AudioRecorder] ✓ RemoteAudioSource 配置完成（直接播放麥克風）");
        }
    }

    public void StartRecording()
    {
        if (isPlaying)
        {
            Debug.LogWarning("[AudioRecorder] 已經在播放中");
            return;
        }

        if (localAudioSource == null)
        {
            Debug.LogError("[AudioRecorder] LocalAudioSource 為空！請在 Inspector 中設置。");
            return;
        }

        // 等待麥克風 AudioClip 生成（可能需要幾幀）
        if (localAudioSource.clip == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("[AudioRecorder] AudioClip 尚未準備好，0.5秒後重試...");
            
            Invoke(nameof(StartRecording), 0.5f);
            return;
        }

        isPlaying = true;

        // 直接將麥克風 AudioClip 設給遠端 AudioSource - 即時播放！
        if (remoteAudioSource != null)
        {
            remoteAudioSource.clip = localAudioSource.clip;
            remoteAudioSource.loop = true;
            remoteAudioSource.Play();
            
            if (showDebugLogs)
            {
                Debug.Log($"[AudioRecorder] ✓ 開始即時播放麥克風音頻");
                Debug.Log($"[AudioRecorder] 麥克風: {localAudioSource.clip.name}");
                Debug.Log($"[AudioRecorder] 採樣率: {localAudioSource.clip.frequency} Hz");
                Debug.Log($"[AudioRecorder] 聲道: {localAudioSource.clip.channels}");
                Debug.Log($"[AudioRecorder] 💡 音頻與嘴型完全同步（無延遲）");
            }
        }
    }

    public void StopRecording()
    {
        if (!isPlaying)
        {
            Debug.LogWarning("[AudioRecorder] 沒有在播放");
            return;
        }

        isPlaying = false;
        
        if (remoteAudioSource != null && remoteAudioSource.isPlaying)
        {
            remoteAudioSource.Stop();
        }
        
        if (showDebugLogs)
            Debug.Log($"[AudioRecorder] 停止播放");
    }

    void Update()
    {
        if (!isPlaying)
            return;

        // 計算音量（用於顯示）
        CalculateVolume();
    }

    void CalculateVolume()
    {
        if (localAudioSource == null || localAudioSource.clip == null)
        {
            currentVolume = 0f;
            return;
        }

        // 從麥克風 AudioClip 讀取最近的樣本計算音量
        int micPosition = Microphone.GetPosition(microphoneDevice);
        if (micPosition < 0)
        {
            currentVolume = 0f;
            return;
        }

        int sampleCount = 1024;
        int startPosition = micPosition - sampleCount;
        if (startPosition < 0) startPosition = 0;

        float[] samples = new float[sampleCount * localAudioSource.clip.channels];
        
        try
        {
            localAudioSource.clip.GetData(samples, startPosition);
        }
        catch
        {
            currentVolume = 0f;
            return;
        }
        
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        
        currentVolume = Mathf.Sqrt(sum / samples.Length);
        
        // 更新峰值
        if (currentVolume > peakVolume)
        {
            peakVolume = currentVolume;
        }
        else
        {
            peakVolume = Mathf.Lerp(peakVolume, 0f, Time.deltaTime * 2f);
        }
    }

    void OnGUI()
    {
        if (!showVolumeOnScreen || !isPlaying)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleLeft;
        style.padding = new RectOffset(10, 10, 10, 10);

        float barWidth = 400f;
        float barHeight = 40f;
        float xPos = 10f;
        float yPos = 10f;

        // 背景
        GUI.Box(new Rect(xPos, yPos, barWidth + 20f, barHeight + 60f), "", style);

        // 標籤
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 16;
        labelStyle.normal.textColor = Color.white;
        
        GUI.Label(new Rect(xPos + 10f, yPos + 10f, barWidth, 20f), 
            $"🎤 即時音頻播放中（零延遲）", labelStyle);

        // 音量條
        float volumeBarWidth = currentVolume * barWidth * 10f; // 放大顯示
        volumeBarWidth = Mathf.Clamp(volumeBarWidth, 0f, barWidth);
        
        Color volumeColor = Color.Lerp(Color.green, Color.red, currentVolume * 5f);
        GUI.color = volumeColor;
        GUI.DrawTexture(new Rect(xPos + 10f, yPos + 40f, volumeBarWidth, barHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 音量數值
        GUI.Label(new Rect(xPos + 10f, yPos + 45f, barWidth, barHeight), 
            $"音量: {(currentVolume * 100f):F1}%", labelStyle);
    }

    void OnDestroy()
    {
        if (isPlaying)
        {
            StopRecording();
        }
    }

    // === 公開方法 ===

    /// <summary>
    /// 獲取當前音量（0-1）
    /// </summary>
    public float GetCurrentVolume()
    {
        return currentVolume;
    }
}
