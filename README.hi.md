<p align="center">
  <a href="README.md">English</a> | <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  
            <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/ScalarScope-Desktop/readme.png"
           alt="ScalarScope" width="400">
</p>

# ScalarScope-Desktop

> [MCP Tool Shop](https://mcptoolshop.com) का एक हिस्सा

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

**ASPIRE Scalar Vortex Visualizer — एक .NET MAUI डेस्कटॉप एप्लिकेशन, जो वैज्ञानिक सटीकता के साथ मशीन लर्निंग (एमएल) के निष्पादन परिणामों की तुलना करने के लिए बनाया गया है।**

---

## ScalarScope क्यों?

ज्यादातर एमएल टीमों द्वारा लॉग की जांच की जाती है। ScalarScope इसे संरचित और पुनरुत्पादित तुलना से बदल देता है।

- **समान-से-समान तुलना:** दो निष्पादन परिणामों को एक साथ लोड करें और देखें कि वास्तव में क्या बदला है।
- **मानक डेल्टा विश्लेषण:** पांच प्रकार के डेल्टा (ΔTc, ΔO, ΔF, ΔĀ, ΔTd) केवल तभी सक्रिय होते हैं जब अंतर सांख्यिकीय रूप से महत्वपूर्ण होते हैं।
- **रनटाइम प्रीसेट:** TFRT प्रीसेट अप्रासंगिक मेट्रिक्स को स्वचालित रूप से दबा देता है, ताकि आप TensorFlow-TRT वर्कलोड के लिए महत्वपूर्ण चीजों पर ध्यान केंद्रित कर सकें।
- **पुनरुत्पादित बंडल:** `.scbundle` अभिलेखागार को SHA-256 अखंडता, स्थिर डेल्टा और पूर्ण प्रामाणिकता मेटाडेटा के साथ निर्यात करें।
- **समीक्षा मोड:** किसी बंडल को फिर से गणना किए बिना खोलें; परिणाम क्रिप्टोग्राफिक रूप से सत्यापित होते हैं, न कि फिर से प्राप्त किए जाते हैं।
- **गोपनीयता पहले:** कोई टेलीमेट्री नहीं, कोई विश्लेषण नहीं, सभी डेटा स्थानीय रहते हैं जब तक कि आप स्पष्ट रूप से निर्यात न करें।

---

## NuGet पैकेज

| पैकेज | संस्करण | विवरण |
| --------- | --------- | ------------- |
| [VortexKit](https://www.nuget.org/packages/VortexKit) | [![NuGet](https://img.shields.io/nuget/v/VortexKit)](https://www.nuget.org/packages/VortexKit) | प्रशिक्षण गतिशीलता के लिए पुन: प्रयोज्य विज़ुअलाइज़ेशन ढांचा - समय-सिंक्रनाइज़ प्लेबैक, एनिमेटेड SkiaSharp कैनवस, तुलना दृश्य, एनोटेशन ओवरले, SVG/PNG निर्यात और एक सिमेंटिक रंग प्रणाली। SkiaSharp + MAUI पर आधारित। |

```bash
dotnet add package VortexKit
```

---

## शुरुआत कैसे करें

### Microsoft Store से

1. [Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK) से **ScalarScope** स्थापित करें (स्टोर आईडी: `9P3HT1PHBKQK`)
2. **दो रन की तुलना करें** पर क्लिक करें
3. बेसलाइन TFRT ट्रेस लोड करें (अनुकूलन से पहले)
4. अनुकूलित TFRT ट्रेस लोड करें (अनुकूलन के बाद)
5. **तुलना** टैब में डेल्टा की समीक्षा करें
6. पुनरुत्पादित साझाकरण के लिए एक `.scbundle` निर्यात करें

### अपने स्वयं के ऐप में VortexKit का उपयोग करना

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

## विशेषताएं

### डेल्टा विश्लेषण — पांच मानक डेल्टा प्रकार

प्रत्येक तुलना में मानक डेल्टा का एक सेट उत्पन्न होता है। प्रत्येक डेल्टा केवल तभी सक्रिय होता है जब अंतर सांख्यिकीय रूप से महत्वपूर्ण होता है; अप्रासंगिक डेल्टा स्वचालित रूप से दबा दिए जाते हैं।

| Delta | पूरा नाम | यह क्या मापता है | कब सक्रिय होता है |
| ------- | ----------- | ------------------ | ------------ |
| **ΔTc** | अभिसरण समय | स्थिर विलंबता प्राप्त करने के चरण | स्थिर अवस्था अलग-अलग चरणों पर प्राप्त होती है (≥3-चरण अलगाव) |
| **ΔO** | आउटपुट विचलन | दोलन / रनटाइम अस्थिरता | एरिया-एबोव-थ्रेशोल्ड स्कोर शोर स्तर से अधिक भिन्न होता है |
| **ΔF** | विफलता दर | विसंगति आवृत्ति | विफलता आवृत्ति या प्रकार दो रन के बीच भिन्न होता है |
| **ΔĀ** | औसत विलंबता | माध्य मीट्रिक मान | माध्य सार्थक रूप से भिन्न होता है (TFRT प्रीसेट में दबा दिया गया) |
| **ΔTd** | कुल अवधि | वास्तविक समय / संरचनात्मक उद्भव | अवधि या प्रभुत्व की शुरुआत भिन्न होती है (TFRT प्रीसेट में दबा दिया गया) |

### रनटाइम प्रीसेट — TFRT

अंतर्निहित **TensorFlow-TRT** प्रीसेट (`tensorflowrt-runtime-v1`) निष्पादन-विशिष्ट संकेतों (विलंबता, थ्रूपुट, मेमोरी, CPU/GPU लोड) को मैप करता है और प्रशिक्षण-केवल डेल्टा (ΔĀ, ΔTd) को दबा देता है, जिनका अर्थ निष्पादन तुलना के लिए नहीं है। गार्डरेल चेतावनी देते हैं जब वार्मअप रन के 50% से अधिक होता है या जब केवल एकत्रित आँकड़े उपलब्ध होते हैं।

### पुनरुत्पादित बंडल

परिणामों को `.scbundle` अभिलेखागार (ComparisonBundle v1.0.0) के रूप में निर्यात करें:

- **`manifest.json`** — बंडल मेटाडेटा, ऐप संस्करण, तुलना लेबल, संरेखण मोड
- **`repro/repro.json`** — इनपुट फिंगरप्रिंट, पूर्वनिर्धारित हैश, नियति बीज, पर्यावरण जानकारी
- **`findings/deltas.json`** — आत्मविश्वास स्कोर, एंकर और ट्रिगर प्रकार के साथ मानक बदलाव
- **`findings/why.json`** — मानव-पठनीय स्पष्टीकरण, सुरक्षा उपाय, पैरामीटर चिप्स
- **`findings/summary.md`** — स्वचालित रूप से उत्पन्न मार्कडाउन सारांश
- **अखंडता** — प्रत्येक फ़ाइल SHA-256 के साथ हैश की गई है; छेड़छाड़ का पता लगाने के लिए बंडल-स्तरीय हैश

### समीक्षा मोड

किसी भी `.scbundle` फ़ाइल को फिर से गणना किए बिना खोलें। समीक्षा मोड अखंडता की जांच करता है, स्थिर बदलाव प्रदर्शित करता है, और एक समीक्षा-मोड बैनर दिखाता है ताकि आपको पता चल सके कि परिणाम सत्यापित हैं, न कि फिर से प्राप्त किए गए।

### वॉर्टेक्सकिट विज़ुअलाइज़ेशन फ्रेमवर्क

वॉर्टेक्सकिट एक निकाला गया विज़ुअलाइज़ेशन इंजन है, जिसे एक स्टैंडअलोन NuGet पैकेज के रूप में प्रकाशित किया गया है:

| घटक | यह क्या करता है |
| ----------- | ------------- |
| `PlaybackController` | प्ले/पॉज़/स्टेप/लूप के साथ साझा 0→1 टाइमलाइन, गति प्रीसेट (0.25x—4x), लगभग 60 fps टिक |
| `AnimatedCanvas` | `SKCanvasView` का सार बेस, समय-सिंक्रनाइज़ अमान्यकरण, ग्रिड ड्राइंग, टच/ड्रैग इवेंट, निर्देशांक सहायक |
| `ITimeSeries<T>` / `TimeSeries<T>` | इंडेक्स↔टाइम मैपिंग और ट्रेल एन्यूमरेशन के साथ जेनेरिक टाइम-सीरीज |
| `ExportService` | सिंगल-फ्रेम PNG, फ्रेम सीक्वेंस (ffmpeg संकेत के साथ), और साइड-बाय-साइड तुलना निर्यात |
| `SvgExportService` | Inkscape लेयर्स, Catmull-Rom स्प्लाइन, हीटमैप, वेक्टर फ़ील्ड और चार रंग पैलेट (डिफ़ॉल्ट, लाइट, हाईकॉन्ट्रास्ट, प्रकाशन) के साथ फुल-वेक्टर SVG निर्यात |
| `IAnnotation` | सैद्धांतिक आधार और प्राथमिकता के साथ टाइप किए गए एनोटेशन (फेज, वार्निंग, इनसाइट, फेलियर, कस्टम) |
| `VortexColors` | सिमेंटिक रंग पैलेट — पृष्ठभूमि परतें, उच्चारण सिमेंटिक्स, गंभीरता कोडिंग, आइगेनवैल्यू पैलेट, lerp/ग्रेडिएंट सहायक |

---

## स्थापना

### Microsoft Store (अनुशंसित)

**स्टोर आईडी:** `9P3HT1PHBKQK`

[इसे Microsoft Store से प्राप्त करें](https://apps.microsoft.com/detail/9P3HT1PHBKQK)

विंडोज 10 (बिल्ड 17763) या बाद के संस्करण की आवश्यकता है।

### स्रोत से

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

### NuGet (केवल लाइब्रेरी)

```bash
dotnet add package VortexKit
```

---

## परियोजना संरचना

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

## परीक्षण

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

## संबंधित

- [ScalarScope (Python)](https://github.com/mcp-tool-shop-org/ScalarScope) — कोर प्रशिक्षण फ्रेमवर्क
- [RESULTS_AND_LIMITATIONS.md](docs/RESULTS_AND_LIMITATIONS.md) — पूर्ण प्रायोगिक परिणाम
- [CHANGELOG.md](CHANGELOG.md) — रिलीज़ इतिहास
- [PRIVACY.md](PRIVACY.md) — गोपनीयता नीति
- [ROADMAP.md](ROADMAP.md) — योजनाबद्ध विशेषताएं

---

## लाइसेंस

[MIT](LICENSE) — कॉपीराइट (c) 2025-2026 ScalarScope प्रोजेक्ट (mcp-tool-shop-org)
