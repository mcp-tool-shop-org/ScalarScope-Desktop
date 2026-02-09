using Microsoft.Maui.Controls.Shapes;
using ScalarScope.Models;
using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 4: Insights Tray
/// Shows all teaching signals (training events + deltas) from the session.
/// Users can review what the system taught them.
/// </summary>
public partial class InsightsTray : ContentView
{
    public static readonly BindableProperty IsExpandedProperty =
        BindableProperty.Create(nameof(IsExpanded), typeof(bool), typeof(InsightsTray),
            defaultValue: false,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: OnIsExpandedChanged);

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    // Computed properties
    public int InsightCount => _bundleInsights?.Count ?? InsightFeedService.Instance.InsightCount;
    public bool HasInsights => InsightCount > 0;

    private InsightFilter _currentFilter = InsightFilter.All;
    
    // Phase 7.2: Bundle insights for review mode
    private List<InsightEvent>? _bundleInsights;
    private bool IsReviewMode => _bundleInsights != null;

    /// <summary>
    /// Fired when user clicks "Show me" on an insight.
    /// </summary>
    public event Action<InsightEvent>? ShowMeRequested;

    public InsightsTray()
    {
        InitializeComponent();
        
        // Subscribe to feed changes
        InsightFeedService.Instance.OnInsightsChanged += OnInsightsChanged;
        
        UpdateFilterButtons();
        RefreshInsightsList();
    }

    /// <summary>
    /// Phase 7.2: Set insights from a bundle (review mode).
    /// </summary>
    public void SetInsights(List<InsightEvent> insights)
    {
        _bundleInsights = insights;
        OnPropertyChanged(nameof(InsightCount));
        OnPropertyChanged(nameof(HasInsights));
        RefreshInsightsList();
    }

    /// <summary>
    /// Phase 7.2: Clear bundle insights (exit review mode).
    /// </summary>
    public void ClearBundleInsights()
    {
        _bundleInsights = null;
        OnPropertyChanged(nameof(InsightCount));
        OnPropertyChanged(nameof(HasInsights));
        RefreshInsightsList();
    }

