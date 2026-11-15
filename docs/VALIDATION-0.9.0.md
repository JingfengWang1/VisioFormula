# 验证记录与运行方法

当前功能版本 **0.9.0**，验证日期 **2026-11-15（Asia/Shanghai）**。本版围绕环境检查、设置中的自动原位更新和 20 MB 缓存管理完成构建、自动化及独立 Visio 文档集成验证，并对最后修改涉及的模块补测。

## 本轮结果

合计 **336 项检查通过**。测试使用隔离配置与缓存；Visio 测试使用自己创建并明确绑定的实例和文档。

| 检查 | 通过数 | 证据 |
| --- | --- | --- |
| 编辑器、快捷输入、撤销、草稿 | 102 | [editor](verification/v0.9.0/editor.json) |
| 原有主题窗口：96 / 168 DPI | 19 + 19 | [96 DPI](verification/v0.9.0/ui-96.json)、[168 DPI](verification/v0.9.0/ui-168.json) |
| 快速/精确预览、取消、会话缓存 | 50 | [realtime](verification/v0.9.0/realtime.json) |
| 环境、磁盘缓存、新设置窗口、首次启动与空闲清理 | 38 | [maintenance](verification/v0.9.0/maintenance.json) |
| 新窗口 96 DPI 与交互 | 7 | [maintenance 96 DPI](verification/v0.9.0/maintenance-96.json) |
| 自动更新与两个独立 Visio 实例 | 24 | [auto update](verification/v0.9.0/auto-update.json) |
| 原生传输、复制粘贴、保存重开、七字体 | 28 | [integration](verification/v0.9.0/integration.json) |
| 批量样式、取消、回滚和一次撤销 | 23 | [batch](verification/v0.9.0/batch.json) |
| 多 Visio 实例真实前台切换与连接恢复 | 20 | [connection](verification/v0.9.0/connection.json) |
| 六种本机 TeX 字体样例与组合公式 | 6 | [fonts](verification/v0.9.0/fonts.json) |

环境检查还实际渲染了全部七种字体；MathJax 最终输出与本机 TeX 路径均通过。Integration 的 skipped 为空，包含系统剪贴板占用时的原生数据传输以及正常复制粘贴。

本次修改也修正了原有实时预览测试的负载敏感断言：原测试硬性要求在 250 ms 内观察到 UI 状态，曾在桌面负载下失败，但已经先显示快速预览。现在验证“快速结果先于精确结果”和“缓存命中复用同一结果”，继续记录延迟，不把 Windows 调度时间当成固定承诺。

最后一轮记录：快速预览 266 ms、MathJax 排版 9 ms、自动精确结果总延迟 2246 ms、缓存结果重显 303 ms，窗口 168 DPI。它们是本机这一轮观察值，不是性能保证或相对旧版的受控性能对比。需要评估速度改动时使用 `test-latency.ps1` 在相同环境比较。

## 新功能重点覆盖

- 旧设置自动补齐新字段；自动更新默认关闭；20 MB / 7 天默认值及持久化。
- 首次启动轻量检查不弹阻塞窗口，状态保存；自动清理关闭时保留旧缓存，空闲开启后清理并保护当前预览。
- 无效手动 TeX 路径、缺少工具、缺少离线资源、不可写设置目录；子进程超时、取消、stdout/stderr 管道排空。
- 七种字体实际渲染；没有本机 TeX 时 MathJax 仍可用；测试中关闭窗口安全取消，保留已完成结果。
- 缓存年龄和 20 MB 容量策略；跨进程持有保护；目录 junction、未知文件、旧版目录、只读文件和引用释放。
- 主窗口不显示自动更新开关；设置保存/取消；环境、缓存和设置窗口在默认/最小尺寸及 96/168 DPI 下检查。
- 原位自动更新保持图形 ID、中心、角度和等比缩放；字号改变应用自然尺寸；同内容不增加撤销记录。
- 在 Visio 撤销后暂停，继续输入不覆盖撤销；明确手动更新后恢复。
- 关闭开关取消待同步写入；改选形状不转移目标；编译中读回新目标使旧请求失效；读回失败暂停旧关联。
- 目标在另一 Visio 实例时更新及 Undo 归属正确；清理与编译并行；外部几何修改、关闭文档、删除已暂停目标、新建、重启均处理。

## 明确未覆盖的项目

- **实际跨窗口鼠标拖放本轮未完成复测**。已构建独立交互验证程序，但桌面工具启动该程序时返回 `Computer Use app approval timed out`，测试窗口没有打开。不能把五次连续 IDataObject API 传输写成物理鼠标落放通过。
- 本机实测 TeX Live 2024、Visio Professional 2021 x64；MiKTeX 的查找和禁用自动安装分支尚未在实际 MiKTeX 安装上验证。
- 96/168 DPI 检查不等同于不同 DPI 显示器间拖动窗口的人工验证；其他 Windows/Office 组合未在本轮穷举。
- 历史基线独立保存在 [0.8.2 验证记录](VALIDATION-0.8.2.md)，不与本轮计数混用。

## 发布包核验

免安装包包含 12 个文件（程序、config、完整离线 assets、使用说明和文件校验清单）。已逐文件比对运行资源与测试构建，并对 ZIP 中每个文件重新计算 SHA-256，全部一致。

- 程序文件版本：0.9.0.0。
- 解压后合计：2,842,004 字节（约 2.71 MiB）。
- ZIP：941,597 字节（约 0.90 MiB）。
- ZIP SHA-256：`467cf4cf1cf90aff55bdd2c46cd80a2011e2fca42c2c97e0674ac7a440a2f1a4`。
- 包中不含个人设置、草稿、编译缓存、测试绘图或本机路径配置。

## 运行方法

先运行 `build.ps1`。脚本默认引用仓库根目录 EXE；所有测试脚本应把 `VISIOFORMULA_CACHE` 指向隔离目录，不清理真实用户缓存。

```powershell
.\build.ps1
.\test-maintenance.ps1
.\test-editor.ps1
.\test-theme.ps1
.\test-realtime.ps1
.\run-tests.ps1
.\test-batch.ps1
.\test-fonts.ps1
.\test-connection.ps1
```

`test-maintenance.ps1 -WithoutVisio` 可运行不依赖 Visio 写入的环境/缓存/UI 部分；使用该参数时不能声称自动更新集成部分已通过。连接测试按 ACTIVATE 提示真实切换两个自建窗口；仅 COM Window.Activate 不足以代表用户前台切换。

需要重做实际鼠标检查时：

```powershell
.\build-interactive-tests.ps1
.\InteractiveTests.exe
```

该程序创建独立设置、缓存与一个专用 Visio 文档；连续拖入两次，检查其 `verification/interactive-*/observed.json` 中两个形状均有公式源码、尺寸一致。还应人工检查设置开关、自动更新及撤销。关闭公式测试窗口会保存测试绘图并关闭其拥有的 Visio 实例。不要把真实用户文档用于替代这些测试。

测试运行日志、截图、诊断中的本机路径和临时绘图保留在被忽略的 verification 目录。公开仓库只保留经过筛选的成功 JSON 报告。
