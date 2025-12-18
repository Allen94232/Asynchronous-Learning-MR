using UnityEngine;
using System.Linq;

/// <summary>
/// 麥克風測試工具
/// 用於檢測和測試麥克風是否正常工作
/// 顯示音量、波形和設備資訊
/// </summary>
public class MicrophoneTest : MonoBehaviour
{
    [Header("麥克風設定")]
    [Tooltip("麥克風設備名稱（留空使用默認）")]
    public string microphoneDevice = null;
    
    [Tooltip("採樣率（Hz）")]
    public int sampleRate = 48000;
    
    [Tooltip("錄製長度（秒）")]
    public int recordLength = 1;

    [Header("音量檢測")]
    [Tooltip("音量閾值（低於此值視為無聲）")]
    [Range(0f, 0.1f)]
    public float silenceThreshold = 0.01f;
    
    [Tooltip("顯示即時音量")]
    public bool showVolume = true;

    [Header("自動測試")]
    [Tooltip("自動開始錄製")]
    public bool autoStart = true;

    [Header("調試資訊")]
    [Tooltip("顯示詳細日誌")]
    public bool showDebugLogs = true;

    // 私有變數
    private AudioSource audioSource;
    private AudioClip micClip;
    private bool isRecording = false;
    private float[] samples = new float[128];
    private float currentVolume = 0f;
    private float maxVolume = 0f;
    private float avgVolume = 0f;
    private int frameCount = 0;

    void Start()
    {
        // 獲取或創建 AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        // 顯示可用麥克風
        ListMicrophones();

        // 檢查麥克風權限（Android）
        CheckMicrophonePermission();

        // 自動開始
        if (autoStart)
        {
            Invoke("StartMicrophoneTest", 0.5f);
        }
    }

    void Update()
    {
        if (isRecording)
        {
            AnalyzeAudio();
            
            if (showVolume)
            {
                UpdateVolumeDisplay();
            }
        }

        // 鍵盤控制
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isRecording)
            {
                StopMicrophoneTest();
            }
            else
            {
                StartMicrophoneTest();
            }
        }
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        float yOffset = 10;
        float lineHeight = 25;

        // 顯示標題
        GUI.Label(new Rect(10, yOffset, 500, lineHeight), "=== 麥克風測試工具 ===", style);
        yOffset += lineHeight;

        // 顯示狀態
        GUIStyle statusStyle = new GUIStyle(style);
        statusStyle.normal.textColor = isRecording ? Color.green : Color.yellow;
        GUI.Label(new Rect(10, yOffset, 500, lineHeight), 
            $"狀態: {(isRecording ? "錄製中 ✓" : "未錄製")}", statusStyle);
        yOffset += lineHeight;

        if (isRecording)
        {
            // 顯示當前音量
            GUIStyle volumeStyle = new GUIStyle(style);
            volumeStyle.normal.textColor = currentVolume > silenceThreshold ? Color.green : Color.red;
            GUI.Label(new Rect(10, yOffset, 500, lineHeight), 
                $"當前音量: {currentVolume:F4} {(currentVolume > silenceThreshold ? "✓" : "✗ 太安靜")}", volumeStyle);
            yOffset += lineHeight;

            // 顯示最大音量
            GUI.Label(new Rect(10, yOffset, 500, lineHeight), 
                $"最大音量: {maxVolume:F4}", style);
            yOffset += lineHeight;

            // 顯示平均音量
            GUI.Label(new Rect(10, yOffset, 500, lineHeight), 
                $"平均音量: {avgVolume:F4}", style);
            yOffset += lineHeight;

            // 音量條
            DrawVolumeBar(10, yOffset, 300, 20, currentVolume);
            yOffset += 30;

            // 波形顯示
            DrawWaveform(10, yOffset, 500, 100);
            yOffset += 110;
        }

        // 顯示麥克風資訊
        GUI.Label(new Rect(10, yOffset, 500, lineHeight), 
            $"麥克風: {(string.IsNullOrEmpty(microphoneDevice) ? "默認設備" : microphoneDevice)}", style);
        yOffset += lineHeight;

        // 顯示控制提示
        GUI.Label(new Rect(10, yOffset, 500, lineHeight), 
            "按 [空白鍵] 開始/停止測試", style);
        yOffset += lineHeight;

        // 操作按鈕
        if (GUI.Button(new Rect(10, yOffset, 150, 40), isRecording ? "停止測試" : "開始測試"))
        {
            if (isRecording)
            {
                StopMicrophoneTest();
            }
            else
            {
                StartMicrophoneTest();
            }
        }

        if (GUI.Button(new Rect(170, yOffset, 150, 40), "列出麥克風"))
        {
            ListMicrophones();
        }

        if (GUI.Button(new Rect(330, yOffset, 150, 40), "重置統計"))
        {
            ResetStatistics();
        }
    }

    /// <summary>
    /// 列出所有可用的麥克風設備
    /// </summary>
    [ContextMenu("列出麥克風")]
    public void ListMicrophones()
    {
        Log("=== 可用麥克風設備 ===");
        
        if (Microphone.devices.Length == 0)
        {
            LogError("❌ 沒有檢測到任何麥克風設備！");
            LogError("請檢查：");
            LogError("1. 麥克風是否已連接");
            LogError("2. Windows 設定 → 隱私權 → 麥克風權限");
            LogError("3. 麥克風是否被其他應用程式佔用");
            return;
        }

        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            string device = Microphone.devices[i];
            Microphone.GetDeviceCaps(device, out int minFreq, out int maxFreq);
            
            Log($"[{i}] {device}");
            Log($"    頻率範圍: {minFreq} Hz ~ {maxFreq} Hz");
            
            if (i == 0)
            {
                Log($"    ← 默認設備");
            }
        }
        
        Log($"總共找到 {Microphone.devices.Length} 個麥克風設備");
        Log("====================");
    }

    /// <summary>
    /// 檢查麥克風權限（主要用於 Android）
    /// </summary>
    private void CheckMicrophonePermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            LogWarning("⚠ Android 麥克風權限未授予");
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
        }
        else
        {
            Log("✓ Android 麥克風權限已授予");
        }
