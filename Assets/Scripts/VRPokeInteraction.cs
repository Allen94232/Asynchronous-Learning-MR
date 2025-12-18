using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// VR UI 手部觸碰交互系統
/// 專門為 Meta Building Blocks Camera Rig 設計
/// 支援手指直接觸碰 UI（Poke Interaction）
/// 
/// 使用方式：
/// 1. 在 Canvas 上添加此腳本
/// 2. 設置 Hand Anchors 引用
/// 3. Canvas 會自動支援手指觸碰
/// 
/// 注意：Building Blocks 自帶的 Poke 系統也可以用，
/// 只需在 Canvas 上添加 PointableCanvas 組件
/// </summary>
public class VRPokeInteraction : MonoBehaviour
{
    [Header("手部設置")]
    [Tooltip("左手 Anchor（從 Building Blocks Camera Rig 中拖入）")]
    public Transform leftHandAnchor;
    
    [Tooltip("右手 Anchor（從 Building Blocks Camera Rig 中拖入）")]
    public Transform rightHandAnchor;
    
    [Tooltip("觸碰點偏移（從手掌中心到指尖）")]
    public Vector3 pokeOffset = new Vector3(0, 0, 0.05f);
    
    [Header("觸碰設置")]
    [Tooltip("觸碰檢測半徑")]
    public float pokeRadius = 0.02f;
    
    [Tooltip("觸碰深度閾值（進入多深才算按下）")]
    public float pokeDepthThreshold = 0.01f;
    
    [Tooltip("觸發 Click 的深度")]
    public float clickDepth = 0.02f;
    
    [Header("視覺反饋")]
    [Tooltip("顯示觸碰點指示器")]
    public bool showPokeIndicator = true;
    
    [Tooltip("指示器大小")]
    public float indicatorSize = 0.01f;
    
    [Tooltip("指示器顏色")]
    public Color indicatorColor = new Color(0f, 1f, 1f, 0.8f);
    
    [Header("進階設置")]
    [Tooltip("同時支援雙手觸碰")]
    public bool allowBothHands = true;
    
    [Tooltip("觸碰時震動反饋")]
    public bool hapticFeedback = true;
    
    [Tooltip("震動強度")]
    [Range(0f, 1f)]
    public float hapticStrength = 0.3f;
    
    // 私有變數
    private Canvas targetCanvas;
    private RectTransform canvasRect;
    private Camera eventCamera;
    
    private GameObject leftIndicator;
    private GameObject rightIndicator;
    
    // 觸碰狀態
    private PokeState leftPokeState = new PokeState();
    private PokeState rightPokeState = new PokeState();
    
    private class PokeState
    {
        public bool isHovering;
        public bool isPressed;
        public float pokeDepth;
        public GameObject hoveredObject;
        public GameObject pressedObject;
        public Vector3 lastPokePosition;
    }
    
    void Start()
    {
        targetCanvas = GetComponent<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("[VRPokeInteraction] 請將此腳本添加到 Canvas 物件上！");
            enabled = false;
            return;
        }
        
        canvasRect = GetComponent<RectTransform>();
        
        // 設置 Event Camera
        if (targetCanvas.renderMode == RenderMode.WorldSpace)
        {
            eventCamera = targetCanvas.worldCamera;
            if (eventCamera == null)
            {
                eventCamera = Camera.main;
                targetCanvas.worldCamera = eventCamera;
            }
        }
        
        // 確保有 GraphicRaycaster
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
        
        // 自動尋找手部 Anchors
        AutoFindHandAnchors();
        
