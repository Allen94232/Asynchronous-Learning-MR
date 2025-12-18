using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 教師錄製端 UI 控制器
/// 管理錄製相關按鈕的顯示和交互
/// </summary>
public class TeacherRecordingUI : MonoBehaviour
{
    [Header("UI 元件")]
    [Tooltip("開始/停止錄製按鈕")]
    public Button startStopButton;
    
    [Tooltip("開始/停止按鈕的文字")]
    public TextMeshProUGUI startStopButtonText;
    
    [Tooltip("儲存按鈕")]
    public Button saveButton;
    
    [Header("管理器")]
    [Tooltip("教師錄製管理器")]
    public TeacherRecordingManager recordingManager;
    
    [Header("顏色設定")]
    [Tooltip("開始錄製按鈕顏色（綠色）")]
    public Color startColor = new Color(0.2f, 0.8f, 0.2f);
    
    [Tooltip("停止錄製按鈕顏色（紅色）")]
    public Color stopColor = new Color(1f, 0.3f, 0.3f);
    
    private Image startStopButtonImage;
    
    void Start()
    {
        // 獲取按鈕的 Image 組件
        startStopButtonImage = startStopButton.GetComponent<Image>();
        
        // 綁定按鈕事件
        startStopButton.onClick.AddListener(OnStartStopClick);
        saveButton.onClick.AddListener(OnSaveClick);
        
        // 初始狀態
        UpdateUI();
    }
    
    void Update()
    {
        UpdateUI();
    }
    
    /// <summary>
    /// 開始/停止錄製按鈕點擊事件
    /// </summary>
    void OnStartStopClick()
    {
        if (recordingManager.IsRecording)
        {
            // 停止錄製
            recordingManager.UI_StopRecording();
        }
        else
        {
            // 開始錄製
            recordingManager.UI_StartRecording();
        }
    }
    
    /// <summary>
    /// 儲存按鈕點擊事件
    /// </summary>
    void OnSaveClick()
    {
        recordingManager.UI_SaveRecording();
        // 儲存後隱藏儲存按鈕
        saveButton.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 更新 UI 狀態
    /// </summary>
    void UpdateUI()
    {
        if (recordingManager == null)
        {
            Debug.LogError("[TeacherRecordingUI] RecordingManager 未設定！");
            return;
        }
        
        if (recordingManager.IsRecording)
        {
            // 錄製中
            startStopButtonText.text = "結束錄製";
            startStopButtonImage.color = stopColor;
            saveButton.gameObject.SetActive(false);
        }
        else
        {
            // 未錄製
            startStopButtonText.text = "開始錄製";
            startStopButtonImage.color = startColor;
            
            // 顯示儲存按鈕（如果有錄製數據）
            saveButton.gameObject.SetActive(recordingManager.HasRecordingToSave());
        }
    }
}
