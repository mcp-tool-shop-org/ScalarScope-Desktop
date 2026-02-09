using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 4: First-occurrence visual grammar hints.
/// Shows contextual hints near the relevant UI element on first encounter.
/// Each hint is ≤12 words and dismissible forever.
/// </summary>
public partial class DeltaHints : ContentView
{
    public static readonly BindableProperty DeltasProperty =
        BindableProperty.Create(nameof(Deltas), typeof(IReadOnlyList<CanonicalDelta>), typeof(DeltaHints),
            defaultValue: null,
            propertyChanged: OnDeltasChanged);

    public IReadOnlyList<CanonicalDelta>? Deltas
    {
        get => (IReadOnlyList<CanonicalDelta>?)GetValue(DeltasProperty);
        set => SetValue(DeltasProperty, value);
    }

    // Computed properties for hint visibility
    public bool ShowFailureHint => ShouldShowHint("delta_f", "hint.delta.failure.first");
    public bool ShowConvergenceHint => ShouldShowHint("delta_tc", "hint.delta.convergence.first");
    public bool ShowEmergenceHint => ShouldShowHint("delta_td", "hint.delta.emergence.first");
    public bool ShowAlignmentHint => ShouldShowHint("delta_a", "hint.delta.alignment.first");
    public bool ShowStabilityHint => ShouldShowHint("delta_o", "hint.delta.stability.first");

    public DeltaHints()
    {
        InitializeComponent();
    }

    private static void OnDeltasChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DeltaHints hints)
        {
            hints.UpdateHintVisibility();
        }
    }

    private void UpdateHintVisibility()
    {
        OnPropertyChanged(nameof(ShowFailureHint));
        OnPropertyChanged(nameof(ShowConvergenceHint));
        OnPropertyChanged(nameof(ShowEmergenceHint));
        OnPropertyChanged(nameof(ShowAlignmentHint));
        OnPropertyChanged(nameof(ShowStabilityHint));
    }

    private bool ShouldShowHint(string deltaId, string hintId)
    {
        // Don't show if already dismissed
        if (UserPreferencesService.IsHintDismissed(hintId))
            return false;

        // Only show if this delta type is present in current comparison
        if (Deltas == null)
            return false;

        return Deltas.Any(d => d.Id == deltaId && d.Status == DeltaStatus.Present);
    }
}
