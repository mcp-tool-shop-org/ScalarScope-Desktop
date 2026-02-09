using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Temporal Alignment Controls for comparison view.
/// Phase 3: Make Comparison the Star
/// 
/// Comparison without alignment is noise.
/// </summary>
public partial class AlignmentControl : ContentView
{
    public static readonly BindableProperty SelectedAlignmentProperty =
        BindableProperty.Create(nameof(SelectedAlignment), typeof(TemporalAlignment), typeof(AlignmentControl),
            defaultValue: TemporalAlignment.ByStep,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: OnSelectedAlignmentChanged);

    public static readonly BindableProperty AlignmentDescriptionProperty =
        BindableProperty.Create(nameof(AlignmentDescription), typeof(string), typeof(AlignmentControl),
            defaultValue: "Aligned by training step");

    public TemporalAlignment SelectedAlignment
    {
        get => (TemporalAlignment)GetValue(SelectedAlignmentProperty);
        set => SetValue(SelectedAlignmentProperty, value);
    }

    public string AlignmentDescription
    {
        get => (string)GetValue(AlignmentDescriptionProperty);
        set => SetValue(AlignmentDescriptionProperty, value);
    }

    /// <summary>
    /// Event fired when alignment changes (for animation triggers).
    /// </summary>
    public event Action<TemporalAlignment>? AlignmentChanged;

    public AlignmentControl()
    {
        InitializeComponent();
        UpdateButtonStates();
    }

    private static void OnSelectedAlignmentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AlignmentControl control)
        {
            control.UpdateButtonStates();
        }
    }

    private void UpdateButtonStates()
    {
        var activeColor = Color.FromArgb("#4ecdc4");
        var inactiveColor = Colors.Transparent;
        var activeTextColor = Color.FromArgb("#0f0f1a");
        var inactiveTextColor = Color.FromArgb("#4ecdc4");

        // By Step
        byStepBtn.BackgroundColor = SelectedAlignment == TemporalAlignment.ByStep ? activeColor : inactiveColor;
        byStepLabel.TextColor = SelectedAlignment == TemporalAlignment.ByStep ? activeTextColor : inactiveTextColor;
        byStepLabel.FontAttributes = SelectedAlignment == TemporalAlignment.ByStep ? FontAttributes.Bold : FontAttributes.None;

        // By Convergence
        byConvergenceBtn.BackgroundColor = SelectedAlignment == TemporalAlignment.ByConvergence ? activeColor : inactiveColor;
        byConvergenceLabel.TextColor = SelectedAlignment == TemporalAlignment.ByConvergence ? activeTextColor : inactiveTextColor;
        byConvergenceLabel.FontAttributes = SelectedAlignment == TemporalAlignment.ByConvergence ? FontAttributes.Bold : FontAttributes.None;

        // By First Instability
        byInstabilityBtn.BackgroundColor = SelectedAlignment == TemporalAlignment.ByFirstInstability ? activeColor : inactiveColor;
        byInstabilityLabel.TextColor = SelectedAlignment == TemporalAlignment.ByFirstInstability ? activeTextColor : inactiveTextColor;
        byInstabilityLabel.FontAttributes = SelectedAlignment == TemporalAlignment.ByFirstInstability ? FontAttributes.Bold : FontAttributes.None;

        // Update description
        alignmentDescriptionLabel.Text = AlignmentDescription;
    }

    private async void OnByStepTapped(object? sender, EventArgs e)
    {
        if (SelectedAlignment == TemporalAlignment.ByStep) return;
        
        await AnimateTransition();
        SelectedAlignment = TemporalAlignment.ByStep;
        AlignmentChanged?.Invoke(SelectedAlignment);
    }

    private async void OnByConvergenceTapped(object? sender, EventArgs e)
    {
        if (SelectedAlignment == TemporalAlignment.ByConvergence) return;
        
        await AnimateTransition();
        SelectedAlignment = TemporalAlignment.ByConvergence;
        AlignmentChanged?.Invoke(SelectedAlignment);
    }

    private async void OnByInstabilityTapped(object? sender, EventArgs e)
    {
        if (SelectedAlignment == TemporalAlignment.ByFirstInstability) return;
        
        await AnimateTransition();
        SelectedAlignment = TemporalAlignment.ByFirstInstability;
        AlignmentChanged?.Invoke(SelectedAlignment);
    }

    private async Task AnimateTransition()
    {
        // Smooth animation for alignment change
        await this.FadeTo(0.7, 100, Easing.CubicOut);
        await this.FadeTo(1.0, 150, Easing.CubicIn);
    }
}