#endif
    }

    /// <summary>
    /// 開始麥克風測試
    /// </summary>
    [ContextMenu("開始測試")]
    public void StartMicrophoneTest()
    {
        if (isRecording)
        {
            LogWarning("麥克風已在錄製中");
            return;
        }

        if (Microphone.devices.Length == 0)
        {
            LogError("❌ 沒有可用的麥克風設備！");
            ListMicrophones();
            return;
        }

        // 選擇麥克風設備（自動跳過虛擬設備）
        if (string.IsNullOrEmpty(microphoneDevice) || !Microphone.devices.Contains(microphoneDevice))
        {
            // 嘗試找到非虛擬的實體麥克風
            microphoneDevice = null;
            
            foreach (string device in Microphone.devices)
            {
                // 跳過 Oculus、Virtual、Loopback 等虛擬設備
                if (!device.Contains("Virtual") && 
                    !device.Contains("Oculus") && 
                    !device.Contains("Loopback") &&
                    !device.Contains("Stereo Mix") &&
                    !device.Contains("Cable"))
                {
                    microphoneDevice = device;
                    Log($"✓ 自動選擇實體麥克風: {device}");
                    break;
                }
            }
            
            // 如果找不到實體麥克風，使用第一個設備並警告
            if (microphoneDevice == null && Microphone.devices.Length > 0)
            {
                microphoneDevice = Microphone.devices[0];
                LogWarning($"⚠ 未找到實體麥克風，使用: {microphoneDevice}");
                
                if (microphoneDevice.Contains("Oculus") || microphoneDevice.Contains("Virtual"))
                {
                    LogWarning("這是虛擬音頻設備，在 Unity Editor 中無法正常工作！");
                    LogWarning("解決方案：");
                    LogWarning("1) 手動指定實體麥克風（在 Inspector 中設定 Microphone Device）");
                    LogWarning("2) 或 Build 到 Quest 設備進行測試");
                    LogWarning("3) 或在 Windows 設定中禁用 Oculus Virtual Audio Device");
                }
            }
        }

        Log($"🎤 開始錄製麥克風: {microphoneDevice ?? "默認設備"}");
        Log($"   採樣率: {sampleRate} Hz");
        Log($"   緩衝長度: {recordLength} 秒");

        // 開始錄製
        micClip = Microphone.Start(microphoneDevice, true, recordLength, sampleRate);
        
        if (micClip == null)
        {
            LogError("❌ 無法開始錄製！麥克風可能被佔用。");
            return;
        }

        // 等待麥克風準備好
        int timeout = 0;
        while (!(Microphone.GetPosition(microphoneDevice) > 0) && timeout < 100)
        {
            timeout++;
            System.Threading.Thread.Sleep(10);
        }

        if (timeout >= 100)
        {
            LogError("❌ 麥克風啟動超時！");
            Microphone.End(microphoneDevice);
            return;
        }

        // 播放音頻（即時回音，可以聽到自己的聲音）
        audioSource.clip = micClip;
        audioSource.Play(); // 啟用即時回音

        isRecording = true;
        ResetStatistics();
        
        Log("✓ 麥克風測試已開始");
        Log("💡 對著麥克風說話，觀察音量變化");
    }

    /// <summary>
    /// 停止麥克風測試
    /// </summary>
    [ContextMenu("停止測試")]
    public void StopMicrophoneTest()
    {
        if (!isRecording)
        {
            LogWarning("麥克風未在錄製");
            return;
        }

        Microphone.End(microphoneDevice);
        
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        isRecording = false;

        Log("✓ 麥克風測試已停止");
        Log($"📊 統計資訊:");
        Log($"   最大音量: {maxVolume:F4}");
        Log($"   平均音量: {avgVolume:F4}");
        Log($"   總幀數: {frameCount}");
        
        if (maxVolume < silenceThreshold)
        {
            LogWarning("⚠ 警告：未檢測到明顯聲音！");
            LogWarning("可能原因：");
            LogWarning("1. 麥克風音量太小（檢查系統音量設定）");
            LogWarning("2. 麥克風被靜音");
            LogWarning("3. 選擇了錯誤的麥克風設備");
        }
        else
        {
            Log("✓ 麥克風工作正常！");
        }
    }

    /// <summary>
    /// 分析音頻數據
    /// </summary>
    private void AnalyzeAudio()
    {
        if (micClip == null) return;

        // 獲取當前麥克風位置
        int position = Microphone.GetPosition(microphoneDevice);
        if (position < samples.Length) return;

        // 獲取音頻樣本
        micClip.GetData(samples, position - samples.Length);

        // 計算音量（RMS - Root Mean Square）
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        currentVolume = Mathf.Sqrt(sum / samples.Length);

        // 更新統計
        if (currentVolume > maxVolume)
        {
            maxVolume = currentVolume;
        }

        avgVolume = (avgVolume * frameCount + currentVolume) / (frameCount + 1);
        frameCount++;
    }

    /// <summary>
    /// 更新音量顯示
    /// </summary>
    private void UpdateVolumeDisplay()
    {
        // 在 Console 中顯示（可選）
        if (frameCount % 30 == 0 && showDebugLogs) // 每 30 幀顯示一次
        {
            string volumeBars = new string('|', Mathf.RoundToInt(currentVolume * 100));
            Log($"音量: {currentVolume:F4} {volumeBars}");
        }
    }

    /// <summary>
    /// 繪製音量條
    /// </summary>
    private void DrawVolumeBar(float x, float y, float width, float height, float volume)
    {
        // 背景
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

        // 音量條
        float volumeWidth = width * Mathf.Clamp01(volume * 10); // 放大 10 倍以便觀察
        
        if (volume > silenceThreshold)
        {
            GUI.color = Color.Lerp(Color.green, Color.red, volume * 10);
        }
        else
        {
            GUI.color = Color.gray;
        }
        
        GUI.DrawTexture(new Rect(x, y, volumeWidth, height), Texture2D.whiteTexture);

        // 邊框
        GUI.color = Color.white;
        GUI.Box(new Rect(x, y, width, height), "");
        
        GUI.color = Color.white;
    }

    /// <summary>
    /// 繪製波形
    /// </summary>
    private void DrawWaveform(float x, float y, float width, float height)
    {
        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

        if (samples.Length == 0) return;

        float centerY = y + height / 2;
        float scale = height / 2;

        GUI.color = Color.cyan;
        for (int i = 0; i < samples.Length - 1; i++)
        {
            float x1 = x + (i / (float)samples.Length) * width;
            float y1 = centerY - samples[i] * scale;
            float x2 = x + ((i + 1) / (float)samples.Length) * width;
            float y2 = centerY - samples[i + 1] * scale;

            DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Color.cyan);
        }

        // 中線
        GUI.color = Color.gray;
        DrawLine(new Vector2(x, centerY), new Vector2(x + width, centerY), Color.gray);

        GUI.color = Color.white;
    }

    /// <summary>
    /// 繪製線條（簡單實現）
    /// </summary>
    private void DrawLine(Vector2 start, Vector2 end, Color color)
    {
        GUI.color = color;
        float length = Vector2.Distance(start, end);
        float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
        
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y, length, 2), Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot(-angle, start);
        GUI.color = Color.white;
    }

    /// <summary>
    /// 重置統計數據
    /// </summary>
    [ContextMenu("重置統計")]
    public void ResetStatistics()
    {
        currentVolume = 0f;
        maxVolume = 0f;
        avgVolume = 0f;
        frameCount = 0;
        Log("✓ 統計數據已重置");
    }

    /// <summary>
    /// 檢查麥克風是否正在錄製
    /// </summary>
    public bool IsMicrophoneRecording()
    {
        return Microphone.IsRecording(microphoneDevice);
    }

    /// <summary>
    /// 獲取當前音量
    /// </summary>
    public float GetCurrentVolume()
    {
        return currentVolume;
    }

    void OnDestroy()
    {
        if (isRecording)
        {
            StopMicrophoneTest();
        }
    }

    void OnApplicationQuit()
    {
        if (isRecording)
        {
            StopMicrophoneTest();
        }
    }

    // 日誌輔助方法
    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[MicTest] {message}");
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MicTest] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[MicTest] {message}");
    }
}