        // 創建觸碰指示器
        if (showPokeIndicator)
        {
            CreateIndicators();
        }
    }
    
    void AutoFindHandAnchors()
    {
        if (leftHandAnchor == null)
        {
            // Building Blocks Camera Rig 的結構
            var leftHand = GameObject.Find("LeftHandAnchor");
            if (leftHand == null) leftHand = GameObject.Find("LeftControllerAnchor");
            if (leftHand == null) leftHand = GameObject.Find("Left Hand Anchor");
            if (leftHand == null) leftHand = GameObject.Find("LeftHand");
            
            // 嘗試尋找 OVRHand（檢查名稱包含 Left）
            var ovrHands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
            foreach (var hand in ovrHands)
            {
                if (hand.gameObject.name.ToLower().Contains("left"))
                {
                    leftHand = hand.gameObject;
                    break;
                }
            }
            
            if (leftHand != null) leftHandAnchor = leftHand.transform;
        }
        
        if (rightHandAnchor == null)
        {
            var rightHand = GameObject.Find("RightHandAnchor");
            if (rightHand == null) rightHand = GameObject.Find("RightControllerAnchor");
            if (rightHand == null) rightHand = GameObject.Find("Right Hand Anchor");
            if (rightHand == null) rightHand = GameObject.Find("RightHand");
            
            var ovrHands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
            foreach (var hand in ovrHands)
            {
                if (hand.gameObject.name.ToLower().Contains("right"))
                {
                    rightHand = hand.gameObject;
                    break;
                }
            }
            
            if (rightHand != null) rightHandAnchor = rightHand.transform;
        }
        
        if (leftHandAnchor == null && rightHandAnchor == null)
        {
            Debug.LogWarning("[VRPokeInteraction] 未找到手部 Anchors，請手動設置或確認 Building Blocks Camera Rig 已正確設置");
        }
    }
    
    void CreateIndicators()
    {
        leftIndicator = CreateSingleIndicator("LeftPokeIndicator");
        rightIndicator = CreateSingleIndicator("RightPokeIndicator");
    }
    
    GameObject CreateSingleIndicator(string name)
    {
        var indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicator.name = name;
        indicator.transform.localScale = Vector3.one * indicatorSize;
        
        // 移除碰撞器
        var collider = indicator.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        
        // 設置材質
        var renderer = indicator.GetComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = indicatorColor;
        
        indicator.SetActive(false);
        return indicator;
    }
    
    void Update()
    {
        // 處理左手
        if (leftHandAnchor != null)
        {
            Vector3 pokePos = GetPokePosition(leftHandAnchor);
            UpdatePokeInteraction(pokePos, leftPokeState, leftIndicator, OVRInput.Controller.LTouch);
        }
        
        // 處理右手
        if (rightHandAnchor != null && allowBothHands)
        {
            Vector3 pokePos = GetPokePosition(rightHandAnchor);
            UpdatePokeInteraction(pokePos, rightPokeState, rightIndicator, OVRInput.Controller.RTouch);
        }
        else if (rightHandAnchor != null)
        {
            Vector3 pokePos = GetPokePosition(rightHandAnchor);
            UpdatePokeInteraction(pokePos, rightPokeState, rightIndicator, OVRInput.Controller.RTouch);
        }
    }
    
    Vector3 GetPokePosition(Transform handAnchor)
    {
        // 觸碰點 = 手部位置 + 偏移（指向前方）
        return handAnchor.position + handAnchor.rotation * pokeOffset;
    }
    
    void UpdatePokeInteraction(Vector3 pokePosition, PokeState state, GameObject indicator, OVRInput.Controller controller)
    {
        // 更新指示器位置
        if (indicator != null && showPokeIndicator)
        {
            indicator.transform.position = pokePosition;
            indicator.SetActive(true);
        }
        
        // 計算到 Canvas 平面的距離
        Vector3 canvasNormal = targetCanvas.transform.forward;
        Vector3 canvasPoint = targetCanvas.transform.position;
        float distanceToPlane = Vector3.Dot(pokePosition - canvasPoint, canvasNormal);
        
        // 檢查是否在 Canvas 範圍內
        Vector3 localPoint = targetCanvas.transform.InverseTransformPoint(pokePosition);
        bool isInBounds = Mathf.Abs(localPoint.x) <= canvasRect.rect.width / 2 &&
                         Mathf.Abs(localPoint.y) <= canvasRect.rect.height / 2;
        
        // 找到觸碰的 UI 元素
        GameObject hitObject = null;
        if (isInBounds && distanceToPlane > -pokeRadius && distanceToPlane < pokeRadius + clickDepth)
        {
            hitObject = FindUIElementAtPosition(pokePosition);
        }
        
        // 處理 Hover
        if (hitObject != state.hoveredObject)
        {
            // 離開舊的
            if (state.hoveredObject != null)
            {
                SendPointerEvent(state.hoveredObject, ExecuteEvents.pointerExitHandler);
            }
            
            // 進入新的
            if (hitObject != null)
            {
                SendPointerEvent(hitObject, ExecuteEvents.pointerEnterHandler);
                
                // 震動反饋
                if (hapticFeedback)
                {
                    OVRInput.SetControllerVibration(0.1f, hapticStrength * 0.5f, controller);
                }
            }
            
            state.hoveredObject = hitObject;
        }
        
        // 更新指示器顏色
        if (indicator != null)
        {
            var renderer = indicator.GetComponent<MeshRenderer>();
            if (hitObject != null)
            {
                renderer.material.color = Color.green;
            }
            else
            {
                renderer.material.color = indicatorColor;
            }
        }
        
        // 處理觸碰深度
        state.pokeDepth = -distanceToPlane; // 正值表示穿入 Canvas
        
        // 按下判定
        if (!state.isPressed && state.pokeDepth > pokeDepthThreshold && hitObject != null)
        {
            // 開始按下
            state.isPressed = true;
            state.pressedObject = hitObject;
            
            SendPointerEvent(hitObject, ExecuteEvents.pointerDownHandler);
            
            // 震動反饋
            if (hapticFeedback)
            {
                OVRInput.SetControllerVibration(0.2f, hapticStrength, controller);
            }
        }
        
        // Click 判定
        if (state.isPressed && state.pokeDepth > clickDepth && state.pressedObject != null)
        {
            // 觸發 Click
            var button = state.pressedObject.GetComponent<Button>();
            if (button != null && button.interactable)
            {
                button.onClick.Invoke();
            }
            
            SendPointerEvent(state.pressedObject, ExecuteEvents.pointerClickHandler);
            
            // 強震動反饋
            if (hapticFeedback)
            {
                OVRInput.SetControllerVibration(0.3f, hapticStrength * 1.5f, controller);
            }
            
            // 重置狀態（防止連續觸發）
            state.isPressed = false;
            state.pressedObject = null;
        }
        
        // 釋放判定
        if (state.isPressed && state.pokeDepth < pokeDepthThreshold * 0.5f)
        {
            // 釋放
            if (state.pressedObject != null)
            {
                SendPointerEvent(state.pressedObject, ExecuteEvents.pointerUpHandler);
            }
            
            state.isPressed = false;
            state.pressedObject = null;
        }
        
        state.lastPokePosition = pokePosition;
    }
    
    GameObject FindUIElementAtPosition(Vector3 worldPosition)
    {
        // 將世界座標轉換為螢幕座標
        if (eventCamera == null) return null;
        
        Vector3 screenPoint = eventCamera.WorldToScreenPoint(worldPosition);
        
        // 使用 EventSystem 進行 Raycast
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = screenPoint;
        
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        
        // 找到屬於這個 Canvas 的 UI 元素
        foreach (var result in results)
        {
            if (result.gameObject.GetComponentInParent<Canvas>() == targetCanvas)
            {
                // 確認是可交互的元素
                var selectable = result.gameObject.GetComponent<Selectable>();
                if (selectable != null && selectable.interactable)
                {
                    return result.gameObject;
                }
            }
        }
        
        return null;
    }
    
    void SendPointerEvent<T>(GameObject target, ExecuteEvents.EventFunction<T> eventFunction) where T : IEventSystemHandler
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(target, pointerData, eventFunction);
    }
    
    void OnDestroy()
    {
        if (leftIndicator != null) Destroy(leftIndicator);
        if (rightIndicator != null) Destroy(rightIndicator);
    }
    
    /// <summary>
    /// 手動設置手部 Anchors
    /// </summary>
    public void SetHandAnchors(Transform left, Transform right)
    {
        leftHandAnchor = left;
        rightHandAnchor = right;
    }
}
