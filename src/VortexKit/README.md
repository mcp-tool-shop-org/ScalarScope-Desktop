# VortexKit

**A reusable visualization framework for training dynamics**

VortexKit extracts the core visualization patterns from ScalarScope into a general-purpose library for building interactive training analysis tools.

## Core Components

### 1. Time-Synchronized Playback

```csharp
// Shared playback controller
var player = new PlaybackController();
player.TimeChanged += () => InvalidateAllViews();

// Multiple views bind to same controller
trajectoryView.CurrentTime = player.Time;
eigenView.CurrentTime = player.Time;
scalarsView.CurrentTime = player.Time;
```

### 2. SkiaSharp Canvas Base

```csharp
public abstract class AnimatedCanvas : SKCanvasView
{
    public double CurrentTime { get; set; }

    protected abstract void OnRender(SKCanvas canvas, SKImageInfo info, double time);

    // Automatic invalidation on time change
    // Smooth animation support
    // Touch/mouse interaction handling
}
```

### 3. Side-by-Side Comparison

```csharp
public class ComparisonView<T> where T : ITimeSeries
{
    public T LeftData { get; set; }
    public T RightData { get; set; }
    public PlaybackController SharedPlayer { get; }

    // Synchronized rendering
    // Differential metrics computation
    // Export with both panels
}
```

### 4. Annotation Overlay System

```csharp
public class AnnotationLayer : SKCanvasView
{
    public IList<Annotation> Annotations { get; set; }
    public bool ShowPhaseLabels { get; set; }
    public bool ShowWarnings { get; set; }

    // Layered on top of main canvas
    // Input-transparent (clicks pass through)
    // Configurable visibility per category
}
```

### 5. Export Service

```csharp
public class ExportService
{
    // Single frame at any resolution
    Task<string> ExportFrameAsync(IRenderable view, double time, ExportOptions options);

    // Frame sequence for video/GIF
    Task<string[]> ExportSequenceAsync(IRenderable view, double startTime, double endTime, ExportOptions options);

    // Comparison export (side-by-side)
    Task<string> ExportComparisonAsync(IRenderable left, IRenderable right, double time, ExportOptions options);
}
```

## Design Patterns

### Pattern 1: Bindable Time Properties

All time-dependent views expose a `CurrentTime` property (0.0 to 1.0) that:
- Triggers re-render when changed
- Can be bound to a shared `PlaybackController`
- Supports both continuous playback and discrete stepping

### Pattern 2: Semantic Color Palettes

```csharp
public static class VortexColors
{
    // Background layers
    public static SKColor Background => SKColor.Parse("#0f0f1a");
    public static SKColor Surface => SKColor.Parse("#1a1a2e");
    public static SKColor Elevated => SKColor.Parse("#16213e");

    // Semantic accents
    public static SKColor Primary => SKColor.Parse("#00d9ff");    // Main accent
    public static SKColor Success => SKColor.Parse("#4ecdc4");    // Positive/left
    public static SKColor Danger => SKColor.Parse("#ff6b6b");     // Negative/right
    public static SKColor Warning => SKColor.Parse("#ff9f43");    // Caution
    public static SKColor Info => SKColor.Parse("#a29bfe");       // Neutral info
    public static SKColor Highlight => SKColor.Parse("#ffd93d");  // Emphasis
}
```

### Pattern 3: Theoretical Grounding

Every visual element should map to a theoretical construct:

```csharp
public interface IAnnotation
{
    string Label { get; }
    string TheoreticalBasis { get; }  // What does this mean?
    AnnotationCategory Category { get; }
    double Time { get; }
    SKPoint Position { get; }
}
```

### Pattern 4: Failure-First Design

Negative results get dedicated visualization:

```csharp
public interface IFailureEvent
{
    double Time { get; }
    string Category { get; }
    FailureSeverity Severity { get; }
    string Description { get; }
}

public class FailureTimeline : AnimatedCanvas
{
    public IList<IFailureEvent> Failures { get; set; }
    public IFailureEvent? SelectedFailure { get; set; }

    // Click to select
    // Severity-coded markers
    // Jump-to-failure navigation
}
```

## Usage Example: Custom Training Visualizer

```csharp
// 1. Define your data model
public record MyTrainingRun : ITimeSeries
{
    public List<StateSnapshot> States { get; init; }
    public List<MetricSnapshot> Metrics { get; init; }
    public List<FailureEvent> Failures { get; init; }
}

// 2. Create custom canvas
public class MyTrajectoryCanvas : AnimatedCanvas
{
    public MyTrainingRun? Run { get; set; }

    protected override void OnRender(SKCanvas canvas, SKImageInfo info, double time)
    {
        var state = Run?.GetStateAtTime(time);
        if (state == null) return;

        // Your rendering logic here
        DrawTrajectory(canvas, state);
        DrawMetrics(canvas, state);
    }
}

// 3. Wire up playback
var player = new PlaybackController();
var trajectory = new MyTrajectoryCanvas { Run = myRun };
var metrics = new MyMetricsPanel { Run = myRun };

player.TimeChanged += () => {
    trajectory.CurrentTime = player.Time;
    metrics.CurrentTime = player.Time;
};

// 4. Add comparison
var comparison = new ComparisonView<MyTrainingRun>
{
    LeftData = runA,
    RightData = runB,
    SharedPlayer = player
};

// 5. Export
var exporter = new ExportService();
await exporter.ExportComparisonAsync(comparison, player.Time, new ExportOptions
{
    Width = 1920,
    Height = 1080,
    ShowAnnotations = true
});
```

## Adapting for Other Domains

VortexKit patterns apply to any training dynamics visualization:

| ScalarScope Domain | General Pattern | Your Domain |
|-------------------|-----------------|-------------|
| Training state | Latent representation | Model embeddings |
| Evaluator feedback | External signals | Reward/loss components |
| Eigenvalue spectrum | Covariance structure | Gradient statistics |
| Phase transitions | Regime changes | Learning rate schedules |
| Failure events | Anomalies | Divergence/instability |

## File Structure

```
VortexKit/
├── Core/
│   ├── PlaybackController.cs
│   ├── AnimatedCanvas.cs
│   ├── ComparisonView.cs
│   └── ExportService.cs
├── Annotations/
│   ├── IAnnotation.cs
│   ├── AnnotationLayer.cs
│   └── AnnotationPanel.cs
├── Failures/
│   ├── IFailureEvent.cs
│   └── FailureTimeline.cs
├── Theme/
│   ├── VortexColors.cs
│   └── VortexFonts.cs
└── Extensions/
    ├── SKCanvasExtensions.cs
    └── TimeSeriesExtensions.cs
```

## Dependencies

- SkiaSharp.Views.Maui.Controls (3.x)
- CommunityToolkit.Mvvm (8.x)
- Microsoft.Maui.Controls (8.x or 9.x)

## License

MIT License - Use freely in research and commercial applications.

---

*VortexKit: Making training dynamics legible.*
