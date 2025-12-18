using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

/// <summary>
/// 錄製檔案診斷工具
/// 用於檢查 .recording 檔案的內容和完整性
/// </summary>
public class RecordingDiagnostics : MonoBehaviour
{
    [Header("診斷設定")]
    [Tooltip("要診斷的錄製檔案名稱（不含 .recording）")]
    public string recordingFileName = "";
    
    [Header("診斷結果")]
    [TextArea(10, 20)]
    public string diagnosticReport = "按 D 鍵開始診斷";

    void Update()
    {
        // D 鍵：診斷錄製檔案
        if (Input.GetKeyDown(KeyCode.D))
        {
            DiagnoseRecording();
        }
    }

    void DiagnoseRecording()
    {
        string folderPath = Path.Combine(Application.dataPath, "Recordings");
        
        if (string.IsNullOrEmpty(recordingFileName))
        {
            // 自動選擇最新的檔案
            string[] files = Directory.GetFiles(folderPath, "*.recording");
            if (files.Length == 0)
            {
                diagnosticReport = "❌ 找不到任何 .recording 檔案";
                Debug.LogError(diagnosticReport);
                return;
            }
            
            // 選擇最新的檔案
            string latestFile = files[files.Length - 1];
            recordingFileName = Path.GetFileNameWithoutExtension(latestFile);
        }
        
        string filePath = Path.Combine(folderPath, recordingFileName + ".recording");
        
        if (!File.Exists(filePath))
        {
            diagnosticReport = $"❌ 檔案不存在: {filePath}";
            Debug.LogError(diagnosticReport);
            return;
        }
        
        try
        {
            BinaryFormatter formatter = new BinaryFormatter();
            AvatarRecordingData recording;
            
            using (FileStream stream = new FileStream(filePath, FileMode.Open))
            {
                recording = (AvatarRecordingData)formatter.Deserialize(stream);
            }
            
            // 生成診斷報告
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("========== 錄製檔案診斷報告 ==========");
            report.AppendLine($"檔案名稱: {recordingFileName}.recording");
            report.AppendLine($"檔案大小: {new FileInfo(filePath).Length / 1024f:F1} KB");
            report.AppendLine();
            
            report.AppendLine("=== 基本資訊 ===");
            report.AppendLine($"錄製名稱: {recording.recordingName}");
            report.AppendLine($"錄製日期: {recording.recordingDate}");
            report.AppendLine($"時長: {recording.duration:F2} 秒");
            report.AppendLine($"FPS: {recording.fps}");
            report.AppendLine();
            
            report.AppendLine("=== 動作數據 ===");
            report.AppendLine($"總幀數: {recording.frames.Count}");
            
            // 檢查前 10 幀
            int framesToCheck = Mathf.Min(10, recording.frames.Count);
            report.AppendLine($"\n前 {framesToCheck} 幀詳細資訊:");
            
            for (int i = 0; i < framesToCheck; i++)
            {
                var frame = recording.frames[i];
                string dataStatus = frame.avatarStreamData != null 
                    ? $"{frame.avatarStreamData.Length} bytes" 
                    : "NULL";
                report.AppendLine($"  幀 {i}: timestamp={frame.timestamp:F3}s, data={dataStatus}");
            }
            
            // 檢查是否有空數據
            int nullFrames = 0;
            int emptyFrames = 0;
            for (int i = 0; i < recording.frames.Count; i++)
            {
                if (recording.frames[i].avatarStreamData == null)
                    nullFrames++;
                else if (recording.frames[i].avatarStreamData.Length == 0)
                    emptyFrames++;
            }
            
            report.AppendLine($"\n數據完整性:");
            report.AppendLine($"  NULL 數據幀: {nullFrames}");
            report.AppendLine($"  空數據幀: {emptyFrames}");
            report.AppendLine($"  有效數據幀: {recording.frames.Count - nullFrames - emptyFrames}");
            
            report.AppendLine();
            report.AppendLine("=== 音頻數據 ===");
            report.AppendLine($"採樣率: {recording.audioSampleRate} Hz");
            report.AppendLine($"聲道數: {recording.audioChannels}");
            report.AppendLine($"音頻樣本數: {recording.audioSamples.Count}");
            
            if (recording.audioChannels > 0)
            {
                int sampleCount = recording.audioSamples.Count / recording.audioChannels;
                float audioDuration = (float)sampleCount / recording.audioSampleRate;
                report.AppendLine($"音頻時長: {audioDuration:F2} 秒");
                report.AppendLine($"音頻/動作時長差異: {Mathf.Abs(audioDuration - recording.duration):F2} 秒");
            }
            
            // 檢查時間戳記是否正常
            report.AppendLine();
            report.AppendLine("=== 時間戳記檢查 ===");
            report.AppendLine($"第一幀時間: {recording.frames[0].timestamp:F3}s");
            
            if (recording.frames.Count > 1)
            {
                float avgFrameTime = recording.duration / recording.frames.Count;
                float expectedFPS = 1f / avgFrameTime;
                report.AppendLine($"最後一幀時間: {recording.frames[recording.frames.Count - 1].timestamp:F3}s");
                report.AppendLine($"平均幀間隔: {avgFrameTime:F4}s");
                report.AppendLine($"實際 FPS: {expectedFPS:F1}");
            }
            
            // 檢查第一幀是否為 0
            if (recording.frames[0].timestamp > 0.1f)
            {
                report.AppendLine($"⚠️  警告: 第一幀時間不是 0 ({recording.frames[0].timestamp:F3}s)");
                report.AppendLine("    這可能導致播放時前面會延遲");
            }
            
            report.AppendLine();
            report.AppendLine("========== 診斷完成 ==========");
            
            if (nullFrames > 0 || emptyFrames > 0)
            {
                report.AppendLine("\n❌ 發現問題: 有些幀缺少動作數據");
            }
            else if (recording.frames[0].timestamp > 0.1f)
            {
                report.AppendLine("\n⚠️  發現問題: 第一幀時間戳記不正確");
            }
            else
            {
                report.AppendLine("\n✅ 檔案看起來正常");
            }
            
            diagnosticReport = report.ToString();
            Debug.Log(diagnosticReport);
        }
        catch (System.Exception e)
        {
            diagnosticReport = $"❌ 診斷失敗: {e.Message}\n\n{e.StackTrace}";
            Debug.LogError(diagnosticReport);
        }
    }
    
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(10, 10, 10, 10);
        
        float width = 400f;
        float height = 80f;
        float xPos = 20f;
        float yPos = 20f;
        
        GUI.Box(new Rect(xPos, yPos, width, height), "", style);
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.normal.textColor = Color.yellow;
        
        GUI.Label(new Rect(xPos + 10f, yPos + 10f, width - 20f, 30f),
            "🔍 錄製檔案診斷工具", labelStyle);
        
        GUI.Label(new Rect(xPos + 10f, yPos + 40f, width - 20f, 30f),
            "按 D 鍵診斷最新的 .recording 檔案", labelStyle);
    }
}
