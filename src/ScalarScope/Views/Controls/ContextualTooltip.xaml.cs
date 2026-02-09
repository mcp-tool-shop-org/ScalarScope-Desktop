using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 2: Contextual tooltip UI.
/// Shows event-triggered explanations that appear once per session.
/// Dismissible and non-blocking.
/// </summary>
public partial class ContextualTooltip : ContentView
{
    private ContextualExplanation? _currentExplanation;

    public ContextualTooltip()
    {
        InitializeComponent();
        
        // Subscribe to explanation events
        ContextualExplanationService.Instance.OnExplanationTriggered += OnExplanationTriggered;
    }

    private void OnExplanationTriggered(ContextualExplanation explanation)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentExplanation = explanation;
            
            titleLabel.Text = explanation.Title;
            messageLabel.Text = explanation.Message;
            hintLabel.Text = explanation.VisualHint;
            
            // Show with fade-in
            Opacity = 0;
            IsVisible = true;
            this.FadeTo(1, 200, Easing.CubicOut);
            
            // Auto-dismiss after 15 seconds if not interacted with
            Task.Delay(15000).ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (_currentExplanation == explanation && IsVisible)
                    {
                        Dismiss();
                    }
                });
            });
        });
    }

    private void OnDismissClicked(object? sender, EventArgs e)
    {
        Dismiss();
    }

    private async void Dismiss()
    {
        await this.FadeTo(0, 150, Easing.CubicIn);
        IsVisible = false;
        _currentExplanation = null;
    }

    /// <summary>
    /// Manually show an explanation.
    /// </summary>
    public void Show(ContextualExplanation explanation)
    {
        OnExplanationTriggered(explanation);
    }
}
