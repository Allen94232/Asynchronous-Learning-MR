# Avatar LLM Controller 使用说明

## 概述

`AvatarLLMController` 是一个用 LLM 驱动 Avatar 的控制系统，可以让 Remote Avatar 根据输入的文字，配合自然的肢体动作念出来。它替代了原本的 `AvatarRecordingManager`（人为录制和回放），实现了 AI 驱动的 Avatar 交互。

## 主要组件

### 1. AvatarLLMController.cs
主控制脚本，负责：
- 管理 Local Avatar 和 Remote Avatar
- 接收文字输入并触发语音合成
- 控制嘴型同步
- 添加自然的肢体动作（点头、手势等）

### 2. SimpleTTSService.cs
文字转语音（TTS）服务，支持：
- **Mock 模式**：生成模拟音频用于测试（跨平台）
- 预留扩展接口：可添加云端 TTS（Azure、Google、Meta Wit.AI 等）

> **注意**：当前版本使用 Mock 模式生成模拟音频。如需真实的语音合成，请参考"扩展指南"部分集成云端 TTS 服务。

## 快速开始

### 步骤 1：在场景中设置

1. **移除或禁用旧的 RecordingManager**
   - 在 Hierarchy 中找到 `RecordingManager` GameObject
   - 禁用或删除 `AvatarRecordingManager` 组件

2. **添加 SimpleTTSService**
   - 创建一个新的空 GameObject，命名为 "SimpleTTSService"
   - 添加 `SimpleTTSService` 组件
   - 配置 TTS 设置：
     - **Provider**: 选择 `Mock`（当前唯一可用选项）
     - **Voice Language**: "zh-CN"（中文）或 "en-US"（英文）- 目前仅用于未来扩展
     - **Rate**: 语速（-10 到 10，0 = 正常）- Mock 模式下暂不生效
     - **Volume**: 音量（0 到 100）
     - **Mock Char Duration**: 每个字的持续时间（秒），默认 0.25

3. **添加 AvatarLLMController**
   - 选择场景中的任意 GameObject（或创建新的 "AvatarLLMController"）
   - 添加 `AvatarLLMController` 组件
   - 配置 Avatar 设置：
     - **Local Avatar**: 拖入 LocalAvatar GameObject
     - **Remote Avatar**: 拖入 RemoteLoopbackAvatar GameObject
     - **Player Camera**: 拖入主相机

4. **配置文字内容**
   - 在 Inspector 的 **Test Text** 字段中输入要测试的文字
   - 例如："你好，我是 AI 助手。我可以帮助你学习折纸。"

### 步骤 2：测试运行

1. 点击 Unity 的 Play 按钮
2. 等待 Avatar 初始化完成
3. 按下快捷键：
   - **空格键**：开始/停止说话
   - **T 键**：测试说话
   - **ESC 键**：停止说话

## 配置选项

### AvatarLLMController 参数

#### Avatar 设置
- **Local Avatar**: 用户控制的第一人称 Avatar
- **Remote Avatar**: AI 驱动的第三人称 Avatar

#### 音频设置
- **Remote Audio Source**: Remote Avatar 的音频源（自动创建）
- **Audio Sample Rate**: 音频采样率（默认 22050）

#### 文字转语音设置
- **Test Text**: 预设的文字内容（用于测试）
- **Speech Rate**: 语速（0.5 到 2.0，1.0 = 正常）
- **Volume**: 音量（0 到 1）

#### 动作设置
- **Enable Gestures**: 启用手势动作
- **Gesture Intensity**: 手势强度（0 到 1）
- **Nod Interval**: 点头频率（秒）
- **Blink Interval**: 眨眼频率（秒）

#### Avatar 位置设置
- **Use Custom Position**: 启用自定义 Remote Avatar 位置
- **Remote Avatar Offset**: 相对于相机的偏移（Z=前后, X=左右, Y=上下）
- **Face Player**: 让 Remote Avatar 面向玩家
- **Player Camera**: 玩家相机引用

### SimpleTTSService 参数

#### TTS 设置
- **PMock`: 模拟音频（当前唯一可用，适合快速测试和开发）
  - `AzureTTS`, `GoogleTTS`, `WitAiTTS`: 预留选项，需要自行实现
- **Voice Language**: 语音语言代码（预留，Mock 模式下不使用）
  - "zh-CN": 简体中文
  - "zh-TW": 繁体中文
  - "en-US": 美式英语
- **Rate**: 语速（-10 到 10）- 预留，Mock 模式下不使用
- **Volume**: 音量（0 到 100）

#### 音频设置
- **Sample Rate**: 采样率（默认 22050）
- **Mock Char Duration**: Mock 模式下每个字的持续时间（秒），默认 0.25
- **Mock Char Duration**: Mock 模式下每个字的持续时间

## 使用示例

### 在代码中调用

```csharp
// 获取控制器
AvatarLLMController controller = FindObjectOfType<AvatarLLMController>();

// 让 Avatar 说话
controller.Speak("欢迎来到 VR 学习空间！");

// 检查是否正在说话
if (controller.IsSpeaking())
{
    Debug.Log("Avatar 正在说话...");
}

