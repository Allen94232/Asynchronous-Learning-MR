using UnityEngine;
using Oculus.Avatar2;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// LLM 驱动的 Avatar 控制器
/// 输入文字，让 Remote Avatar 配合自然肢体动作念出来
/// 替代原本的 AvatarRecordingManager，用于 LLM 驱动而非人为录制
/// </summary>
public class AvatarLLMController : MonoBehaviour
{
    [Header("Avatar 设定")]
    [Tooltip("本地 Avatar（用户第一人称控制）")]
    public OvrAvatarEntity localAvatar;
    
    [Tooltip("远端 Avatar（LLM 驱动的 AI）")]
    public OvrAvatarEntity remoteAvatar;

    [Header("音频设定")]
    [Tooltip("远端 Avatar 的 AudioSource")]
    public AudioSource remoteAudioSource;
    
    [Tooltip("音频采样率")]
    public int audioSampleRate = 22050;

    [Header("文字转语音设定")]
    [Tooltip("预设的文字内容（测试用）")]
    [TextArea(3, 10)]
    public string testText = "你好，我是 AI 助手。我可以帮助你学习折纸。首先，我们需要准备一张正方形的纸。";
    
    [Tooltip("语速（1.0 = 正常速度）")]
    [Range(0.5f, 2.0f)]
    public float speechRate = 1.0f;
    
