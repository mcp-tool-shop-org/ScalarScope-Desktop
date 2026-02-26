<p align="center">
  <a href="README.md">English</a> | <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  
            <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/ScalarScope-Desktop/readme.png"
           alt="ScalarScope" width="400">
</p>

# ScalarScope-Desktop

> Parte di [MCP Tool Shop](https://mcptoolshop.com)

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

**ASPIRE Scalar Vortex Visualizer: un'applicazione desktop .NET MAUI per confrontare le esecuzioni di inferenza di modelli di machine learning con rigore scientifico.**

---

## Perché ScalarScope?

La maggior parte dei team di machine learning analizza i log. ScalarScope sostituisce questo processo con un confronto strutturato e riproducibile.

- **Confronto diretto:** Carica due tracce di inferenza affiancate e osserva esattamente cosa è cambiato.
- **Analisi delta standard:** Cinque tipi di delta (ΔTc, ΔO, ΔF, ΔĀ, ΔTd) vengono attivati solo quando le differenze sono statisticamente significative.
- **Impostazioni predefinite per l'esecuzione:** L'impostazione predefinita TFRT sopprime automaticamente le metriche irrilevanti, consentendoti di concentrarti su ciò che è importante per i carichi di lavoro TensorFlow-TRT.
- **Pacchetti riproducibili:** Esporta archivi `.scbundle` con integrità SHA-256, delta "congelati" e metadati completi.
- **Modalità di revisione:** Apri un pacchetto senza doverlo ricalcolare; i risultati sono verificati crittograficamente, non ricavati.
- **Priorità alla privacy:** Nessuna telemetria, nessuna analisi, tutti i dati rimangono locali a meno che tu non li esporti esplicitamente.

---

## Pacchetti NuGet

| Pacchetto | Versione | Descrizione |
| --------- | --------- | ------------- |
| [VortexKit](https://www.nuget.org/packages/VortexKit) | [![NuGet](https://img.shields.io/nuget/v/VortexKit)](https://www.nuget.org/packages/VortexKit) | Framework di visualizzazione riutilizzabile per l'analisi della dinamica dell'addestramento: riproduzione sincronizzata nel tempo, canvas animati SkiaSharp, viste di confronto, sovrapposizioni di annotazioni, esportazione SVG/PNG e un sistema di colori semantico. Costruito su SkiaSharp + MAUI. |

```bash
dotnet add package VortexKit
```

---

## Guida rapida

### Dal Microsoft Store

1. Installa **ScalarScope** dal [Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK) (ID Store: `9P3HT1PHBKQK`)
2. Clicca su **Confronta due esecuzioni**
3. Carica la traccia TFRT di base (prima dell'ottimizzazione)
4. Carica la traccia TFRT ottimizzata (dopo l'ottimizzazione)
5. Esamina i delta nella scheda **Confronta**
6. Esporta un file `.scbundle` per la condivisione riproducibile

### Utilizzo di VortexKit nella tua applicazione

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

## Funzionalità

### Analisi dei delta: cinque tipi di delta standard

Ogni confronto produce un insieme di delta standard. Ogni delta viene attivato solo quando la differenza è statisticamente significativa; i delta irrilevanti vengono soppressi automaticamente.

| Delta | Nome completo | Cosa misura | Si attiva quando |
| ------- | ----------- | ------------------ | ------------ |
| **ΔTc** | Tempo di convergenza | Numero di passaggi per raggiungere una latenza stabile | Stato stazionario raggiunto in un numero diverso di passaggi (separazione di almeno 3 passaggi) |
| **ΔO** | Variabilità dell'output | Oscillazione / instabilità durante l'esecuzione | Il punteggio area-sopra-soglia differisce oltre la soglia del rumore |
| **ΔF** | Tasso di errore | Frequenza delle anomalie | La frequenza o il tipo di errore differisce tra le esecuzioni |
| **ΔĀ** | Latenza media | Valore medio della metrica | La media differisce in modo significativo (soppressa nell'impostazione predefinita TFRT) |
| **ΔTd** | Durata totale | Tempo reale / emergenza strutturale | La durata o l'inizio della dominanza differiscono (soppressa nell'impostazione predefinita TFRT) |

### Impostazioni predefinite per l'esecuzione: TFRT

L'impostazione predefinita integrata **TensorFlow-TRT** (`tensorflowrt-runtime-v1`) mappa i segnali specifici per l'inferenza (latenza, throughput, memoria, carico della CPU/GPU) e sopprime i delta relativi solo all'addestramento (ΔĀ, ΔTd) che non hanno significato per il confronto dell'inferenza. I meccanismi di protezione avvisano quando il riscaldamento supera il 50% dell'esecuzione o quando sono disponibili solo statistiche aggregate.

### Pacchetti riproducibili

Esporta i risultati come archivi `.scbundle` (ComparisonBundle v1.0.0):

- **`manifest.json`** — metadati del pacchetto, versione dell'app, etichette di confronto, modalità di allineamento.
- **`repro/repro.json`** — impronte digitali di input, hash predefinito, seme per la determinazione, informazioni sull'ambiente.
- **`findings/deltas.json`** — delta canonici con punteggi di confidenza, ancoraggi e tipi di trigger.
- **`findings/why.json`** — spiegazioni leggibili, linee guida, parametri.
- **`findings/summary.md`** — riepilogo in formato Markdown generato automaticamente.
- **Integrità** — ogni file viene sottoposto a hashing con SHA-256; hash a livello di pacchetto per la rilevazione di manomissioni.

### Modalità di revisione

Apri qualsiasi file `.scbundle` senza doverlo ricalcolare. La modalità di revisione verifica l'integrità, visualizza i delta "congelati" e mostra un banner che indica che i risultati sono verificati e non ricalcolati.

### Framework di visualizzazione VortexKit

VortexKit è il motore di visualizzazione estratto, pubblicato come pacchetto NuGet autonomo:

| Componente | Cosa fa |
| ----------- | ------------- |
| `PlaybackController` | Timeline condivisa 0→1 con pulsanti di riproduzione/pausa/avanzamento/loop, preset di velocità (0.25x—4x), circa 60 fotogrammi al secondo. |
| `AnimatedCanvas` | Classe base `SKCanvasView` astratta con invalidazione sincronizzata con il tempo, disegno della griglia, gestione di eventi touch/drag, funzioni di supporto per le coordinate. |
| `ITimeSeries<T>` / `TimeSeries<T>` | Serie temporale generica con mappatura indice↔tempo e enumerazione della sequenza. |
| `ExportService` | Esportazione in formato PNG a singolo fotogramma, sequenze di fotogrammi (con suggerimenti per ffmpeg) e confronto affiancato. |
| `SvgExportService` | Esportazione completa in formato SVG con livelli Inkscape, spline di Catmull-Rom, mappe di calore, campi vettoriali e quattro palette di colori (Predefinita, Chiaro, Alto Contrasto, Pubblicazione). |
| `IAnnotation` | Annotazioni tipizzate (Fase, Avviso, Informazione, Errore, Personalizzata) con base teorica e priorità. |
| `VortexColors` | Palette di colori semantica: livelli di sfondo, semantica degli elementi, codifica della gravità, palette degli autovalori, funzioni di supporto per lerp/gradient. |

---

## Installazione

### Microsoft Store (consigliato)

**ID del negozio:** `9P3HT1PHBKQK`

[Scaricalo dal Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK)

Richiede Windows 10 (build 17763) o versione successiva.

### Dal codice sorgente

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

### NuGet (solo libreria)

```bash
dotnet add package VortexKit
```

---

## Struttura del progetto

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

## Test

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

## Correlati

- [ScalarScope (Python)](https://github.com/mcp-tool-shop-org/ScalarScope) — Framework di training principale
- [RESULTS_AND_LIMITATIONS.md](docs/RESULTS_AND_LIMITATIONS.md) — Risultati sperimentali completi
- [CHANGELOG.md](CHANGELOG.md) — Cronologia delle versioni
- [PRIVACY.md](PRIVACY.md) — Informativa sulla privacy
- [ROADMAP.md](ROADMAP.md) — Funzionalità pianificate

---

## Licenza

[MIT](LICENSE) — Copyright (c) 2025-2026 ScalarScope Project (mcp-tool-shop-org)
