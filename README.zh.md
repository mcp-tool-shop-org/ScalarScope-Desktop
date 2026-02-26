<p align="center">
  <a href="README.md">English</a> | <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  
            <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/ScalarScope-Desktop/readme.png"
           alt="ScalarScope" width="400">
</p>

# ScalarScope-Desktop

> [MCP Tool Shop](https://mcptoolshop.com) 的一部分

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/ScalarScope-Desktop/actions/workflows/build.yml"><img src="https://github.com/mcp-tool-shop-org/ScalarScope-Desktop/actions/workflows/build.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square" alt="License: MIT"></a>
  <a href="https://mcp-tool-shop-org.github.io/ScalarScope-Desktop/"><img src="https://img.shields.io/badge/Landing_Page-live-blue?style=flat-square" alt="Landing Page"></a>
  <a href="https://apps.microsoft.com/detail/9P3HT1PHBKQK">
            <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/ScalarScope-Desktop/readme.png"
           alt="Microsoft Store"></a>
  <a href="https://www.nuget.org/packages/VortexKit">
            <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/ScalarScope-Desktop/readme.png"
           alt="NuGet: VortexKit"></a>
</p>

**ASPIRE Scalar Vortex Visualizer：一个 .NET MAUI 桌面应用程序，用于以科学严谨的方式比较机器学习推理运行结果。**

---

## 为什么选择 ScalarScope？

大多数机器学习团队会简单地查看日志。ScalarScope 通过结构化、可重复的比较来替代这种方式。

- **同类比较**：并排加载两个推理结果，并查看具体的变化。
- **规范的差异分析**：五种差异类型（ΔTc、ΔO、ΔF、ΔĀ、ΔTd）仅在差异在统计上具有意义时才会触发。
- **运行时预设**：TFRT 预设会自动抑制不相关的指标，让您专注于 TensorFlow-TRT 工作负载中重要的数据。
- **可重现的包**：导出包含 SHA-256 完整性校验、冻结的差异和完整元数据的 `.scbundle` 归档文件。
- **审查模式**：无需重新计算即可打开一个包；结果经过密码学验证，而不是重新计算。
- **注重隐私**：零数据收集，零分析，所有数据都保存在本地，除非您明确导出。

---

## NuGet 包

| 包名 | 版本 | 描述 |
| --------- | --------- | ------------- |
| [VortexKit](https://www.nuget.org/packages/VortexKit) | [![NuGet](https://img.shields.io/nuget/v/VortexKit)](https://www.nuget.org/packages/VortexKit) | 一个用于可视化训练动态的框架，具有时间同步的播放、动画 SkiaSharp 画布、比较视图、批注叠加、SVG/PNG 导出以及一种语义颜色系统。基于 SkiaSharp + MAUI 构建。 |

```bash
dotnet add package VortexKit
```

---

## 快速开始

### 从 Microsoft Store

1. 从 [Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK) (Store ID: `9P3HT1PHBKQK`) 安装 **ScalarScope**。
2. 点击 **比较两个运行结果**。
3. 加载优化前的 TFRT 跟踪数据（基准）。
4. 加载优化后的 TFRT 跟踪数据。
5. 在 **比较** 选项卡中查看差异。
6. 导出 `.scbundle` 文件以进行可重复的共享。

### 在您自己的应用程序中使用 VortexKit

```csharp
using VortexKit.Core;

// 1. Create a shared playback controller (0.0 -> 1.0 timeline)
var player = new PlaybackController { Duration = 10.0, Loop = true };

// 2. Bind multiple animated canvases to the same controller
player.TimeChanged += () =>
{
    trajectoryCanvas.CurrentTime = player.Time;
    eigenCanvas.CurrentTime      = player.Time;
    scalarsCanvas.CurrentTime    = player.Time;
};

// 3. Subclass AnimatedCanvas for custom rendering
public class MyTrajectoryCanvas : AnimatedCanvas
{
    protected override void OnRender(SKCanvas canvas, SKImageInfo info, double time)
    {
        // Your SkiaSharp rendering at the current time position
    }
}

// 4. Export a side-by-side comparison as PNG
var exporter = new ExportService();
await exporter.ExportComparisonAsync(
    leftRender, rightRender, time: 0.5,
    outputPath: "comparison.png",
    new ComparisonExportOptions
    {
        Width = 1920, Height = 1080,
        LeftLabel = "Baseline", RightLabel = "Optimized",
        ShowLabels = true
    });

// 5. Export as layered SVG (Inkscape-compatible)
var svgExporter = new SvgExportService();
await svgExporter.ExportSvgAsync(svgData, "trajectory.svg",
    new SvgExportOptions
    {
        Palette = SvgColorPalette.Publication,
        UseCatmullRomSplines = true,
        EnableGlow = false
    });
```

---

## 特性

### 差异分析 — 五种规范的差异类型

每次比较都会产生一组规范的差异。 只有当差异在统计上具有意义时，才会触发每个差异；不相关的差异会自动被抑制。

| Delta | 全称 | 测量内容 | 触发条件 |
| ------- | ----------- | ------------------ | ------------ |
| **ΔTc** | 收敛时间 | 达到稳定延迟所需的步骤 | 在不同的步骤达到稳态（≥3 步分离） |
| **ΔO** | 输出变化率 | 振荡/运行时不稳定 | 阈值以上区域得分超出噪声范围 |
| **ΔF** | 失败率 | 异常频率 | 运行之间的失败频率或类型不同 |
| **ΔĀ** | 平均延迟 | 平均指标值 | 平均值有显著差异（在 TFRT 预设中被抑制） |
| **ΔTd** | 总时长 | 实时时间/结构化出现 | 时长或出现时间不同（在 TFRT 预设中被抑制） |

### 运行时预设 — TFRT

内置的 **TensorFlow-TRT** 预设 (`tensorflowrt-runtime-v1`) 映射了与推理相关的信号（延迟、吞吐量、内存、CPU/GPU 负载），并抑制了仅适用于训练的差异（ΔĀ、ΔTd），这些差异对于推理比较没有意义。 警报会提示当预热时间超过运行时间的 50% 或仅有聚合统计数据可用时。

### 可重现的包

将结果导出为 `.scbundle` 归档文件（ComparisonBundle v1.0.0）：

- **`manifest.json`** — 捆绑包的元数据，应用版本，比较标签，对齐模式
- **`repro/repro.json`** — 输入指纹，预设哈希值，确定性种子，环境信息
- **`findings/deltas.json`** — 带有置信度评分、锚点和触发类型的规范差异
- **`findings/why.json`** — 人类可读的解释，安全措施，参数提示
- **`findings/summary.md`** — 自动生成的 Markdown 摘要
- **完整性** — 每个文件使用 SHA-256 哈希；捆绑包级别的哈希用于检测篡改

### 审查模式

无需重新计算即可打开任何 `.scbundle` 文件。审查模式验证完整性，显示冻结的差异，并显示一个审查模式横幅，以便您知道结果是经过验证的，而不是重新计算的。

### VortexKit 可视化框架

VortexKit 是提取的可视化引擎，以独立的 NuGet 包形式发布：

| 组件 | 功能 |
| ----------- | ------------- |
| `PlaybackController` | 共享 0→1 时间轴，带有播放/暂停/单步/循环功能，速度预设（0.25x—4x），约 60 fps 的刷新率 |
| `AnimatedCanvas` | 抽象的 `SKCanvasView` 基础类，具有与时间同步的重绘功能，网格绘制，触摸/拖动事件，坐标辅助函数 |
| `ITimeSeries<T>` / `TimeSeries<T>` | 泛型时间序列，具有索引↔时间映射和轨迹枚举功能 |
| `ExportService` | 单帧 PNG，帧序列（带有 ffmpeg 提示），以及并排比较导出功能 |
| `SvgExportService` | 完整的矢量 SVG 导出，带有 Inkscape 层，Catmull-Rom 样条线，热图，矢量场，以及四种颜色方案（默认，浅色，高对比度，出版） |
| `IAnnotation` | 带有理论基础和优先级的类型化注释（阶段，警告，洞察，失败，自定义） |
| `VortexColors` | 语义颜色方案 — 背景层，强调语义，严重程度编码，特征值方案，以及插值/渐变辅助函数 |

---

## 安装

### Microsoft Store（推荐）

**商店 ID:** `9P3HT1PHBKQK`

[从 Microsoft Store 获取](https://apps.microsoft.com/detail/9P3HT1PHBKQK)

需要 Windows 10（版本 17763）或更高版本。

### 从源代码

```bash
# Prerequisites:
#   .NET 9.0 SDK (global.json pins 9.0.100)
#   Visual Studio 2022 with MAUI workload, or:
#     dotnet workload install maui-windows

git clone https://github.com/mcp-tool-shop-org/ScalarScope-Desktop.git
cd ScalarScope-Desktop
dotnet restore
dotnet build

# Run the desktop app
dotnet run --project src/ScalarScope
```

### NuGet（仅限库）

```bash
dotnet add package VortexKit
```

---

## 项目结构

```
ScalarScope-Desktop/
├── src/
│   ├── ScalarScope/                    # .NET MAUI desktop app
│   │   ├── Models/                     # GeometryRun, InsightEvent
│   │   ├── ViewModels/                 # Welcome, Comparison, Export, Settings, TrajectoryPlayer, VortexSession
│   │   ├── Views/                      # XAML pages + 30+ custom controls
│   │   │   ├── WelcomePage.xaml        # First-60-seconds onboarding
│   │   │   ├── ComparisonPage.xaml     # Side-by-side delta comparison
│   │   │   ├── TrajectoryPage.xaml     # Animated trajectory playback
│   │   │   ├── GeometryPage.xaml       # Eigenvalue spectrum view
│   │   │   └── Controls/              # DeltaZone, BundleExportPanel, PlaybackControl, etc.
│   │   ├── Services/
│   │   │   ├── Connectors/            # RunTraceComparer, TfrtRuntimePreset, validation
│   │   │   ├── Bundles/               # BundleBuilder, BundleExporter, integrity, schemas
│   │   │   ├── Evidence/              # Comparison evidence reports, detector diagnostics
│   │   │   ├── Plugins/               # PluginManager
│   │   │   ├── CanonicalDeltaService.cs
│   │   │   ├── DeltaTypes.cs          # 5 canonical deltas + detector configs
│   │   │   ├── DeterminismService.cs  # Reproducible seed management
│   │   │   ├── FlowFieldService.cs    # Vector field computation
│   │   │   └── ...                    # 40+ service files
│   │   └── Resources/
│   │       ├── Styles/DesignSystem.xaml # Unified visual grammar
│   │       └── Raw/Samples/            # Built-in example traces
│   │
│   └── VortexKit/                      # Standalone NuGet library
│       ├── Core/
│       │   ├── AnimatedCanvas.cs       # Time-synced SkiaSharp canvas base
│       │   ├── PlaybackController.cs   # Shared playback timeline
│       │   ├── ITimeSeries.cs          # Generic time-series interface
│       │   ├── ExportService.cs        # PNG frame/sequence export
│       │   └── SvgExportService.cs     # Layered SVG export
│       ├── Annotations/
│       │   └── IAnnotation.cs          # Typed annotation system
│       └── Theme/
│           └── VortexColors.cs         # Semantic color palette
│
├── tests/
│   ├── ScalarScope.FixtureTests/       # Golden-file fixture tests
│   ├── ScalarScope.DeterminismTests/   # Reproducibility verification
│   ├── ScalarScope.SoakTests/          # Long-running stability tests
│   └── Fixtures/                       # Shared test data
│
├── docs/                               # Design docs, results, limitations
├── .github/workflows/
│   ├── build.yml                       # CI: restore, build, format check, pack, artifacts
│   ├── publish.yml                     # NuGet publish
│   └── release.yml                     # GitHub Release + Store submission
├── global.json                         # .NET SDK 9.0.100
├── ScalarScope.sln                     # Solution file
├── CHANGELOG.md                        # Keep-a-Changelog format
├── PRIVACY.md                          # Privacy policy (no telemetry)
├── SECURITY.md                         # Security policy
└── STORE_LISTING.md                    # Microsoft Store listing copy
```

---

## 测试

```bash
# Run all tests
dotnet test

# Fixture smoke tests only
dotnet test --filter Category=FixtureSmoke

# Determinism tests (verifies reproducible deltas)
dotnet test --filter Category=Determinism

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## 相关

- [ScalarScope (Python)](https://github.com/mcp-tool-shop-org/ScalarScope) — 核心训练框架
- [RESULTS_AND_LIMITATIONS.md](docs/RESULTS_AND_LIMITATIONS.md) — 完整的实验结果
- [CHANGELOG.md](CHANGELOG.md) — 发布历史
- [PRIVACY.md](PRIVACY.md) — 隐私政策
- [ROADMAP.md](ROADMAP.md) — 计划中的功能

---

## 许可证

[MIT](LICENSE) — 版权所有 (c) 2025-2026 ScalarScope Project (mcp-tool-shop-org)
