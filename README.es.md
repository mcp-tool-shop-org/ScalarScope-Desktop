<p align="center">
  <a href="README.md">English</a> | <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  
            <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/ScalarScope-Desktop/readme.png"
           alt="ScalarScope" width="400">
</p>

# ScalarScope-Desktop

> Parte de [MCP Tool Shop](https://mcptoolshop.com)

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

**ASPIRE Scalar Vortex Visualizer: una aplicación de escritorio .NET MAUI para comparar ejecuciones de inferencia de modelos de aprendizaje automático con rigor científico.**

---

## ¿Por qué ScalarScope?

La mayoría de los equipos de aprendizaje automático analizan registros. ScalarScope reemplaza eso con una comparación estructurada y reproducible.

- **Comparación directa** — Cargue dos trazas de inferencia una al lado de la otra y vea exactamente qué ha cambiado.
- **Análisis de diferencias canónicas** — Cinco tipos de diferencias (ΔTc, ΔO, ΔF, ΔĀ, ΔTd) solo se activan cuando las diferencias son estadísticamente significativas.
- **Preajustes de tiempo de ejecución** — El preajuste TFRT suprime automáticamente las métricas irrelevantes para que se concentre en lo que importa para las cargas de trabajo de TensorFlow-TRT.
- **Paquetes reproducibles** — Exporte archivos `.scbundle` con integridad SHA-256, diferencias congeladas y metadatos de procedencia completos.
- **Modo de revisión** — Abra un paquete sin volver a calcular; los resultados se verifican criptográficamente, no se recalculan.
- **Privacidad primero** — Sin telemetría, sin análisis, todos los datos permanecen locales a menos que los exporte explícitamente.

---

## Paquetes NuGet

| Paquete | Versión | Descripción |
| --------- | --------- | ------------- |
| [VortexKit](https://www.nuget.org/packages/VortexKit) | [![NuGet](https://img.shields.io/nuget/v/VortexKit)](https://www.nuget.org/packages/VortexKit) | Marco de visualización reutilizable para la dinámica del entrenamiento: reproducción sincronizada en el tiempo, lienzos animados de SkiaSharp, vistas de comparación, superposiciones de anotaciones, exportación SVG/PNG y un sistema de colores semántico. Construido sobre SkiaSharp + MAUI. |

```bash
dotnet add package VortexKit
```

---

## Cómo empezar

### Desde la Microsoft Store

1. Instale **ScalarScope** desde la [Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK) (ID de la tienda: `9P3HT1PHBKQK`)
2. Haga clic en **Comparar dos ejecuciones**
3. Cargue la traza TFRT de referencia (antes de la optimización)
4. Cargue la traza TFRT optimizada (después de la optimización)
5. Revise las diferencias en la pestaña **Comparar**
6. Exporte un archivo `.scbundle` para compartir de forma reproducible

### Usar VortexKit en su propia aplicación

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

### Análisis de diferencias: cinco tipos de diferencias canónicas

Cada comparación produce un conjunto de diferencias canónicas. Cada diferencia solo se activa cuando la diferencia es estadísticamente significativa; las diferencias irrelevantes se suprimen automáticamente.

| Delta | Nombre completo | Lo que mide | Se activa cuando |
| ------- | ----------- | ------------------ | ------------ |
| **ΔTc** | Tiempo de convergencia | Pasos para alcanzar una latencia estable | Se alcanza el estado estacionario en diferentes pasos (separación de ≥3 pasos) |
| **ΔO** | Variabilidad de la salida | Oscilación / inestabilidad en tiempo de ejecución | La puntuación del área por encima del umbral difiere más allá del nivel de ruido |
| **ΔF** | Tasa de error | Frecuencia de anomalías | La frecuencia o el tipo de error difiere entre las ejecuciones |
| **ΔĀ** | Latencia promedio | Valor medio de la métrica | El valor medio difiere de manera significativa (se suprime en el preajuste TFRT) |
| **ΔTd** | Duración total | Tiempo real / emergencia estructural | La duración o el inicio de la dominancia difieren (se suprime en el preajuste TFRT) |

### Preajustes de tiempo de ejecución: TFRT

El preajuste integrado de **TensorFlow-TRT** (`tensorflowrt-runtime-v1`) mapea señales específicas de la inferencia (latencia, rendimiento, memoria, carga de la CPU/GPU) y suprime las diferencias que solo son relevantes para el entrenamiento (ΔĀ, ΔTd) que no tienen sentido para la comparación de la inferencia. Las protecciones alertan cuando el calentamiento supera el 50% de la ejecución o cuando solo están disponibles estadísticas agregadas.

### Paquetes reproducibles

Exporte los resultados como archivos `.scbundle` (ComparisonBundle v1.0.0):

- **`manifest.json`** — metadatos del paquete, versión de la aplicación, etiquetas de comparación, modo de alineación.
- **`repro/repro.json`** — huellas digitales de entrada, hash predefinido, semilla de determinismo, información del entorno.
- **`findings/deltas.json`** — deltas canónicos con puntuaciones de confianza, anclajes y tipos de activación.
- **`findings/why.json`** — explicaciones legibles por humanos, restricciones, parámetros.
- **`findings/summary.md`** — resumen en formato Markdown generado automáticamente.
- **Integridad** — cada archivo con hash SHA-256; hash a nivel de paquete para la detección de manipulaciones.

### Modo de revisión

Abra cualquier archivo `.scbundle` sin volver a calcular. El modo de revisión verifica la integridad, muestra los deltas congelados y muestra un banner de modo de revisión para que sepa que los resultados están verificados, no recalculados.

### Marco de visualización de VortexKit

VortexKit es el motor de visualización extraído, publicado como un paquete NuGet independiente:

| Componente | ¿Qué hace? |
| ----------- | ------------- |
| `PlaybackController` | Línea de tiempo compartida 0→1 con reproducción/pausa/avance/bucle, preajustes de velocidad (0.25x—4x), ~60 fps. |
| `AnimatedCanvas` | Clase base `SKCanvasView` abstracta con invalidación sincronizada con el tiempo, dibujo de cuadrícula, eventos táctiles/de arrastre, funciones de ayuda de coordenadas. |
| `ITimeSeries<T>` / `TimeSeries<T>` | Serie de tiempo genérica con mapeo de índice a tiempo y enumeración de trazas. |
| `ExportService` | Exportación de imágenes PNG de un solo fotograma, secuencias de fotogramas (con sugerencias de ffmpeg) y comparación lado a lado. |
| `SvgExportService` | Exportación SVG vectorial completa con capas de Inkscape, curvas Catmull-Rom, mapas de calor, campos vectoriales y cuatro paletas de colores (Predeterminada, Clara, Alto contraste, Publicación). |
| `IAnnotation` | Anotaciones tipadas (Fase, Advertencia, Información, Fallo, Personalizada) con base teórica y prioridad. |
| `VortexColors` | Paleta de colores semántica: capas de fondo, semántica de acento, codificación de gravedad, paleta de autovalores, funciones de ayuda de interpolación/gradiente. |

---

## Instalación

### Microsoft Store (recomendado)

**ID de la tienda:** `9P3HT1PHBKQK`

[Obténlo de la Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK)

Requiere Windows 10 (compilación 17763) o posterior.

### Desde el código fuente

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

### NuGet (solo biblioteca)

```bash
dotnet add package VortexKit
```

---

## Estructura del proyecto

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

## Pruebas

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

- [ScalarScope (Python)](https://github.com/mcp-tool-shop-org/ScalarScope) — Marco de entrenamiento principal
- [RESULTS_AND_LIMITATIONS.md](docs/RESULTS_AND_LIMITATIONS.md) — Resultados experimentales completos
- [CHANGELOG.md](CHANGELOG.md) — Historial de versiones
- [PRIVACY.md](PRIVACY.md) — Política de privacidad
- [ROADMAP.md](ROADMAP.md) — Funciones planificadas

---

## Licencia

[MIT](LICENSE) — Copyright (c) 2025-2026 ScalarScope Project (mcp-tool-shop-org)
