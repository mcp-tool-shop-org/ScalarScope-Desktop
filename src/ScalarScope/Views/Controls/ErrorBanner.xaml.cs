// Phase 6.2.4: Error Banner - Dismissable error notification component
using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 6.2: A banner component for displaying errors at the top of pages.
/// Supports dismissal, detailed view, and auto-fade options.
/// </summary>
public partial class ErrorBanner : ContentView
{
    private ErrorState? _currentError;
    private ErrorExplanation? _currentExplanation;
    private CancellationTokenSource? _autoHideCts;
    
    /// <summary>
    /// Event raised when user wants to see error details.
    /// </summary>
    public event EventHandler<ErrorExplanation>? DetailsRequested;
    
    /// <summary>
    /// Event raised when the banner is dismissed.
    /// </summary>
    public event EventHandler? Dismissed;
    
    /// <summary>
    /// Whether to auto-hide recoverable errors after a delay.
    /// </summary>
    public bool AutoHideRecoverable { get; set; } = true;
    
    /// <summary>
    /// Auto-hide delay in milliseconds for recoverable errors.
    /// </summary>
    public int AutoHideDelayMs { get; set; } = 8000;
    
    public ErrorBanner()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Show an error in the banner.
    /// </summary>
    public void Show(ErrorState error)
    {
        _currentError = error;
        _currentExplanation = ErrorExplanationService.Explain(error);
        
        TitleLabel.Text = error.UserTitle;
        MessageLabel.Text = error.UserExplanation;
        
        // Update banner color based on severity
        UpdateBannerColor(error.Severity);
        
        ErrorContainer.IsVisible = true;
        
        // Auto-hide recoverable errors
        if (AutoHideRecoverable && error.IsRecoverable)
        {
            StartAutoHideTimer();
        }
    }
    
    /// <summary>
    /// Show an error from an exception.
    /// </summary>
    public void Show(Exception ex)
    {
        var errorState = ErrorStateMapping.MapException(ex);
        Show(errorState);
    }
    
    /// <summary>
    /// Show an error explanation directly.
    /// </summary>
    public void Show(ErrorExplanation explanation)
    {
        _currentExplanation = explanation;
        _currentError = null;
        
        TitleLabel.Text = explanation.Title;
        MessageLabel.Text = explanation.Summary;
        
        ErrorContainer.IsVisible = true;
    }
    
    /// <summary>
    /// Hide the error banner.
    /// </summary>
    public void Hide()
    {
        CancelAutoHide();
        ErrorContainer.IsVisible = false;
        _currentError = null;
        _currentExplanation = null;
    }
    
    /// <summary>
    /// Check if banner is currently showing.
    /// </summary>
    public bool IsShowing => ErrorContainer.IsVisible;
    
    private void UpdateBannerColor(ErrorSeverity severity)
    {
        var (startColor, endColor) = severity switch
        {
            ErrorSeverity.Critical => ("#B22222", "#8B0000"),
            ErrorSeverity.Error => ("#CD5C5C", "#B22222"),
            ErrorSeverity.Warning => ("#DAA520", "#B8860B"),
            ErrorSeverity.Info => ("#4682B4", "#2F5F8F"),
            _ => ("#CD5C5C", "#B22222")
        };
        
        ErrorContainer.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop { Color = Color.FromArgb(startColor), Offset = 0 },
                new GradientStop { Color = Color.FromArgb(endColor), Offset = 1 }
            }
        };
    }
    
    private void StartAutoHideTimer()
    {
        CancelAutoHide();
        _autoHideCts = new CancellationTokenSource();
        
        Task.Delay(AutoHideDelayMs, _autoHideCts.Token)
            .ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    MainThread.BeginInvokeOnMainThread(Hide);
                }
            });
    }
    
    private void CancelAutoHide()
    {
        _autoHideCts?.Cancel();
        _autoHideCts?.Dispose();
        _autoHideCts = null;
    }
    
    private void OnDetailsClicked(object? sender, EventArgs e)
    {
        if (_currentExplanation is not null)
        {
            DetailsRequested?.Invoke(this, _currentExplanation);
        }
    }
    
    private void OnDismissClicked(object? sender, EventArgs e)
    {
        Hide();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }
}