    [Tooltip("音量")]
    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Header("动作设定")]
    [Tooltip("启用手势动作")]
    public bool enableGestures = true;
    
    [Tooltip("手势强度")]
    [Range(0f, 1f)]
    public float gestureIntensity = 0.5f;
    
    [Tooltip("点头频率（秒）")]
    [Range(2f, 8f)]
    public float nodInterval = 4f;
    
    [Tooltip("眨眼频率（秒）")]
    [Range(2f, 6f)]
    public float blinkInterval = 3.5f;

    [Header("Avatar 位置设定")]
    [Tooltip("启用自定义 RemoteAvatar 位置")]
    public bool useCustomPosition = true;
    
    [Tooltip("RemoteAvatar 相对于相机的偏移")]
    public Vector3 remoteAvatarOffset = new Vector3(0, 0, 1.5f);
    
    [Tooltip("让 RemoteAvatar 面向玩家")]
    public bool facePlayer = true;
    
    [Tooltip("玩家相机")]
    public Camera playerCamera;

    [Header("调试")]
    [Tooltip("显示调试日志")]
    public bool showDebugLogs = true;

    // === 私有变量 ===
    private OvrAvatarLipSyncContext remoteLipSyncContext;
    private bool isSpeaking = false;
    private Coroutine speakingCoroutine;
    private Coroutine gestureCoroutine;
    private Coroutine blinkCoroutine;
    
    // 动作参数
    private float nextNodTime = 0f;
    private float nextBlinkTime = 0f;
    private float[] lipSyncVisemes = new float[15]; // OVR LipSync viseme count
    private float[] currentVisemeData; // 当前 TTS 生成的 viseme 数据
    private int visemeFrameIndex = 0;

    void Start()
    {
        FindComponents();
        InitializeRemoteAvatar();
        
        if (showDebugLogs)
            Debug.Log("[AvatarLLMController] 初始化完成");
    }

    void FindComponents()
    {
        // 自动查找组件
        if (localAvatar == null)
            localAvatar = GameObject.Find("LocalAvatar")?.GetComponent<OvrAvatarEntity>();
        
        if (remoteAvatar == null)
            remoteAvatar = GameObject.Find("RemoteLoopbackAvatar")?.GetComponent<OvrAvatarEntity>();
        
        if (playerCamera == null)
            playerCamera = Camera.main;
        
        if (remoteAudioSource == null && remoteAvatar != null)
        {
            remoteAudioSource = remoteAvatar.GetComponent<AudioSource>();
            if (remoteAudioSource == null)
                remoteAudioSource = remoteAvatar.gameObject.AddComponent<AudioSource>();
        }
        
        // 设置 AudioSource 参数
        if (remoteAudioSource != null)
        {
            remoteAudioSource.loop = false;
            remoteAudioSource.playOnAwake = false;
            remoteAudioSource.volume = volume;
            remoteAudioSource.spatialBlend = 0f; // 2D sound
        }
    }

    void InitializeRemoteAvatar()
    {
        if (remoteAvatar == null)
        {
            Debug.LogError("[AvatarLLMController] RemoteAvatar 未找到！");
            return;
        }

        // 等待 Avatar 创建完成
        StartCoroutine(WaitForAvatarReady());
    }

    IEnumerator WaitForAvatarReady()
    {
        // 等待 Remote Avatar 准备好
        while (remoteAvatar != null && !remoteAvatar.IsCreated)
        {
            yield return null;
        }

        if (remoteAvatar == null)
        {
            Debug.LogError("[AvatarLLMController] RemoteAvatar 创建失败");
            yield break;
        }

        // 注意：SetActiveView 是 protected 方法，只能在 OvrAvatarEntity 的子类中调用
        // Remote Avatar 应该已经设置为第三人称视角
        
        // 获取或添加 LipSync Context
        remoteLipSyncContext = remoteAvatar.GetComponentInChildren<OvrAvatarLipSyncContext>();
        if (remoteLipSyncContext == null)
        {
            var lipSyncObj = new GameObject("LipSyncContext");
            lipSyncObj.transform.SetParent(remoteAvatar.transform);
            remoteLipSyncContext = lipSyncObj.AddComponent<OvrAvatarLipSyncContext>();
            
            if (showDebugLogs)
                Debug.Log("[AvatarLLMController] 已添加 LipSyncContext");
        }

        // 设置 Avatar 位置
        if (useCustomPosition && playerCamera != null)
        {
            PositionRemoteAvatar();
        }

        // 开始背景动作（眨眼、点头等）
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(AutoBlinkRoutine());

        if (showDebugLogs)
            Debug.Log("[AvatarLLMController] RemoteAvatar 准备完成");
    }

    void PositionRemoteAvatar()
    {
        if (remoteAvatar == null || playerCamera == null) return;

        // 设置位置（相对于相机）
        Vector3 targetPosition = playerCamera.transform.position + 
                                 playerCamera.transform.forward * remoteAvatarOffset.z +
                                 playerCamera.transform.right * remoteAvatarOffset.x +
                                 playerCamera.transform.up * remoteAvatarOffset.y;
        
        remoteAvatar.transform.position = targetPosition;

        // 让 Avatar 面向玩家
        if (facePlayer)
        {
            Vector3 lookDirection = playerCamera.transform.position - remoteAvatar.transform.position;
            lookDirection.y = 0; // 只旋转 Y 轴
            if (lookDirection != Vector3.zero)
            {
                remoteAvatar.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }

    void Update()
    {
        // 处理键盘输入
        HandleKeyboardInput();
        
        // 更新 Avatar 位置（如果需要跟随相机）
        if (useCustomPosition && playerCamera != null && !isSpeaking)
        {
            // 可以选择是否持续更新位置
            // PositionRemoteAvatar();
        }
    }

    void HandleKeyboardInput()
    {
        // 空格键：开始说话
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isSpeaking)
            {
                StartSpeaking(testText);
            }
            else
            {
                StopSpeaking();
            }
        }

        // T 键：测试说话
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (!isSpeaking)
            {
                StartSpeaking(testText);
            }
        }

        // ESC 键：停止说话
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StopSpeaking();
        }
    }

    /// <summary>
    /// 开始说话（输入文字）
    /// </summary>
    public void StartSpeaking(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("[AvatarLLMController] 文字内容为空");
            return;
        }

        if (remoteAvatar == null || !remoteAvatar.IsCreated)
        {
            Debug.LogError("[AvatarLLMController] RemoteAvatar 未准备好");
            return;
        }

        // 停止之前的说话
        StopSpeaking();

        if (showDebugLogs)
            Debug.Log($"[AvatarLLMController] 开始说话: {text}");

        // 开始说话协程
        speakingCoroutine = StartCoroutine(SpeakingRoutine(text));
    }

    /// <summary>
    /// 停止说话
    /// </summary>
    public void StopSpeaking()
    {
        if (speakingCoroutine != null)
        {
            StopCoroutine(speakingCoroutine);
            speakingCoroutine = null;
        }

        if (gestureCoroutine != null)
        {
            StopCoroutine(gestureCoroutine);
            gestureCoroutine = null;
        }

        isSpeaking = false;

        // 停止音频
        if (remoteAudioSource != null && remoteAudioSource.isPlaying)
        {
            remoteAudioSource.Stop();
        }

        // 清除嘴型
        ClearLipSync();

        if (showDebugLogs)
            Debug.Log("[AvatarLLMController] 停止说话");
    }

    /// <summary>
    /// 说话协程
    /// </summary>
    IEnumerator SpeakingRoutine(string text)
    {
        isSpeaking = true;

        // 1. 生成 TTS 音频（这里使用 Unity 的 TextToSpeech 或模拟）
        AudioClip speechClip = GenerateSpeechAudio(text);
        
        if (speechClip == null)
        {
            Debug.LogError("[AvatarLLMController] 无法生成语音");
            isSpeaking = false;
            yield break;
        }

        // 2. 播放音频
        remoteAudioSource.clip = speechClip;
        remoteAudioSource.Play();

        // 3. 开始肢体动作
        if (enableGestures)
        {
            gestureCoroutine = StartCoroutine(GestureRoutine(speechClip.length));
        }

        // 4. 同步嘴型（基于音频播放）
        StartCoroutine(LipSyncRoutine(speechClip));

        // 5. 等待音频播放完成
        yield return new WaitForSeconds(speechClip.length / speechRate);

        // 6. 结束说话
        isSpeaking = false;
        ClearLipSync();

        if (showDebugLogs)
            Debug.Log("[AvatarLLMController] 说话完成");
    }

    /// <summary>
    /// 生成 TTS 音频
    /// 使用 SimpleTTSService 生成语音
    /// </summary>
    AudioClip GenerateSpeechAudio(string text)
    {
        // 使用 TTS 服务生成语音
        float[] visemeData;
        AudioClip clip = SimpleTTSService.Instance.GenerateSpeech(text, out visemeData);
        
        if (clip == null)
        {
            Debug.LogError("[AvatarLLMController] TTS 生成失败");
            return null;
        }
        
        // 保存 viseme 数据供后续使用
        currentVisemeData = visemeData;
        
        if (showDebugLogs)
            Debug.Log($"[AvatarLLMController] 生成音频: {text.Length} 字，{clip.length:F2} 秒");
        
        return clip;
    }

    /// <summary>
    /// 嘴型同步协程
    /// 使用从 TTS 服务获取的 viseme 数据
    /// </summary>
    IEnumerator LipSyncRoutine(AudioClip clip)
    {
        if (remoteLipSyncContext == null)
        {
            Debug.LogWarning("[AvatarLLMController] LipSyncContext 未找到，尝试使用音频数据同步");
            yield return LipSyncFromAudioRoutine(clip);
            yield break;
        }

        float startTime = Time.time;
        float duration = clip.length;
        visemeFrameIndex = 0;

        while (Time.time - startTime < duration && isSpeaking)
        {
            float t = (Time.time - startTime) / duration;
            
            // 如果有 TTS 提供的 viseme 数据，使用它
            if (currentVisemeData != null && currentVisemeData.Length > 0)
            {
                visemeFrameIndex = Mathf.FloorToInt(t * currentVisemeData.Length);
                visemeFrameIndex = Mathf.Clamp(visemeFrameIndex, 0, currentVisemeData.Length - 1);
                
                float visemeIntensity = currentVisemeData[visemeFrameIndex];
                UpdateLipSyncVisemesFromData(visemeIntensity);
            }
            else
            {
                // 否则使用简单的动画
                UpdateLipSyncVisemes(t);
            }

            yield return null;
        }

        ClearLipSync();
    }

    /// <summary>
    /// 从音频数据进行嘴型同步（备用方案）
    /// </summary>
    IEnumerator LipSyncFromAudioRoutine(AudioClip clip)
    {
        float startTime = Time.time;
        float duration = clip.length;
        
        // 获取音频数据
        float[] audioData = new float[clip.samples * clip.channels];
        clip.GetData(audioData, 0);
        
        int samplesPerFrame = Mathf.CeilToInt(clip.frequency / 30f); // 假设 30fps
        
        while (Time.time - startTime < duration && isSpeaking)
        {
            float t = (Time.time - startTime) / duration;
            int sampleIndex = Mathf.FloorToInt(t * audioData.Length);
            
            // 分析音频强度
            float amplitude = 0f;
            int sampleCount = Mathf.Min(samplesPerFrame, audioData.Length - sampleIndex);
            
            for (int i = 0; i < sampleCount; i++)
            {
                if (sampleIndex + i < audioData.Length)
                {
                    amplitude += Mathf.Abs(audioData[sampleIndex + i]);
                }
            }
            
            amplitude /= sampleCount;
            amplitude = Mathf.Clamp01(amplitude * 10f); // 放大并限制在 0-1
            
            // 根据音频强度更新嘴型
            UpdateLipSyncVisemesFromData(amplitude);
            
            yield return null;
        }

        ClearLipSync();
    }

    /// <summary>
    /// 根据 viseme 强度数据更新嘴型
    /// </summary>
    void UpdateLipSyncVisemesFromData(float intensity)
    {
        System.Array.Clear(lipSyncVisemes, 0, lipSyncVisemes.Length);
        
        // 根据强度在不同 viseme 之间分配
        // viseme 索引: 0=sil, 10-14 是元音 (aa, E, I, O, U)
        
        if (intensity < 0.1f)
        {
            // 几乎没有声音，嘴巴闭合
            lipSyncVisemes[0] = 1f; // sil
        }
        else
        {
            // 在元音之间循环
            int cycle = (int)(Time.time * 5f); // 每秒切换 5 次
            int primaryViseme = (cycle % 5) + 10; // aa, E, I, O, U
            
            lipSyncVisemes[primaryViseme] = intensity;
            
            // 添加次要 viseme（平滑过渡）
            int secondaryViseme = ((cycle + 1) % 5) + 10;
            lipSyncVisemes[secondaryViseme] = intensity * 0.3f;
        }
        
        ApplyLipSyncVisemes();
    }

    /// <summary>
    /// 更新嘴型 viseme 数据
    /// </summary>
    void UpdateLipSyncVisemes(float normalizedTime)
    {
        // 简单的嘴型动画（实际应该从 TTS viseme 数据获取）
        // viseme 索引: 0=sil, 1=PP, 2=FF, 3=TH, 4=DD, 5=kk, 6=CH, 7=SS, 8=nn, 9=RR, 10=aa, 11=E, 12=I, 13=O, 14=U
        
        // 清除所有 viseme
        System.Array.Clear(lipSyncVisemes, 0, lipSyncVisemes.Length);
        
        // 使用正弦波模拟说话的嘴型变化
        float cycle = normalizedTime * 20f; // 快速变化
        int primaryViseme = (int)(cycle) % 5 + 10; // 在元音之间循环 (aa, E, I, O, U)
        
        lipSyncVisemes[primaryViseme] = 0.6f + 0.4f * Mathf.Sin(cycle * Mathf.PI * 2);
        
        // 添加一些次要的嘴型
        int secondaryViseme = ((int)(cycle) + 2) % 5 + 10;
        lipSyncVisemes[secondaryViseme] = 0.3f;

        // 应用到 LipSync Context
        ApplyLipSyncVisemes();
    }

    /// <summary>
    /// 应用 viseme 数据到 LipSync
    /// 通过 AudioSource 让 Meta Avatar SDK 自动处理嘴型同步
    /// </summary>
    void ApplyLipSyncVisemes()
    {
        if (remoteLipSyncContext == null || remoteAudioSource == null) return;

        // Meta Avatar SDK 会自动从 AudioSource 读取音频数据并生成嘴型
        // 我们只需要确保 AudioSource 正在播放音频即可
        // SDK 会使用 OVR Lip Sync 插件自动分析音频并驱动嘴型
        
        // 如果需要手动控制 viseme，可以使用类似下面的方法：
        // remoteLipSyncContext.SetVisemeData(lipSyncVisemes);
        // 但这需要检查 SDK 的具体 API
    }

    /// <summary>
    /// 清除嘴型
    /// </summary>
    void ClearLipSync()
    {
        System.Array.Clear(lipSyncVisemes, 0, lipSyncVisemes.Length);
        ApplyLipSyncVisemes();
    }

    /// <summary>
    /// 肢体动作协程
    /// 添加自然的手势和身体动作
    /// </summary>
    IEnumerator GestureRoutine(float duration)
    {
        float startTime = Time.time;
        float elapsed = 0f;
        
        // 随机选择手势类型
        int gestureType = Random.Range(0, 3);
        
        // 注意：Meta Avatar SDK 的骨骼控制需要通过其他方式实现
        // 目前手势动画只是占位符，实际需要使用 Avatar SDK 的动画系统
        bool hasBodyControl = false; // 预留，待实现
        
        if (showDebugLogs)
        {
            Debug.Log("[AvatarLLMController] 手势动画功能待实现（需要 Avatar SDK 的动画 API）");
        }
        
        while (elapsed < duration && isSpeaking)
        {
            elapsed = Time.time - startTime;
            float t = elapsed / duration;
            
            // 执行不同的手势动作
            if (hasBodyControl)
            {
                switch (gestureType)
                {
                    case 0:
                        PerformPointingGesture(t);
                        break;
                    case 1:
                        PerformExplainingGesture(t);
                        break;
                    case 2:
                        PerformThinkingGesture(t);
                        break;
                }
            }
            
            // 随机点头
            if (Time.time >= nextNodTime)
            {
                StartCoroutine(NodHeadOnce());
                nextNodTime = Time.time + nodInterval + Random.Range(-1f, 1f);
            }
            
            yield return null;
        }
    }

    /// <summary>
    /// 指向手势 - 简单实现
    /// </summary>
    void PerformPointingGesture(float t)
    {
        // 注意：这是一个简化的实现
        // 实际的骨骼控制需要使用 Meta Avatar SDK 的 API
        // 例如：remoteAvatar.GetSkeletonTransform(jointType)
        
        // 创建手指指向的动画（使用正弦波）
        float angle = Mathf.Sin(t * Mathf.PI * 2) * 15f * gestureIntensity;
        
        // TODO: 应用到右手关节
        // var rightHand = remoteAvatar?.GetSkeletonTransform(SkeletonJointType.RightHand);
        // if (rightHand != null)
        // {
        //     rightHand.localRotation = Quaternion.Euler(angle, 0, 0);
        // }
    }

    /// <summary>
    /// 解释手势（双手展开）- 简单实现
    /// </summary>
    void PerformExplainingGesture(float t)
    {
        // 双手向两侧展开，模拟解释的动作
        float spread = Mathf.Sin(t * Mathf.PI) * 30f * gestureIntensity;
        
        // TODO: 应用到左右手关节
    }

    /// <summary>
    /// 思考手势（手托下巴）- 简单实现
    /// </summary>
    void PerformThinkingGesture(float t)
    {
        // 手慢慢移向下巴位置
        float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2f));
        
        // TODO: 应用到手部关节
    }

    /// <summary>
    /// 点头一次
    /// </summary>
    IEnumerator NodHeadOnce()
    {
        float nodDuration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < nodDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / nodDuration;
            
            // 使用正弦波创建平滑的点头动作
            float nodAngle = Mathf.Sin(t * Mathf.PI) * 10f * gestureIntensity;
            
            // TODO: 应用到头部骨骼
            // var head = remoteAvatar?.GetSkeletonTransform(SkeletonJointType.Head);
            // if (head != null)
            // {
            //     head.localRotation = Quaternion.Euler(nodAngle, 0, 0);
            // }
            
            yield return null;
        }
    }

    /// <summary>
    /// 自动眨眼协程
    /// </summary>
    IEnumerator AutoBlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(blinkInterval + Random.Range(-1f, 1f));
            
            // TODO: 触发眨眼动作
            // 这可能需要通过 Avatar SDK 的面部表情系统来实现
        }
    }

    /// <summary>
    /// 外部调用：让 Avatar 说指定的文字
    /// </summary>
    public void Speak(string text)
    {
        StartSpeaking(text);
    }

    /// <summary>
    /// 外部调用：检查是否正在说话
    /// </summary>
    public bool IsSpeaking()
    {
        return isSpeaking;
    }

    void OnGUI()
    {
        if (!showDebugLogs) return;

        // 显示状态信息
        GUILayout.BeginArea(new Rect(10, 10, 400, 200));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label($"<b>Avatar LLM Controller</b>");
        GUILayout.Label($"状态: {(isSpeaking ? "说话中..." : "待机")}");
        GUILayout.Label($"Remote Avatar: {(remoteAvatar != null && remoteAvatar.IsCreated ? "就绪" : "未就绪")}");
        GUILayout.Space(10);
        GUILayout.Label("<b>快捷键:</b>");
        GUILayout.Label("空格键 - 开始/停止说话");
        GUILayout.Label("T 键 - 测试说话");
        GUILayout.Label("ESC 键 - 停止");
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        StopSpeaking();
        
        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);
    }
}
