using UnityEngine;
using Oculus.Avatar2;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using CAPI = Oculus.Avatar2.CAPI;

/// <summary>
/// 教師錄製管理器
/// 用於教師端場景，負責錄製動作、嘴型和音頻
/// </summary>
public class TeacherRecordingManager : MonoBehaviour
{
    [Header("Avatar 設定")]
    [Tooltip("教師 Avatar（錄製來源）")]
    public OvrAvatarEntity teacherAvatar;

    [Header("音頻設定")]
    [Tooltip("麥克風 AudioSource（來自 LipSyncInput）")]
    public AudioSource microphoneSource;
    
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
    
    [Tooltip("課程名稱（用於檔名）")]
    public string lessonName = "Lesson";

    [Header("UI 設定")]
    [Tooltip("顯示調試訊息")]
    public bool showDebugLogs = true;
    
    [Tooltip("在螢幕顯示錄製狀態")]
    public bool showRecordingUI = true;

    // === 私有變數 ===
    private AvatarRecordingData currentRecording;
    private bool isRecording = false;
    private float recordingTimer = 0f;
    private float frameTimer = 0f;
    private float frameInterval;
    private int lastMicPosition = 0;

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
                Debug.Log($"[TeacherRecording] 創建錄製資料夾: {savePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TeacherRecording] 創建資料夾失敗: {e.Message}");
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"[TeacherRecording] 錄製資料夾: {savePath}");
    }

    void FindComponents()
    {
        if (teacherAvatar == null)
            teacherAvatar = GameObject.Find("LocalAvatar")?.GetComponent<OvrAvatarEntity>();
        
        if (microphoneSource == null)
        {
            var lipSyncInput = GameObject.Find("LipSyncInput");
            if (lipSyncInput != null)
                microphoneSource = lipSyncInput.GetComponent<AudioSource>();
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
                Debug.LogWarning($"[TeacherRecording] 達到最大錄製時長 {maxRecordingDuration} 秒，自動停止");
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
    }
    
    void HandleKeyboardInput()
    {
        // R 鍵：開始/停止錄製
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (isRecording)
                StopRecording();
            else
                StartRecording();
        }

        // S 鍵：儲存
        if (Input.GetKeyDown(KeyCode.S) && !isRecording && currentRecording != null && currentRecording.frames.Count > 0)
        {
            SaveRecording();
        }
    }

    // ==================== 錄製功能 ====================
    
    public void StartRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning("[TeacherRecording] 已經在錄製中");
            return;
        }
        
        if (teacherAvatar == null || !teacherAvatar.IsCreated)
        {
            Debug.LogError("[TeacherRecording] TeacherAvatar 未準備好");
            return;
        }
        
        if (microphoneSource == null || microphoneSource.clip == null)
        {
            Debug.LogError("[TeacherRecording] 麥克風未準備好");
            return;
        }
        
        // 創建新錄製
        string recordingName = $"{lessonName}_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        int channels = microphoneSource.clip.channels;
        currentRecording = new AvatarRecordingData(recordingName, recordingFPS, audioSampleRate, channels);
        
        isRecording = true;
        recordingTimer = 0f;  // 確保從 0 開始
        frameTimer = 0f;      // 確保從 0 開始
        lastMicPosition = Microphone.GetPosition(microphoneDevice);
        
        if (showDebugLogs)
        {
            Debug.Log($"[TeacherRecording] ✓ 開始錄製: {recordingName}");
            Debug.Log($"[TeacherRecording] FPS: {recordingFPS}, 音頻: {audioSampleRate} Hz");
        }
    }
    
    public void StopRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning("[TeacherRecording] 沒有在錄製");
            return;
        }
        
        isRecording = false;
        currentRecording.duration = recordingTimer;
        
        if (showDebugLogs)
        {
            Debug.Log($"[TeacherRecording] ✓ 停止錄製");
            Debug.Log($"[TeacherRecording] 錄製時長: {recordingTimer:F2} 秒");
            Debug.Log($"[TeacherRecording] 總幀數: {currentRecording.frames.Count}");
        }
    }
    
    void RecordFrame()
    {
        if (teacherAvatar == null || !teacherAvatar.IsCreated)
            return;
        
        AvatarFrameData frame = new AvatarFrameData();
        
        // **關鍵修正：使用音頻樣本數計算精確時間戳**
        // 這樣 frame timestamp 就會和音頻完美對齊
        int totalAudioSamples = currentRecording.audioSamples.Count / currentRecording.audioChannels;
        frame.timestamp = (float)totalAudioSamples / currentRecording.audioSampleRate;
        
        // 錄製 Avatar 串流數據（動作 + 嘴型）
        frame.avatarStreamData = RecordAvatarStream();
        
        currentRecording.frames.Add(frame);
        
        // 第一幀顯示詳細訊息
        if (showDebugLogs && currentRecording.frames.Count == 1)
        {
            Debug.Log($"[TeacherRecording] 第一幀已錄製: timestamp={frame.timestamp:F3}s, 數據大小={frame.avatarStreamData?.Length ?? 0} bytes");
        }
        
        // 同時持續錄製音頻
        RecordAudioSamples();
    }
    
    byte[] RecordAvatarStream()
    {
        NativeArray<byte> nativeBuffer = default;
        
        try
        {
            uint bytesWritten = teacherAvatar.RecordStreamData_AutoBuffer(
                streamLOD,
                ref nativeBuffer
            );
            
            if (bytesWritten > 0 && nativeBuffer.IsCreated)
            {
                byte[] streamData = new byte[bytesWritten];
                NativeArray<byte>.Copy(nativeBuffer, streamData, (int)bytesWritten);
                return streamData;
            }
        }
        finally
        {
            if (nativeBuffer.IsCreated)
                nativeBuffer.Dispose();
        }
        
        return null;
    }
    
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
                    Debug.Log($"[TeacherRecording] 錄製音頻: {samplesAvailable} 樣本，總計 {currentRecording.audioSamples.Count / channels} 樣本");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TeacherRecording] 讀取音頻失敗: {e.Message}");
            }
        }
        
        lastMicPosition = currentPosition;
    }

    // ==================== 存檔功能 ====================
    
    public void SaveRecording(string customFilename = null)
    {
        if (currentRecording == null || currentRecording.frames.Count == 0)
        {
            Debug.LogError("[TeacherRecording] 沒有可儲存的錄製數據");
            return;
        }
        
        if (isRecording)
        {
            Debug.LogWarning("[TeacherRecording] 請先停止錄製");
            return;
        }
        
        string filename = customFilename ?? currentRecording.recordingName;
        string savePath = GetSaveFilePath(filename);
        
        try
        {
            // 確保資料夾存在
            string directory = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
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
                Debug.Log($"[TeacherRecording] ✓ 課程錄製已儲存: {savePath}");
                Debug.Log($"[TeacherRecording] 檔案大小: {fileInfo.Length / 1024f:F1} KB");
                Debug.Log($"[TeacherRecording] 完整路徑: {Path.GetFullPath(savePath)}");
            }
            else
            {
                Debug.LogError($"[TeacherRecording] 檔案儲存後未找到: {savePath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TeacherRecording] 儲存失敗: {e.Message}");
        }
    }
    
    public string[] ListSavedRecordings()
    {
        string folderPath = GetSaveFolderPath();
        
        if (!Directory.Exists(folderPath))
            return new string[0];
        
        string[] files = Directory.GetFiles(folderPath, "*.recording");
        
        for (int i = 0; i < files.Length; i++)
        {
            files[i] = Path.GetFileNameWithoutExtension(files[i]);
        }
        
        return files;
    }

    string GetSaveFolderPath()
    {
        return Path.Combine(Application.dataPath, "Recordings");
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
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(15, 15, 10, 10);
        
        float width = 400f;
        float height = 120f;
        float xPos = Screen.width - width - 20f;
        float yPos = 20f;
        
        GUI.Box(new Rect(xPos, yPos, width, height), "", style);
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 18;
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontStyle = FontStyle.Bold;
        
        float yOffset = yPos + 15f;
        
        if (isRecording)
        {
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 35f),
                "🔴 教師錄製中...", labelStyle);
            yOffset += 35f;
            
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 30f),
                $"時間: {recordingTimer:F1}s / {maxRecordingDuration:F0}s", labelStyle);
            yOffset += 30f;
            
            if (currentRecording != null)
            {
                GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                    $"幀數: {currentRecording.frames.Count}", labelStyle);
            }
        }
        else
        {
            GUI.Label(new Rect(xPos + 15f, yOffset, width, 35f),
                "⏸ 就緒 - 按 R 開始錄製", labelStyle);
            
            if (currentRecording != null && currentRecording.frames.Count > 0)
            {
                yOffset += 40f;
                GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                    $"已錄製: {currentRecording.duration:F1}s", labelStyle);
                yOffset += 25f;
                GUI.Label(new Rect(xPos + 15f, yOffset, width, 25f),
                    "按 S 儲存課程", labelStyle);
            }
        }
    }

    // ==================== 摺紙步驟記錄 ====================
    
    /// <summary>
    /// 記錄摺紙步驟切換事件（由 OrigamiStepGuide 呼叫）
    /// </summary>
    public void RecordOrigamiStepEvent(int stepIndex, string stepName)
    {
        if (!isRecording || currentRecording == null)
        {
            Debug.LogWarning("[TeacherRecording] 沒有在錄製，無法記錄摺紙步驟事件");
            return;
        }
        
        // 使用音頻樣本數計算精確時間戳
        int totalAudioSamples = currentRecording.audioSamples.Count / currentRecording.audioChannels;
        float timestamp = (float)totalAudioSamples / currentRecording.audioSampleRate;
        
        OrigamiStepEvent stepEvent = new OrigamiStepEvent(timestamp, stepIndex, stepName);
        currentRecording.origamiStepEvents.Add(stepEvent);
        
        if (showDebugLogs)
        {
            Debug.Log($"[TeacherRecording] ✓ 記錄摺紙步驟事件: 步驟 {stepIndex} '{stepName}' 於 {timestamp:F3}s");
        }
    }

    // ==================== 公開屬性 ====================
    
    public bool IsRecording => isRecording;
    public float RecordingDuration => recordingTimer;
    public int RecordedFrames => currentRecording?.frames.Count ?? 0;
    public AvatarRecordingData CurrentRecording => currentRecording;
    
    // ==================== UI 控制函數 ====================
    
    /// <summary>
    /// UI：開始錄製（按鈕呼叫）
    /// </summary>
    public void UI_StartRecording()
    {
        StartRecording();
    }
    
    /// <summary>
    /// UI：停止錄製（按鈕呼叫）
    /// </summary>
    public void UI_StopRecording()
    {
        StopRecording();
    }
    
    /// <summary>
    /// UI：儲存錄製（按鈕呼叫）
    /// </summary>
    public void UI_SaveRecording()
    {
        SaveRecording();
    }
    
    /// <summary>
    /// 檢查是否有錄製數據可儲存
    /// </summary>
    public bool HasRecordingToSave()
    {
        return !isRecording && currentRecording != null && currentRecording.frames.Count > 0;
    }
}
