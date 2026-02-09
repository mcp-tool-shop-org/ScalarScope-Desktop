using ScalarScope.Services;
using ScalarScope.ViewModels;

namespace ScalarScope.Views;

public partial class ComparisonPage : ContentPage
{
    // Use the shared comparison instance from App
    private ComparisonViewModel ViewModel => App.Comparison;

    public ComparisonPage()
    {
        InitializeComponent();
        BindingContext = App.Comparison;
        
        // Wire up Phase 4 insight events
        SetupInsightHandlers();
    }

    private void OnHighlightDeltaRequested(HelpPage sender, string deltaType)
    {
        // Map deltaType string to delta ID
        var deltaId = deltaType switch
        {
            "failure" => "delta_f",
            "convergence" => "delta_tc",
            "dominance" => "delta_td",
            "alignment" => "delta_a",
            "oscillation" => "delta_o",
            _ => null
        };

        if (deltaId != null)
        {
            // Highlight the delta in DeltaZone
            ViewModel.HighlightedDeltaId = deltaId;
            
            // Expand the Why? panel for this delta
            var delta = ViewModel.CanonicalDeltas?.FirstOrDefault(d => d.Id == deltaId);
            if (delta != null)
            {
                deltaZone.SelectedDelta = delta;
                deltaZone.IsWhyPanelExpanded = true;
            }
        }
    }

    private void SetupInsightHandlers()
    {
        // DeltaZone "Show me" navigates to anchor
        deltaZone.ShowMeRequested += OnShowMeRequested;
        
        // InsightsTray "Show me" navigates to insight source
        insightsTray.ShowMeRequested += OnInsightShowMeRequested;
        
        // Subscribe to deltas changes to publish insights and set idle calming
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.CanonicalDeltas))
            {
                PublishDeltaInsights();
                
                // Phase 5.1: Slow down demo animations when deltas are visible
                var hasDeltasVisible = ViewModel.CanonicalDeltas?.Any(d => d.Status == DeltaStatus.Present) ?? false;
                DemoStateService.Instance.SetIdleCalming(hasDeltasVisible);
            }
        };
    }

    private void OnShowMeRequested(CanonicalDelta delta)
    {
        // Highlight the delta's visual anchor
        ViewModel.HighlightedDeltaId = delta.Id;
        
        // If there's an anchor time, seek to it
        if (delta.VisualAnchorTime > 0)
        {
            ViewModel.Player.Time = delta.VisualAnchorTime;
        }
    }

    private void OnInsightShowMeRequested(Models.InsightEvent insight)
    {
        // Navigate to target view if needed
        if (insight.TargetView != null && insight.TargetView != "compare")
        {
            Shell.Current.GoToAsync($"//{insight.TargetView}");
            return;
        }

        // If it's a delta insight, highlight it
        if (insight.DeltaId != null)
        {
            ViewModel.HighlightedDeltaId = insight.DeltaId;
        }

        // Seek to anchor time
        if (insight.AnchorTime.HasValue && insight.AnchorTime.Value > 0)
        {
            ViewModel.Player.Time = insight.AnchorTime.Value;
        }
    }

    private void PublishDeltaInsights()
    {
        if (ViewModel.CanonicalDeltas == null) return;

        foreach (var delta in ViewModel.CanonicalDeltas)
        {
            if (delta.Status == DeltaStatus.Present)
            {
                // Determine trigger type based on delta type
                var triggerType = DetermineTriggerType(delta);
                InsightFeedService.Instance.PublishDelta(delta, triggerType);
            }
        }
    }

    private static string? DetermineTriggerType(CanonicalDelta delta)
    {
        return delta.Id switch
        {
            "delta_td" => delta.DominanceRatioK > 0.5 ? "sustained" : "recurrence",
            "delta_a" => "persistence_weighted",
            "delta_o" => "area_episode",
            "delta_tc" => delta.ConvergenceConfidence.HasValue ? "step_difference" : "one_run_converged",
            "delta_f" => (delta.FailedA == true || delta.FailedB == true) ? "event" : "proxy",
            _ => null
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Update delta label when time changes
        ViewModel.Player.TimeChanged += UpdateDeltaLabel;
        // Trigger initial update if runs are loaded
        UpdateDeltaLabel();
        
        // Re-subscribe to MessagingCenter (may have been unsubscribed)
        MessagingCenter.Subscribe<HelpPage, string>(this, "HighlightDelta", OnHighlightDeltaRequested);
    }

    private void UpdateDeltaLabel()
    {
        if (!ViewModel.HasBothRuns) return;

        var metrics = ViewModel.GetCurrentMetrics();
        var deltaSign = metrics.FirstFactorDelta >= 0 ? "+" : "";

        deltaLabel.Text = $"Δλ₁: {deltaSign}{metrics.FirstFactorDelta:P0}";

        // Color based on whether Path B shows improvement
        deltaLabel.TextColor = metrics.FirstFactorDelta > 0.1
            ? Color.FromArgb("#4ecdc4")  // Green - Path B better
            : metrics.FirstFactorDelta < -0.1
                ? Color.FromArgb("#ff6b6b")  // Red - Path A better
                : Color.FromArgb("#ffd93d"); // Yellow - similar
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ViewModel.Player.TimeChanged -= UpdateDeltaLabel;
        
        // Unsubscribe from MessagingCenter to prevent memory leaks
        MessagingCenter.Unsubscribe<HelpPage, string>(this, "HighlightDelta");
    }
}
