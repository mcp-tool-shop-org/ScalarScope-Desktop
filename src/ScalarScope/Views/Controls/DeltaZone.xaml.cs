using Microsoft.Maui.Controls.Shapes;
using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Delta Zone: Central panel showing canonical differences between two runs.
/// Phase 3: Make Comparison the Star
/// 
/// Design principles:
/// - Always visible in Compare mode
/// - Summarizes differences, not raw values
/// - Updates with timeline scrubbing
/// - Visually quieter than paths, but readable
/// - Supports hover-to-highlight source
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

    // Computed properties for binding
    public int DeltaCount => Deltas?.Count ?? 0;
    public bool HasNoDeltas => DeltaCount == 0;

    private static void OnHighlightedDeltaIdChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var zone = (DeltaZone)bindable;
        zone.UpdateHighlight((string?)newValue);
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

    public DeltaZone()
    {
        InitializeComponent();
    }

    private static void OnDeltasChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DeltaZone zone)
        {
            zone.UpdateDeltaItems();
            zone.OnPropertyChanged(nameof(DeltaCount));
            zone.OnPropertyChanged(nameof(HasNoDeltas));
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

        // Visual anchor indicator (clickable)
        var anchorLabel = new Label
        {
            Text = $"@ {delta.VisualAnchorTime:P0}",
            TextColor = Color.FromArgb("#666"),
            FontSize = 9,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(anchorLabel, 2);
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
