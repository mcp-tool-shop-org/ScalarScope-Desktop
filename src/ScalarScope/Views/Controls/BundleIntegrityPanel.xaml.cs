// Phase 7.2.3: Bundle Integrity Panel
// Displays bundle hash and verification status.

using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 7.2: Panel for displaying and verifying bundle integrity.
/// Shows bundle hash and verification status.
/// </summary>
public partial class BundleIntegrityPanel : ContentView
{
    private string? _bundlePath;
    private string? _bundleHash;
    private BundleIntegrityVerification? _verification;
    private bool _isVerified;
    
    public BundleIntegrityPanel()
    {
        InitializeComponent();
        BindingContext = this;
    }
    
    #region Bindable Properties
    
    public string? BundlePath
    {
        get => _bundlePath;
        set
        {
            _bundlePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowVerifyButton));
        }
    }
    
    public string? BundleHash
    {
        get => _bundleHash;
        set
        {
            _bundleHash = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayHash));
            OnPropertyChanged(nameof(HasBundleHash));
        }
    }
    
    #endregion
    
    #region Computed Properties
    
    public bool HasBundleHash => !string.IsNullOrEmpty(_bundleHash);
    
    public string DisplayHash => _bundleHash?.Length > 32 
        ? $"{_bundleHash[..16]}...{_bundleHash[^8..]}" 
        : _bundleHash ?? "";
    
    public string StatusText => _verification switch
    {
        null when _isVerified => "Verified ✓",
        null => "Not verified",
        { IsValid: true } => "Verified ✓",
        { HasErrors: true } => "Failed ✗",
        { HasWarnings: true } => "Warnings ⚠",
        _ => "Unknown"
    };
    
    public Color StatusColor => _verification switch
    {
        null when _isVerified => Color.FromArgb("#10B981"),
        null => Color.FromArgb("#888"),
        { IsValid: true } => Color.FromArgb("#10B981"),
        { HasErrors: true } => Color.FromArgb("#EF4444"),
        { HasWarnings: true } => Color.FromArgb("#F59E0B"),
        _ => Color.FromArgb("#888")
    };
    
    public bool HasVerification => _verification is not null;
    
    public string FileCountText => _verification is not null 
        ? $"{_verification.ActualFileCount}/{_verification.ExpectedFileCount}" 
        : "";
    
    public bool HasIssues => _verification?.Issues.Count > 0;
    
    public bool ShowVerifyButton => !string.IsNullOrEmpty(_bundlePath) && !_isVerified;
    
    public bool HasTimestamp => _verification?.VerifiedAt != default;
    
    public string TimestampText => _verification?.VerifiedAt.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Set verification result from an already completed verification.
    /// </summary>
    public void SetVerification(BundleIntegrityVerification verification)
    {
        _verification = verification;
        _isVerified = verification.IsValid;
        BundleHash = verification.ActualBundleHash;
        
        UpdateIssuesDisplay();
        UpdateAllProperties();
    }
    
    /// <summary>
    /// Set just the bundle hash (e.g., after export).
    /// </summary>
    public void SetExportedHash(string hash)
    {
        _bundleHash = hash;
        _isVerified = true;
        _verification = null;
        
        UpdateAllProperties();
    }
    
    /// <summary>
    /// Reset the panel.
    /// </summary>
    public void Reset()
    {
        _bundlePath = null;
        _bundleHash = null;
        _verification = null;
        _isVerified = false;
        
        issuesContainer.Children.Clear();
        UpdateAllProperties();
    }
    
    #endregion
    
    #region Event Handlers
    
    private async void OnCopyHashClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_bundleHash)) return;
        
        await Clipboard.SetTextAsync(_bundleHash);
        
        // Visual feedback
        if (sender is Button btn)
        {
            var originalText = btn.Text;
            btn.Text = "✓";
            await Task.Delay(1000);
            btn.Text = originalText;
        }
    }
    
    private async void OnVerifyClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_bundlePath)) return;
        
        verifyButton.IsEnabled = false;
        verifyButton.Text = "Verifying...";
        
        try
        {
            var verification = await BundleIntegrityService.VerifyBundleAsync(_bundlePath);
            SetVerification(verification);
        }
        catch (Exception ex)
        {
            // Create a failed verification
            var failedVerification = new BundleIntegrityVerification
            {
                BundlePath = _bundlePath,
                VerifiedAt = DateTime.UtcNow,
                IsValid = false
            };
            failedVerification.AddError("VerificationFailed", $"Verification failed: {ex.Message}");
            SetVerification(failedVerification);
            
            ErrorLoggingService.Instance.Log(ex, "BundleIntegrityPanel");
        }
        finally
        {
            verifyButton.IsEnabled = true;
            verifyButton.Text = "Verify Integrity";
        }
    }
    
    #endregion
    
    #region Private Methods
    
    private void UpdateIssuesDisplay()
    {
        issuesContainer.Children.Clear();
        
        if (_verification?.Issues.Count > 0)
        {
            foreach (var issue in _verification.Issues)
            {
                var color = issue.Severity == IntegrityIssueSeverity.Error 
                    ? Color.FromArgb("#EF4444") 
                    : Color.FromArgb("#F59E0B");
                
                var label = new Label
                {
                    Text = $"• {issue.Message}",
                    TextColor = color,
                    FontSize = 10,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                
                issuesContainer.Children.Add(label);
            }
        }
    }
    
    private void UpdateAllProperties()
    {
        OnPropertyChanged(nameof(BundlePath));
        OnPropertyChanged(nameof(BundleHash));
        OnPropertyChanged(nameof(HasBundleHash));
        OnPropertyChanged(nameof(DisplayHash));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(HasVerification));
        OnPropertyChanged(nameof(FileCountText));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(ShowVerifyButton));
        OnPropertyChanged(nameof(HasTimestamp));
        OnPropertyChanged(nameof(TimestampText));
    }
    
    #endregion
    
    #region Events
    
    /// <summary>
    /// Raised when verification is complete.
    /// </summary>
    public event EventHandler<BundleIntegrityVerification>? VerificationCompleted;
    
    #endregion
}
