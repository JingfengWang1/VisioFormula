# Visio Formula

**将渲染好的公式以矢量图直接拖入 Visio，支持多种数学字体。**

Visio Formula 是一款面向 Windows 桌面 Visio 的公式工具：使用 TeX / LaTeX 数学语法编辑公式、实时预览，并根据图中文字的风格选择数学字体。渲染完成后，从预览区直接拖入 Visio，矢量公式在缩放后仍保持清晰，无需先手动保存文件再导入。

目前提供 **7 种数学字体**：Latin Modern、Times / NewTX、STIX Two、Palatino / NewPX、Libertinus、Sans / NewTXsf 和 MathJax / TeX。公式源码、字体和字号随 Visio 文档保存，重开文档后可读回编辑、原位更新。

当前功能版本：**0.9.0**。所有公式渲染均在本机进行。

**后续 AI 或开发者请先阅读 [项目交接与实现说明](docs/AI_HANDOFF.md)，再阅读 [验证记录](docs/VALIDATION.md)。**

## 功能

- **矢量公式直接进入 Visio**：从预览区拖入，缩放保持清晰；也支持复制粘贴和导出 SVG。
- **多种数学字体**：使用同一份 TeX 公式源码，切换 7 种数学字体、设置字号，与图中文字协调搭配。
- **TeX 编辑与实时预览**：输入时使用离线 MathJax 快速预览；停止输入后自动生成所选字体的精确 SVG。
- 源码、字体、字号保存在 Visio 图形的数据单元格中；保存并重开文档后仍可编辑。
- 在 Visio 中选中公式，在工具中点击“编辑选中公式”；修改后原位更新，保留外层图形 ID、中心位置、旋转与已有等比缩放。
- 参考 Quick LaTeX 使用习惯的快捷输入、44 条默认缩写规则、自定义规则导入导出、占位跳转、分式与上下标自动转换。
- 语法高亮、独立撤销/重做、输入法组合输入处理。
- 自动恢复当前草稿、新建公式、最近 20 条历史草稿。
- 批量统一当前选中范围或整页公式的字体/字号，支持组内公式、取消、失败回滚与一次撤销。
- 在设置中可开启自动原位更新：读回目标后，停止输入约 600 ms 且精确结果就绪时自动同步；支持 Visio 撤销，冲突时暂停。
- 环境检查：识别 Visio / TeX 路径，实际测试七种字体，提供手动路径选择和可复制的诊断信息。
- 缓存管理：默认自动清理，容量目标 20 MB、保留 7 天，保护其他窗口正在使用的结果。
- 紧凑的上下布局、统一的设置和菜单样式、DPI 适配、预览缩放与完整适配。

## 运行条件

| 用途 | 条件 |
| --- | --- |
| 运行工具 | 64 位 Windows 10/11，.NET Framework 4.8 或更新版本 |
| MathJax 预览与 SVG 导出 | 已随程序附带离线资源，无须另装 LaTeX |
| 六种本机 LaTeX 字体 | `latex.exe`、`dvisvgm.exe` 及相关宏包；当前主要验证环境为 TeX Live 2024 |
| 拖入、复制、读回与更新 | Microsoft Visio 桌面版；当前实测 Visio Professional 2021 x64 |

