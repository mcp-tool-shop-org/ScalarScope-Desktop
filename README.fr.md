<p align="center">
  <a href="README.md">English</a> | <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/ScalarScope-Desktop/readme.png" alt="ScalarScope" width="400">
</p>

# ScalarScope-Desktop

> Fait partie de [MCP Tool Shop](https://mcptoolshop.com)

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/ScalarScope-Desktop/actions/workflows/build.yml"><img src="https://github.com/mcp-tool-shop-org/ScalarScope-Desktop/actions/workflows/build.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square" alt="License: MIT"></a>
  <a href="https://mcp-tool-shop-org.github.io/ScalarScope-Desktop/"><img src="https://img.shields.io/badge/Landing_Page-live-blue?style=flat-square" alt="Landing Page"></a>
  <a href="https://apps.microsoft.com/detail/9P3HT1PHBKQK"><img src="https://img.shields.io/badge/Microsoft%20Store-9P3HT1PHBKQK-0078D4?style=flat-square&logo=microsoft" alt="Microsoft Store"></a>
  <a href="https://www.nuget.org/packages/VortexKit"><img src="https://img.shields.io/nuget/v/VortexKit?label=VortexKit&logo=nuget&style=flat-square" alt="NuGet: VortexKit"></a>
</p>

**ASPIRE Scalar Vortex Visualizer : une application de bureau .NET MAUI pour comparer les exécutions d'inférence d'apprentissage automatique avec rigueur scientifique.**

---

## Pourquoi ScalarScope ?

La plupart des équipes d'apprentissage automatique analysent les journaux. ScalarScope remplace cela par une comparaison structurée et reproductible.

- **Comparaison équitable** : Chargez deux traces d'inférence côte à côte et voyez exactement ce qui a changé.
- **Analyse delta canonique** : Cinq types de delta (ΔTc, ΔO, ΔF, ΔĀ, ΔTd) ne s'activent que lorsque les différences sont statistiquement significatives.
- **Présets d'exécution** : Le présélectionné TFRT supprime automatiquement les métriques non pertinentes afin que vous puissiez vous concentrer sur ce qui compte pour les charges de travail TensorFlow-TRT.
- **Bundles reproductibles** : Exportez des archives `.scbundle` avec une intégrité SHA-256, des deltas figés et des métadonnées complètes.
- **Mode examen** : Ouvrez un bundle sans recalculer ; les résultats sont vérifiés cryptographiquement, et non recalculés.
- **Confidentialité avant tout** : Zéro télémétrie, zéro analyse, toutes les données restent locales, sauf si vous les exportez explicitement.

---

## Paquets NuGet

| Paquet | Version | Description |
| --------- | --------- | ------------- |
| [VortexKit](https://www.nuget.org/packages/VortexKit) | [![NuGet](https://img.shields.io/nuget/v/VortexKit)](https://www.nuget.org/packages/VortexKit) | Framework de visualisation réutilisable pour la dynamique de l'entraînement : lecture synchronisée dans le temps, toiles SkiaSharp animées, vues de comparaison, superpositions d'annotations, exportation SVG/PNG et un système de couleurs sémantique. Construit sur SkiaSharp + MAUI. |

```bash
dotnet add package VortexKit
```

---

## Démarrage rapide

### Depuis le Microsoft Store

1. Installez **ScalarScope** depuis le [Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK) (ID de la boutique : `9P3HT1PHBKQK`)
2. Cliquez sur **Comparer deux exécutions**
3. Chargez la trace TFRT de référence (avant optimisation)
4. Chargez la trace TFRT optimisée (après optimisation)
5. Examinez les deltas dans l'onglet **Comparer**
6. Exportez un fichier `.scbundle` pour un partage reproductible

### Utilisation de VortexKit dans votre propre application

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

## Fonctionnalités

### Analyse des deltas : Cinq types de deltas canoniques

Chaque comparaison produit un ensemble de deltas canoniques. Chaque delta ne s'active que lorsque la différence est statistiquement significative ; les deltas non pertinents sont automatiquement supprimés.

| Delta | Nom complet | Ce que cela mesure | S'active lorsque |
| ------- | ----------- | ------------------ | ------------ |
| **ΔTc** | Temps de convergence | Nombre d'étapes pour atteindre une latence stable | État stable atteint à des étapes différentes (séparation d'au moins 3 étapes) |
| **ΔO** | Variabilité de la sortie | Oscillation / instabilité de l'exécution | Le score de l'amplitude au-dessus du seuil diffère au-delà du bruit de fond |
| **ΔF** | Taux d'erreur | Fréquence des anomalies | La fréquence ou le type d'erreur diffère entre les exécutions |
| **ΔĀ** | Latence moyenne | Valeur moyenne de la métrique | La moyenne diffère de manière significative (supprimée dans le présélectionné TFRT) |
| **ΔTd** | Durée totale | Temps réel / émergence structurelle | La durée ou le début de la dominance diffère (supprimée dans le présélectionné TFRT) |

### Présets d'exécution : TFRT

Le présélectionné **TensorFlow-TRT** (`tensorflowrt-runtime-v1`) mappe les signaux spécifiques à l'inférence (latence, débit, mémoire, charge CPU/GPU) et supprime les deltas spécifiques à l'entraînement (ΔĀ, ΔTd) qui n'ont pas de sens pour la comparaison de l'inférence. Des avertissements sont affichés lorsque le temps de préchauffage dépasse 50 % de l'exécution ou lorsque seules des statistiques agrégées sont disponibles.

### Bundles reproductibles

Exportez les résultats sous forme d'archives `.scbundle` (ComparisonBundle v1.0.0) :

- **`manifest.json`** — métadonnées du paquet, version de l'application, étiquettes de comparaison, mode d'alignement.
- **`repro/repro.json`** — empreintes des entrées, hachage prédéfini, amorce de déterminisme, informations sur l'environnement.
- **`findings/deltas.json`** — différences canoniques avec scores de confiance, ancres et types de déclenchement.
- **`findings/why.json`** — explications lisibles par l'homme, garde-fous, puces de paramètres.
- **`findings/summary.md`** — résumé Markdown généré automatiquement.
- **Intégrité** — chaque fichier est haché avec SHA-256 ; hachage au niveau du paquet pour la détection de modifications.

### Mode d'examen

Ouvrez n'importe quel fichier `.scbundle` sans recalcul. Le mode d'examen vérifie l'intégrité, affiche les différences figées et affiche une bannière indiquant que les résultats sont vérifiés et non recalculés.

### Framework de visualisation VortexKit

VortexKit est le moteur de visualisation extrait, publié en tant que package NuGet autonome :

| Composant | Ce qu'il fait |
| ----------- | ------------- |
| `PlaybackController` | Chronologie partagée 0→1 avec lecture/pause/étape/boucle, préréglages de vitesse (0,25x—4x), fréquence d'images d'environ 60 ips. |
| `AnimatedCanvas` | Classe de base `SKCanvasView` abstraite avec invalidation synchronisée avec le temps, dessin de grille, événements tactiles/glissement, fonctions d'aide aux coordonnées. |
| `ITimeSeries<T>` / `TimeSeries<T>` | Série temporelle générique avec mappage index↔temps et énumération de la trace. |
| `ExportService` | Exportation en image PNG à une seule image, séquences d'images (avec indications ffmpeg) et comparaison côte à côte. |
| `SvgExportService` | Exportation SVG vectorielle complète avec calques Inkscape, splines Catmull-Rom, cartes de chaleur, champs vectoriels et quatre palettes de couleurs (Par défaut, Clair, Contraste élevé, Publication). |
| `IAnnotation` | Annotations typées (Phase, Avertissement, Information, Échec, Personnalisé) avec base théorique et priorité. |
| `VortexColors` | Palette de couleurs sémantique — calques d'arrière-plan, sémantique d'accentuation, codage de gravité, palette de valeurs propres, fonctions d'aide à l'interpolation/dégradé. |

---

## Installation

### Microsoft Store (recommandé)

**ID de la boutique :** `9P3HT1PHBKQK`

[Téléchargez-le depuis le Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK)

Nécessite Windows 10 (build 17763) ou version ultérieure.

### À partir du code source

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

### NuGet (bibliothèque uniquement)

```bash
dotnet add package VortexKit
```

---

## Structure du projet

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

## Tests

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

## Liés

- [ScalarScope (Python)](https://github.com/mcp-tool-shop-org/ScalarScope) — Framework de formation principal
- [RESULTS_AND_LIMITATIONS.md](docs/RESULTS_AND_LIMITATIONS.md) — Résultats expérimentaux complets
- [CHANGELOG.md](CHANGELOG.md) — Historique des versions
- [PRIVACY.md](PRIVACY.md) — Politique de confidentialité
- [ROADMAP.md](ROADMAP.md) — Fonctionnalités prévues

---

## Licence

[MIT](LICENSE) — Copyright (c) 2025-2026 ScalarScope Project (mcp-tool-shop-org)
