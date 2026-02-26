using UnityEngine;
using System.Collections;

/// <summary>
/// 简单的 TTS 服务
/// 目前使用模拟音频进行测试
/// 可以扩展为使用云端 TTS 服务（Azure, Google, Meta Wit.AI 等）
/// </summary>
public class SimpleTTSService : MonoBehaviour
{
    public enum TTSProvider
    {
        Mock,          // 模拟音频（用于测试）
        AzureTTS,      // Azure 云端 TTS（需要实现）
        GoogleTTS,     // Google 云端 TTS（需要实现）
        WitAiTTS       // Meta Wit.AI TTS（需要实现）
    }

    [Header("TTS 设置")]
    [Tooltip("TTS 提供者")]
    public TTSProvider provider = TTSProvider.Mock;
    
    [Tooltip("语音语言")]
    public string voiceLanguage = "zh-CN"; // 中文
    
    [Tooltip("语速（-10 到 10，0 = 正常）")]
    [Range(-10, 10)]
    public int rate = 0;
    
    [Tooltip("音量（0 到 100）")]
    [Range(0, 100)]
    public int volume = 80;

    [Header("音频设置")]
    [Tooltip("采样率")]
    public int sampleRate = 22050;
    
    [Tooltip("mock 音频：每个字的持续时间（秒）")]
    public float mockCharDuration = 0.25f;

    private static SimpleTTSService instance;

    public static SimpleTTSService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SimpleTTSService>();
                if (instance == null)
                {
                    GameObject go = new GameObject("SimpleTTSService");
                    instance = go.AddComponent<SimpleTTSService>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeTTS();
    }

    void InitializeTTS()
    {
        // 目前只支持 Mock 模式
        if (provider != TTSProvider.Mock)
        {
            Debug.LogWarning($"[SimpleTTS] {provider} 尚未实现，切换到 Mock 模式");
            provider = TTSProvider.Mock;
        }
        
        Debug.Log("[SimpleTTS] 初始化完成 - Mock 模式");
    }

    /// <summary>
    /// 生成语音 AudioClip
    /// </summary>
    public AudioClip GenerateSpeech(string text, out float[] visemeData)
    {
        visemeData = null;

        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("[SimpleTTS] 文本为空");
            return null;
        }

        // 目前只支持 Mock 模式
        return GenerateMockSpeech(text, out visemeData);
    }

    /// <summary>
    /// 生成模拟语音（用于测试）
    /// </summary>
    AudioClip GenerateMockSpeech(string text, out float[] visemeData)
    {
        // 计算音频长度
        float duration = text.Length * mockCharDuration;
        int sampleCount = (int)(sampleRate * duration);
        
        // 创建 AudioClip
        AudioClip clip = AudioClip.Create("MockSpeech", sampleCount, 1, sampleRate, false);
        
        // 生成音频样本
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            
            // 基础频率（模拟说话的音调变化）
            float baseFreq = 180f + 40f * Mathf.Sin(2 * Mathf.PI * 2f * t); // 180-220Hz
            
            // 生成复合波形
            float sample = Mathf.Sin(2 * Mathf.PI * baseFreq * t) * 0.4f;
            
            // 添加谐波（让声音更丰富）
            sample += Mathf.Sin(2 * Mathf.PI * baseFreq * 2f * t) * 0.2f;
            sample += Mathf.Sin(2 * Mathf.PI * baseFreq * 3f * t) * 0.1f;
            
            // 音调调制（模拟说话的节奏）
            float modulation = 1f + 0.3f * Mathf.Sin(2 * Mathf.PI * 4f * t);
            sample *= modulation;
            
            // 淡入淡出
            float fadeTime = 0.05f;
            if (t < fadeTime)
                sample *= t / fadeTime;
            else if (t > duration - fadeTime)
                sample *= (duration - t) / fadeTime;
            
            samples[i] = sample * 0.8f;
        }
        
        clip.SetData(samples, 0);
        
        // 生成 viseme 数据
        visemeData = GenerateSimpleVisemeData(text, duration);
        
        Debug.Log($"[SimpleTTS] 生成模拟语音: {duration:F2} 秒");
        return clip;
    }

    /// <summary>
    /// 生成简单的 viseme 数据
    /// </summary>
    float[] GenerateSimpleVisemeData(string text, float duration)
    {
        // 每帧的 viseme 数据（假设 30fps）
        int frameCount = Mathf.CeilToInt(duration * 30f);
        float[] visemeData = new float[frameCount];
        
        // 简单的 viseme 动画（在不同的 viseme 之间循环）
        for (int i = 0; i < frameCount; i++)
        {
            float t = (float)i / frameCount;
            
            // 使用正弦波创建平滑的嘴型变化
            visemeData[i] = (Mathf.Sin(t * text.Length * Mathf.PI * 2) + 1f) * 0.5f;
        }
        
        return visemeData;
    }
}
