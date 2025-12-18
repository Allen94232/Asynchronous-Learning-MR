using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 摺紙形狀偵測器 - 與 Python YOLO 模型整合
/// 用於驗證學生的摺紙步驟是否正確
/// 支援 VR Passthrough 模式
/// </summary>
public class ShapeDetector : MonoBehaviour
{
    /// <summary>
    /// 截圖模式
    /// </summary>
    public enum CaptureMode
    {
        VirtualCamera,      // 使用虛擬相機截圖（適用於虛擬場景）
        WebCamera,          // 使用電腦鏡頭或 USB 攝影機
        RealSense,          // 使用 Intel RealSense 攝影機
        Manual              // 手動提供圖片路徑
    }
    [Header("Python 設置")]
    [Tooltip("Python 執行檔路徑（留空則使用專案的虛擬環境）")]
    [SerializeField] private string pythonPath = "";
    
    [Tooltip("偵測腳本路徑（相對於 Assets 資料夾）")]
    [SerializeField] private string scriptPath = "share_model/detect_shapes.py";
    
    [Tooltip("模型路徑（相對於腳本資料夾，留空使用預設）")]
    [SerializeField] private string modelPath = "";
    
    [Header("偵測設置")]
    [Tooltip("信心度閾值 (0-1)")]
    [Range(0.1f, 0.95f)]
    public float confidenceThreshold = 0.5f;
    
    [Tooltip("偵測超時時間（秒）")]
    [SerializeField] private float timeout = 30f;
    
    [Header("截圖設置")]
    [Tooltip("截圖模式選擇")]
    [SerializeField] private CaptureMode captureMode = CaptureMode.WebCamera;
    
    [Tooltip("用於截取摺紙畫面的相機（虛擬相機模式使用）")]
    [SerializeField] private Camera captureCamera;
    
    [Tooltip("截圖解析度")]
    [SerializeField] private Vector2Int screenshotResolution = new Vector2Int(640, 640);
    
    [Tooltip("手動圖片路徑（手動模式使用）")]
    [SerializeField] private string manualImagePath = "";
    
    [Header("WebCamera 設定（外接 USB 攝影機 - 推薦用於 MR/Passthrough）")]
    [Tooltip("攝影機裝置名稱（留空使用預設攝影機）\n提示：使用 Context Menu > 列出可用攝影機 來查看所有可用設備")]
    [SerializeField] private string webCameraDeviceName = "";
    
    [Tooltip("攝影機解析度（建議 1280x720 或 1920x1080）")]
    [SerializeField] private Vector2Int webCameraResolution = new Vector2Int(1280, 720);
    
    [Tooltip("攝影機 FPS")]
    [SerializeField] private int webCameraFPS = 30;
    
    [Tooltip("自動啟動攝影機（場景載入時自動開啟預覽）")]
    [SerializeField] private bool autoStartWebCamera = true;
    
    [Tooltip("顯示攝影機預覽（在 VR 中或 2D UI 上顯示相機畫面）")]
    [SerializeField] private UnityEngine.UI.RawImage cameraPreview;
    
    [Tooltip("是否需要將圖片裁切成正方形（YOLO 模型通常需要）")]
    [SerializeField] private bool cropToSquare = true;
    
    [Header("RealSense 設置（Intel RealSense 深度相機）")]
    [Tooltip("RealSense 使用 RGB 流（彩色圖像，用於 YOLO 辨識）")]
    [SerializeField] private bool realSenseUseRGB = true;
    
    [Tooltip("自動偵測 RealSense 設備（啟動時搜尋包含 'RealSense' 或 'Intel' 的相機）")]
    [SerializeField] private bool autoDetectRealSense = true;
    
    [Tooltip("RealSense 設備關鍵字（用於自動偵測，可自訂）")]
    [SerializeField] private string[] realSenseKeywords = { "realsense", "intel", "rgb camera" };
    
    [Header("截圖時機")]
    [Tooltip("截圖前延遲（秒），讓用戶準備好")]
    [SerializeField] private float captureDelay = 0.5f;
    
    [Tooltip("截圖倒數提示（可選）")]
    [SerializeField] private TMPro.TextMeshProUGUI countdownText;
    
    [Header("運行時測試")]
    [Tooltip("啟用鍵盤快捷鍵測試（按 T 鍵截圖，按 C 鍵顯示相機資訊）")]
    [SerializeField] private bool enableRuntimeTesting = true;
    
    [Tooltip("測試截圖的快捷鍵")]
    [SerializeField] private KeyCode testCaptureKey = KeyCode.T;
    
    [Tooltip("顯示相機資訊的快捷鍵")]
    [SerializeField] private KeyCode showCameraInfoKey = KeyCode.I;
    
    [Tooltip("運行時訊息顯示 UI（可選，用於顯示測試結果）")]
    [SerializeField] private TMPro.TextMeshProUGUI runtimeMessageText;
    
    [Header("除錯")]
    [SerializeField] private bool showDebugLogs = true;
    
    // 事件
    public event Action<VerificationResult> OnVerificationComplete;
    public event Action<string> OnError;
    
    // 狀態
    private bool isProcessing = false;
    
    // WebCamera 相關
    private WebCamTexture webCamTexture;
    private bool isWebCameraReady = false;
    
    public bool IsProcessing => isProcessing;
    public bool IsWebCameraReady => isWebCameraReady;
    
    // 路徑
    private string fullPythonPath;
    private string fullScriptPath;
    private string tempScreenshotPath;
    
    /// <summary>
    /// 單個檢測結果
    /// </summary>
    [Serializable]
    public class Detection
    {
        public string class_name;
        public int class_id;
        public float confidence;
        public float[] bbox;  // [x1, y1, x2, y2]
    }
    
    /// <summary>
    /// 驗證結果結構
    /// </summary>
    [Serializable]
    public class VerificationResult
    {
        public bool success;
        public string expected;
        public string detected;
        public float confidence;
        public string message;
        public string error;
        
        // 所有檢測結果（支持多检测验证）
        public Detection[] all_detections;
        
        // JSON 解析用的備用欄位
        public bool detected_any;  // 是否偵測到任何形狀
        public string class_name;
        public int class_id;
        
