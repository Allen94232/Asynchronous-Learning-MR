# YOLO 模型测试指南

## 📁 文件结构

```
share_model/
├── best.pt                      # YOLO 模型文件
├── detect_shapes.py             # Unity 调用的检测脚本
├── test_single_image.py         # 单图测试脚本（新）
├── test_with_confidence.py      # 多信心度测试脚本（新）
├── test_model.bat               # Windows 快捷启动（新）
├── TESTING_GUIDE.md            # 本文档
└── test_images/                 # 测试图片文件夹（新）
    ├── README.txt
    └── (放你的测试图片在这里)
```

## 🚀 快速开始

### 方法 1：批处理文件（最简单）

1. 将测试图片复制到 `test_images/` 文件夹
2. 双击 `test_model.bat`
3. 查看检测结果

### 方法 2：Python 命令行

```bash
# 进入目录
cd "C:\Users\user\Desktop\Asynchronous Learning\Assets\share_model"

# 激活虚拟环境（如果有）
..\..\..\.venv\Scripts\activate

# 测试所有图片
python test_single_image.py

# 测试单张图片的多个信心度
python test_with_confidence.py test_images/origami.png
```

## 📊 测试工具说明

### 1. test_single_image.py

**功能**：测试 test_images 文件夹中的所有图片

**输出**：
- 每张图片的检测结果
- 信心度分数
- 标注后的图片（保存为 `result_*.png`）

**示例输出**：
```
✅ 检测到 1 个物体：

   [1] shape_1
       信心度: 85.3%
       边界框: (120, 200) - (450, 580)

🎯 最佳检测: shape_1 (信心度: 85.3%)
💾 标注图片已保存: result_origami.png
```

### 2. test_with_confidence.py

**功能**：用不同的信心度阈值（0.1-0.7）测试同一张图片

**用法**：
```bash
python test_with_confidence.py test_images/your_image.png
```

**输出示例**：
```
📊 检测结果汇总
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
阈值     检测结果         信心度
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
0.1      shape_1          85.3%
0.2      shape_1          85.3%
0.3      shape_1          85.3%
0.4      shape_1          85.3%
0.5      shape_1          85.3%
0.6      未检测到          -
0.7      未检测到          -

💡 建议：
   最低可用阈值: 0.1
   ✅ 检测质量很好，可以使用较高阈值
```

## 🎯 测试建议

### 拍摄条件优化

1. **背景**：
   - ✅ 白纸或纯色桌面
   - ❌ 避免文字、图案、复杂纹理

2. **光线**：
   - ✅ 均匀明亮的光线
   - ❌ 避免强烈阴影和反光

3. **角度**：
   - ✅ 正面平拍（相机垂直于桌面）
   - ❌ 避免过度倾斜

4. **折纸位置**：
   - ✅ 完整在画面中，占据 50-70% 区域
   - ✅ 居中摆放
   - ❌ 不要被遮挡或切边

### 信心度阈值选择

| 阈值范围 | 适用场景 | 说明 |
|---------|---------|------|
| 0.7-0.9 | 理想环境 | 高质量图片，干净背景 |
| 0.5-0.7 | 标准环境 | 一般质量图片，较干净背景 |
| 0.3-0.5 | 复杂环境 | 背景较复杂或光线不佳 |
| 0.1-0.3 | 困难环境 | 背景很复杂或图片质量差 |

## 🔧 故障排除

### 问题：未检测到任何物体

**解决方案**：
1. 运行 `test_with_confidence.py` 查看不同阈值的效果
2. 改善拍摄条件（背景、光线）
3. 降低信心度阈值到 0.2-0.3
4. 确保折纸完整在画面中

### 问题：检测结果不稳定

**解决方案**：
1. 固定相机位置
2. 使用稳定的光源
3. 使用纯色背景
4. 提高信心度阈值过滤误检

### 问题：检测到错误的类别

**可能原因**：
1. 折纸状态不符合预期（未完成或超过步骤）
2. 拍摄角度导致形状变形
3. 模型训练数据不足

## 📝 输出文件

### 标注图片
- 位置：`test_images/result_*.png`
- 内容：原图 + 检测框 + 标签 + 信心度

### Unity 临时截图
- 位置：`C:/Users/user/AppData/Local/Temp/DefaultCompany/Asynchronous Learning/origami_capture.png`
- 说明：Unity 验证时的实时截图

## 💡 使用技巧

1. **快速迭代测试**：
   - 将多张不同条件的图片放入 test_images
   - 运行 test_single_image.py 批量测试
   - 对比结果找出最佳拍摄条件

2. **阈值调优**：
   - 使用 test_with_confidence.py 找到最佳阈值
   - 在 Unity ShapeDetector 中设置相同阈值

3. **质量验证**：
   - 查看 result_*.png 标注图片
   - 检查检测框是否准确框住折纸
   - 确认标签和信心度是否合理

## 🔗 相关文件

- Unity 脚本：`Assets/Scripts/ShapeDetector.cs`
- 诊断工具：`Assets/Scripts/DiagnoseCapture.cs`
- Python 检测：`detect_shapes.py`（Unity 调用）

## ❓ 常见问题

**Q: 为什么 Unity 能截图但检测不到？**
A: 先用测试工具验证模型是否能检测该图片，排除是截图质量问题还是模型问题。

**Q: 如何提高检测准确率？**
A: 1) 改善拍摄条件 2) 调整信心度阈值 3) 使用更多训练数据重新训练模型

**Q: 测试工具显示能检测，但 Unity 中不行？**
A: 检查 Unity ShapeDetector 的信心度阈值设置，确保与测试工具一致。
