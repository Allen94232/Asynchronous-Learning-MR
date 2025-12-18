using UnityEngine;
using Oculus.Avatar2;
using CAPI = Oculus.Avatar2.CAPI;

/// <summary>
/// 簡單的 Avatar 測試腳本
/// 用於測試 Meta Avatar SDK 的基本功能
/// 適用於 Meta Avatars SDK v40.0.1+
/// </summary>
public class SimpleAvatarTest : MonoBehaviour
{
    [Header("Avatar 配置")]
    [Tooltip("Avatar 實體組件")]
    public OvrAvatarEntity avatarEntity;

    [Header("調試資訊")]
    [Tooltip("是否顯示調試日誌")]
    public bool showDebugLogs = true;

    private void Start()
    {
        // 如果未指定，嘗試從當前物件獲取
        if (avatarEntity == null)
        {
            avatarEntity = GetComponent<OvrAvatarEntity>();
        }

        if (avatarEntity == null)
        {
            LogError("未找到 OvrAvatarEntity 組件！請將此腳本添加到帶有 OvrAvatarEntity 的物件上。");
            return;
        }

        // 訂閱 Avatar 事件
        SetupAvatarEvents();
    }

    /// <summary>
    /// 設置 Avatar 相關事件
    /// </summary>
    private void SetupAvatarEvents()
    {
        // Avatar 創建完成
        avatarEntity.OnCreatedEvent.AddListener(OnAvatarCreated);

        // Skeleton 載入完成
        avatarEntity.OnSkeletonLoadedEvent.AddListener(OnSkeletonLoaded);

        // 用戶 Avatar 載入完成
        avatarEntity.OnUserAvatarLoadedEvent.AddListener(OnUserAvatarLoaded);

        // Avatar 載入失敗
        avatarEntity.OnLoadFailedEvent.AddListener(OnAvatarLoadFailed);

        Log("Avatar 事件已設置");
    }

    #region Avatar 事件回調

    private void OnAvatarCreated(OvrAvatarEntity entity)
    {
        Log($"✓ Avatar 已創建 - 狀態: {entity.CurrentState}");
    }

    private void OnSkeletonLoaded(OvrAvatarEntity entity)
    {
        Log($"✓ Skeleton 已載入 - 狀態: {entity.CurrentState}");
    }

    private void OnUserAvatarLoaded(OvrAvatarEntity entity)
    {
        Log($"✓ 用戶 Avatar 載入完成！");
        Log($"  - 當前狀態: {entity.CurrentState}");
        Log($"  - 是否為本地玩家: {entity.IsLocal}");
        Log($"  - 實體已創建: {entity.IsCreated}");
    }

    private void OnAvatarLoadFailed(OvrAvatarEntity entity, CAPI.ovrAvatar2LoadRequestInfo loadRequestInfo)
    {
        LogError($"✗ Avatar 載入失敗！");
        LogError($"  - 失敗原因: {loadRequestInfo.failedReason}");
        LogError($"  - 請求 ID: {loadRequestInfo.id}");
        LogError($"  - 當前狀態: {entity.CurrentState}");
    }

    #endregion

    #region 公開方法 - 可以從 Inspector 或其他腳本調用

    /// <summary>
    /// 顯示 Avatar 當前狀態資訊
    /// </summary>
    public void ShowAvatarInfo()
    {
        if (avatarEntity == null)
        {
            LogError("Avatar Entity 為空！");
            return;
        }

        Log("=== Avatar 資訊 ===");
        Log($"狀態: {avatarEntity.CurrentState}");
        Log($"是否為本地玩家: {avatarEntity.IsLocal}");
        Log($"是否已創建: {avatarEntity.IsCreated}");
        Log($"Active Stream LOD: {avatarEntity.activeStreamLod}");
        Log($"==================");
    }

    /// <summary>
    /// 切換 Avatar 流式傳輸 LOD 等級
    /// </summary>
    public void SetStreamLOD(int lodLevel)
    {
        if (avatarEntity == null || !avatarEntity.IsCreated) 
        {
            LogError("Avatar 尚未創建，無法設置 LOD");
            return;
        }

        // 0=Full, 1=High, 2=Medium, 3=Low
        lodLevel = Mathf.Clamp(lodLevel, 0, 3);
        var streamLod = (OvrAvatarEntity.StreamLOD)lodLevel;
        avatarEntity.ForceStreamLod(streamLod);
        Log($"Avatar Stream LOD 已設置為: {streamLod}");
    }

    /// <summary>
    /// 重新載入 Avatar（通過 Teardown 和重新創建）
    /// </summary>
    public void ReloadAvatar()
    {
        if (avatarEntity == null) return;

        Log("重新載入 Avatar（Teardown 並重新創建）...");
        
        // 新版 API 需要先 Teardown 然後讓組件自動重新創建
        if (avatarEntity.IsCreated)
        {
            avatarEntity.Teardown();
        }
        
        // 重新啟用組件會觸發重新創建
        StartCoroutine(ReenableAvatar());
    }

    private System.Collections.IEnumerator ReenableAvatar()
    {
        yield return new WaitForSeconds(0.1f);
        if (avatarEntity != null)
        {
            avatarEntity.enabled = false;
            yield return null;
            avatarEntity.enabled = true;
            Log("Avatar 重新啟用完成");
        }
    }

    #endregion

    #region 調試輔助方法

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SimpleAvatarTest] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[SimpleAvatarTest] {message}");
    }

    #endregion

    #region Unity 編輯器調試按鈕

#if UNITY_EDITOR
    [ContextMenu("顯示 Avatar 資訊")]
    private void EditorShowAvatarInfo()
    {
        ShowAvatarInfo();
    }

    [ContextMenu("設置 Stream LOD - Full")]
    private void EditorSetStreamLODFull()
    {
        SetStreamLOD(0);
    }

    [ContextMenu("設置 Stream LOD - High")]
    private void EditorSetStreamLODHigh()
    {
        SetStreamLOD(1);
    }

    [ContextMenu("設置 Stream LOD - Medium")]
    private void EditorSetStreamLODMedium()
    {
        SetStreamLOD(2);
    }

    [ContextMenu("設置 Stream LOD - Low")]
    private void EditorSetStreamLODLow()
    {
        SetStreamLOD(3);
    }

    [ContextMenu("重新載入 Avatar")]
    private void EditorReloadAvatar()
    {
        ReloadAvatar();
    }
#endif

    #endregion
}
