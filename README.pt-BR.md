<p align="center">
  <a href="README.md">English</a> | <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="assets/logo.png" alt="ScalarScope" width="400">
</p>

# ScalarScope-Desktop

> Parte de [MCP Tool Shop](https://mcptoolshop.com)

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/ScalarScope-Desktop/actions/workflows/build.yml"><img src="https://github.com/mcp-tool-shop-org/ScalarScope-Desktop/actions/workflows/build.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square" alt="License: MIT"></a>
  <a href="https://mcp-tool-shop-org.github.io/ScalarScope-Desktop/"><img src="https://img.shields.io/badge/Landing_Page-live-blue?style=flat-square" alt="Landing Page"></a>
  <a href="https://apps.microsoft.com/detail/9P3HT1PHBKQK"><img src="https://img.shields.io/badge/Microsoft%20Store-9P3HT1PHBKQK-0078D4?style=flat-square&logo=microsoft" alt="Microsoft Store"></a>
  <a href="https://www.nuget.org/packages/VortexKit"><img src="https://img.shields.io/nuget/v/VortexKit?label=VortexKit&logo=nuget&style=flat-square" alt="NuGet: VortexKit"></a>
</p>

**ASPIRE Scalar Vortex Visualizer — um aplicativo de desktop .NET MAUI para comparar execuções de inferência de modelos de aprendizado de máquina com rigor científico.**

---

## Por que ScalarScope?

A maioria das equipes de aprendizado de máquina analisa logs. O ScalarScope substitui isso por comparações estruturadas e reproduzíveis.

- **Comparação direta** — Carregue dois rastreamentos de inferência lado a lado e veja exatamente o que mudou.
- **Análise de diferenças canônicas** — Cinco tipos de diferenças (ΔTc, ΔO, ΔF, ΔĀ, ΔTd) são ativados apenas quando as diferenças são estatisticamente significativas.
- **Configurações de tempo de execução** — A configuração TFRT suprime automaticamente as métricas irrelevantes para que você se concentre no que é importante para as cargas de trabalho TensorFlow-TRT.
- **Pacotes reproduzíveis** — Exporte arquivos `.scbundle` com integridade SHA-256, diferenças fixas e metadados de rastreabilidade completos.
- **Modo de revisão** — Abra um pacote sem recalcular; os resultados são verificados criptograficamente, não recalculados.
- **Privacidade em primeiro lugar** — Sem telemetria, sem análise, todos os dados permanecem locais, a menos que você os exporte explicitamente.

---

## Pacotes NuGet

| Pacote | Versão | Descrição |
| --------- | --------- | ------------- |
| [VortexKit](https://www.nuget.org/packages/VortexKit) | [![NuGet](https://img.shields.io/nuget/v/VortexKit)](https://www.nuget.org/packages/VortexKit) | Estrutura de visualização reutilizável para a dinâmica do treinamento — reprodução sincronizada no tempo, telas animadas SkiaSharp, visualizações de comparação, sobreposições de anotações, exportação SVG/PNG e um sistema de cores semântico. Construído com SkiaSharp + MAUI. |

```bash
dotnet add package VortexKit
```

---

## Como começar

### Da Microsoft Store

1. Instale o **ScalarScope** da [Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK) (ID da loja: `9P3HT1PHBKQK`)
2. Clique em **Comparar duas execuções**
3. Carregue o rastreamento TFRT de referência (antes da otimização)
4. Carregue o rastreamento TFRT otimizado (após a otimização)
5. Analise as diferenças na guia **Comparar**
6. Exporte um arquivo `.scbundle` para compartilhamento reproduzível

### Usando VortexKit em seu próprio aplicativo

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

## Características

### Análise de diferenças — Cinco tipos de diferenças canônicas

Cada comparação produz um conjunto de diferenças canônicas. Cada diferença é ativada apenas quando a diferença é estatisticamente significativa; as diferenças irrelevantes são suprimidas automaticamente.

| Delta | Nome completo | O que ele mede | É ativado quando |
| ------- | ----------- | ------------------ | ------------ |
| **ΔTc** | Tempo de convergência | Passos para atingir a latência estável | Estado estacionário atingido em diferentes passos (separação de ≥3 passos) |
| **ΔO** | Variação da saída | Oscilação / instabilidade em tempo de execução | A pontuação acima do limite difere além do nível de ruído |
| **ΔF** | Taxa de falha | Frequência de anomalias | A frequência ou o tipo de falha difere entre as execuções |
| **ΔĀ** | Latência média | Valor médio da métrica | A média difere de forma significativa (suprimida na configuração TFRT) |
| **ΔTd** | Duração total | Tempo real / surgimento estrutural | A duração ou o início da dominância difere (suprimido na configuração TFRT) |

### Configurações de tempo de execução — TFRT

A configuração integrada **TensorFlow-TRT** (`tensorflowrt-runtime-v1`) mapeia sinais específicos de inferência (latência, taxa de transferência, memória, carga da CPU/GPU) e suprime as diferenças que só se aplicam ao treinamento (ΔĀ, ΔTd) que não têm significado para a comparação de inferência. Os avisos alertam quando o aquecimento excede 50% da execução ou quando apenas estatísticas agregadas estão disponíveis.

### Pacotes reproduzíveis

Exporte os resultados como arquivos `.scbundle` (ComparisonBundle v1.0.0):

- **`manifest.json`** — metadados do pacote, versão do aplicativo, rótulos de comparação, modo de alinhamento.
- **`repro/repro.json`** — impressões digitais de entrada, hash predefinido, semente de determinismo, informações do ambiente.
- **`findings/deltas.json`** — deltas canônicos com pontuações de confiança, âncoras e tipos de gatilho.
- **`findings/why.json`** — explicações legíveis por humanos, diretrizes, chips de parâmetros.
- **`findings/summary.md`** — resumo em Markdown gerado automaticamente.
- **Integridade** — cada arquivo com hash SHA-256; hash de nível de pacote para detecção de adulteração.

### Modo de Revisão

Abra qualquer arquivo `.scbundle` sem recalcular. O modo de revisão verifica a integridade, exibe os deltas fixos e mostra um banner de modo de revisão para que você saiba que os resultados foram verificados e não recalculados.

### Framework de Visualização VortexKit

VortexKit é o motor de visualização extraído, publicado como um pacote NuGet independente:

| Componente | O que ele faz |
| ----------- | ------------- |
| `PlaybackController` | Linha do tempo compartilhada 0→1 com reprodução/pausa/avanço/loop, predefinições de velocidade (0,25x—4x), ~60 fps. |
| `AnimatedCanvas` | Classe base `SKCanvasView` abstrata com invalidação sincronizada com o tempo, desenho de grade, eventos de toque/arrasto, funções auxiliares de coordenadas. |
| `ITimeSeries<T>` / `TimeSeries<T>` | Série temporal genérica com mapeamento de índice↔tempo e enumeração de trilhas. |
| `ExportService` | Exportação de imagens PNG de um único quadro, sequências de quadros (com dicas do ffmpeg) e comparação lado a lado. |
| `SvgExportService` | Exportação SVG vetorial completa com camadas do Inkscape, splines Catmull-Rom, mapas de calor, campos vetoriais e quatro paletas de cores (Padrão, Claro, Alto Contraste, Publicação). |
| `IAnnotation` | Anotações tipadas (Fase, Aviso, Informação, Falha, Personalizado) com base teórica e prioridade. |
| `VortexColors` | Paleta de cores semântica — camadas de fundo, semântica de destaque, codificação de severidade, paleta de autovalor, funções auxiliares de interpolação/gradiente. |

---

## Instalação

### Microsoft Store (recomendado)

**ID da Loja:** `9P3HT1PHBKQK`

[Obtenha-o na Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK)

Requer Windows 10 (build 17763) ou posterior.

### A partir do Código Fonte

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

### NuGet (apenas biblioteca)

```bash
dotnet add package VortexKit
```

---

## Estrutura do Projeto

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

## Testes

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

## Relacionado

- [ScalarScope (Python)](https://github.com/mcp-tool-shop-org/ScalarScope) — Framework de treinamento principal
- [RESULTS_AND_LIMITATIONS.md](docs/RESULTS_AND_LIMITATIONS.md) — Resultados experimentais completos
- [CHANGELOG.md](CHANGELOG.md) — Histórico de lançamentos
- [PRIVACY.md](PRIVACY.md) — Política de privacidade
- [ROADMAP.md](ROADMAP.md) — Recursos planejados

---

## Licença

[MIT](LICENSE) — Copyright (c) 2025-2026 ScalarScope Project (mcp-tool-shop-org)
