using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 步驟分組定義（允許將多個步驟合併播放）
/// </summary>
[Serializable]
public class StepGroup
{
    [Tooltip("分組名稱")]
    public string groupName = "分組 1";
    
    [Tooltip("包含的步驟索引列表（例如：0,1,2 表示步驟 1,2,3）")]
    public List<int> stepIndices = new List<int>();
}

/// <summary>
/// Avatar 錄製數據結構（共享）
/// 用於教師錄製和學生播放的數據交換
/// </summary>
[Serializable]
public class AvatarRecordingData
{
    public string recordingName;
    public DateTime recordingDate;
    public float duration;
    public int fps;
    public int audioSampleRate;
    public int audioChannels;
    public List<AvatarFrameData> frames = new List<AvatarFrameData>();
    public List<float> audioSamples = new List<float>();  // 完整連續音頻
    public List<OrigamiStepEvent> origamiStepEvents = new List<OrigamiStepEvent>();  // 摺紙步驟事件
    
    public AvatarRecordingData(string name, int fps, int sampleRate, int channels)
    {
        recordingName = name;
        recordingDate = DateTime.Now;
        this.fps = fps;
        this.audioSampleRate = sampleRate;
        this.audioChannels = channels;
    }
}

/// <summary>
/// 單幀 Avatar 數據（僅動作數據）
/// </summary>
[Serializable]
public class AvatarFrameData
{
    public float timestamp;           // 時間戳
    public byte[] avatarStreamData;   // Avatar 串流數據（動作+嘴型）
}

/// <summary>
/// 摺紙步驟事件（記錄什麼時候切換步驟）
/// </summary>
[Serializable]
public class OrigamiStepEvent
{
    public float timestamp;      // 觸發時間
    public int stepIndex;        // 步驟索引
    public string stepName;      // 步驟名稱（供調試）
    
    public OrigamiStepEvent(float time, int index, string name)
    {
        timestamp = time;
        stepIndex = index;
        stepName = name;
    }
}
