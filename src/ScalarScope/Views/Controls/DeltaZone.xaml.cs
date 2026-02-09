using Microsoft.Maui.Controls.Shapes;
using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Delta Zone: Central panel showing canonical differences between two runs.
/// Phase 3: Make Comparison the Star
/// Phase 4: Added "Why?" panel for teaching
/// 
/// Design principles:
/// - Always visible in Compare mode
/// - Summarizes differences, not raw values
/// - Updates with timeline scrubbing
/// - Visually quieter than paths, but readable
/// - Supports hover-to-highlight source
/// - "Why?" panel explains why a delta fired (Phase 4)
/// </summary>
public partial class DeltaZone : ContentView
{
    public static readonly BindableProperty DeltasProperty =
        BindableProperty.Create(nameof(Deltas), typeof(IReadOnlyList<CanonicalDelta>), typeof(DeltaZone),
            defaultValue: Array.Empty<CanonicalDelta>(),
            propertyChanged: OnDeltasChanged);

    public static readonly BindableProperty AlignmentDescriptionProperty =
        BindableProperty.Create(nameof(AlignmentDescription), typeof(string), typeof(DeltaZone),
            defaultValue: "By training step");

    public static readonly BindableProperty CurrentTimeProperty =
        BindableProperty.Create(nameof(CurrentTime), typeof(double), typeof(DeltaZone), 0.0);

    public static readonly BindableProperty HighlightedDeltaIdProperty =
        BindableProperty.Create(nameof(HighlightedDeltaId), typeof(string), typeof(DeltaZone),
            defaultValue: null,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: OnHighlightedDeltaIdChanged);

    public static readonly BindableProperty SelectedDeltaProperty =
        BindableProperty.Create(nameof(SelectedDelta), typeof(CanonicalDelta), typeof(DeltaZone),
            defaultValue: null,
            propertyChanged: OnSelectedDeltaChanged);

    public static readonly BindableProperty IsWhyPanelExpandedProperty =
        BindableProperty.Create(nameof(IsWhyPanelExpanded), typeof(bool), typeof(DeltaZone),
            defaultValue: false,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: OnIsWhyPanelExpandedChanged);

    public IReadOnlyList<CanonicalDelta> Deltas
    {
        get => (IReadOnlyList<CanonicalDelta>)GetValue(DeltasProperty);
        set => SetValue(DeltasProperty, value);
    }

    public string AlignmentDescription
    {
        get => (string)GetValue(AlignmentDescriptionProperty);
        set => SetValue(AlignmentDescriptionProperty, value);
    }

    public double CurrentTime
    {
        get => (double)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public string? HighlightedDeltaId
    {
        get => (string?)GetValue(HighlightedDeltaIdProperty);
        set => SetValue(HighlightedDeltaIdProperty, value);
    }

    public CanonicalDelta? SelectedDelta
    {
        get => (CanonicalDelta?)GetValue(SelectedDeltaProperty);
        set => SetValue(SelectedDeltaProperty, value);
    }

    public bool IsWhyPanelExpanded
    {
        get => (bool)GetValue(IsWhyPanelExpandedProperty);
        set => SetValue(IsWhyPanelExpandedProperty, value);
    }

    // Computed properties for binding
    public int DeltaCount => Deltas?.Count ?? 0;
    public bool HasNoDeltas => DeltaCount == 0;
    public bool HasDeltas => DeltaCount > 0;
    
    public Color SelectedDeltaColor => SelectedDelta != null 
        ? GetDeltaTypeColor(SelectedDelta.DeltaType) 
        : Color.FromArgb("#00d9ff");

    private static void OnHighlightedDeltaIdChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var zone = (DeltaZone)bindable;
        zone.UpdateHighlight((string?)newValue);
    }

