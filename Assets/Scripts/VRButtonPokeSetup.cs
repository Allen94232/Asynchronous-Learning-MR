using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VR Button Poke 修復器
/// 自動為 Building Blocks Poke Interaction 添加必要的組件
/// 
/// 問題原因：
/// 1. Unity UI 的 Button 預設沒有 3D Collider
/// 2. Poke Interaction 需要 Collider 才能偵測到觸碰
/// 3. 需要正確的層級和碰撞設置
/// 
/// 使用方式：
/// 1. 將此腳本添加到 Canvas 上
/// 2. 腳本會自動為所有 Button 添加 Box Collider
/// 3. 調整 Collider 深度以適應 Poke
/// </summary>
[RequireComponent(typeof(Canvas))]
public class VRButtonPokeSetup : MonoBehaviour
{
    [Header("自動設置")]
    [Tooltip("自動為所有按鈕添加 Collider")]
    public bool autoAddColliders = true;
    
    [Tooltip("Collider 深度（Z軸厚度）")]
    public float colliderDepth = 0.01f;
    
    [Tooltip("Collider 向前延伸（讓手指更容易觸碰）")]
    public float colliderExtend = 0.02f;
    
    [Header("除錯")]
    [Tooltip("顯示除錯訊息")]
    public bool showDebugInfo = true;
    
    [Tooltip("顯示 Collider 邊界")]
    public bool showColliderBounds = false;
    
    private Canvas targetCanvas;
    
    void Start()
    {
        targetCanvas = GetComponent<Canvas>();
        
        if (autoAddColliders)
        {
            SetupAllButtons();
        }
    }
    
    /// <summary>
    /// 為 Canvas 下所有 Button 設置 Collider
    /// </summary>
    [ContextMenu("設置所有按鈕的 Collider")]
    public void SetupAllButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        
        if (showDebugInfo)
        {
            Debug.Log($"[VRButtonPokeSetup] 找到 {buttons.Length} 個按鈕");
        }
        
        int setupCount = 0;
        foreach (Button button in buttons)
        {
            if (SetupButton(button))
            {
                setupCount++;
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[VRButtonPokeSetup] 成功設置 {setupCount} 個按鈕");
        }
    }
    
    /// <summary>
    /// 為單個按鈕設置 Collider
    /// </summary>
    bool SetupButton(Button button)
    {
        // 檢查是否已有 Collider
        BoxCollider existingCollider = button.GetComponent<BoxCollider>();
        
        RectTransform rectTransform = button.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogWarning($"[VRButtonPokeSetup] {button.name} 沒有 RectTransform");
            return false;
        }
        
        BoxCollider collider = existingCollider;
        if (collider == null)
        {
            collider = button.gameObject.AddComponent<BoxCollider>();
            if (showDebugInfo)
            {
                Debug.Log($"[VRButtonPokeSetup] 為 {button.name} 添加 BoxCollider");
            }
        }
        
        // 計算 Collider 大小（基於 RectTransform）
        Rect rect = rectTransform.rect;
        Vector3 size = new Vector3(rect.width, rect.height, colliderDepth);
        
        // 設置 Collider
        collider.size = size;
        
        // 設置中心點（向前延伸一點，讓手指更容易觸碰）
        Vector3 center = new Vector3(0, 0, -(colliderExtend / 2));
        collider.center = center;
        