        /// <summary>
        /// 檢查所有檢測結果中是否有符合預期步驟的形狀
        /// </summary>
        public bool HasMatchingShape(int expectedStep, float minConfidence)
        {
            if (all_detections == null || all_detections.Length == 0)
                return false;
                
            string expectedClassName = $"shape_{expectedStep}";
            
            foreach (var detection in all_detections)
            {
                if (detection.class_name == expectedClassName && detection.confidence >= minConfidence)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 獲取符合預期步驟的最佳檢測結果
        /// </summary>
        public Detection GetBestMatchingDetection(int expectedStep)
        {
            if (all_detections == null || all_detections.Length == 0)
                return null;
                
            string expectedClassName = $"shape_{expectedStep}";
            Detection bestMatch = null;
            
            foreach (var detection in all_detections)
            {
                if (detection.class_name == expectedClassName)
                {
                    if (bestMatch == null || detection.confidence > bestMatch.confidence)
                    {
                        bestMatch = detection;
                    }
                }
            }
            
            return bestMatch;
        }
    }
    
    private void Awake()
    {
        InitializePaths();
    }
    
    private void Start()
    {
        // 自動啟動 WebCamera
        if (captureMode == CaptureMode.WebCamera && autoStartWebCamera)
        {
            StartWebCamera();
        }
        else if (captureMode == CaptureMode.RealSense)
        {
            StartRealSense();
            // RealSense 啟動需要時間，延遲檢查狀態
            StartCoroutine(CheckCameraStatusAfterDelay(2f));
        }
        else if (captureMode == CaptureMode.VirtualCamera)
        {
            // VirtualCamera 模式需要設定 captureCamera
            if (captureCamera == null)
            {
                Debug.LogWarning("[ShapeDetector] VirtualCamera 模式需要指定 Capture Camera！\n" +
                    "如果要使用筆電/USB 相機，請切換到 WebCamera 模式：\n" +
                    "右鍵 ShapeDetector > 設置 WebCamera 模式（筆電前置鏡頭）");
            }
        }
    }
    
    /// <summary>
    /// 延遲檢查相機狀態（用於 RealSense 等需要啟動時間的設備）
    /// </summary>
    private System.Collections.IEnumerator CheckCameraStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            Debug.Log($"[ShapeDetector] ✅ 相機已就緒: {webCamTexture.deviceName} ({webCamTexture.width}x{webCamTexture.height})");
        }
        else
        {
            Debug.LogWarning("[ShapeDetector] ⚠️ 相機啟動失敗或未完成！\n" +
                "請執行：右鍵 ShapeDetector > 列出可用攝影機");
        }
    }
    
    private void OnDestroy()
    {
        StopWebCamera();
    }
    
    private void Update()
    {
        // 運行時測試快捷鍵
        if (enableRuntimeTesting)
        {
            // 按 T 鍵測試截圖
            if (Input.GetKeyDown(testCaptureKey))
            {
                TestCaptureRuntime();
            }
            
            // 按 I 鍵顯示相機資訊
            if (Input.GetKeyDown(showCameraInfoKey))
            {
                ShowCurrentCameraInfoRuntime();
            }
        }
    }
    
    #region WebCamera 控制
    
    /// <summary>
    /// 列出所有可用的攝影機裝置
    /// </summary>
    [ContextMenu("列出可用攝影機")]
    public void ListAvailableCameras()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        
        Debug.Log($"==================== 可用攝影機列表 ====================");
        Debug.Log($"找到 {devices.Length} 個攝影機裝置：\n");
        
        if (devices.Length == 0)
        {
            Debug.LogWarning("[ShapeDetector] ⚠️ 未找到任何攝影機！\n" +
                "請檢查：\n" +
                "1. USB 攝影機是否已連接到電腦\n" +
                "2. Windows 設定 > 隱私權 > 相機 權限是否已開啟\n" +
                "3. 其他應用程式是否正在使用攝影機");
            return;
        }
        
        for (int i = 0; i < devices.Length; i++)
        {
            string cameraType = devices[i].isFrontFacing ? "前置" : "後置";
            string recommended = i == 0 ? " ⭐ (預設)" : "";
            Debug.Log($"[{i}] {devices[i].name}\n" +
                $"    類型: {cameraType}{recommended}\n" +
                $"    複製此名稱到 'webCameraDeviceName' 欄位來指定此攝影機\n");
        }
        
