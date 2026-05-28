# HDR SDR 亮度调节器

一个 Windows HDR 显示器 SDR 内容亮度调节工具，支持实时调节亮度、自定义亮度范围、开机自启动和最小化到系统托盘。

## 功能特性

- 🎛️ **实时亮度调节** — 拖动滑块即时改变 SDR 内容亮度
- 📖 **读取当前亮度** — 启动时自动读取系统当前 SDR 白色电平
- 🔧 **自定义亮度范围** — 可配置最低/默认/最高亮度，适配不同显示器
- 🚀 **开机自启动** — 支持注册表方式开机自动运行
- 📌 **最小化到托盘** — 关闭窗口最小化到系统托盘，支持开机静默启动
- 📝 **操作日志** — 记录每次亮度调整的结果和 HRESULT 返回值
- 💾 **设置持久化** — 所有配置自动保存到 JSON 文件

## 系统要求

- Windows 10 1903+ / Windows 11
- HDR 已开启的显示器
- .NET 8.0 运行时（单文件版已内含）

## 原理

使用 `dwmapi.dll` 中未文档化的 `DwmpSDRToHDRBoost` API（序号 171）来设置 SDR 白色电平：

```c
HRESULT DwmpSDRToHDRBoost(HMONITOR monitor, double brightness)
```

- `brightness` 值 ≥ 1.0，其中 1.0 = 80 nits，6.0 = 480 nits
- Windows 设置中的 SDR 内容亮度滑块范围通常为 1.0 ~ 6.0
- 超出显示器支持范围的值会导致画面过曝/剪切

读取当前亮度使用 `DisplayConfigGetDeviceInfo` + `DISPLAYCONFIG_SDR_WHITE_LEVEL` API。

## 下载

前往 [Releases](../../releases) 页面下载单文件可执行版本，无需安装 .NET 运行时。

## 使用方法

1. 运行 `HdrBrightness.exe`
2. 在「亮度调节」页面拖动滑块或使用快捷按钮
3. 在「设置」页面自定义亮度范围和启动选项

### 命令行参数

| 参数 | 说明 |
|------|------|
| `--minimized` | 启动时最小化到系统托盘 |

### 配置文件

配置文件保存在 `%APPDATA%\HdrBrightness\settings.json`：

```json
{
  "MinBrightness": 0.1,
  "MaxBrightness": 10.0,
  "DefaultBrightness": 1.0,
  "AutoStart": false,
  "StartMinimized": true,
  "LiveAdjust": true
}
```

## 从源码构建

```bash
# 还原依赖
dotnet restore

# 构建
dotnet build -c Release

# 单文件发布（内含运行时，无需安装 .NET）
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true
```

## 注意事项

- ⚠️ 此工具使用了 Windows 未文档化 API，未来系统更新可能导致功能异常
- ⚠️ 仅在 HDR 已开启的显示器上有效
- ⚠️ 设置过高亮度值可能导致画面过曝

## 致谢

- [DwmpSDRToHDRBoost API 发现](https://dev59.com/kHwQtIcB2Jgan1znUuDX) — lulle2007207
- C# 版本实现参考 — MaloW

## 许可证

MIT License