    private static void OnIsExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is InsightsTray tray)
        {
            tray.chevronLabel.Text = (bool)newValue ? "▼" : "▲";
            
            // Phase 5.2: Debounce layout recalculation to prevent flicker
            _ = LayoutDebouncer.RequestLayoutUpdateOnMainThread(() =>
            {
                tray.InvalidateMeasure();
            });
        }
    }

    private void OnToggleTapped(object? sender, TappedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private void OnInsightsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(InsightCount));
            OnPropertyChanged(nameof(HasInsights));
            RefreshInsightsList();
        });
    }

    private void OnFilterAll(object? sender, EventArgs e)
    {
        _currentFilter = InsightFilter.All;
        UpdateFilterButtons();
        RefreshInsightsList();
    }

    private void OnFilterDeltas(object? sender, EventArgs e)
    {
        _currentFilter = InsightFilter.Deltas;
        UpdateFilterButtons();
        RefreshInsightsList();
    }

    private void OnFilterEvents(object? sender, EventArgs e)
    {
        _currentFilter = InsightFilter.Events;
        UpdateFilterButtons();
        RefreshInsightsList();
    }

    private void UpdateFilterButtons()
    {
        var activeColor = Color.FromArgb("#00d9ff");
        var inactiveColor = Color.FromArgb("#666");
        var activeBg = Color.FromArgb("#252550");
        var inactiveBg = Color.FromArgb("#1a1a2e");

        filterAllBtn.TextColor = _currentFilter == InsightFilter.All ? activeColor : inactiveColor;
        filterAllBtn.BackgroundColor = _currentFilter == InsightFilter.All ? activeBg : inactiveBg;
        
        filterDeltasBtn.TextColor = _currentFilter == InsightFilter.Deltas ? activeColor : inactiveColor;
        filterDeltasBtn.BackgroundColor = _currentFilter == InsightFilter.Deltas ? activeBg : inactiveBg;
        
        filterEventsBtn.TextColor = _currentFilter == InsightFilter.Events ? activeColor : inactiveColor;
        filterEventsBtn.BackgroundColor = _currentFilter == InsightFilter.Events ? activeBg : inactiveBg;
    }

    private void RefreshInsightsList()
    {
        insightsContainer.Children.Clear();

        IReadOnlyList<InsightEvent> insights;
        
        // Phase 7.2: Use bundle insights in review mode
        if (IsReviewMode)
        {
            insights = _currentFilter switch
            {
                InsightFilter.Deltas => _bundleInsights!
                    .Where(i => i.Category != InsightCategory.TrainingEvent).ToList(),
                InsightFilter.Events => _bundleInsights!
                    .Where(i => i.Category == InsightCategory.TrainingEvent).ToList(),
                _ => _bundleInsights!
            };
        }
        else
        {
            insights = _currentFilter switch
            {
                InsightFilter.Deltas => InsightFeedService.Instance.GetDeltas(),
                InsightFilter.Events => InsightFeedService.Instance.GetTrainingEvents(),
                _ => InsightFeedService.Instance.Insights
            };
        }

        if (insights.Count == 0)
        {
            insightsContainer.Children.Add(emptyLabel);
            emptyLabel.IsVisible = true;
            return;
        }

        emptyLabel.IsVisible = false;

        foreach (var insight in insights)
        {
            var item = CreateInsightItem(insight);
            insightsContainer.Children.Add(item);
        }
    }

    private View CreateInsightItem(InsightEvent insight)
    {
        var accentColor = Color.FromArgb(insight.AccentColor);

        var container = new Border
        {
            BackgroundColor = Color.FromArgb("#1a1a2e"),
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#2a2a4e"),
            Padding = new Thickness(8, 6),
            StrokeShape = new RoundRectangle { CornerRadius = 4 }
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
            ColumnSpacing = 8,
            RowSpacing = 2
        };

        // Icon
        var iconLabel = new Label
        {
            Text = insight.Icon,
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(iconLabel, 0);
        Grid.SetRowSpan(iconLabel, 2);
        grid.Children.Add(iconLabel);

        // Title
        var titleLabel = new Label
        {
            Text = insight.Title,
            TextColor = accentColor,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(titleLabel, 1);
        Grid.SetRow(titleLabel, 0);
        grid.Children.Add(titleLabel);

        // Description + trigger type
        var descText = insight.Description;
        if (!string.IsNullOrEmpty(insight.TriggerType))
        {
            descText = $"[{insight.TriggerType}] {descText}";
        }

        var descLabel = new Label
        {
            Text = descText,
            TextColor = Color.FromArgb("#888"),
            FontSize = 9,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(descLabel, 1);
        Grid.SetRow(descLabel, 1);
        grid.Children.Add(descLabel);

        // Confidence indicator (if available)
        if (insight.Confidence.HasValue && insight.Confidence.Value > 0)
        {
            var confidenceStack = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center
            };

            var confLabel = new Label
            {
                Text = $"{insight.Confidence.Value:P0}",
                TextColor = Color.FromArgb("#666"),
                FontSize = 8,
                HorizontalOptions = LayoutOptions.End
            };
            confidenceStack.Children.Add(confLabel);

            var confBar = new ProgressBar
            {
                Progress = insight.Confidence.Value,
                ProgressColor = accentColor,
                WidthRequest = 40,
                HeightRequest = 3
            };
            confidenceStack.Children.Add(confBar);

            Grid.SetColumn(confidenceStack, 2);
            Grid.SetRowSpan(confidenceStack, 2);
            grid.Children.Add(confidenceStack);
        }

        container.Content = grid;

        // Tap to show details
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => ShowMeRequested?.Invoke(insight);
        container.GestureRecognizers.Add(tapGesture);

        // Hover effect
        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerEntered += (s, e) => container.BackgroundColor = Color.FromArgb("#252540");
        pointerGesture.PointerExited += (s, e) => container.BackgroundColor = Color.FromArgb("#1a1a2e");
        container.GestureRecognizers.Add(pointerGesture);

        return container;
    }

    private enum InsightFilter
    {
        All,
        Deltas,
        Events
    }
}
