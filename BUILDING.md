# Building / 构建

## 中文

公开的 C# 源码用于审阅修改器逻辑。Windows x64 且安装 .NET Framework 4.x 的机器可在 PowerShell 中运行 `./Build.ps1`，输出位于 `build/AoE2DE.Plus12.exe`。脚本只调用系统自带的 C# 编译器，不下载依赖。

本地持续开关版还需 `ContinuousInstantModes.cs`。运行 `./Test.ps1` 可编译并执行回归测试，覆盖开关、持续写入、精确还原、玩家归属及界面语言切换。测试只操作测试进程自行分配的内存，不连接游戏进行修改；界面预览位于 `build/instant-mode-ui.png`。

界面已改为版本和连接信息侧栏，不再使用封面素材。默认构建使用系统图标；可用 `./Build.ps1 -IconPath <绝对ICO路径>` 嵌入自己的图标。

### 源码结构

- `AoE2DELatestTrainer.cs`：游戏内存引擎、类型定义、程序入口及版本信息。
- `ContinuousInstantModes.cs`：持续开关和数值还原。
- `UI/TrainerForm.cs`：事件、功能操作、连接检测和窗口生命周期。
- `UI/TrainerForm.Layout.cs`：标题栏、语言选择和通用行布局。
- `UI/TrainerForm.Sections.cs`：侧栏、功能分组和个人 GitHub 入口。
- `UI/SpeedToggleSwitch.cs`、`UI/RoundedApplyButton.cs`、`UI/WindowCaptionButton.cs`：自绘控件。
- `UI/AudioFeedback.cs`：操作提示音；`Localization.cs`：本地化文本。

构建与测试脚本都会编译 `UI/*.cs`。修改功能时请保留界面语言重建、热键同步、正常关闭还原和玩家隔离测试。

GitHub Release 中的 EXE 使用了额外图像素材并经过 Obfuscar 2.2.50 混淆；由于素材没有公开，公开源码无法逐字节复现该 EXE。请根据 README 的 SHA-256 校验发布文件。公开源码不包含原版 FLiNG 程序、游戏文件、诊断内存转储或扫描结果。

## English

The published C# source is provided for inspecting the trainer logic. On Windows x64 with .NET Framework 4.x, run `./Build.ps1` in PowerShell. It writes `build/AoE2DE.Plus12.exe` and only invokes the local C# compiler; it downloads no dependencies.

The local continuous-toggle build also requires `ContinuousInstantModes.cs`. Run `./Test.ps1` for toggle, persistent-write, exact-restore, ownership, hotkey, and language-rebuild tests. Memory tests use allocations owned by the test process, never a game process. The rendered UI preview is written to `build/instant-mode-ui.png`.

The sidebar now displays version and connection information instead of cover art. The default build uses the system icon; use `./Build.ps1 -IconPath <absolute ICO path>` to embed your own icon. Both build scripts compile `UI/*.cs`, separating form behavior, layout, feature sections, custom controls and audio from the game engine.

The GitHub Release EXE includes separate artwork and was obfuscated with Obfuscar 2.2.50. As that artwork is not public, the repository cannot reproduce the Release EXE byte for byte. Verify the distributed file against the SHA-256 in the README. The published source excludes the original FLiNG binary, game files, memory dumps, and scan results.
