# Visio Formula

一个面向 Windows 桌面 Visio 的轻量 LaTeX 公式工具。上方编辑源码、下方实时预览，公式可直接拖入 Visio，并随绘图保存源码，之后读回和原位更新。

当前功能版本：**0.8.2**。采用 C# / WinForms / .NET Framework 4.8，x64 构建。所有公式渲染均在本机进行。

**后续 AI 或开发者请先阅读 [项目交接与实现说明](docs/AI_HANDOFF.md)，再阅读 [验证记录](docs/VALIDATION.md)。**

## 功能

- 输入时使用离线 MathJax 快速预览；停止输入后自动生成本机 LaTeX 精确 SVG。
- 从预览区直接拖入 Visio，支持复制粘贴、导出 SVG。
- 源码、字体、字号保存在 Visio 图形的数据单元格中；保存并重开文档后仍可编辑。
- 在 Visio 中选中公式，在工具中点击“编辑选中公式”；修改后原位更新，保留外层图形 ID、中心位置、旋转与已有等比缩放。
- 七种字体：Latin Modern、Times / NewTX、STIX Two、Palatino / NewPX、Libertinus、Sans / NewTXsf、MathJax / TeX。
- 参考 Quick LaTeX 使用习惯的快捷输入、44 条默认缩写规则、自定义规则导入导出、占位跳转、分式与上下标自动转换。
- 语法高亮、独立撤销/重做、输入法组合输入处理。
- 自动恢复当前草稿、新建公式、最近 20 条历史草稿。
- 批量统一当前选中范围或整页公式的字体/字号，支持组内公式、取消、失败回滚与一次撤销。
- 紧凑的上下布局、统一的设置和菜单样式、DPI 适配、预览缩放与完整适配。

## 运行条件

| 用途 | 条件 |
| --- | --- |
| 运行工具 | 64 位 Windows 10/11，.NET Framework 4.8 或更新版本 |
| MathJax 预览与 SVG 导出 | 已随程序附带离线资源，无须另装 LaTeX |
| 六种本机 LaTeX 字体 | `latex.exe`、`dvisvgm.exe` 及相关宏包；当前主要验证环境为 TeX Live 2024 |
| 拖入、复制、读回与更新 | Microsoft Visio 桌面版；当前实测 Visio Professional 2021 x64 |

这是免安装的本地工具，不是 Visio Web 插件；无需安装 Visio 加载项或启用宏。保持程序与 Visio 使用相同的普通用户权限。

## 从源码构建

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

按顺序查找：程序旁的 `tex-bin.txt`、系统 `PATH`、`D:\texlive` 与 `C:\texlive` 的版本子目录中的 `bin\windows`。

非标准安装路径可在程序旁创建 `tex-bin.txt`，只填工具所在目录，例如：

```text
E:\TeXLive\2026\bin\windows
```

此文件是本机配置，Git 默认忽略。程序不会自动安装 TeX 或宏包。

## 草稿和设置

保存在 `%LOCALAPPDATA%\VisioFormula`：

| 文件 | 内容 |
| --- | --- |
| `settings.json` | 字体、字号、预览缩放 |
| `settings.quick-input.json` | 快捷输入开关与自定义规则 |
| `settings.drafts.json` | 当前草稿、光标及最近 20 条历史 |

当前草稿在编辑停顿约 450 ms 后保存，关闭时再保存。历史记录在新建、读回 Visio 公式、恢复另一草稿前归档当前非空内容；相同源码、字体、字号会去重。草稿恢复不恢复 Visio 图形的 COM 绑定。

复制程序文件夹即可迁移程序；要迁移个人设置和草稿，需要另外复制上述用户配置目录。图中公式源码随 `.vsdx` 保存。

## 测试

先构建，然后按改动范围运行：

```powershell
.\test-editor.ps1
.\test-theme.ps1
.\test-realtime.ps1
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