        // 設置為 Trigger（Poke Interaction 需要）
        collider.isTrigger = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"[VRButtonPokeSetup] {button.name}: Size={size}, Center={center}");
        }
        
        // 顯示邊界（除錯用）
        if (showColliderBounds)
        {
            CreateBoundsVisualizer(button.gameObject, collider);
        }
        
        return true;
    }
    
    /// <summary>
    /// 創建 Collider 邊界視覺化（除錯用）
    /// </summary>
    void CreateBoundsVisualizer(GameObject buttonObject, BoxCollider collider)
    {
        // 檢查是否已有視覺化物件
        Transform existing = buttonObject.transform.Find("_ColliderBounds");
        if (existing != null)
        {
            return;
        }
        
        // 創建立方體顯示 Collider 範圍
        GameObject boundsViz = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boundsViz.name = "_ColliderBounds";
        boundsViz.transform.SetParent(buttonObject.transform, false);
        boundsViz.transform.localPosition = collider.center;
        boundsViz.transform.localScale = collider.size;
        
        // 半透明材質
        var renderer = boundsViz.GetComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Transparent/Diffuse"));
        material.color = new Color(0f, 1f, 0f, 0.3f);
        renderer.material = material;
        
        // 移除自己的 Collider
        Destroy(boundsViz.GetComponent<Collider>());
    }
    
    /// <summary>
    /// 移除所有按鈕的 Collider（重置用）
    /// </summary>
    [ContextMenu("移除所有按鈕的 Collider")]
    public void RemoveAllColliders()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        
        foreach (Button button in buttons)
        {
            BoxCollider collider = button.GetComponent<BoxCollider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }
            
            // 移除邊界視覺化
            Transform boundsViz = button.transform.Find("_ColliderBounds");
            if (boundsViz != null)
            {
                DestroyImmediate(boundsViz.gameObject);
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[VRButtonPokeSetup] 已移除所有按鈕的 Collider");
        }
    }
    
    /// <summary>
    /// 檢查 Canvas 設置
    /// </summary>
    [ContextMenu("檢查 Canvas 設置")]
    public void ValidateCanvasSetup()
    {
        Debug.Log("=== Canvas 設置檢查 ===");
        
        Canvas canvas = GetComponent<Canvas>();
        
        // 1. 檢查 Render Mode
        Debug.Log($"1. Render Mode: {canvas.renderMode}");
        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning("   ⚠ 建議使用 World Space");
        }
        
        // 2. 檢查 Event Camera
        Debug.Log($"2. Event Camera: {(canvas.worldCamera != null ? canvas.worldCamera.name : "未設置")}");
        if (canvas.worldCamera == null)
        {
            Debug.LogWarning("   ⚠ 需要設置 Event Camera（CenterEyeAnchor 的相機）");
        }
        
        // 3. 檢查 Scale
        Vector3 scale = transform.localScale;
        Debug.Log($"3. Canvas Scale: {scale}");
        if (scale.x < 0.01f || scale.y < 0.01f)
        {
            Debug.LogWarning($"   ⚠ Scale 太小 ({scale})，建議至少 0.01 以上");
        }
        
        // 4. 檢查 GraphicRaycaster
        var raycaster = GetComponent<GraphicRaycaster>();
        Debug.Log($"4. GraphicRaycaster: {(raycaster != null ? "有" : "無")}");
        if (raycaster == null)
        {
            Debug.LogWarning("   ⚠ 缺少 GraphicRaycaster");
        }
        
        // 5. 檢查 PointableCanvas
        var pointable = GetComponent("PointableCanvas");
        Debug.Log($"5. PointableCanvas: {(pointable != null ? "有" : "無")}");
        if (pointable == null)
        {
            Debug.LogWarning("   ⚠ 缺少 PointableCanvas（Building Block）");
        }
        
        // 6. 檢查按鈕的 Collider
        Button[] buttons = GetComponentsInChildren<Button>(true);
        int buttonsWithCollider = 0;
        foreach (Button button in buttons)
        {
            if (button.GetComponent<BoxCollider>() != null)
            {
                buttonsWithCollider++;
            }
        }
        Debug.Log($"6. 按鈕狀態: {buttonsWithCollider}/{buttons.Length} 個按鈕有 Collider");
        if (buttonsWithCollider < buttons.Length)
        {
            Debug.LogWarning($"   ⚠ 有 {buttons.Length - buttonsWithCollider} 個按鈕缺少 Collider");
        }
        
        // 7. 檢查場景中的 Poke Interactor
        var pokeInteractors = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int pokeCount = 0;
        foreach (var obj in pokeInteractors)
        {
            if (obj.GetType().Name.Contains("Poke"))
            {
                Debug.Log($"   找到: {obj.gameObject.name} - {obj.GetType().Name}");
                pokeCount++;
            }
        }
        Debug.Log($"7. Poke Interactors: 找到 {pokeCount} 個");
        if (pokeCount == 0)
        {
            Debug.LogWarning("   ⚠ 未找到 Poke Interactor，請確認已添加到手部 Anchors");
        }
        
        Debug.Log("=== 檢查完成 ===");
    }
    
    /// <summary>
    /// 快速修復所有問題
    /// </summary>
    [ContextMenu("一鍵修復所有問題")]
    public void QuickFixAll()
    {
        Debug.Log("[VRButtonPokeSetup] 開始一鍵修復...");
        
        Canvas canvas = GetComponent<Canvas>();
        
        // 1. 設置 Render Mode
        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            Debug.Log("✓ 已設置為 World Space");
        }
        
        // 2. 設置 Event Camera
        if (canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
            Debug.Log("✓ 已設置 Event Camera");
        }
        
        // 3. 添加 GraphicRaycaster
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
            Debug.Log("✓ 已添加 GraphicRaycaster");
        }
        
        // 4. 為所有按鈕添加 Collider
        SetupAllButtons();
        
        // 5. 檢查 Scale
        Vector3 scale = transform.localScale;
        if (scale.x < 0.01f || scale.y < 0.01f || scale.z < 0.01f)
        {
            Debug.LogWarning($"⚠ Canvas Scale 很小 ({scale})，如果按鈕太小難以觸碰，請調整 Scale");
            Debug.LogWarning("  建議調整為至少 0.01 或更大");
        }
        
        Debug.Log("[VRButtonPokeSetup] 修復完成！請測試按鈕是否可以觸碰");
    }
}
