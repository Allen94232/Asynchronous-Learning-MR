using UnityEngine;
using Oculus.Avatar2;
using System.Collections;

/// <summary>
/// 快速切換 Avatar 外觀
/// 支援 SampleAvatarEntity 的 Preset 和 CDN User ID 切換
/// 
/// 使用方法：
/// 1. 將此腳本掛到有 SampleAvatarEntity 的 GameObject 上
/// 2. 按數字鍵切換預設 Avatar（0-9）或從 CDN 載入測試 Avatar（F1-F4）
/// </summary>
public class AvatarSwitcher : MonoBehaviour
{
    [Header("Avatar 設定")]
    [Tooltip("要切換外觀的 Avatar（SampleAvatarEntity）")]
    public OvrAvatarEntity avatarEntity;

    [Header("本地 Preset Avatar（從 zip 載入）")]
    [Tooltip("Meta SDK 內建的 Preset Avatar 索引（0-9）")]
    public string[] presetPaths = new string[]
    {
        "0",   // Preset 0 - 預設男性
        "1",   // Preset 1 - 預設女性
        "2",   // Preset 2
        "3",   // Preset 3
        "4",   // Preset 4
        "5",   // Preset 5
        "6",   // Preset 6
        "7",   // Preset 7
        "8",   // Preset 8
        "9"    // Preset 9
    };

    [Header("CDN 測試 Avatar ID")]
    [Tooltip("從 Meta CDN 載入的測試 Avatar ID")]
    public ulong[] testAvatarIds = new ulong[]
    {
        10150662882222579,  // 測試 Avatar 1
        10150790293402892,  // 測試 Avatar 2
        10208503309050098,  // 測試 Avatar 3
        10101736649754876   // 測試 Avatar 4
    };

    private int currentPresetIndex = 0;
    private int currentCdnIndex = 0;
    private bool isLoadingFromCdn = false;

    void Start()
    {
        if (avatarEntity == null)
        {
            avatarEntity = GetComponent<OvrAvatarEntity>();
        }
    }

