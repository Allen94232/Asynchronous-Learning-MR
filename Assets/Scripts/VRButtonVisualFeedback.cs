using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// VR 按鈕視覺反饋系統
/// 當按鈕被按下時，提供視覺反饋（移動、縮放、顏色變化）
/// 參考 Meta XR Interaction SDK 的 ManipulatorAffordanceController
/// </summary>
public class VRButtonVisualFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("按鈕組件")]
    [SerializeField] private Button button;
    [SerializeField] private RectTransform buttonRectTransform;
    
    [Header("背景面板（可選）")]
    [Tooltip("按鈕的背景面板，會跟著按鈕一起移動")]
    [SerializeField] private RectTransform backgroundPanel;

    [Header("按下視覺反饋設置")]
    [Tooltip("按下時的位置偏移（Z軸，負數表示向內凹）")]
    [SerializeField] private float pressedDepth = -0.01f;
    
    [Tooltip("按下時的縮放比例")]
    [SerializeField] private float pressedScale = 0.95f;
    
    [Tooltip("按下時的顏色暗化程度（0-1，數值越大越暗）")]
    [SerializeField] private float pressedDarken = 0.2f;

    [Header("Hover 視覺反饋設置")]
    [Tooltip("Hover 時的縮放比例")]
    [SerializeField] private float hoverScale = 1.05f;
    
    [Tooltip("Hover 時的發光強度")]
    [SerializeField] private float hoverBrightness = 1.2f;

    [Header("動畫設置")]
    [Tooltip("動畫過渡時間（秒）")]
    [SerializeField] private float transitionDuration = 0.1f;

    [Header("音效設置（可選）")]
    [Tooltip("按下音效")]
    [SerializeField] private AudioClip pressSound;
    
    [Tooltip("釋放音效")]
    [SerializeField] private AudioClip releaseSound;
    
    [Tooltip("Hover 音效")]
    [SerializeField] private AudioClip hoverSound;

    // 私有變量
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Color originalColor;
    private Image buttonImage;
    private AudioSource audioSource;
    private bool isPressed = false;
    private bool isHovered = false;

    private void Awake()
    {
        // 自動獲取組件
        if (button == null)
            button = GetComponent<Button>();
        
        if (buttonRectTransform == null)
            buttonRectTransform = GetComponent<RectTransform>();
        
        buttonImage = GetComponent<Image>();
        
        // 添加 AudioSource（如果需要音效）
        if (pressSound != null || releaseSound != null || hoverSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D 音效
        }
    }

    private void Start()
    {
        // 保存原始狀態
        originalPosition = buttonRectTransform.localPosition;
        originalScale = buttonRectTransform.localScale;
        if (buttonImage != null)
            originalColor = buttonImage.color;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable) return;
        
        isPressed = true;
        ApplyPressedState();
        
        // 播放按下音效
        PlaySound(pressSound);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!button.interactable) return;
        
        isPressed = false;
        
        if (isHovered)
            ApplyHoverState();
        else
            ApplyNormalState();
        
        // 播放釋放音效
        PlaySound(releaseSound);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!button.interactable) return;
        
        isHovered = true;
        
        if (!isPressed)
        {
            ApplyHoverState();
            PlaySound(hoverSound);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!button.interactable) return;
        
        isHovered = false;
        
        if (!isPressed)
            ApplyNormalState();
    }

    private void ApplyPressedState()
    {
        // 按下效果：向內凹陷 + 縮小 + 變暗
        Vector3 pressedPosition = originalPosition;
        pressedPosition.z += pressedDepth;
        
        StopAllCoroutines();
        StartCoroutine(AnimateTransform(pressedPosition, originalScale * pressedScale));
        
        if (buttonImage != null)
        {
            Color darkenedColor = originalColor * (1f - pressedDarken);
            darkenedColor.a = originalColor.a;
            StartCoroutine(AnimateColor(darkenedColor));
        }

        // 背景面板跟隨移動
        if (backgroundPanel != null)
        {
            Vector3 panelPressedPosition = backgroundPanel.localPosition;
            panelPressedPosition.z += pressedDepth;
            StartCoroutine(AnimatePanel(panelPressedPosition));
        }
    }

    private void ApplyHoverState()
    {
        // Hover 效果：稍微放大 + 變亮
        StopAllCoroutines();
        StartCoroutine(AnimateTransform(originalPosition, originalScale * hoverScale));
        
        if (buttonImage != null)
        {
            Color brightColor = originalColor * hoverBrightness;
            brightColor.a = originalColor.a;
            StartCoroutine(AnimateColor(brightColor));
        }

        // 背景面板恢復原位
        if (backgroundPanel != null)
        {
            StartCoroutine(AnimatePanel(backgroundPanel.localPosition));
        }
    }

    private void ApplyNormalState()
    {
        // 恢復正常狀態
        StopAllCoroutines();
        StartCoroutine(AnimateTransform(originalPosition, originalScale));
        
        if (buttonImage != null)
        {
            StartCoroutine(AnimateColor(originalColor));
        }

        // 背景面板恢復原位
        if (backgroundPanel != null)
        {
            StartCoroutine(AnimatePanel(backgroundPanel.localPosition));
        }
    }

    private System.Collections.IEnumerator AnimateTransform(Vector3 targetPosition, Vector3 targetScale)
    {
        float elapsed = 0f;
        Vector3 startPosition = buttonRectTransform.localPosition;
        Vector3 startScale = buttonRectTransform.localScale;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            
            // 使用平滑插值
            t = Mathf.SmoothStep(0f, 1f, t);
            
            buttonRectTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            buttonRectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            
            yield return null;
        }

        buttonRectTransform.localPosition = targetPosition;
        buttonRectTransform.localScale = targetScale;
    }

    private System.Collections.IEnumerator AnimateColor(Color targetColor)
    {
        if (buttonImage == null) yield break;

        float elapsed = 0f;
        Color startColor = buttonImage.color;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            
            t = Mathf.SmoothStep(0f, 1f, t);
            
            buttonImage.color = Color.Lerp(startColor, targetColor, t);
            
            yield return null;
        }

        buttonImage.color = targetColor;
    }

    private System.Collections.IEnumerator AnimatePanel(Vector3 targetPosition)
    {
        if (backgroundPanel == null) yield break;

        float elapsed = 0f;
        Vector3 startPosition = backgroundPanel.localPosition;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            
            t = Mathf.SmoothStep(0f, 1f, t);
            
            backgroundPanel.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            
            yield return null;
        }

        backgroundPanel.localPosition = targetPosition;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // 公開方法：手動觸發狀態變化
    public void SetPressed(bool pressed)
    {
        isPressed = pressed;
        if (pressed)
            ApplyPressedState();
        else if (isHovered)
            ApplyHoverState();
        else
            ApplyNormalState();
    }

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        if (!isPressed)
        {
            if (hovered)
                ApplyHoverState();
            else
                ApplyNormalState();
        }
    }

    // 調試用：在 Editor 中可視化
    private void OnValidate()
    {
        if (buttonRectTransform == null)
            buttonRectTransform = GetComponent<RectTransform>();
    }
}