下载可运行版本请查看 [Releases](https://github.com/JingfengWang1/VisioFormula/releases)。解压整个程序文件夹后运行 `VisioFormula.exe`。

这是免安装的本地工具，不是 Visio Web 插件；无需安装 Visio 加载项或启用宏。保持程序与 Visio 使用相同的普通用户权限。

## 从源码构建

项目采用 C# / WinForms / .NET Framework 4.8，x64 构建。

在仓库根目录运行 PowerShell：

```powershell
.\build.ps1
.\VisioFormula.exe
```

构建脚本直接使用 Windows 的 `.NET Framework64\v4.0.30319\csc.exe`，无需 Visual Studio、Node.js、npm 或 NuGet。当前代码保持 C# 5 兼容。

也可以输出到独立目录：

```powershell
.\build.ps1 -OutputDirectory .\dist\VisioFormula
.\dist\VisioFormula\VisioFormula.exe
```

分发时复制整个输出文件夹，至少保留 `VisioFormula.exe`、`VisioFormula.exe.config` 和完整 `assets` 目录。

## 基本操作

1. 输入数学公式主体，等待预览；本机 LaTeX 字体会先显示近似预览，再切换为精确结果。
2. 拖动预览中的公式到 Visio，或点击“复制到 Visio”后粘贴。
3. 要修改已有公式，先在 Visio 中选中完整的公式组，再点击工具中的“编辑选中公式”。
4. 修改完成后点击“原位更新”，并保存 Visio 文档。
5. “更多”中可配置快捷输入、恢复最近草稿、批量统一公式样式。

输入 `al` 后按空格展开为 `\alpha`；`sq` 展开为 `\sqrt{}` 并进入参数。Tab / Shift+Tab 在占位位置间移动。Ctrl+Z 撤销，Ctrl+Y / Ctrl+Shift+Z 重做，Ctrl+N 新建，Ctrl+Enter 强制精确重编。

## LaTeX 工具查找

优先使用“更多 → 环境检查”中手动选择并保存的路径；未指定时依次查找程序旁的 `tex-bin.txt`、系统 `PATH`、固定磁盘根目录下的 TeX Live 版本目录，以及常见 MiKTeX 安装目录。

非标准安装路径可在程序旁创建 `tex-bin.txt`，只填工具所在目录，例如：

```text
E:\TeXLive\2026\bin\windows
```

此文件是本机配置，Git 默认忽略。程序不会自动安装 TeX 或宏包。新电脑可先打开“更多 → 环境检查”，再点击“测试渲染”逐项确认字体可用。

## 草稿和设置

保存在 `%LOCALAPPDATA%\VisioFormula`：

| 文件 | 内容 |
| --- | --- |
| `settings.json` | 字体、字号、预览缩放、自动更新、TeX 路径和缓存策略 |
| `settings.quick-input.json` | 快捷输入开关与自定义规则 |
| `settings.drafts.json` | 当前草稿、光标及最近 20 条历史 |

当前草稿在编辑停顿约 450 ms 后保存，关闭时再保存。历史记录在新建、读回 Visio 公式、恢复另一草稿前归档当前非空内容；相同源码、字体、字号会去重。草稿恢复不恢复 Visio 图形的 COM 绑定。

复制程序文件夹即可迁移程序；要迁移个人设置和草稿，需要另外复制上述用户配置目录。图中公式源码随 `.vsdx` 保存。

详细步骤见 [0.9 使用说明](docs/USER_GUIDE.md)。

## 0.9 新功能的使用

- **自动原位更新**：在“更多 → 设置”启用，默认关闭；开关只在设置中。先“编辑选中公式”建立关联，之后源码、字体和字号可自动同步。同一内容不会重复写回；每次实际同步可在 Visio 撤销。检测到目标被修改或撤销后暂停，可重新读回或手动更新后继续。新建、恢复草稿、关闭文档或删除目标会解除关联；重开工具不会恢复旧目标。Visio 文档仍由用户保存。
- **环境检查**：区分 Visio 未安装、未运行和可连接状态；显示工具路径与版本。快速检查只确认基本环境，七种字体需要点击“测试渲染”实际编译。缺少本机 TeX 时仍可使用 MathJax 字体。手动选择的路径在关闭检查窗口后保存。
- **缓存管理**：新缓存位于 `%LOCALAPPDATA%\VisioFormula\Cache`，不会进入同步的程序目录。默认保留 7 天、容量目标 20 MB，可调整；使用中的文件可能暂时超过目标。立即清理会删除可释放的缓存，保留草稿、个人配置和文档。可勾选清理当前程序目录下已识别的旧版缓存。

## 测试

先构建，然后按改动范围运行：

```powershell
.\test-editor.ps1
.\test-theme.ps1
.\test-realtime.ps1
.\test-maintenance.ps1  # 包括独立 Visio 实例；无 Visio 环境可加 -WithoutVisio
```

Visio 交互测试使用独立测试文档；其中原生复制测试会使用系统剪贴板：

```powershell
.\test-batch.ps1
.\run-tests.ps1
.\test-connection.ps1
```

跨实例连接测试需按 `ACTIVATE:` 提示激活对应的测试窗口。测试依赖、历史结果和覆盖边界见 [验证记录](docs/VALIDATION.md)。

## 仓库结构

`src` 是唯一受 Git 管理的应用与测试源码；`assets` 是离线运行资源；根目录脚本负责构建与测试；`docs` 是交接和验证说明；`samples` 与 `字体对照.html` 提供字体示例。

本机旧工作副本、发布包、缓存、日志、个人配置和临时绘图被 `.gitignore` 排除。后续开发请修改仓库根目录下的源码。

## 依赖与许可

第三方资源及其许可证见 [assets/THIRD-PARTY.txt](assets/THIRD-PARTY.txt)。本仓库公开项目源码；项目自有代码的开源许可证尚未指定。