    void Update()
    {
        // **修改**：使用 Ctrl + 數字鍵 0-9 切換本地 Preset Avatar（避免與 StudentPlaybackManager 的 1-9 步驟播放衝突）
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKeyDown(KeyCode.Alpha0)) LoadPresetAvatar(0);
            else if (Input.GetKeyDown(KeyCode.Alpha1)) LoadPresetAvatar(1);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) LoadPresetAvatar(2);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) LoadPresetAvatar(3);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) LoadPresetAvatar(4);
            else if (Input.GetKeyDown(KeyCode.Alpha5)) LoadPresetAvatar(5);
            else if (Input.GetKeyDown(KeyCode.Alpha6)) LoadPresetAvatar(6);
            else if (Input.GetKeyDown(KeyCode.Alpha7)) LoadPresetAvatar(7);
            else if (Input.GetKeyDown(KeyCode.Alpha8)) LoadPresetAvatar(8);
            else if (Input.GetKeyDown(KeyCode.Alpha9)) LoadPresetAvatar(9);
        }
        
        // 按 F1-F4 切換 CDN 測試 Avatar
        if (Input.GetKeyDown(KeyCode.F1)) LoadCdnAvatar(0);
        else if (Input.GetKeyDown(KeyCode.F2)) LoadCdnAvatar(1);
        else if (Input.GetKeyDown(KeyCode.F3)) LoadCdnAvatar(2);
        else if (Input.GetKeyDown(KeyCode.F4)) LoadCdnAvatar(3);
    }

    /// <summary>
    /// 載入本地 Preset Avatar（從 OvrAvatar2Assets.zip）
    /// </summary>
    void LoadPresetAvatar(int presetIndex)
    {
        if (presetIndex < 0 || presetIndex >= presetPaths.Length)
        {
            Debug.LogWarning($"[AvatarSwitcher] 無效的 Preset 索引: {presetIndex}");
            return;
        }

        if (avatarEntity == null)
        {
            Debug.LogError("[AvatarSwitcher] AvatarEntity 未設定");
            return;
        }

        currentPresetIndex = presetIndex;
        isLoadingFromCdn = false;
        
        string presetPath = presetPaths[presetIndex];
        
        Debug.Log($"[AvatarSwitcher] 切換到 Preset Avatar {presetIndex}: {presetPath}");
        
        // 使用反射或直接呼叫 SampleAvatarEntity 的方法
        // 方法 1: 透過設定 Asset 並重新載入
        StartCoroutine(LoadPresetAvatarCoroutine(presetPath));
    }
    
    IEnumerator LoadPresetAvatarCoroutine(string presetPath)
    {
        // 先 Tear Down 當前 Avatar
        if (avatarEntity.IsCreated)
        {
            avatarEntity.Teardown();
        }
        
        yield return null;
        
        // 設定為本地載入模式
        var sampleAvatar = avatarEntity as SampleAvatarEntity;
        if (sampleAvatar != null)
        {
            // 使用反射設定私有欄位（因為 SampleAvatarEntity 沒有公開方法）
            var field = typeof(SampleAvatarEntity).GetField("_loadUserFromCdn", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(sampleAvatar, false);
            }
        }
        
        // 重新啟用 GameObject 以觸發 Avatar 重新載入
        avatarEntity.gameObject.SetActive(false);
        yield return null;
        avatarEntity.gameObject.SetActive(true);
    }

    /// <summary>
    /// 從 CDN 載入測試 Avatar
    /// </summary>
    void LoadCdnAvatar(int cdnIndex)
    {
        if (cdnIndex < 0 || cdnIndex >= testAvatarIds.Length)
        {
            Debug.LogWarning($"[AvatarSwitcher] 無效的 CDN Avatar 索引: {cdnIndex}");
            return;
        }

        if (avatarEntity == null)
        {
            Debug.LogError("[AvatarSwitcher] AvatarEntity 未設定");
            return;
        }

        currentCdnIndex = cdnIndex;
        isLoadingFromCdn = true;
        
        ulong userId = testAvatarIds[cdnIndex];
        
        Debug.Log($"[AvatarSwitcher] 從 CDN 載入 Avatar {cdnIndex + 1}: User ID {userId}");
        
        StartCoroutine(LoadCdnAvatarCoroutine(userId));
    }
    
    IEnumerator LoadCdnAvatarCoroutine(ulong userId)
    {
        // 先 Tear Down 當前 Avatar
        if (avatarEntity.IsCreated)
        {
            avatarEntity.Teardown();
        }
        
        yield return null;
        
        // 設定為 CDN 載入模式
        var sampleAvatar = avatarEntity as SampleAvatarEntity;
        if (sampleAvatar != null)
        {
            // 設定 User ID
            var userIdField = typeof(OvrAvatarEntity).GetField("_userId", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (userIdField != null)
            {
                userIdField.SetValue(sampleAvatar, userId);
            }
            
            // 設定為從 CDN 載入
            var cdnField = typeof(SampleAvatarEntity).GetField("_loadUserFromCdn", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (cdnField != null)
            {
                cdnField.SetValue(sampleAvatar, true);
            }
        }
        
        // 重新啟用 GameObject 以觸發 Avatar 重新載入
        avatarEntity.gameObject.SetActive(false);
        yield return null;
        avatarEntity.gameObject.SetActive(true);
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 16;
        style.normal.textColor = Color.yellow;
        style.fontStyle = FontStyle.Bold;

        string message = "";
        if (isLoadingFromCdn)
        {
            message = $"當前 CDN Avatar {currentCdnIndex + 1} (User ID: {testAvatarIds[currentCdnIndex]})";
        }
        else
        {
            message = $"當前 Preset Avatar {currentPresetIndex}";
        }
        
        GUI.Label(new Rect(10, 10, 600, 25), message, style);
        
        style.fontSize = 14;
        style.fontStyle = FontStyle.Normal;
        GUI.Label(new Rect(10, 35, 600, 20), "按 Ctrl+0-9 切換 Preset Avatar | 按 F1-F4 切換 CDN 測試 Avatar", style);
    }
}
