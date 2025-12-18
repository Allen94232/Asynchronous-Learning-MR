using UnityEngine;

/// <summary>
/// 為 NetworkLoopbackExample 場景添加麥克風指定功能
/// 將此腳本添加到場景中任何 GameObject，它會自動配置所有 LipSyncMicInput 組件
/// </summary>
public class NetworkLoopbackMicrophoneSelector : MonoBehaviour
{
    [Header("麥克風設定")]
    [Tooltip("指定麥克風設備名稱（留空自動選擇非虛擬設備）")]
    public string targetMicrophoneDevice = "";
    
    [Tooltip("在開始時自動配置麥克風")]
    public bool autoConfigureOnStart = true;
    
    [Tooltip("列出所有可用麥克風")]
    public bool listMicrophonesOnStart = true;

    [Header("調試")]
    [Tooltip("顯示詳細日誌")]
    public bool showDebugLogs = true;

    private string selectedMicrophone = null;

    void Start()
    {
        if (listMicrophonesOnStart)
        {
            ListAvailableMicrophones();
        }

        if (autoConfigureOnStart)
        {
            ConfigureAllMicrophones();
        }
    }

    /// <summary>
    /// 列出所有可用麥克風
    /// </summary>
    [ContextMenu("列出所有麥克風")]
    public void ListAvailableMicrophones()
    {
        Log("=== 可用麥克風設備 ===");
        
        if (Microphone.devices.Length == 0)
        {
            LogError("❌ 沒有檢測到任何麥克風設備！");
            return;
        }

        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            string device = Microphone.devices[i];
            bool isVirtual = IsVirtualDevice(device);
            string marker = isVirtual ? "⚠ 虛擬設備" : "✓ 實體設備";
            
            Log($"[{i}] {device} {marker}");
            
            if (i == 0)
            {
                Log($"    ← 當前默認設備");
            }
        }
        
