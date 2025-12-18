using UnityEngine;
using System.IO;

/// <summary>
/// 诊断工具：检查WebCamera截图质量和内容
/// 附加到ShapeDetector同一个物体上，按D键进行完整诊断
/// </summary>
public class DiagnoseCapture : MonoBehaviour
{
    [Header("引用")]
    public ShapeDetector shapeDetector;
    
    [Header("诊断设置")]
    public KeyCode diagnoseKey = KeyCode.D;
    public bool saveUnprocessedImage = true;
    public bool saveProcessedImage = true;
    
    private void Update()
    {
        if (Input.GetKeyDown(diagnoseKey))
        {
            DiagnoseCurrentSetup();
        }
    }
    
    public void DiagnoseCurrentSetup()
    {
        Debug.Log("==================== 📸 截图诊断工具 ====================");
        
        if (shapeDetector == null)
        {
            Debug.LogError("❌ ShapeDetector 引用未设置！");
            return;
        }
        
        // 1. 检查相机状态
        Debug.Log("\n【1】相机状态检查：");
        if (shapeDetector.IsWebCameraReady)
        {
            Debug.Log("✅ WebCamera 已启动");
        }
        else
        {
            Debug.LogError("❌ WebCamera 未启动！");
            Debug.LogError("   解决方案：确保 Auto Start Web Camera 已勾选");
            return;
        }
        
        // 2. 检查截图设置
        Debug.Log("\n【2】截图设置检查：");
        var captureMode = GetPrivateField<ShapeDetector.CaptureMode>(shapeDetector, "captureMode");
        Debug.Log($"   截图模式: {captureMode}");
        
        var cropToSquare = GetPrivateField<bool>(shapeDetector, "cropToSquare");
        Debug.Log($"   裁剪为正方形: {cropToSquare}");
        
        var screenshotResolution = GetPrivateField<Vector2Int>(shapeDetector, "screenshotResolution");
        Debug.Log($"   截图分辨率: {screenshotResolution.x}x{screenshotResolution.y}");
        
        var confidenceThreshold = GetPrivateField<float>(shapeDetector, "confidenceThreshold");
        Debug.Log($"   信心度阈值: {confidenceThreshold}");
        
        // 3. 信心度阈值建议
        Debug.Log("\n【3】信心度阈值分析：");
        if (confidenceThreshold > 0.5f)
        {
            Debug.LogWarning($"⚠️ 当前信心度阈值 {confidenceThreshold} 较高！");
            Debug.LogWarning("   建议：降低到 0.3-0.4 进行测试");
        }
        else
        {
            Debug.Log($"✅ 信心度阈值 {confidenceThreshold} 合理");
        }
        
        // 4. 拍摄建议
        Debug.Log("\n【4】拍摄质量建议：");
        Debug.Log("   ✓ 光线充足（避免阴影和反光）");
        Debug.Log("   ✓ 背景简洁（纯色背景最佳，避免文字干扰）");
        Debug.Log("   ✓ 折纸完整（确保整个折纸在画面中）");
        Debug.Log("   ✓ 角度合适（正面平拍，避免过度倾斜）");
        Debug.Log("   ✓ 对焦清晰（避免模糊）");
        
        // 5. 裁剪警告
        if (cropToSquare)
        {
            Debug.LogWarning("\n【5】⚠️ 裁剪警告：");
            Debug.LogWarning("   当前启用了裁剪为正方形功能");
            Debug.LogWarning("   如果相机是16:9或4:3，裁剪可能会丢失重要部分");
            Debug.LogWarning("   建议：暂时关闭 cropToSquare 测试完整截图");
        }
        
        // 6. YOLO模型检查
        Debug.Log("\n【6】YOLO 模型检查：");
        string modelPath = Path.Combine(Application.dataPath, "share_model", "best.pt");
        if (File.Exists(modelPath))
        {
            FileInfo fileInfo = new FileInfo(modelPath);
            Debug.Log($"✅ 模型文件存在: {modelPath}");
            Debug.Log($"   文件大小: {fileInfo.Length / 1024 / 1024:F1} MB");
            Debug.Log($"   最后修改: {fileInfo.LastWriteTime}");
        }
        else
        {
            Debug.LogError($"❌ 模型文件不存在: {modelPath}");
        }
        
        // 7. 截图路径检查
        Debug.Log("\n【7】截图保存位置：");
        string screenshotPath = Path.Combine(Application.temporaryCachePath, "origami_capture.png");
        Debug.Log($"   {screenshotPath}");
        if (File.Exists(screenshotPath))
        {
            FileInfo fileInfo = new FileInfo(screenshotPath);
            Debug.Log($"✅ 上次截图: {fileInfo.LastWriteTime}");
            Debug.Log($"   文件大小: {fileInfo.Length / 1024:F1} KB");
        }
        else
        {
            Debug.LogWarning("⚠️ 尚未有截图文件");
        }
        
        Debug.Log("\n======================================================");
        Debug.Log("💡 下一步操作：");
        Debug.Log("   1. 按 T 键拍摄测试照片");
        Debug.Log("   2. 打开截图文件检查质量");
        Debug.Log("   3. 如果背景复杂，尝试使用纯色背景");
        Debug.Log("   4. 如果仍检测不到，降低信心度阈值到 0.3");
        Debug.Log("======================================================\n");
    }
    
    private T GetPrivateField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public);
        
        if (field != null)
        {
            return (T)field.GetValue(obj);
        }
        
        return default(T);
    }
}