// 停止说话
controller.StopSpeaking();
```

### 与 LLM 集成

未来可以与 LLM API 集成：

```csharp
// 伪代码示例
async void OnUserQuestion(string question)
{
    // 1. 发送问题到 LLM
    string answer = await LLMService.GetResponse(question);
    
    // 2. 让 Avatar 说出答案
    AvatarLLMController controller = FindObjectOfType<AvatarLLMController>();
    controller.Speak(answer);
}
```

## 功能特性

### 1. 智能嘴型同步
- 自动从音频数据分析生成嘴型
- 支持 Meta Avatar SDK 的 LipSync 系统
- 平滑的 viseme 过渡

### 2. 自然动作
- 随机点头
- 自动眨眼
- 手势动画（指向、解释、思考）
- 可扩展更多动作

### 3. 灵活的 TTS
- 支持多种 TTS 提供者
- 易于扩展云端 TTS 服务
- Mock 模式用于快速测试

## 扩展指南

### 添加真实的云端 TTS

当前版本使用 Mock 音频。要使用真实的 TTS，可以集成云端服务：

#### 方案 1：使用 Azure TTS（推荐）

```csharp
// 在 SimpleTTSService.cs 中添加 Azure TTS 实现
using UnityEngine.Networking; // 用于 HTTP 请求

AudioClip GenerateSpeechAzure(string text, out float[] visemeData)
{
    visemeData = null;
    
    // 1. 构建 Azure TTS API 请求
    string apiKey = "YOUR_AZURE_API_KEY";
    string region = "eastus";
    string endpoint = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";
    
    // 2. 发送 HTTP 请求获取音频
    // 使用 StartCoroutine 异步调用
    // ...
    
    // 3. 将返回的音频数据转换为 AudioClip
    return clip;
}
```

#### 方案 2：使用 Unity Web Request + 外部 TTS API

```csharp
public IEnumerator GenerateSpeechAsync(string text, System.Action<AudioClip> callback)
{
    // 使用 Google TTS API
    string apiKey = "YOUR_GOOGLE_API_KEY";
    string url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";
    
    // 构建请求 JSON
    string jsonData = $@"{{
        ""input"": {{""text"": ""{text}""}},
        ""voice"": {{""languageCode"": ""zh-CN"", ""name"": ""zh-CN-Standard-A""}},
        ""audioConfig"": {{""audioEncoding"": ""MP3""}}
    }}";
    
    using (UnityWebRequest request = UnityWebRequest.Post(url, jsonData))
    {
        request.SetRequestHeader("Content-Type", "application/json");
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            // 解析音频数据并转换为 AudioClip
            // ...
        }
    }
}
```

#### 方案 3：使用 Meta Wit.AI TTS

项目中已包含 Meta.WitAi.TTS SDK，可以直接使用：
存在于场景中
2. 查看 Console 的错误信息
3. 确认 Remote Avatar 已正确加载
4. 检查 Test Text 字段是否有内容

### 没有声音
1. 检查 Remote Avatar 上的 Audio Source 音量设置
2. 确认 Unity 的主音量没有静音
3. Mock 模式会生成模拟的电子音，这是正常的
4. 检查系统音量
        TTSClipData clipData = ttsService.Load(text);
        // 等待音频生成完成
        return clipData.clip;
    }
    
    return null;
}集成真实的 TTS 服务（Azure、Google 或 Meta Wit.AI）
- [ ] 完善骨骼动作控制
- [ ] 添加更多自然动作库
- [ ] 实现情感表达（高兴、悲伤等）
- [ ] 集成 LLM API（OpenAI、Claude 等）
- [ ] 添加语音识别（STT）支持用户语音输入
- [ ] 优化嘴型同步精度` 中添加新的手势：

```csharp
void PerformWavingGesture(float t)
{
    // 实现挥手动作
    float waveAngle = Mathf.Sin(t * 10f * Mathf.PI) * 30f;
    // 应用到手部骨骼
}
```

### 访问 Avatar 骨骼

使用 Meta Avatar SDK 的 API 控制骨骼：

```csharp
// 获取骨骼关节
var skeleton = remoteAvatar.GetComponent<OvrAvatarSkinnedRenderable>();
if (skeleton != null && skeleton.skeleton != null)
{
    // 访问特定关节
    var headJoint = skeleton.skeleton.GetJoint(CAPI.ovrAvatar2JointType.Head);
    // 控制关节旋转
    // ...
}
```

## 已知限制

1. **骨骼动作控制**：当前版本的手势动作是框架代码，需要进一步实现 Meta Avatar SDK 的骨骼控制 API
2. **Windows TTS 语言支持**：需要确保 Windows 系统安装了对应语言的语音包
3. **嘴型同步**：目前依赖 Meta Avatar SDK 的自动 LipSync，手动控制需要更深入的 SDK 集成

## 故障排除

### Avatar 不说话
1. 检查 SimpleTTSService 是否正确初始化
2. 查看 Console 的错误信息
3. 确认 Remote Avatar 已正确加载

### 没有声音
1. 检查 Audio Source 的音量设置
2. 确认 Unity 的主音量没有静音
3. 检查 TTS Provider 是否正常工作

### Windows TTS 无法使用
1. 确认运行在 Windows 平台
2. 检查系统是否安装了对应语言的 TTS 语音包
3. 尝试切换到 Mock 模式测试

## 下一步计划

- [ ] 完善骨骼动作控制
- [ ] 集成云端 TTS 服务（Azure、Google）
- [ ] 添加更多自然动作库
- [ ] 实现情感表达（高兴、悲伤等）
- [ ] 集成 LLM API（OpenAI、Claude 等）
- [ ] 添加语音识别（STT）支持用户语音输入

## 反馈与支持

如果遇到问题或有改进建议，请记录详细的错误信息和 Unity 版本。
