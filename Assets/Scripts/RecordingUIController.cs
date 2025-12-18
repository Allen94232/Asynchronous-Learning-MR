using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 錄製 UI 控制器
/// 提供簡單的按鈕來控制錄製和播放
/// </summary>
public class RecordingUIController : MonoBehaviour
{
    [Header("組件參考")]
    [Tooltip("AvatarRecordingManager 組件")]
    public AvatarRecordingManager recordingManager;

    [Header("UI 按鈕")]
    [Tooltip("開始錄製按鈕")]
    public Button startRecordButton;
    
    [Tooltip("停止錄製按鈕")]
    public Button stopRecordButton;
    
    [Tooltip("儲存錄製按鈕")]
    public Button saveRecordButton;
    
    [Tooltip("載入錄製按鈕")]
    public Button loadRecordButton;
    
    [Tooltip("開始播放按鈕")]
    public Button startPlaybackButton;
    
    [Tooltip("停止播放按鈕")]
    public Button stopPlaybackButton;

    [Header("UI 文字")]
    [Tooltip("狀態文字")]
    public Text statusText;
    
    [Tooltip("錄製時間文字")]
    public Text timeText;

    [Header("設定")]
    [Tooltip("預設錄製檔案名稱")]
    public string defaultFilename = "MyRecording";

    void Start()
    {
        // 自動尋找 RecordingManager
        if (recordingManager == null)
        {
            recordingManager = FindObjectOfType<AvatarRecordingManager>();
        }

        // 設定按鈕事件
        if (startRecordButton != null)
        {
            startRecordButton.onClick.AddListener(OnStartRecord);
        }
        
        if (stopRecordButton != null)
        {
            stopRecordButton.onClick.AddListener(OnStopRecord);
        }
        
        if (saveRecordButton != null)
        {
            saveRecordButton.onClick.AddListener(OnSaveRecord);
        }
        
        if (loadRecordButton != null)
        {
            loadRecordButton.onClick.AddListener(OnLoadRecord);
        }
        
        if (startPlaybackButton != null)
        {
            startPlaybackButton.onClick.AddListener(OnStartPlayback);
        }
        
        if (stopPlaybackButton != null)
        {
            stopPlaybackButton.onClick.AddListener(OnStopPlayback);
        }

        UpdateUI();
    }

    void Update()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        if (recordingManager == null)
            return;

        // 更新按鈕狀態
        if (startRecordButton != null)
            startRecordButton.interactable = !recordingManager.IsRecording && !recordingManager.IsPlaying;
        
        if (stopRecordButton != null)
            stopRecordButton.interactable = recordingManager.IsRecording;
        
        if (saveRecordButton != null)
            saveRecordButton.interactable = !recordingManager.IsRecording && recordingManager.RecordedFrames > 0;
        
        if (loadRecordButton != null)
            loadRecordButton.interactable = !recordingManager.IsRecording && !recordingManager.IsPlaying;
        
        if (startPlaybackButton != null)
            startPlaybackButton.interactable = !recordingManager.IsRecording && !recordingManager.IsPlaying && recordingManager.RecordedFrames > 0;
        
        if (stopPlaybackButton != null)
            stopPlaybackButton.interactable = recordingManager.IsPlaying;

        // 更新狀態文字
        if (statusText != null)
        {
            if (recordingManager.IsRecording)
            {
                statusText.text = "🔴 錄製中...";
                statusText.color = Color.red;
            }
            else if (recordingManager.IsPlaying)
            {
                statusText.text = "▶ 播放中...";
                statusText.color = Color.green;
            }
            else if (recordingManager.RecordedFrames > 0)
            {
                statusText.text = "✓ 就緒";
                statusText.color = Color.white;
            }
            else
            {
                statusText.text = "⏸ 等待中";
                statusText.color = Color.gray;
            }
        }

        // 更新時間文字
        if (timeText != null)
        {
            if (recordingManager.IsRecording)
            {
                timeText.text = $"時間: {recordingManager.RecordingDuration:F1}s\n幀數: {recordingManager.RecordedFrames}";
            }
            else if (recordingManager.RecordedFrames > 0)
            {
                timeText.text = $"總幀數: {recordingManager.RecordedFrames}";
            }
            else
            {
                timeText.text = "";
            }
        }
    }

    // === 按鈕事件 ===

    void OnStartRecord()
    {
        if (recordingManager != null)
        {
            recordingManager.StartRecording();
            Debug.Log("[UI] 開始錄製");
        }
    }

    void OnStopRecord()
    {
        if (recordingManager != null)
        {
            recordingManager.StopRecording();
            Debug.Log("[UI] 停止錄製");
        }
    }

    void OnSaveRecord()
    {
        if (recordingManager != null)
        {
            recordingManager.SaveRecording(defaultFilename);
            Debug.Log($"[UI] 儲存錄製: {defaultFilename}");
        }
    }

    void OnLoadRecord()
    {
        if (recordingManager != null)
        {
            // 列出可用的錄製檔案
            string[] recordings = recordingManager.ListSavedRecordings();
            
            if (recordings.Length > 0)
            {
                // 載入最新的檔案
                string latestFile = recordings[recordings.Length - 1];
                bool success = recordingManager.LoadRecording(latestFile);
                
                if (success)
                {
                    Debug.Log($"[UI] 載入錄製: {latestFile}");
                }
                else
                {
                    Debug.LogError($"[UI] 載入失敗: {latestFile}");
                }
            }
            else
            {
                // 嘗試載入預設檔案
                bool success = recordingManager.LoadRecording(defaultFilename);
                
                if (success)
                {
                    Debug.Log($"[UI] 載入錄製: {defaultFilename}");
                }
                else
                {
                    Debug.LogError($"[UI] 找不到錄製檔案");
                }
            }
        }
    }

    void OnStartPlayback()
    {
        if (recordingManager != null)
        {
            recordingManager.StartPlayback();
            Debug.Log("[UI] 開始播放");
        }
    }

    void OnStopPlayback()
    {
        if (recordingManager != null)
        {
            recordingManager.StopPlayback();
            Debug.Log("[UI] 停止播放");
        }
    }

    // === 鍵盤快捷鍵 ===
    
    void LateUpdate()
    {
        if (recordingManager == null)
            return;

        // R 鍵：開始/停止錄製
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (recordingManager.IsRecording)
            {
                OnStopRecord();
            }
            else if (!recordingManager.IsPlaying)
            {
                OnStartRecord();
            }
        }

        // S 鍵：儲存
        if (Input.GetKeyDown(KeyCode.S) && !recordingManager.IsRecording && recordingManager.RecordedFrames > 0)
        {
            OnSaveRecord();
        }

        // L 鍵：載入
        if (Input.GetKeyDown(KeyCode.L) && !recordingManager.IsRecording && !recordingManager.IsPlaying)
        {
            OnLoadRecord();
        }

        // P 鍵：開始/停止播放
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (recordingManager.IsPlaying)
            {
                OnStopPlayback();
            }
            else if (!recordingManager.IsRecording && recordingManager.RecordedFrames > 0)
            {
                OnStartPlayback();
            }
        }
    }
}
