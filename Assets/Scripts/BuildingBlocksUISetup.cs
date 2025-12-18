using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Building Blocks UI 設置助手
/// 自動為 Canvas 設置 Meta XR Interaction SDK 的 Poke 交互
/// 
/// 使用方式（二選一）：
/// 
/// 【方法一：使用此腳本（簡單）】
/// 1. 在 Canvas 上添加此腳本
/// 2. 腳本會自動設置所需組件
/// 
/// 【方法二：使用 Building Blocks（推薦）】
/// 1. 在 Unity 選單 → Meta → Tools → Building Blocks
/// 2. 搜尋 "Pointable Canvas"
/// 3. 點擊添加到你的 Canvas
/// 4. Building Blocks 會自動設置所有交互組件
/// 
/// Building Blocks Camera Rig 結構：
/// - OVRCameraRig / [BuildingBlock] Camera Rig
///   ├── TrackingSpace
///   │   ├── CenterEyeAnchor
///   │   ├── LeftHandAnchor (或 LeftControllerAnchor)
///   │   │   └── [BuildingBlock] Poke Interactor
///   │   └── RightHandAnchor (或 RightControllerAnchor)
///   │       └── [BuildingBlock] Poke Interactor
/// </summary>
public class BuildingBlocksUISetup : MonoBehaviour
{
    [Header("自動設置選項")]
    [Tooltip("自動尋找並設置 Hand Anchors")]
    public bool autoFindHands = true;
    
    [Tooltip("使用 Meta Interaction SDK 的 PointableCanvas")]
    public bool useMetaInteractionSDK = true;
    
    [Header("手動引用（可選）")]
    [Tooltip("左手 Poke 位置")]
    public Transform leftPokePoint;
    
    [Tooltip("右手 Poke 位置")]
    public Transform rightPokePoint;
    
    [Header("除錯")]
    public bool showDebugInfo = false;
    
    private Canvas targetCanvas;
    
    void Start()
    {
        targetCanvas = GetComponent<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("[BuildingBlocksUISetup] 請將此腳本添加到 Canvas 上！");
            return;
        }
        
        SetupCanvas();
        
        if (autoFindHands)
        {
            FindHandAnchors();
        }
        
        if (useMetaInteractionSDK)
        {
            SetupMetaInteraction();
        }
    }
    
    void SetupCanvas()
    {
        // 確保是 World Space Canvas
        if (targetCanvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning("[BuildingBlocksUISetup] 建議使用 World Space Canvas");
        }
        
        // 設置 Event Camera
        if (targetCanvas.worldCamera == null)
        {
            targetCanvas.worldCamera = Camera.main;
        }
        
        // 確保有 GraphicRaycaster
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }
    
    void FindHandAnchors()
    {
        // Building Blocks Camera Rig 的標準命名
        string[] leftHandNames = { 
            "LeftHandAnchor", 
            "LeftControllerAnchor", 
            "Left Hand Anchor",
            "LeftHand",
            "LeftPokeInteractor"
        };
        
        string[] rightHandNames = { 
            "RightHandAnchor", 
            "RightControllerAnchor", 
            "Right Hand Anchor",
            "RightHand",
            "RightPokeInteractor"
        };
        
        if (leftPokePoint == null)
        {
            foreach (var name in leftHandNames)
            {
                var obj = GameObject.Find(name);
                if (obj != null)
                {
                    leftPokePoint = obj.transform;
                    if (showDebugInfo) Debug.Log($"[BuildingBlocksUISetup] 找到左手: {name}");
                    break;
                }
            }
        }
        
        if (rightPokePoint == null)
        {
            foreach (var name in rightHandNames)
            {
                var obj = GameObject.Find(name);
                if (obj != null)
                {
                    rightPokePoint = obj.transform;
                    if (showDebugInfo) Debug.Log($"[BuildingBlocksUISetup] 找到右手: {name}");
                    break;
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[BuildingBlocksUISetup] 左手: {(leftPokePoint != null ? leftPokePoint.name : "未找到")}");
            Debug.Log($"[BuildingBlocksUISetup] 右手: {(rightPokePoint != null ? rightPokePoint.name : "未找到")}");
        }
    }
    
    void SetupMetaInteraction()
    {
        // 檢查是否已有 PointableCanvas 組件
        var pointableCanvas = GetComponent("PointableCanvas");
        if (pointableCanvas != null)
        {
            if (showDebugInfo) Debug.Log("[BuildingBlocksUISetup] PointableCanvas 已存在");
            return;
        }
        
        // 嘗試添加 PointableCanvas（來自 Meta XR Interaction SDK）
        System.Type pointableCanvasType = System.Type.GetType("Oculus.Interaction.PointableCanvas, Oculus.Interaction");
        
        if (pointableCanvasType != null)
        {
            gameObject.AddComponent(pointableCanvasType);
            if (showDebugInfo) Debug.Log("[BuildingBlocksUISetup] 已添加 PointableCanvas");
        }
        else
        {
            Debug.LogWarning("[BuildingBlocksUISetup] 找不到 PointableCanvas，請確認已安裝 Meta XR Interaction SDK，或使用 Building Blocks 手動添加");
            
            // 提示用戶手動設置
            Debug.Log("[BuildingBlocksUISetup] 手動設置步驟：");
            Debug.Log("  1. Unity 選單 → Meta → Tools → Building Blocks");
            Debug.Log("  2. 搜尋 'Pointable Canvas'");
            Debug.Log("  3. 選中你的 Canvas，點擊添加 Block");
        }
    }
    
    /// <summary>
    /// 在 Inspector 中提供快速設置按鈕
    /// </summary>
    [ContextMenu("列出場景中的手部物件")]
    public void ListHandObjects()
    {
        Debug.Log("=== 場景中可能的手部物件 ===");
        
        // 尋找所有可能是手部的物件
        string[] keywords = { "hand", "controller", "anchor", "poke", "touch" };
        
        var allObjects = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var obj in allObjects)
        {
            string lowerName = obj.name.ToLower();
            foreach (var keyword in keywords)
            {
                if (lowerName.Contains(keyword))
                {
                    Debug.Log($"  - {obj.name} (路徑: {GetFullPath(obj)})");
                    break;
                }
            }
        }
    }
    
    string GetFullPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
    
    [ContextMenu("顯示 Building Blocks 設置說明")]
    public void ShowSetupInstructions()
    {
        string instructions = @"
========================================
Building Blocks VR UI 設置說明
========================================

【步驟 1：設置 Camera Rig】
1. 刪除場景中的 Main Camera
2. Unity 選單 → Meta → Tools → Building Blocks
3. 搜尋 'Camera Rig' 並添加
4. 如果要手部追蹤，添加 'Hand Tracking'
5. 如果要 Passthrough，添加 'Passthrough'

【步驟 2：設置 UI 交互】
1. 選中你的 Canvas
2. 在 Building Blocks 中搜尋 'Poke'
3. 添加 'Poke Interactor' 到雙手
4. 添加 'Pointable Canvas' 到 Canvas

【步驟 3：設置 Canvas】
- Render Mode: World Space
- Event Camera: 拖入 CenterEyeAnchor 的 Camera
- 添加 GraphicRaycaster 組件

【步驟 4：按鈕設置】
- 確保按鈕有 Collider（Box Collider）
- 或使用 Building Blocks 的 'Pointable Unity UI'

========================================
";
        Debug.Log(instructions);
    }
}
