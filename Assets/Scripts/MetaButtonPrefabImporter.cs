using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEditor;

/// <summary>
/// Meta VR 按鈕 Prefab 匯入工具
/// 自動從 Meta XR SDK 複製範例按鈕到專案中
/// </summary>
public class MetaButtonPrefabImporter : MonoBehaviour
{
    [Header("匯入設置")]
    [Tooltip("匯入目標資料夾")]
    public string targetFolder = "Assets/Prefabs/VRButtons";
    
    [Header("按鈕選項")]
    [Tooltip("匯入 Poke Button（推薦）")]
    public bool importPokeButton = true;
    
    [Tooltip("匯入 Circular Button")]
    public bool importCircularButton = true;
    
    [Tooltip("匯入 Menu Button")]
    public bool importMenuButton = true;
    
    // Prefab 路徑
    private const string POKE_BUTTON_PATH = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/OculusInteractionSamplePokeButton.prefab";
    private const string CIRCULAR_BUTTON_PATH = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/CircularButton.prefab";
    private const string MENU_BUTTON_PATH = "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/MenuButton.prefab";
    
    /// <summary>
    /// 顯示所有可用的按鈕 Prefab 路徑
    /// </summary>
    [ContextMenu("列出所有 Meta 按鈕 Prefab")]
    public void ListAllButtonPrefabs()
    {
        Debug.Log("=== Meta XR SDK 按鈕 Prefab 列表 ===\n");
        
        Debug.Log("【推薦使用】");
        Debug.Log($"1. Poke Button: {POKE_BUTTON_PATH}");
        Debug.Log($"2. Circular Button: {CIRCULAR_BUTTON_PATH}");
        Debug.Log($"3. Menu Button: {MENU_BUTTON_PATH}");
        
        Debug.Log("\n【其他按鈕】");
        Debug.Log("4. TextTileButton: Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/TextTileButton_IconAndLabel_Regular_ComprehensiveScene.prefab");
        Debug.Log("5. ButtonMenu: Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/OculusInteractionSamplesButtonMenu.prefab");
        Debug.Log("6. Avatars SDK Button: Assets/Samples/Meta Avatars SDK/40.0.1/Sample Scenes/Common/Prefabs/UI/AvatarsSDKUIButton.prefab");
        
        Debug.Log("\n=== 使用方式 ===");
        Debug.Log("方法1: 在 Project 視窗找到上述路徑，直接拖入場景");
        Debug.Log("方法2: 右鍵此組件 → '匯入按鈕到專案'");
    }
    
    /// <summary>
    /// 匯入選中的按鈕到專案中
    /// </summary>
    [ContextMenu("匯入按鈕到專案")]
    public void ImportButtonsToProject()
    {
#if UNITY_EDITOR
        // 創建目標資料夾
        if (!AssetDatabase.IsValidFolder(targetFolder))
        {
            string parentFolder = Path.GetDirectoryName(targetFolder);
            string folderName = Path.GetFileName(targetFolder);
            AssetDatabase.CreateFolder(parentFolder, folderName);
            Debug.Log($"創建資料夾: {targetFolder}");
        }
        
        int importCount = 0;
        
        if (importPokeButton)
        {
            if (CopyPrefab(POKE_BUTTON_PATH, "PokeButton.prefab"))
                importCount++;
        }
        
        if (importCircularButton)
        {
            if (CopyPrefab(CIRCULAR_BUTTON_PATH, "CircularButton.prefab"))
                importCount++;
        }
        
        if (importMenuButton)
        {
            if (CopyPrefab(MENU_BUTTON_PATH, "MenuButton.prefab"))
                importCount++;
        }
        
        AssetDatabase.Refresh();
        Debug.Log($"✓ 匯入完成！共匯入 {importCount} 個按鈕到 {targetFolder}");
        Debug.Log("現在可以從 Project 視窗拖入場景使用");
#else
        Debug.LogWarning("此功能只能在 Unity Editor 中使用");
#endif
    }
    
#if UNITY_EDITOR
    private bool CopyPrefab(string sourcePath, string targetName)
    {
        try
        {
            // 檢查來源是否存在
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"找不到 Prefab: {sourcePath}");
                return false;
            }
            
            // 目標路徑
            string targetPath = Path.Combine(targetFolder, targetName);
            
            // 複製檔案
            AssetDatabase.CopyAsset(sourcePath, targetPath);
            Debug.Log($"✓ 已匯入: {targetName}");
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"匯入失敗 {targetName}: {e.Message}");
            return false;
        }
    }
#endif
    
    /// <summary>
    /// 在場景中創建 Meta Poke Button
    /// </summary>
    [ContextMenu("在場景中創建 Poke Button")]
    public void CreatePokeButtonInScene()
    {
#if UNITY_EDITOR
        // 載入 Prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(POKE_BUTTON_PATH);
        
        if (prefab == null)
        {
            Debug.LogError($"找不到 Prefab: {POKE_BUTTON_PATH}");
            Debug.LogWarning("請確認已安裝 Meta XR Interaction SDK");
            return;
        }
        
        // 實例化到場景中
        GameObject button = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        
        // 放在 Canvas 下（如果有的話）
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            button.transform.SetParent(canvas.transform, false);
            button.transform.localPosition = Vector3.zero;
        }
        
        // 選中新按鈕
        Selection.activeGameObject = button;
        
        Debug.Log($"✓ 已創建 Poke Button: {button.name}");
        Debug.Log("提示: 可以在 Inspector 中修改按鈕文字和事件");
#endif
    }
    
    /// <summary>
    /// 將現有 Unity Button 轉換為 VR 可用按鈕
    /// </summary>
    [ContextMenu("將場景中的 Button 轉換為 VR Button")]
    public void ConvertSceneButtonsToVR()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        
        if (buttons.Length == 0)
        {
            Debug.LogWarning("場景中沒有找到 Button");
            return;
        }
        
        int convertCount = 0;
        
        foreach (Button button in buttons)
        {
            // 添加 Box Collider
            BoxCollider collider = button.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = button.gameObject.AddComponent<BoxCollider>();
                
                // 設置 Collider 大小
                RectTransform rect = button.GetComponent<RectTransform>();
                if (rect != null)
                {
                    collider.size = new Vector3(rect.rect.width, rect.rect.height, 10f);
                    collider.center = new Vector3(0, 0, -5f);
                }
                
                collider.isTrigger = true;
                convertCount++;
                
                Debug.Log($"✓ 已轉換: {button.name}");
            }
        }
        
        Debug.Log($"✓ 轉換完成！共轉換 {convertCount} 個按鈕");
        Debug.Log("現在這些按鈕應該可以用 Poke Interaction 觸碰了");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MetaButtonPrefabImporter))]
public class MetaButtonPrefabImporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        MetaButtonPrefabImporter importer = (MetaButtonPrefabImporter)target;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox("Meta VR 按鈕 Prefab 匯入工具\n使用下方按鈕快速匯入或創建 VR 按鈕", MessageType.Info);
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("📋 列出所有 Meta 按鈕 Prefab", GUILayout.Height(30)))
        {
            importer.ListAllButtonPrefabs();
        }
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("📥 匯入按鈕到專案", GUILayout.Height(30)))
        {
            importer.ImportButtonsToProject();
        }
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("➕ 在場景中創建 Poke Button", GUILayout.Height(30)))
        {
            importer.CreatePokeButtonInScene();
        }
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("🔄 轉換現有 Button 為 VR Button", GUILayout.Height(30)))
        {
            importer.ConvertSceneButtonsToVR();
        }
    }
}
#endif
