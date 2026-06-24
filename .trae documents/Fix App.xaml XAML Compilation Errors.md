# 修复 XAML 编译错误

## 任务描述
编译时发现多个 XAML 编译错误，主要是 App.xaml 中 ControlTemplate 使用了无效属性。需要全面审查并修复所有错误。

## 当前已知错误

### 1. App.xaml 中的无效属性

| 行号 | 控件 | 无效属性 | 修复方案 |
|------|------|----------|----------|
| 366 | ContentPresenter | FontSize | 已修复 - 移除 |
| 234-235 | DataGrid | HorizontalGridLineBrush, VerticalGridLineBrush | 已修复 - 移除 |
| 138 | ComboBox | SelectedContentTemplate | 已修复 - 移除 |
| 148 | StackPanel | Padding | 已修复 - 移除 |

### 2. MainWindow.xaml 错误
- 第 443 行：属性 "MinMin" 不存在 - 需要检查

### 3. WiiPluginEditorWindow.xaml 错误
- 第 116 行：属性 "Background" 不存在 - 需要检查

### 4. App.xaml XML 无效
- "根级别上的数据无效。第 1 行，位置 1" - 可能是 BOM 或编码问题

## 修复步骤

### Step 1: 检查并修复 App.xaml 的 XML 有效性
- 检查文件是否有 BOM 字符
- 确保 XML 声明正确

### Step 2: 全面审查 App.xaml 中的所有 ControlTemplate
- 检查所有 ContentPresenter 是否使用了不支持的属性 (FontSize, FontWeight, Foreground 等)
- 检查所有 StackPanel 是否使用了不支持的属性 (Padding)
- 检查所有 ItemsPresenter 是否使用了不支持的属性

### Step 3: 修复 MainWindow.xaml 错误
- 定位第 443 行的 MinMin 属性错误

### Step 4: 修复 WiiPluginEditorWindow.xaml 错误
- 定位第 116 行的 Background 属性错误

### Step 5: 验证编译
- 清理解决方案
- 重新生成解决方案
- 确认无编译错误

## 需要检查的文件
1. App.xaml - 全局样式资源
2. MainWindow.xaml - 主窗口
3. WiiPluginEditorWindow.xaml - 插件编辑器窗口
