# 验证记录与运行方法

当前技术原型 **0.10.0-preview.1**，验证日期 **2026-11-17（Asia/Shanghai）**。稳定版本仍为 0.9.1。

本轮按用户要求未使用 computer use。通过代码、WinForms 控件及 Office COM 接口检查 **357 项**；PowerPoint 实际鼠标拖放、Ctrl+Z/Ctrl+Y 和视觉验收仍由用户执行，不能据此宣布核心人工验收已经通过。

## 当前自动检查

| 检查 | 通过数 | 证据 |
| --- | --- | --- |
| 编辑器、快捷输入、撤销和草稿 | 102 | [editor](verification/v0.10.0-preview.1/editor.json) |
| 主题与菜单，96 / 168 DPI | 19 + 19 | [96](verification/v0.10.0-preview.1/ui-96.json)、[168](verification/v0.10.0-preview.1/ui-168.json) |
| 快速/精确预览、取消和会话缓存 | 50 | [realtime](verification/v0.10.0-preview.1/realtime.json) |
| Visio 原生传输、保存重开、原位更新和撤销/重做 | 28 | [integration](verification/v0.10.0-preview.1/integration.json) |
| Visio 批量样式、事务和撤销 | 23 | [batch](verification/v0.10.0-preview.1/batch.json) |
| 环境、缓存与设置窗口 | 38 + 7 | [maintenance](verification/v0.10.0-preview.1/maintenance.json)、[96](verification/v0.10.0-preview.1/maintenance-96.json) |
| Visio 自动更新与目标归属 | 24 | [auto-update](verification/v0.10.0-preview.1/auto-update.json) |
| PowerPoint 新目的端、元数据、原生复制与保存/更新接口 | 47 | [powerpoint](verification/v0.10.0-preview.1/powerpoint.json) |

测试使用隔离配置和缓存。Office 操作仅针对自建测试文档；PowerPoint 数据测试使用隐藏演示文稿，没有通过鼠标、键盘或 computer use 操作桌面。原生复制检查会使用系统剪贴板。

PowerPoint 接口检查覆盖：

- 目的端切换解除旧目标、保留输入；偏好持久化；PowerPoint 不创建自动更新绑定且禁用批量入口，切回 Visio 恢复功能。
- 大小写安全编码、长 Unicode 源码分块、校验和损坏拒绝。
- 三次获取真实原生拖放数据提供器，并通过原生粘贴确认源码仍在；暂存文档关闭恢复、复制后关闭暂存仍可粘贴。
- 七种字体的实际 SVG 嵌入与保存重开；MathJax 来自实际离线预览引擎。
- `.pptx` 内包含 SVG media、svgBlip 引用和图形标签；没有对本机缓存的外部关系。
- 清理缓存后重开仍可读回；更新保留 Shape ID、中心、角度、缩放和层级，基本动画对象仍指向原 Shape ID。
- 显式字号变化、非等比拉伸拒绝、无效 SVG 写入前拒绝、再次保存重开、目标删除保护。

新增测试曾因测试宿主仅循环 DoEvents，导致异步浏览器 COM 调用线程不正确。已改用与正式程序相同的 STA + WinForms Application.Run；未因此更改生产渲染逻辑或放宽断言。最后成功的 PowerPoint 报告独立保存。

## 等待用户验收

[PowerPoint 验证清单](POWERPOINT_CHECKLIST.md) 的 P1–P7 是核心验收：直接拖入、连续拖入、保存重开读回、原位更新、一次撤销、重做、再次保存。

目前未执行这些物理鼠标/键盘步骤。PowerPoint 的一次撤销分组、真实播放和跨窗口体验必须由用户确认；API 获得 IDataObject 或粘贴成功，不等同于实际拖放通过。异常发生在更新中途时的恢复分支尚未完成故障注入验证。

未覆盖：组合内公式、多个独立 PowerPoint 进程、PowerPoint 网页版/macOS、实际 MiKTeX 环境、不同 DPI 显示器间的真实拖动。本轮没有重跑需要实际前台切换的 Visio 连接测试。

PowerPoint 自动更新和批量样式按约定留到验收后再开发。当前实测 Office 16.0 桌面接口与 TeX Live；所有历史结果与本轮结果分开记录。

## 运行方法

```powershell
.\build.ps1
.\test-powerpoint.ps1
.\test-editor.ps1
.\test-theme.ps1
.\test-realtime.ps1
.\run-tests.ps1
.\test-batch.ps1
.\test-maintenance.ps1
```

PowerPoint 测试脚本需要本机桌面 PowerPoint 和当前七字体所需的 TeX 宏包。默认新建并清理自己拥有的演示文稿；不得改为在用户现有文档上运行写入测试。

## 历史记录

- [0.9.1](VALIDATION-0.9.1.md)
- [0.9.0](VALIDATION-0.9.0.md)
- [0.8.2](VALIDATION-0.8.2.md)
## 发布包核验

包内包含程序、config、完整离线资源、使用说明、PowerPoint 验证清单和 SHA256SUMS.txt，共 13 个文件。已逐文件核对已测构建与 ZIP 内容，程序文件版本 0.10.0.0。

ZIP SHA-256：`6ecd077fca34e02a9c2bc1436ce0bf77ea7051e8a750087ef13d7ac1ddb3c450`。