        Log($"總共找到 {Microphone.devices.Length} 個麥克風");
        Log("====================");
    }

    /// <summary>
    /// 配置場景中所有的 LipSyncMicInput 組件
    /// </summary>
    [ContextMenu("配置所有麥克風")]
    public void ConfigureAllMicrophones()
    {
        // 選擇麥克風
        SelectBestMicrophone();

        if (string.IsNullOrEmpty(selectedMicrophone))
        {
            LogError("❌ 無法找到可用的麥克風！");
            return;
        }

        Log($"✓ 選定麥克風: {selectedMicrophone}");

        // 查找所有 LipSyncMicInput 組件
        var micInputs = FindObjectsOfType<MonoBehaviour>();
        int configuredCount = 0;

        foreach (var component in micInputs)
        {
            // 檢查是否是 LipSyncMicInput 類型
            if (component.GetType().Name == "LipSyncMicInput")
            {
                // 使用反射設置私有字段
                var deviceField = component.GetType().GetField("_selectedDevice", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (deviceField != null)
                {
                    deviceField.SetValue(component, selectedMicrophone);
                    configuredCount++;
                    Log($"✓ 已配置 {component.gameObject.name} 的麥克風");
                }
                else
                {
                    // 嘗試通過公共方法設置
                    var selectMethod = component.GetType().GetMethod("SelectMicrophone");
                    if (selectMethod != null)
                    {
                        selectMethod.Invoke(component, new object[] { selectedMicrophone });
                        configuredCount++;
                        Log($"✓ 已配置 {component.gameObject.name} 的麥克風");
                    }
                }
            }
        }

        if (configuredCount == 0)
        {
            LogWarning("⚠ 場景中沒有找到 LipSyncMicInput 組件");
            LogWarning("NetworkLoopbackExample 場景預設不包含音頻錄製");
            LogWarning("如需添加音頻，請參考 AVATAR_AUDIO_SETUP.md");
        }
        else
        {
            Log($"✓ 成功配置 {configuredCount} 個麥克風組件");
        }

        // 更新所有 AudioSource 的麥克風
        ConfigureAudioSources();
    }

    /// <summary>
    /// 配置所有 AudioSource 使用選定的麥克風
    /// </summary>
    private void ConfigureAudioSources()
    {
        if (string.IsNullOrEmpty(selectedMicrophone))
        {
            return;
        }

        AudioSource[] audioSources = FindObjectsOfType<AudioSource>();
        int configuredCount = 0;

        foreach (var audioSource in audioSources)
        {
            // 只配置沒有 clip 或 clip 來自麥克風的 AudioSource
            if (audioSource.clip == null || audioSource.clip.name.Contains("Microphone"))
            {
                // 停止當前錄製
                if (Microphone.IsRecording(null))
                {
                    Microphone.End(null);
                }

                // 使用新麥克風開始錄製
                audioSource.clip = Microphone.Start(selectedMicrophone, true, 1, 48000);
                audioSource.loop = true;
                
                // 等待麥克風準備好
                while (!(Microphone.GetPosition(selectedMicrophone) > 0)) { }
                
                // 可選：播放以聽到回音（通常不需要）
                // audioSource.Play();

                configuredCount++;
                Log($"✓ 已配置 AudioSource: {audioSource.gameObject.name}");
            }
        }

        if (configuredCount > 0)
        {
            Log($"✓ 成功配置 {configuredCount} 個 AudioSource");
        }
    }

    /// <summary>
    /// 選擇最佳麥克風（優先選擇實體設備）
    /// </summary>
    private void SelectBestMicrophone()
    {
        // 如果手動指定了麥克風
        if (!string.IsNullOrEmpty(targetMicrophoneDevice))
        {
            if (System.Array.Exists(Microphone.devices, device => device == targetMicrophoneDevice))
            {
                selectedMicrophone = targetMicrophoneDevice;
                Log($"✓ 使用手動指定的麥克風: {selectedMicrophone}");
                return;
            }
            else
            {
                LogWarning($"⚠ 指定的麥克風 '{targetMicrophoneDevice}' 不存在");
            }
        }

        // 自動選擇非虛擬設備
        foreach (string device in Microphone.devices)
        {
            if (!IsVirtualDevice(device))
            {
                selectedMicrophone = device;
                Log($"✓ 自動選擇實體麥克風: {selectedMicrophone}");
                return;
            }
        }

        // 如果只有虛擬設備，使用第一個
        if (Microphone.devices.Length > 0)
        {
            selectedMicrophone = Microphone.devices[0];
            LogWarning($"⚠ 只找到虛擬設備，使用: {selectedMicrophone}");
            LogWarning("在 Unity Editor 中，虛擬設備可能無法正常工作");
        }
    }

    /// <summary>
    /// 判斷是否為虛擬設備
    /// </summary>
    private bool IsVirtualDevice(string deviceName)
    {
        return deviceName.Contains("Virtual") ||
               deviceName.Contains("Oculus") ||
               deviceName.Contains("Loopback") ||
               deviceName.Contains("Stereo Mix") ||
               deviceName.Contains("Cable");
    }

    /// <summary>
    /// 在運行時切換麥克風
    /// </summary>
    public void SwitchMicrophone(string newDevice)
    {
        if (string.IsNullOrEmpty(newDevice))
        {
            LogError("麥克風名稱不能為空");
            return;
        }

        if (!System.Array.Exists(Microphone.devices, device => device == newDevice))
        {
            LogError($"麥克風 '{newDevice}' 不存在");
            return;
        }

        targetMicrophoneDevice = newDevice;
        ConfigureAllMicrophones();
    }

    /// <summary>
    /// 獲取當前選定的麥克風
    /// </summary>
    public string GetSelectedMicrophone()
    {
        return selectedMicrophone;
    }

    // 日誌輔助方法
    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[MicSelector] {message}");
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MicSelector] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[MicSelector] {message}");
    }

#if UNITY_EDITOR
    [ContextMenu("強制重新配置")]
    private void ForceReconfigure()
    {
        selectedMicrophone = null;
        ConfigureAllMicrophones();
    }

    [ContextMenu("測試當前麥克風")]
    private void TestCurrentMicrophone()
    {
        if (string.IsNullOrEmpty(selectedMicrophone))
        {
            LogError("尚未選擇麥克風");
            return;
        }

        Log($"測試麥克風: {selectedMicrophone}");
        
        if (Microphone.IsRecording(selectedMicrophone))
        {
            Log("✓ 麥克風正在錄製");
        }
        else
        {
            Log("✗ 麥克風未在錄製");
        }
    }
#endif
}
