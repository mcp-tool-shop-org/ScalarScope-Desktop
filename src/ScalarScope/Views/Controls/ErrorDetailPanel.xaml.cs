// Phase 6.2.4: Error Detail Panel - Expanded error information display
using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 6.2: A panel component for displaying detailed error information.
/// Shows the full error explanation with troubleshooting steps.
/// </summary>
public partial class ErrorDetailPanel : ContentView
{
    private ErrorExplanation? _currentExplanation;
    
    /// <summary>
    /// Event raised when user wants to report the issue.
    /// </summary>
    public event EventHandler<ErrorExplanation>? ReportRequested;
    
    /// <summary>
    /// Event raised when the panel is closed.
    /// </summary>
    public event EventHandler? Closed;

    public ErrorDetailPanel()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Show error details in the panel.
    /// </summary>
    public void Show(ErrorExplanation explanation)
    {
        _currentExplanation = explanation;
        
        TitleLabel.Text = explanation.Title;
        SummaryLabel.Text = explanation.Summary;
        RootCauseLabel.Text = explanation.RootCause;
        ErrorCodeLabel.Text = explanation.Code;
        
        // Populate troubleshooting steps
        StepsContainer.Children.Clear();
        for (int i = 0; i < explanation.TroubleshootingSteps.Count; i++)
        {
            var step = explanation.TroubleshootingSteps[i];
            var stepView = CreateStepView(i + 1, step);
            StepsContainer.Children.Add(stepView);
        }
        
        // Show technical note if present
        if (!string.IsNullOrEmpty(explanation.TechnicalNote))
        {
            TechnicalNoteLabel.Text = explanation.TechnicalNote;
            TechnicalNoteContainer.IsVisible = true;
        }
        else
        {
            TechnicalNoteContainer.IsVisible = false;
        }
        
        // Show report button for internal errors
        ReportButton.IsVisible = explanation.ShowBugReportPrompt;
        
        PanelContainer.IsVisible = true;
    }
    
    /// <summary>
    /// Show error details from an ErrorState.
    /// </summary>
    public void Show(ErrorState error)
    {
        var explanation = ErrorExplanationService.Explain(error);
        Show(explanation);
    }
    
    /// <summary>
    /// Show error details from an exception.
    /// </summary>
    public void Show(Exception ex)
    {
        var explanation = ErrorExplanationService.ExplainException(ex);
        Show(explanation);
    }
    
    /// <summary>
    /// Hide the panel.
    /// </summary>
    public void Hide()
    {
        PanelContainer.IsVisible = false;
        _currentExplanation = null;
    }
    
    /// <summary>
    /// Check if panel is currently showing.
    /// </summary>
    public bool IsShowing => PanelContainer.IsVisible;
    
    private View CreateStepView(int number, string step)
    {
        return new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Frame
                {
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    BorderColor = Colors.Transparent,
                    CornerRadius = 12,
                    WidthRequest = 24,
                    HeightRequest = 24,
                    Padding = 0,
                    Content = new Label
                    {
                        Text = number.ToString(),
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#888888"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    }
                },
                new Label
                {
                    Text = step,
                    FontSize = 14,
                    TextColor = Color.FromArgb("#CCCCCC"),
                    VerticalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        };
    }
    
    private void OnCloseClicked(object? sender, EventArgs e)
    {
        Hide();
        Closed?.Invoke(this, EventArgs.Empty);
    }
    
    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        if (_currentExplanation is null) return;
        
        var text = _currentExplanation.ToDetailedText();
        
        try
        {
            await Clipboard.Default.SetTextAsync(text);
            
            // Visual feedback
            CopyButton.Text = "Copied!";
            await Task.Delay(1500);
            CopyButton.Text = "Copy Details";
        }
        catch
        {
            // Clipboard operation failed silently
        }
    }
    
    private void OnReportClicked(object? sender, EventArgs e)
    {
        if (_currentExplanation is not null)
        {
            ReportRequested?.Invoke(this, _currentExplanation);
        }
    }
}
