# 验证记录与运行方法

本文件区分本次仓库整理后重新运行的结果和此前 0.8.2 发布时保留的结果。日期：2026-11-14。应用源码保持 0.8.2 行为，本次主要整理规范开发入口与交接文档。

## 本次从仓库根目录重新运行

在根目录 `build.ps1` 构建后运行了三个测试脚本，合计 **190 项检查通过**，均未连接或修改用户的 Visio 绘图。

| 命令 | 结果 | 证据 |
| --- | --- | --- |
| `./build.ps1` | x64 应用构建成功 | 构建退出码 0 |
| `./test-editor.ps1` | 102 项通过 | [editor](verification/repository-editor-results.json) |
| `./test-theme.ps1` | 96 DPI 19 项、168 DPI 19 项通过 | [96 DPI](verification/repository-theme-96.json)、[168 DPI](verification/repository-theme-native.json) |
| `./test-realtime.ps1` | 50 项通过 | [realtime](verification/repository-realtime-results.json) |

覆盖了快捷展开、占位导航、撤销/重做、IME、纯文本粘贴、高亮与选区、草稿与备份恢复、设置窗口、多种窗口尺寸、工具栏对齐、预览缩放、MathJax 最终 SVG、本机 TeX 自动精确渲染、取消、缓存和旧结果失效。

本机一次运行的实时预览测量如下，不是跨电脑性能保证：

| 指标 | 观察值 |
| --- | --- |
| 热启动快速预览 | 127 ms |
| 测量到的 MathJax 排版阶段 | 9 ms |
| 自动精确结果总延迟 | 1244 ms |
| 返回已缓存公式总延迟 | 29 ms |
| 窗口 DPI | 168 |

测试使用自己的配置路径，结果和截图写入根目录 `verification/`；该运行目录被 Git 忽略。提交的 JSON 是筛选后的结果记录，不包含真实用户草稿、缓存或临时绘图。

## 保留的 0.8.2 Visio 验证基线

以下是此前 0.8.2 发布验证保留的 **71 项**结果，不是本次文档整理期间重新执行的 Visio 测试：

| 测试 | 通过 | 主要覆盖 | 证据 |
| --- | --- | --- | --- |
| Connection | 20 | 两个 Visio 实例间重新连接、目标所属实例的撤销、暂存文档切换、关闭旧实例、保存重开 | [connection](verification/connection-results.json) |
| Batch | 23 | 组内/选中/整页范围、统一字体字号、单次撤销重做、取消、范围变化、损坏 SVG 预检查、提交失败回滚 | [batch](verification/batch-results.json) |
| Integration | 28 | 连续原生数据传输、剪贴板占用、复制粘贴、源码随文档保存、更新尺寸与位置、七种字体、重复对象独立性 | [integration](verification/integration-results.json) |

Integration 报告的 `skipped` 为空。其 `nativeMouseDrag` 明确说明本轮使用 IDataObject API 传输与手势过滤验证，未重新做实际鼠标落放；不能把它写成“物理鼠标连续拖拽实测”。更早的交互迭代中，用户反馈连续两次拖拽通过。

0.8.1 的编辑器、主题和实时预览也曾通过相同的 190 项检查；本文件以上方“本次重新运行”的独立报告为当前仓库整理的证据，避免混用版本来源。

## 如何选择测试

先在仓库根目录运行 `./build.ps1`，再运行需要的测试。脚本通常引用根目录的 `VisioFormula.exe`，不是自动引用最新 `dist` 目录。默认运行全部测试并非每次修改的必要条件。

| 命令 | 依赖与影响 |
| --- | --- |
| `./test-editor.ps1` | .NET/WinForms；使用隔离设置和测试窗口；不要求 Visio |
| `./test-theme.ps1` | WinForms 桌面环境；原生 DPI 和 96 DPI 两次；不要求 Visio |
| `./test-realtime.ps1` | 离线 MSHTML/MathJax、本机 TeX；不要求 Visio；会更新示例 MathJax SVG |
| `./test-fonts.ps1` | 本机 TeX 与六种字体宏包；写入字体样例及验证结果 |
| `./test-latency.ps1` | 离线预览与 TeX；测量特定样例，适合比较延迟改动 |
| `./run-tests.ps1` | 桌面 Visio、本机 TeX、剪贴板；使用测试拥有的实例和文档 |
| `./test-batch.ps1` | 桌面 Visio、TeX 和离线 MathJax；测试事务、失败与 UI 调度 |
| `./test-connection.ps1` | 两个独立 Visio 测试实例；按 `ACTIVATE:` 提示真实切换前台窗口 |

示例：

```powershell
.\build.ps1
.\test-editor.ps1
.\test-theme.ps1
.\test-realtime.ps1
```

与 Visio 相关的测试应在可交互桌面中进行。只有明确创建并持有的测试实例才可以自动关闭；不要用当前用户文档代替隔离测试。复制测试会改变系统剪贴板。`--without-clipboard` 是 Integration 测试 EXE 的可选降级参数，使用后必须明确记录跳过项，不应当作完整通过。

跨实例连接测试会尝试 COM 激活并等待活动对象注册，某些环境需要按 ACTIVATE 提示实际点击相应测试窗口。若超时，先检查实例和前台切换，再判断业务逻辑；不要用静默跳过掩盖问题。

## 覆盖边界

- 本次未重新执行上述 71 项 Visio 测试；应用源码与保留的 0.8.2 开发副本逐文件核对，不更改其行为。
- 96/168 DPI 布局检查不等同于不同 DPI 显示器之间拖动窗口的完整人工验证。
- API 数据传输不等同于真实鼠标拖放；新改拖动过滤器或 OLE 逻辑时应补做连续两次以上物理拖拽。
- 不把当前电脑成功当成所有 Windows、Office 位数或 TeX 发行版都已验证。
- 尚无 CI 工作流。后续增加 CI 时，纯编辑逻辑和含桌面/Visio 的测试应区别安排。

结果变更时保留日期、版本、命令和真实跳过项。请勿把包含用户路径、文档名或业务源码的完整错误日志直接发布到公开仓库。