    private static void OnSelectedDeltaChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var zone = (DeltaZone)bindable;
        zone.OnPropertyChanged(nameof(SelectedDeltaColor));
    }

    private void UpdateHighlight(string? deltaId)
    {
        // Visually highlight the specified delta item
        // Implementation would update visual states on delta item views
    }

    /// <summary>
    /// Event fired when user hovers over a delta item.
    /// </summary>
    public event Action<CanonicalDelta>? DeltaHovered;

    /// <summary>
    /// Event fired when user clicks a delta item.
    /// </summary>
    public event Action<CanonicalDelta>? DeltaClicked;

    /// <summary>
    /// Event fired when user clicks "Show me" in Why? panel.
    /// </summary>
    public event Action<CanonicalDelta>? ShowMeRequested;

    public DeltaZone()
    {
        InitializeComponent();
        
        // Wire up Why? panel events
        whyPanel.ShowMeRequested += delta => ShowMeRequested?.Invoke(delta);
        whyPanel.Closed += () => IsWhyPanelExpanded = false;
    }

    private void OnWhyClicked(object? sender, EventArgs e)
    {
        // If no delta selected, select the first one
        if (SelectedDelta == null && Deltas?.Count > 0)
        {
            SelectedDelta = Deltas[0];
        }
        
        IsWhyPanelExpanded = !IsWhyPanelExpanded;
    }

    private async void OnCopySummaryClicked(object? sender, EventArgs e)
    {
        if (Deltas == null || Deltas.Count == 0) return;

        var summary = BuildComparisonSummary();
        await Clipboard.SetTextAsync(summary);

        // Visual feedback
        if (sender is Button btn)
        {
            var originalText = btn.Text;
            btn.Text = "✓";
            await Task.Delay(1500);
            btn.Text = originalText;
        }
    }

    private string BuildComparisonSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Comparison Summary");
        sb.AppendLine();

        // Sort deltas by canonical hierarchy: ΔF → ΔTc → ΔTd → ΔĀ → ΔO
        var orderedDeltas = Deltas
            .Where(d => d.Status == DeltaStatus.Present)
            .OrderBy(d => d.Id switch
            {
                "delta_f" => 1,
                "delta_tc" => 2,
                "delta_td" => 3,
                "delta_a" => 4,
                "delta_o" => 5,
                _ => 99
            })
            .ToList();

        if (orderedDeltas.Count == 0)
        {
            sb.AppendLine("No significant differences detected.");
        }
        else
        {
            sb.AppendLine($"**{orderedDeltas.Count} delta(s) detected:**");
            sb.AppendLine();

            foreach (var delta in orderedDeltas)
            {
                sb.AppendLine($"## {delta.Name}");
                sb.AppendLine($"- **What:** {delta.Explanation}");
                
                // Add specific values based on delta type
                AppendDeltaSpecifics(sb, delta);
                
                if (delta.ConvergenceConfidence.HasValue)
                    sb.AppendLine($"- **Confidence:** {delta.ConvergenceConfidence:P0}");
                else if (delta.Confidence > 0 && delta.Confidence < 1)
                    sb.AppendLine($"- **Confidence:** {delta.Confidence:P0}");
                
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine($"_Delta Spec v3.2.0 | Generated at {DateTime.Now:yyyy-MM-dd HH:mm:ss}_");

        return sb.ToString();
    }

    private static void AppendDeltaSpecifics(System.Text.StringBuilder sb, CanonicalDelta delta)
    {
        switch (delta.Id)
        {
            case "delta_f":
                if (delta.FailedA == true)
                    sb.AppendLine($"- **Path A:** Failed{(delta.FailureKindA != null ? $" ({delta.FailureKindA})" : "")}");
                if (delta.FailedB == true)
                    sb.AppendLine($"- **Path B:** Failed{(delta.FailureKindB != null ? $" ({delta.FailureKindB})" : "")}");
                break;

            case "delta_tc":
                if (delta.TcA.HasValue)
                    sb.AppendLine($"- **Path A convergence:** Step {delta.TcA}");
                if (delta.TcB.HasValue)
                    sb.AppendLine($"- **Path B convergence:** Step {delta.TcB}");
                if (delta.DeltaTcSteps.HasValue)
                    sb.AppendLine($"- **Difference:** {delta.DeltaTcSteps} steps");
                break;

            case "delta_td":
                if (delta.DominanceRatioK.HasValue)
                    sb.AppendLine($"- **λ₁ dominance:** {delta.DominanceRatioK:P0}");
                if (delta.TdA.HasValue)
                    sb.AppendLine($"- **Path A onset:** Step {delta.TdA}");
                if (delta.TdB.HasValue)
                    sb.AppendLine($"- **Path B onset:** Step {delta.TdB}");
                break;

            case "delta_a":
                if (delta.MeanAlignA.HasValue)
                    sb.AppendLine($"- **Path A mean alignment:** {delta.MeanAlignA:F3}");
                if (delta.MeanAlignB.HasValue)
                    sb.AppendLine($"- **Path B mean alignment:** {delta.MeanAlignB:F3}");
                break;

            case "delta_o":
                if (delta.ScoreA.HasValue)
                    sb.AppendLine($"- **Path A oscillation:** {delta.ScoreA:F3}");
                if (delta.ScoreB.HasValue)
                    sb.AppendLine($"- **Path B oscillation:** {delta.ScoreB:F3}");
                break;
        }
    }

    private static void OnDeltasChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DeltaZone zone)
        {
            zone.UpdateDeltaItems();
            zone.OnPropertyChanged(nameof(DeltaCount));
            zone.OnPropertyChanged(nameof(HasNoDeltas));
            zone.OnPropertyChanged(nameof(HasDeltas));
            
            // Reset selection when deltas change
            zone.SelectedDelta = null;
            zone.IsWhyPanelExpanded = false;
        }
    }
    
    /// <summary>
    /// Phase 5.2: Debounce layout when Why? panel expands/collapses.
    /// </summary>
    private static void OnIsWhyPanelExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DeltaZone zone)
        {
            // Debounce layout recalculation to prevent flicker
            _ = LayoutDebouncer.RequestLayoutUpdateOnMainThread(() =>
            {
                zone.InvalidateMeasure();
            });
        }
    }

    private void UpdateDeltaItems()
    {
        deltaItemsContainer.Children.Clear();

        if (Deltas == null || Deltas.Count == 0)
            return;

        foreach (var delta in Deltas)
        {
            var item = CreateDeltaItem(delta);
            deltaItemsContainer.Children.Add(item);
        }
    }

    private View CreateDeltaItem(CanonicalDelta delta)
    {
        var accentColor = GetDeltaTypeColor(delta.DeltaType);

        var container = new Border
        {
            BackgroundColor = Color.FromArgb("#1a1a2e"),
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#2a2a4e"),
            Padding = new Thickness(10, 8),
            StrokeShape = new RoundRectangle { CornerRadius = 6 }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnSpacing = 10,
            RowSpacing = 3
        };

        // Type indicator
        var typeIndicator = new BoxView
        {
            WidthRequest = 3,
            HeightRequest = 28,
            Color = accentColor,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(typeIndicator, 0);
        Grid.SetRowSpan(typeIndicator, 2);
        grid.Children.Add(typeIndicator);

        // Delta name
        var nameLabel = new Label
        {
            Text = delta.Name,
            TextColor = accentColor,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold
        };
        Grid.SetColumn(nameLabel, 1);
        Grid.SetRow(nameLabel, 0);
        grid.Children.Add(nameLabel);

        // Explanation
        var explanationLabel = new Label
        {
            Text = delta.Explanation,
            TextColor = Color.FromArgb("#aaa"),
            FontSize = 10,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(explanationLabel, 1);
        Grid.SetRow(explanationLabel, 1);
        grid.Children.Add(explanationLabel);

        // Info button (Why? for this specific delta)
        var infoButton = new Button
        {
            Text = "?",
            FontSize = 10,
            TextColor = Color.FromArgb("#666"),
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(6, 2),
            WidthRequest = 24,
            HeightRequest = 24,
            VerticalOptions = LayoutOptions.Center
        };
        infoButton.Clicked += (s, e) =>
        {
            SelectedDelta = delta;
            IsWhyPanelExpanded = true;
        };
        Grid.SetColumn(infoButton, 2);
        Grid.SetRowSpan(infoButton, 2);
        grid.Children.Add(infoButton);

        // Visual anchor indicator (clickable)
        var anchorLabel = new Label
        {
            Text = $"@ {delta.VisualAnchorTime:P0}",
            TextColor = Color.FromArgb("#666"),
            FontSize = 9,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(anchorLabel, 3);
        Grid.SetRowSpan(anchorLabel, 2);
        grid.Children.Add(anchorLabel);

        container.Content = grid;

        // Interactions
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => DeltaClicked?.Invoke(delta);
        container.GestureRecognizers.Add(tapGesture);

        // Pointer hover for highlighting (desktop)
        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerEntered += (s, e) =>
        {
            container.BackgroundColor = Color.FromArgb("#252540");
            DeltaHovered?.Invoke(delta);
        };
        pointerGesture.PointerExited += (s, e) =>
        {
            container.BackgroundColor = Color.FromArgb("#1a1a2e");
        };
        container.GestureRecognizers.Add(pointerGesture);

        return container;
    }

    private static Color GetDeltaTypeColor(DeltaType type) => type switch
    {
        DeltaType.Event => Color.FromArgb("#ff6b6b"),      // Red for failures/events
        DeltaType.Timing => Color.FromArgb("#4ecdc4"),     // Teal for timing
        DeltaType.Structure => Color.FromArgb("#a29bfe"), // Purple for structure
        DeltaType.Behavior => Color.FromArgb("#ffd93d"),  // Yellow for behavior
        _ => Color.FromArgb("#888")
    };
}
