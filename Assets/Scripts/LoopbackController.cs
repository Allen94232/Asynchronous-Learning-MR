using UnityEngine;

/// <summary>
/// 控制 NetworkLoopbackManager 的即時同步功能
/// 用於學生播放場景：保留 Manager 以初始化 Avatar，但停用即時同步
/// </summary>
public class LoopbackController : MonoBehaviour
{
    [Header("NetworkLoopback 管理")]
    [Tooltip("NetworkLoopbackManager 組件")]
    public MonoBehaviour loopbackManager;
    
    [Header("同步設定")]
    [Tooltip("啟用即時同步（教師端勾選，學生端取消勾選）")]
    public bool enableRealtimeSync = false;

    [Header("調試")]
    [Tooltip("顯示狀態訊息")]
    public bool showDebugLogs = true;

    private bool wasEnabled = false;

    void Start()
    {
        // 自動尋找 NetworkLoopbackManager
        if (loopbackManager == null)
        {
            var loopbackObj = GameObject.Find("NetworkLoopbackManager");
            if (loopbackObj != null)
            {
                // NetworkLoopbackManager 可能是多種類型，都繼承自 MonoBehaviour
                loopbackManager = loopbackObj.GetComponent<MonoBehaviour>();
            }
        }

        // 設定初始狀態
        UpdateSyncState();
    }

    void Update()
    {
        // 即時更新同步狀態
        if (loopbackManager != null && loopbackManager.enabled != enableRealtimeSync)
        {
            UpdateSyncState();
        }
    }

    void UpdateSyncState()
    {
        if (loopbackManager == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("[LoopbackController] NetworkLoopbackManager 未找到");
            return;
        }

        loopbackManager.enabled = enableRealtimeSync;
        
        if (showDebugLogs && wasEnabled != enableRealtimeSync)
        {
            if (enableRealtimeSync)
            {
                Debug.Log("[LoopbackController] ✓ 即時同步已啟用（教師端模式）");
            }
            else
            {
                Debug.Log("[LoopbackController] ✗ 即時同步已停用（學生端模式）");
            }
        }
        
        wasEnabled = enableRealtimeSync;
    }

    /// <summary>
    /// 啟用即時同步（用於教師端預覽）
    /// </summary>
    public void EnableRealtimeSync()
    {
        enableRealtimeSync = true;
        UpdateSyncState();
    }

    /// <summary>
    /// 停用即時同步（用於學生端播放）
    /// </summary>
    public void DisableRealtimeSync()
    {
        enableRealtimeSync = false;
        UpdateSyncState();
    }

    void OnGUI()
    {
        if (!showDebugLogs)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = enableRealtimeSync ? Color.green : Color.gray;

        string status = enableRealtimeSync 
            ? "即時同步: 啟用 ✓" 
            : "即時同步: 停用 ✗";

        GUI.Label(new Rect(10, 60, 300, 20), status, style);
    }
}