        Debug.Log($"======================================================");
        Debug.Log($"💡 提示：留空 'webCameraDeviceName' 將使用第一個攝影機");
    }
    
    /// <summary>
    /// 測試截圖功能（用於檢查攝影機角度和畫面）
    /// </summary>
    [ContextMenu("測試截圖")]
    public async void TestCapture()
    {
        Debug.Log("[ShapeDetector] 🔍 開始測試截圖...");
        
        string imagePath = await GetImagePathAsync();
        
        if (!string.IsNullOrEmpty(imagePath))
        {
            Debug.Log($"✅ 截圖成功！圖片已儲存至：\n{imagePath}\n\n" +
                $"請開啟此圖片檢查：\n" +
                $"1. 摺紙區域是否完整在畫面中\n" +
                $"2. 光線是否充足\n" +
                $"3. 畫面是否清晰（無模糊）\n" +
                $"4. 背景是否簡潔（避免干擾）");
        }
        else
        {
            Debug.LogError("❌ 截圖失敗！請檢查攝影機設置。");
        }
    }
    
    /// <summary>
    /// 啟動 WebCamera
    /// </summary>
    public void StartWebCamera()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            return;
        }
        
        WebCamDevice[] devices = WebCamTexture.devices;
        
        if (devices.Length == 0)
        {
            Debug.LogError("[ShapeDetector] 未找到任何攝影機！");
            return;
        }
        
        // 選擇攝影機
        string selectedDevice = "";
        
        if (!string.IsNullOrEmpty(webCameraDeviceName))
        {
            // 尋找指定名稱的攝影機
            foreach (var device in devices)
            {
                if (device.name.Contains(webCameraDeviceName))
                {
                    selectedDevice = device.name;
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(selectedDevice))
            {
                Debug.LogWarning($"[ShapeDetector] 找不到名為 '{webCameraDeviceName}' 的攝影機，使用預設攝影機");
            }
        }
        
        // 如果沒有指定或找不到，智能選擇最佳攝影機
        if (string.IsNullOrEmpty(selectedDevice))
        {
            selectedDevice = SelectBestCamera(devices);
        }
        
        // 創建 WebCamTexture
        webCamTexture = new WebCamTexture(selectedDevice, webCameraResolution.x, webCameraResolution.y, webCameraFPS);
        
        // 啟動攝影機
        webCamTexture.Play();
        
        // 檢查是否成功啟動
        StartCoroutine(VerifyCameraStartup(selectedDevice));
        
        // 設置預覽
        if (cameraPreview != null)
        {
            cameraPreview.texture = webCamTexture;
        }
        
        isWebCameraReady = true;
        
        // 顯示詳細的相機資訊
        Debug.Log($"==================== WebCamera 已啟動 ====================");
        Debug.Log($"📷 相機名稱: {selectedDevice}");
        Debug.Log($"📐 請求解析度: {webCameraResolution.x}x{webCameraResolution.y}");
        Debug.Log($"🎥 幀率: {webCameraFPS} FPS");
        Debug.Log($"💡 提示: 按 I 鍵查看相機狀態，按 T 鍵測試截圖");
        Debug.Log($"⏳ 等待相機初始化...");
        Debug.Log($"========================================================");
    }
    
    /// <summary>
    /// 驗證相機啟動狀態
    /// </summary>
    private System.Collections.IEnumerator VerifyCameraStartup(string deviceName)
    {
        // 等待相機初始化
        float timeout = 5f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            if (webCamTexture != null && webCamTexture.isPlaying && webCamTexture.width > 16)
            {
                Debug.Log($"[ShapeDetector] ✅ 相機啟動成功！");
                Debug.Log($"[ShapeDetector]    實際解析度: {webCamTexture.width}x{webCamTexture.height}");
                Debug.Log($"[ShapeDetector]    isPlaying: {webCamTexture.isPlaying}");
                yield break;
            }
            
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
        
        // 啟動失敗
        if (webCamTexture == null || !webCamTexture.isPlaying)
        {
            Debug.LogError($"[ShapeDetector] ❌ 相機啟動失敗！");
            Debug.LogError($"[ShapeDetector]    設備名稱: {deviceName}");
            if (webCamTexture != null)
            {
                Debug.LogError($"[ShapeDetector]    isPlaying: {webCamTexture.isPlaying}");
                Debug.LogError($"[ShapeDetector]    width: {webCamTexture.width}");
                Debug.LogError($"[ShapeDetector]    height: {webCamTexture.height}");
            }
            Debug.LogError($"[ShapeDetector] 可能原因：");
            Debug.LogError($"[ShapeDetector]    1. 相機被其他程序占用");
            Debug.LogError($"[ShapeDetector]    2. RealSense Depth相機無法作為WebCam使用（需要RGB相機）");
            Debug.LogError($"[ShapeDetector]    3. 相機權限未授予");
            Debug.LogError($"[ShapeDetector] 解決方案：");
            Debug.LogError($"[ShapeDetector]    - 右鍵 ShapeDetector > 列出可用攝影機");
            Debug.LogError($"[ShapeDetector]    - 手動設置 webCameraDeviceName 為 RGB 相機");
            
            isWebCameraReady = false;
        }
    }
    
    /// <summary>
    /// 智能選擇最佳攝影機（排除 VR 頭盔相機）
    /// </summary>
    private string SelectBestCamera(WebCamDevice[] devices)
    {
        // VR 頭盔相機的常見關鍵字（需要排除）
        string[] headsetKeywords = { "oculus", "quest", "vive", "index", "wmr", "mixed reality", "hololens" };
        
        // 優先選擇的相機關鍵字（筆電/USB 相機）
        string[] preferredKeywords = { "usb", "webcam", "integrated", "frontal", "camera" };
        
        Debug.Log($"[ShapeDetector] 智能選擇相機（共 {devices.Length} 個設備）：");
        
        // 第一步：過濾掉 VR 頭盔相機
        var filteredDevices = new System.Collections.Generic.List<WebCamDevice>();
        
        foreach (var device in devices)
        {
            string deviceNameLower = device.name.ToLower();
            bool isHeadset = false;
            
            foreach (var keyword in headsetKeywords)
            {
                if (deviceNameLower.Contains(keyword))
                {
                    isHeadset = true;
                    Debug.Log($"  ❌ 跳過 VR 頭盔相機: {device.name}");
                    break;
                }
            }
            
            if (!isHeadset)
            {
                filteredDevices.Add(device);
                Debug.Log($"  ✅ 候選相機: {device.name} ({(device.isFrontFacing ? "前置" : "後置")})");
            }
        }
        
        if (filteredDevices.Count == 0)
        {
            Debug.LogWarning("[ShapeDetector] 過濾後沒有可用相機，使用第一個設備");
            return devices[0].name;
        }
        
        // 第二步：優先選擇前置相機（通常是筆電內建相機）
        foreach (var device in filteredDevices)
        {
            if (device.isFrontFacing)
            {
                Debug.Log($"  ⭐ 選擇前置相機: {device.name}");
                return device.name;
            }
        }
        
        // 第三步：使用第一個過濾後的相機
        Debug.Log($"  ⭐ 選擇第一個候選相機: {filteredDevices[0].name}");
        return filteredDevices[0].name;
    }
    
    /// <summary>
    /// 停止 WebCamera
    /// </summary>
    public void StopWebCamera()
    {
        if (webCamTexture != null)
        {
            if (webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
            }
            Destroy(webCamTexture);
            webCamTexture = null;
            isWebCameraReady = false;
            
            if (showDebugLogs)
            {
                Debug.Log("[ShapeDetector] WebCamera 已停止");
            }
        }
    }
    
    /// <summary>
    /// 從 WebCamera 截圖（外接 USB 攝影機）
    /// </summary>
    private string CaptureFromWebCamera()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying)
        {
            Debug.LogError("[ShapeDetector] WebCamera 未啟動！嘗試啟動中...");
            StartWebCamera();
            
            // 等待一下讓攝影機準備好
            if (webCamTexture == null || !webCamTexture.isPlaying)
            {
                Debug.LogError("[ShapeDetector] 無法啟動 WebCamera！請檢查：\n" +
                    "1. USB 攝影機是否已連接\n" +
                    "2. 攝影機權限是否已授予 Unity\n" +
                    "3. 使用 Context Menu > 列出可用攝影機 來檢查設備");
                return null;
            }
        }
        
        try
        {
            if (showDebugLogs)
            {
                Debug.Log($"[ShapeDetector] 正在從 WebCamera 截圖... ({webCamTexture.width}x{webCamTexture.height})");
            }
            
            // 創建 Texture2D 並複製攝影機畫面
            Texture2D screenshot = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
            screenshot.SetPixels(webCamTexture.GetPixels());
            screenshot.Apply();
            
            // 如果需要裁切成正方形
            if (cropToSquare && screenshotResolution.x == screenshotResolution.y)
            {
                screenshot = CropToSquare(screenshot);
            }
            
            // 調整大小
            if (screenshot.width != screenshotResolution.x || screenshot.height != screenshotResolution.y)
            {
                screenshot = ResizeTexture(screenshot, screenshotResolution.x, screenshotResolution.y);
            }
            
            // 儲存為 PNG
            byte[] bytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(tempScreenshotPath, bytes);
            Destroy(screenshot);
            
            if (showDebugLogs)
            {
                Debug.Log($"[ShapeDetector] WebCamera 截圖已儲存: {tempScreenshotPath}");
            }
            
            return tempScreenshotPath;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ShapeDetector] WebCamera 截圖失敗: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 裁切成正方形（取中間區域）
    /// </summary>
    private Texture2D CropToSquare(Texture2D source)
    {
        int size = Mathf.Min(source.width, source.height);
        int xOffset = (source.width - size) / 2;
        int yOffset = (source.height - size) / 2;
        
        Color[] pixels = source.GetPixels(xOffset, yOffset, size, size);
        Texture2D result = new Texture2D(size, size, TextureFormat.RGB24, false);
        result.SetPixels(pixels);
        result.Apply();
        
        Destroy(source);
        return result;
    }
    
    /// <summary>
    /// 調整圖片大小
    /// </summary>
    private Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
        RenderTexture.active = rt;
        
        Graphics.Blit(source, rt);
        
        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        
        Destroy(source);
        return result;
    }
    
    #endregion
    
    #region RealSense 控制
    
    /// <summary>
    /// 啟動 RealSense（會自動偵測並使用 RGB 相機）
    /// </summary>
    private void StartRealSense()
    {
        Debug.Log("==================== RealSense 初始化 ====================");
        Debug.Log("[ShapeDetector] 🔍 正在搜尋 RealSense 設備...\n");
        
        // RealSense 在 Unity 中會被識別為 WebCamera 設備
        // 需要先安裝 Intel RealSense SDK
        
        WebCamDevice[] devices = WebCamTexture.devices;
        
        if (devices.Length == 0)
        {
            Debug.LogError("❌ 未找到任何相機設備！\n" +
                "請檢查：\n" +
                "1. RealSense 相機是否已連接 USB 3.0 接口\n" +
                "2. Intel RealSense SDK 是否已安裝\n" +
                "3. Windows 設定 > 隱私權 > 相機 權限是否已開啟");
            return;
        }
        
        // 顯示所有可用設備
        Debug.Log($"找到 {devices.Length} 個相機設備：");
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"  [{i}] {devices[i].name}");
        }
        Debug.Log("");
        
        // 嘗試找到 RealSense RGB 設備（優先）或其他 RealSense 設備
        string realSenseRGBDevice = "";
        string realSenseDepthDevice = "";
        string matchedKeyword = "";
        
        foreach (var device in devices)
        {
            string deviceNameLower = device.name.ToLower();
            
            // 檢查是否包含 RealSense 關鍵字
            foreach (var keyword in realSenseKeywords)
            {
                if (deviceNameLower.Contains(keyword.ToLower()))
                {
                    matchedKeyword = keyword;
                    
                    // 優先選擇 RGB 相機
                    if (deviceNameLower.Contains("rgb"))
                    {
                        realSenseRGBDevice = device.name;
                        Debug.Log($"  ✅ 找到 RealSense RGB 相機: {device.name}");
                    }
                    else if (deviceNameLower.Contains("depth"))
                    {
                        realSenseDepthDevice = device.name;
                        Debug.Log($"  ⚠️ 找到 RealSense Depth 相機: {device.name}（不適合彩色截圖）");
                    }
                    else
                    {
                        // 其他 RealSense 設備
                        if (string.IsNullOrEmpty(realSenseRGBDevice))
                        {
                            realSenseRGBDevice = device.name;
                            Debug.Log($"  ✅ 找到 RealSense 設備: {device.name}");
                        }
                    }
                    break;
                }
            }
        }
        
        // 決定使用哪個設備
        string realSenseDevice = "";
        if (!string.IsNullOrEmpty(realSenseRGBDevice))
        {
            realSenseDevice = realSenseRGBDevice;
            Debug.Log($"\n✅ 選擇 RealSense RGB 相機進行彩色截圖");
        }
        else if (!string.IsNullOrEmpty(realSenseDepthDevice))
        {
            Debug.LogWarning($"\n⚠️ 只找到 Depth 相機，可能無法正常截取彩色畫面！");
            Debug.LogWarning("建議：確保 RealSense RGB 相機已啟用");
            realSenseDevice = realSenseDepthDevice;
        }
        
        if (!string.IsNullOrEmpty(realSenseDevice))
        {
            Debug.Log($"✅ 找到 RealSense 設備！");
            Debug.Log($"   設備名稱: {realSenseDevice}");
            Debug.Log($"   匹配關鍵字: {matchedKeyword}\n");
            
            // 設定並啟動
            webCameraDeviceName = realSenseDevice;
            StartWebCamera();
            
            Debug.Log("======================================================");
            Debug.Log("💡 RealSense 已啟動！接下來可以：");
            Debug.Log("   1. 右鍵 ShapeDetector > 測試截圖");
            Debug.Log("   2. 檢查截圖是否清晰");
            Debug.Log("   3. 調整相機角度和距離");
            Debug.Log("======================================================");
        }
        else
        {
            Debug.LogWarning("⚠️ 未找到 RealSense 設備！\n");
            Debug.LogWarning("可能原因：");
            Debug.LogWarning("1. RealSense SDK 未安裝或未正確安裝");
            Debug.LogWarning("2. RealSense 未連接或驅動有問題");
            Debug.LogWarning("3. 相機名稱不包含預設關鍵字\n");
            
            Debug.LogWarning($"當前搜尋的關鍵字: {string.Join(", ", realSenseKeywords)}\n");
            
            Debug.LogWarning("解決方案：");
            Debug.LogWarning("1. 安裝 Intel RealSense SDK: https://www.intelrealsense.com/sdk-2/");
            Debug.LogWarning("2. 重新插拔 RealSense USB 連接");
            Debug.LogWarning("3. 在上方列表中找到 RealSense 相機名稱");
            Debug.LogWarning("4. 手動設定 webCameraDeviceName 為該名稱");
            Debug.LogWarning("5. 或修改 realSenseKeywords 陣列加入新的關鍵字\n");
        }
    }
    
    /// <summary>
    /// 檢查 RealSense 是否已連接並識別
    /// </summary>
    [ContextMenu("檢查 RealSense 連接")]
    public void CheckRealSenseConnection()
    {
        Debug.Log("==================== RealSense 連接檢查 ====================");
        
        WebCamDevice[] devices = WebCamTexture.devices;
        
        if (devices.Length == 0)
        {
            Debug.LogError("❌ 系統未識別到任何相機設備！");
            Debug.LogError("請檢查：");
            Debug.LogError("1. USB 連接是否穩固（建議使用 USB 3.0）");
            Debug.LogError("2. 設備管理員中是否顯示 RealSense");
            Debug.LogError("3. Intel RealSense Viewer 是否能正常開啟相機");
            return;
        }
        
        Debug.Log($"系統識別到 {devices.Length} 個相機設備：\n");
        
        bool foundRealSense = false;
        
        for (int i = 0; i < devices.Length; i++)
        {
            string deviceName = devices[i].name;
            string deviceNameLower = deviceName.ToLower();
            bool isRealSense = false;
            string matchedKeyword = "";
            
            // 檢查是否為 RealSense
            foreach (var keyword in realSenseKeywords)
            {
                if (deviceNameLower.Contains(keyword.ToLower()))
                {
                    isRealSense = true;
                    matchedKeyword = keyword;
                    foundRealSense = true;
                    break;
                }
            }
            
            if (isRealSense)
            {
                Debug.Log($"✅ [{i}] {deviceName}");
                Debug.Log($"    ⭐ 這是 RealSense 設備！（匹配關鍵字: {matchedKeyword}）");
                Debug.Log($"    類型: {(devices[i].isFrontFacing ? "前置" : "後置")}");
            }
            else
            {
                Debug.Log($"   [{i}] {deviceName}");
                Debug.Log($"    類型: {(devices[i].isFrontFacing ? "前置" : "後置")}");
            }
            Debug.Log("");
        }
        
        Debug.Log("======================================================");
        
        if (foundRealSense)
        {
            Debug.Log("✅ 找到 RealSense 設備！");
            Debug.Log("下一步：");
            Debug.Log("1. 設定 Capture Mode = RealSense");
            Debug.Log("2. 勾選 Auto Start Web Camera");
            Debug.Log("3. 執行場景測試");
        }
        else
        {
            Debug.LogWarning("⚠️ 未找到 RealSense 設備");
            Debug.LogWarning("如果上方列表中有 RealSense 相機：");
            Debug.LogWarning("- 複製完整的設備名稱");
            Debug.LogWarning("- 設定到 webCameraDeviceName 欄位");
            Debug.LogWarning("- 或在 realSenseKeywords 中加入名稱的部分關鍵字");
        }
        
        Debug.Log("======================================================");
    }
    
    /// <summary>
    /// 使用 Virtual Camera 截圖（適用於 VR 場景內物件）
    /// </summary>
    private string CaptureVirtualCameraScreenshot()
    {
        if (captureCamera == null)
        {
            Debug.LogError("[ShapeDetector] 未設置 captureCamera！請在 Inspector 中設置用於截圖的攝影機。");
            return null;
        }
        
        try
        {
            // 創建 RenderTexture
            int width = screenshotResolution.x;
            int height = screenshotResolution.y;
            RenderTexture rt = new RenderTexture(width, height, 24);
            captureCamera.targetTexture = rt;
            
            // 渲染
            captureCamera.Render();
            
            // 讀取像素
            RenderTexture.active = rt;
            Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenShot.Apply();
            
            // 清理
            captureCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);
            
            // 保存圖片
            byte[] bytes = screenShot.EncodeToPNG();
            Destroy(screenShot);
            
            File.WriteAllBytes(tempScreenshotPath, bytes);
            
            if (showDebugLogs)
            {
                Debug.Log($"[ShapeDetector] Virtual Camera 截圖已保存: {tempScreenshotPath}");
            }
            
            return tempScreenshotPath;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ShapeDetector] Virtual Camera 截圖失敗: {e.Message}");
            return null;
        }
    }
    
    #endregion
    
    private void InitializePaths()
    {
        // 設置 Python 路徑
        if (string.IsNullOrEmpty(pythonPath))
        {
            // 使用專案的虛擬環境
            string venvPath = Path.Combine(Application.dataPath, "..", ".venv", "Scripts", "python.exe");
            if (File.Exists(venvPath))
            {
                fullPythonPath = Path.GetFullPath(venvPath);
            }
            else
            {
                // 嘗試系統 Python
                fullPythonPath = "python";
            }
        }
        else
        {
            fullPythonPath = pythonPath;
        }
        
        // 設置腳本路徑
        fullScriptPath = Path.Combine(Application.dataPath, scriptPath);
        
        // 設置臨時截圖路徑
        tempScreenshotPath = Path.Combine(Application.temporaryCachePath, "origami_capture.png");
        
        if (showDebugLogs)
        {
            Debug.Log($"[ShapeDetector] Python 路徑: {fullPythonPath}");
            Debug.Log($"[ShapeDetector] 腳本路徑: {fullScriptPath}");
            Debug.Log($"[ShapeDetector] 截圖路徑: {tempScreenshotPath}");
        }
    }
    
    /// <summary>
    /// 驗證當前摺紙是否符合指定步驟
    /// </summary>
    /// <param name="expectedStep">預期的步驟編號 (1, 2, 3)</param>
    /// <param name="imagePath">圖片路徑（留空則自動截圖）</param>
    public async Task<VerificationResult> VerifyStepAsync(int expectedStep, string imagePath = null)
    {
        if (isProcessing)
        {
            return new VerificationResult
            {
                success = false,
                error = "偵測器正在處理中，請稍候"
            };
        }
        
        isProcessing = true;
        
        try
        {
            // 如果沒有提供圖片路徑，則根據模式截圖或等待照片
            if (string.IsNullOrEmpty(imagePath))
            {
                imagePath = await GetImagePathAsync();
                if (string.IsNullOrEmpty(imagePath))
                {
                    return new VerificationResult
                    {
                        success = false,
                        error = "無法獲取圖片"
                    };
                }
            }
            
            // 執行 Python 偵測
            var result = await RunPythonDetectionAsync(imagePath, expectedStep);
            
            // 觸發事件
            if (result.error == null)
            {
                OnVerificationComplete?.Invoke(result);
            }
            else
            {
                OnError?.Invoke(result.error);
            }
            
            return result;
        }
        finally
        {
            isProcessing = false;
        }
    }
    
    /// <summary>
    /// 異步獲取圖片路徑（支援各種攝影機模式）
    /// </summary>
    private async Task<string> GetImagePathAsync()
    {
        switch (captureMode)
        {
            case CaptureMode.VirtualCamera:
                return CaptureVirtualCameraScreenshot();
                
            case CaptureMode.Manual:
                return GetManualImagePath();
                
            case CaptureMode.WebCamera:
            case CaptureMode.RealSense:
                // 有延遲倒數
                if (captureDelay > 0)
                {
                    await ShowCaptureCountdown();
                }
                return CaptureFromWebCamera();
                
            default:
                return null;
        }
    }
    
    /// <summary>
    /// 顯示截圖倒數
    /// </summary>
    private async Task ShowCaptureCountdown()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ShapeDetector] {captureDelay} 秒後截圖...");
        }
        
        float remaining = captureDelay;
        while (remaining > 0)
        {
            if (countdownText != null)
            {
                countdownText.text = $"截圖倒數: {remaining:F1}";
            }
            
            await Task.Delay(100);
            remaining -= 0.1f;
        }
        
        if (countdownText != null)
        {
            countdownText.text = "截圖！";
            // 短暫顯示後清除
            await Task.Delay(500);
            countdownText.text = "";
        }
    }
    
    /// <summary>
    /// 偵測圖片中的形狀（不驗證特定步驟）
    /// </summary>
    public async Task<VerificationResult> DetectShapeAsync(string imagePath = null)
    {
        if (isProcessing)
        {
            return new VerificationResult
            {
                success = false,
                error = "偵測器正在處理中，請稍候"
            };
        }
        
        isProcessing = true;
        
        try
        {
            // 如果沒有提供圖片路徑，則截圖
            if (string.IsNullOrEmpty(imagePath))
            {
                imagePath = await GetImagePathAsync();
                if (string.IsNullOrEmpty(imagePath))
                {
                    return new VerificationResult
                    {
                        success = false,
                        error = "截圖失敗"
                    };
                }
            }
            
            // 執行 Python 偵測（不指定步驟）
            var result = await RunPythonDetectionAsync(imagePath, -1);
            return result;
        }
        finally
        {
            isProcessing = false;
        }
    }
    
    /// <summary>
    /// 同步版本的驗證方法（會阻塞主線程，建議使用協程版本）
    /// </summary>
    public void VerifyStep(int expectedStep, Action<VerificationResult> callback, string imagePath = null)
    {
        StartCoroutine(VerifyStepCoroutine(expectedStep, callback, imagePath));
    }
    
    private System.Collections.IEnumerator VerifyStepCoroutine(int expectedStep, Action<VerificationResult> callback, string imagePath)
    {
        var task = VerifyStepAsync(expectedStep, imagePath);
        
        while (!task.IsCompleted)
        {
            yield return null;
        }
        
        if (task.IsFaulted)
        {
            callback?.Invoke(new VerificationResult
            {
                success = false,
                error = task.Exception?.Message ?? "未知錯誤"
            });
        }
        else
        {
            callback?.Invoke(task.Result);
        }
    }
    
    /// <summary>
    /// 在主線程截取畫面（使用 RenderTexture）
    /// 這個方法保持向後兼容
    /// </summary>
    public string CaptureScreenshot()
    {
        switch (captureMode)
        {
            case CaptureMode.WebCamera:
            case CaptureMode.RealSense:
                return CaptureFromWebCamera();
            case CaptureMode.VirtualCamera:
                return CaptureVirtualCameraScreenshot();
            case CaptureMode.Manual:
                return GetManualImagePath();
            default:
                return CaptureVirtualCameraScreenshot();
        }
    }
    
    /// <summary>
    /// 獲取手動指定的圖片路徑
    /// </summary>
    private string GetManualImagePath()
    {
        if (string.IsNullOrEmpty(manualImagePath))
        {
            Debug.LogError("[ShapeDetector] 手動模式需要指定圖片路徑");
            return null;
        }
        
        string fullPath = Path.IsPathRooted(manualImagePath) ? 
            manualImagePath : 
            Path.Combine(Application.dataPath, manualImagePath);
        
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[ShapeDetector] 找不到指定的圖片: {fullPath}");
            return null;
        }
        
        return fullPath;
    }
    
    /// <summary>
    /// 執行 Python 偵測腳本
    /// </summary>
    private async Task<VerificationResult> RunPythonDetectionAsync(string imagePath, int verifyStep)
    {
        return await Task.Run(() =>
        {
            try
            {
                // 構建參數
                string arguments = $"\"{fullScriptPath}\" \"{imagePath}\" --unity --conf {confidenceThreshold}";
                
                if (verifyStep > 0)
                {
                    arguments += $" --verify {verifyStep}";
                }
                
                if (!string.IsNullOrEmpty(modelPath))
                {
                    arguments += $" --model \"{modelPath}\"";
                }
                
                if (showDebugLogs)
                {
                    Debug.Log($"[ShapeDetector] 執行: {fullPythonPath} {arguments}");
                }
                
                // 創建進程
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = fullPythonPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(fullScriptPath)
                };
                
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    
                    bool exited = process.WaitForExit((int)(timeout * 1000));
                    
                    if (!exited)
                    {
                        process.Kill();
                        return new VerificationResult
                        {
                            success = false,
                            error = $"偵測超時（{timeout} 秒）"
                        };
                    }
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"[ShapeDetector] 輸出: {output}");
                        if (!string.IsNullOrEmpty(error))
                        {
                            Debug.LogWarning($"[ShapeDetector] 錯誤輸出: {error}");
                        }
                    }
                    
                    // 解析 JSON 輸出
                    if (string.IsNullOrEmpty(output))
                    {
                        return new VerificationResult
                        {
                            success = false,
                            error = $"Python 腳本無輸出。錯誤: {error}"
                        };
                    }
                    
                    try
                    {
                        var result = JsonUtility.FromJson<VerificationResult>(output.Trim());
                        return result;
                    }
                    catch (Exception parseError)
                    {
                        return new VerificationResult
                        {
                            success = false,
                            error = $"JSON 解析失敗: {parseError.Message}. 原始輸出: {output}"
                        };
                    }
                }
            }
            catch (Exception e)
            {
                return new VerificationResult
                {
                    success = false,
                    error = $"執行 Python 失敗: {e.Message}"
                };
            }
        });
    }
    
    /// <summary>
    /// 測試偵測系統是否正常運作
    /// </summary>
    [ContextMenu("測試偵測系統")]
    public void TestDetectionSystem()
    {
        StartCoroutine(TestCoroutine());
    }
    
    /// <summary>
    /// 設置 WebCamera 模式並顯示使用說明
    /// </summary>
    [ContextMenu("設置 WebCamera 模式（筆電前置鏡頭）")]
    public void SetupWebCameraMode()
    {
        captureMode = CaptureMode.WebCamera;
        webCameraDeviceName = ""; // 清空，讓系統自動選擇第一個相機（通常是筆電前置鏡頭）
        
        Debug.Log("==================== WebCamera 模式 ====================");
        Debug.Log("✅ 已切換到 WebCamera 模式（筆電前置鏡頭）");
        Debug.Log("");
        Debug.Log("使用步驟：");
        Debug.Log("1. 系統會使用第一個可用的攝影機（通常是筆電前置鏡頭）");
        Debug.Log("2. 如果要確認使用哪個相機：");
        Debug.Log("   右鍵 > 列出可用攝影機");
        Debug.Log("3. 如果要指定特定相機：");
        Debug.Log("   - 複製相機名稱");
        Debug.Log("   - 貼到 webCameraDeviceName 欄位");
        Debug.Log("4. 點擊驗證按鈕時會自動截圖並分析");
        Debug.Log("======================================================");
        
        // 自動啟動 WebCamera
        if (autoStartWebCamera)
        {
            StartWebCamera();
        }
        
        // 顯示正在使用的相機
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            Debug.Log($"🎥 當前使用相機: {webCamTexture.deviceName}");
        }
    }
    
    private void ClearCountdown()
    {
        if (countdownText != null)
        {
            countdownText.text = "";
        }
    }
    
    /// <summary>
    /// 設置 RealSense 模式
    /// </summary>
    [ContextMenu("設置 RealSense 模式")]
    public void SetupRealSenseMode()
    {
        captureMode = CaptureMode.RealSense;
        
        Debug.Log("==================== RealSense 模式 ====================");
        Debug.Log("✅ 已切換到 RealSense 模式");
        Debug.Log("");
        Debug.Log("使用步驟：");
        Debug.Log("1. 確保 Intel RealSense 相機已連接 USB 3.0");
        Debug.Log("2. 系統會自動搜尋並連接 RealSense 設備");
        Debug.Log("3. 如果找不到 RealSense：");
        Debug.Log("   - 檢查 Intel RealSense SDK 是否已安裝");
        Debug.Log("   - 右鍵 > 檢查 RealSense 連接");
        Debug.Log("4. 點擊驗證按鈕時會自動截圖並分析");
        Debug.Log("======================================================");
        
        // 自動啟動 RealSense
        StartRealSense();
    }
    
    /// <summary>
    /// 顯示當前使用的相機資訊
    /// </summary>
    [ContextMenu("顯示當前相機資訊")]
    public void ShowCurrentCameraInfo()
    {
        Debug.Log("==================== 當前相機資訊 ====================");
        Debug.Log($"截圖模式: {captureMode}");
        Debug.Log("");
        
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            Debug.Log("✅ 相機狀態: 已啟動");
            Debug.Log($"🎥 相機名稱: {webCamTexture.deviceName}");
            Debug.Log($"📐 解析度: {webCamTexture.width} x {webCamTexture.height}");
            Debug.Log($"🎞️  FPS: {webCamTexture.requestedFPS}");
        }
        else
        {
            Debug.LogWarning("⚠️ 相機狀態: 未啟動");
            Debug.LogWarning("請先啟動相機（場景載入時會自動啟動，或手動執行 SetupWebCameraMode / SetupRealSenseMode）");
        }
        
        Debug.Log("");
        Debug.Log("可用操作：");
        Debug.Log("- 右鍵 > 列出可用攝影機：查看所有可用設備");
        Debug.Log("- 右鍵 > 測試截圖：測試當前相機截圖功能");
        Debug.Log("- 右鍵 > 設置 WebCamera 模式（筆電前置鏡頭）");
        Debug.Log("- 右鍵 > 設置 RealSense 模式");
        Debug.Log("======================================================");
    }
    
    /// <summary>
    /// 運行時測試截圖（用於遊戲執行時按快捷鍵測試）
    /// </summary>
    private async void TestCaptureRuntime()
    {
        string message = $"[{System.DateTime.Now:HH:mm:ss}] testing...";
        Debug.Log(message);
        ShowRuntimeMessage(message);
        
        string imagePath = await GetImagePathAsync();
        
        if (!string.IsNullOrEmpty(imagePath))
        {
            message = $"ScreenShot Success! \nImage path: {imagePath}";
            Debug.Log(message);
            ShowRuntimeMessage(message, 5f);
            
            // 如果在 Windows，自動開啟檔案總管到截圖位置
            if (Application.platform == RuntimePlatform.WindowsPlayer || 
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{imagePath}\"");
            }
        }
        else
        {
            message = "❌ ScreenShot Failed! Please check camera settings.";
            Debug.LogError(message);
            ShowRuntimeMessage(message, 5f);
        }
    }
    
    /// <summary>
    /// 運行時顯示相機資訊（用於遊戲執行時按快捷鍵查看）
    /// </summary>
    private void ShowCurrentCameraInfoRuntime()
    {
        string message = "";
        
        // 詳細調試信息
        Debug.Log("==================== 相機狀態調試 ====================");
        Debug.Log($"模式: {captureMode}");
        Debug.Log($"webCamTexture: {(webCamTexture != null ? "已創建" : "NULL")}");
        if (webCamTexture != null)
        {
            Debug.Log($"isPlaying: {webCamTexture.isPlaying}");
            Debug.Log($"deviceName: {webCamTexture.deviceName}");
            Debug.Log($"width x height: {webCamTexture.width} x {webCamTexture.height}");
            Debug.Log($"didUpdateThisFrame: {webCamTexture.didUpdateThisFrame}");
        }
        Debug.Log($"isWebCameraReady: {isWebCameraReady}");
        Debug.Log("======================================================");
        
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            message = $"✅ 相機已啟動\n" +
                     $"📷 {webCamTexture.deviceName}\n" +
                     $"📐 {webCamTexture.width}x{webCamTexture.height} @ {webCameraFPS}fps\n" +
                     $"模式: {captureMode}";
        }
        else if (webCamTexture != null && !webCamTexture.isPlaying)
        {
            message = $"⚠️ 相機已創建但未播放\n" +
                     $"📷 {webCamTexture.deviceName}\n" +
                     $"模式: {captureMode}\n" +
                     $"提示: 嘗試重新啟動遊戲";
        }
        else
        {
            message = $"⚠️ 相機未啟動\n" +
                     $"模式: {captureMode}\n" +
                     $"提示: 請檢查 Console 日誌查看啟動錯誤";
        }
        
        Debug.Log(message);
        ShowRuntimeMessage(message, 5f);
    }
    
    /// <summary>
    /// 在運行時顯示訊息（在 UI 上顯示）
    /// </summary>
    private void ShowRuntimeMessage(string message, float duration = 3f)
    {
        if (runtimeMessageText != null)
        {
            runtimeMessageText.text = message;
            CancelInvoke(nameof(ClearRuntimeMessage));
            Invoke(nameof(ClearRuntimeMessage), duration);
        }
    }
    
    /// <summary>
    /// 清除運行時訊息
    /// </summary>
    private void ClearRuntimeMessage()
    {
        if (runtimeMessageText != null)
        {
            runtimeMessageText.text = "";
        }
    }
    
    /// <summary>
    /// 顯示「驗證中...」訊息（黃色）
    /// </summary>
    public void ShowVerifyingMessage()
    {
        if (runtimeMessageText != null)
        {
            runtimeMessageText.text = "Verifying...";
            runtimeMessageText.color = Color.yellow;
        }
    }
    
    /// <summary>
    /// 顯示成功訊息（綠色，3秒後消失）
    /// </summary>
    public void ShowSuccessMessage(string message)
    {
        if (runtimeMessageText != null)
        {
            runtimeMessageText.text = message;
            runtimeMessageText.color = Color.green;
            CancelInvoke(nameof(ClearRuntimeMessage));
            Invoke(nameof(ClearRuntimeMessage), 3f);
        }
    }
    
    /// <summary>
    /// 顯示失敗訊息（紅色，3秒後消失）
    /// </summary>
    public void ShowFailureMessage(string message)
    {
        if (runtimeMessageText != null)
        {
            runtimeMessageText.text = message;
            runtimeMessageText.color = Color.red;
            CancelInvoke(nameof(ClearRuntimeMessage));
            Invoke(nameof(ClearRuntimeMessage), 3f);
        }
    }
    
    /// <summary>
    /// 手動觸發照片檢測（測試用）
    /// </summary>
    [ContextMenu("測試照片檢測")]
    public void TestPhotoDetection()
    {
        if (captureMode == CaptureMode.WebCamera || captureMode == CaptureMode.RealSense)
        {
            StartCoroutine(TestPhotoDetectionCoroutine());
        }
        else
        {
            Debug.LogWarning("[ShapeDetector] 請先設置為 WebCamera 或 RealSense 模式");
        }
    }
    
    private System.Collections.IEnumerator TestPhotoDetectionCoroutine()
    {
        Debug.Log("[ShapeDetector] 開始測試照片檢測...");
        
        var task = GetImagePathAsync();
        while (!task.IsCompleted)
        {
            yield return null;
        }
        
        if (task.Result != null)
        {
            Debug.Log($"[ShapeDetector] ✓ 成功獲取照片: {task.Result}");
        }
        else
        {
            Debug.LogWarning("[ShapeDetector] ✗ 無法獲取照片");
        }
    }
    
    private System.Collections.IEnumerator TestCoroutine()
    {
        Debug.Log("[ShapeDetector] 開始測試偵測系統...");
        
        // 測試 Python 環境
        var testTask = Task.Run(() =>
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = fullPythonPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output.Trim();
                }
            }
            catch (Exception e)
            {
                return $"錯誤: {e.Message}";
            }
        });
        
        while (!testTask.IsCompleted)
        {
            yield return null;
        }
        
        Debug.Log($"[ShapeDetector] Python 版本: {testTask.Result}");
        
        // 檢查腳本檔案
        if (File.Exists(fullScriptPath))
        {
            Debug.Log($"[ShapeDetector] ✓ 偵測腳本存在: {fullScriptPath}");
        }
        else
        {
            Debug.LogError($"[ShapeDetector] ✗ 找不到偵測腳本: {fullScriptPath}");
        }
        
        // 檢查模型檔案
        string modelFullPath = Path.Combine(Path.GetDirectoryName(fullScriptPath), "best.pt");
        if (File.Exists(modelFullPath))
        {
            Debug.Log($"[ShapeDetector] ✓ 模型檔案存在: {modelFullPath}");
        }
        else
        {
            Debug.LogError($"[ShapeDetector] ✗ 找不到模型檔案: {modelFullPath}");
        }
        
        Debug.Log("[ShapeDetector] 測試完成！");
    }
}
