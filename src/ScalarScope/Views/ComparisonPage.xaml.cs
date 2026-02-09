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
        
        // Wire up Phase 7.2 bundle import events
        SetupBundleHandlers();
    }

    /// <summary>
    /// Phase 7.2: Open Bundle file picker and import.
    /// </summary>
    private async void OnOpenBundleClicked(object? sender, EventArgs e)
    {
        await ErrorBoundary.TrySafeAsync(async () =>
        {
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".scbundle" } },
                { DevicePlatform.macOS, new[] { "scbundle" } },
                { DevicePlatform.iOS, new[] { "public.data" } },
                { DevicePlatform.Android, new[] { "application/octet-stream" } }
            });

            var options = new PickOptions
            {
                PickerTitle = "Open Comparison Bundle",
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);
            if (result == null) return;

            // Show loading indicator
            openBundleButton.IsEnabled = false;
            openBundleButton.Text = "Loading...";

            try
            {
                // Import the bundle
                var importResult = await BundleImportService.Instance.ImportAsync(result.FullPath);
                
                if (importResult.Success && importResult.LoadedBundle != null)
                {
                    // Hydrate the UI with bundle data
                    await HydrateBundleAsync(importResult.LoadedBundle);
                }
                else
                {
                    // Show error
                    var errorMessage = importResult.ErrorMessage ?? "Failed to import bundle";
                    if (importResult.ErrorExplanation != null)
                    {
                        errorMessage += $"\n\n{importResult.ErrorExplanation.Summary}";
                    }
                    
                    await DisplayAlert("Import Failed", errorMessage, "OK");
                }
            }
            finally
            {
                openBundleButton.IsEnabled = true;
                openBundleButton.Text = "📦 Open Bundle...";
            }
        }, "Bundle open");
    }

    /// <summary>
    /// Phase 7.2: Hydrate UI with bundle data.
    /// </summary>
    private async Task HydrateBundleAsync(LoadedBundle bundle)
    {
        // Set review mode in ViewModel
        ViewModel.EnterReviewMode(bundle);
        
        // Update delta zone with bundled deltas
        deltaZone.Deltas = bundle.Deltas;
        
        // Update insights tray with bundled insights
        if (bundle.Insights != null)
        {
            insightsTray.SetInsights(bundle.Insights);
        }
        
        // Show review mode banner
        await DisplayAlert(
            "Bundle Loaded",
            $"Loaded {bundle.Manifest.Profile} bundle\n" +
            $"Created: {bundle.Manifest.CreatedAt:g}\n" +
            $"Deltas: {bundle.Deltas.Count}\n" +
            $"Badge: {bundle.ReproducibilityBadge}",
            "OK");
    }

    private void SetupBundleHandlers()
    {
        // Listen for bundle unload
        BundleImportService.Instance.BundleUnloaded += (s, e) =>
        {
            if (ViewModel.IsReviewMode)
            {
                ViewModel.ExitReviewMode();
            }
        };
        
        // Wire up review mode banner exit
        reviewModeBanner.ExitRequested += OnExitReviewModeRequested;
    }

    /// <summary>
    /// Phase 7.2: Handle exit review mode request.
    /// </summary>
    private void OnExitReviewModeRequested(object? sender, EventArgs e)
    {
        ViewModel.ExitReviewMode();
        
        // Clear insights tray bundle mode
        insightsTray.ClearBundleInsights();
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

    private async void OnShowMeRequested(CanonicalDelta delta)
    {
        // Phase 5.5: Wrap navigation in error boundary
        await ErrorBoundary.TrySafeAsync(async () =>
        {
            // Phase 5.2: Use choreographed navigation with smooth seek and highlight pulse
            var startTime = ViewModel.Player.Time;
            var targetTime = delta.VisualAnchorTime > 0 ? delta.VisualAnchorTime : startTime;
            
            await TransitionService.NavigateToAnchor(
                targetTime: targetTime,
                highlightElementId: delta.Id,
                onSeek: t =>
                {
                    // Lerp from current position to target
                    var interpolated = startTime + (targetTime - startTime) * (t / targetTime);
                    ViewModel.Player.Time = Math.Min(interpolated, targetTime);
                },
                onHighlight: id => ViewModel.HighlightedDeltaId = id
            );
        }, "ShowMe navigation");
    }

    private async void OnInsightShowMeRequested(Models.InsightEvent insight)
    {
        // Phase 5.5: Wrap navigation in error boundary
        await ErrorBoundary.TrySafeAsync(async () =>
        {
            // Navigate to target view if needed
            if (insight.TargetView != null && insight.TargetView != "compare")
            {
                await Shell.Current.GoToAsync($"//{insight.TargetView}");
                return;
            }

            // Phase 5.2: Use choreographed navigation
            var startTime = ViewModel.Player.Time;
            var targetTime = insight.AnchorTime ?? startTime;
            
            await TransitionService.NavigateToAnchor(
                targetTime: targetTime,
                highlightElementId: insight.DeltaId,
                onSeek: t =>
                {
                    var interpolated = startTime + (targetTime - startTime) * (t / targetTime);
                    ViewModel.Player.Time = Math.Min(interpolated, targetTime);
                },
                onHighlight: id =>
                {
                    if (id != null) ViewModel.HighlightedDeltaId = id;
                }
            );
        }, "Insight navigation");
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
