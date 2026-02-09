// Phase 7.2.2: Bundle Export Panel
// UI for exporting comparison bundles with profile selection.

using ScalarScope.Models;
using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 7.2: Panel for exporting comparison bundles.
/// Provides profile selection and content preview before export.
/// </summary>
public partial class BundleExportPanel : ContentView
{
    private BundleProfile _selectedProfile = BundleProfile.Review;
    private DeltaComputationResult? _comparisonResult;
    private string? _trajectory1Path;
    private string? _trajectory2Path;
    private ImportPreset? _preset;
    private List<InsightEvent>? _insights;
    
    public BundleExportPanel()
    {
        InitializeComponent();
        BindingContext = this;
    }
    
    #region Bindable Properties
    
    public static readonly BindableProperty IsVisibleProperty =
        BindableProperty.Create(nameof(IsVisible), typeof(bool), typeof(BundleExportPanel), false);
    
    public new bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }
    
    #endregion
    
    #region Profile Selection Properties
    
    public bool IsShareSelected
    {
        get => _selectedProfile == BundleProfile.Share;
        set
        {
            if (value) _selectedProfile = BundleProfile.Share;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsReviewSelected));
            OnPropertyChanged(nameof(IsAuditSelected));
            UpdateProfileVisuals();
        }
    }
    
    public bool IsReviewSelected
    {
        get => _selectedProfile == BundleProfile.Review;
        set
        {
            if (value) _selectedProfile = BundleProfile.Review;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsShareSelected));
            OnPropertyChanged(nameof(IsAuditSelected));
            UpdateProfileVisuals();
        }
    }
    
    public bool IsAuditSelected
    {
        get => _selectedProfile == BundleProfile.Audit;
        set
        {
            if (value) _selectedProfile = BundleProfile.Audit;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsShareSelected));
            OnPropertyChanged(nameof(IsReviewSelected));
            UpdateProfileVisuals();
        }
    }
    
    #endregion
    
    #region Profile Visual Properties
    
    public Color ShareProfileBackground => IsShareSelected ? Color.FromArgb("#1a2a3a") : Color.FromArgb("#12121f");
    public Color ShareProfileBorder => IsShareSelected ? Color.FromArgb("#ffd93d") : Color.FromArgb("#2a2a4e");
    public Color ReviewProfileBackground => IsReviewSelected ? Color.FromArgb("#1a2a3a") : Color.FromArgb("#12121f");
    public Color ReviewProfileBorder => IsReviewSelected ? Color.FromArgb("#ffd93d") : Color.FromArgb("#2a2a4e");
    public Color AuditProfileBackground => IsAuditSelected ? Color.FromArgb("#1a2a3a") : Color.FromArgb("#12121f");
    public Color AuditProfileBorder => IsAuditSelected ? Color.FromArgb("#ffd93d") : Color.FromArgb("#2a2a4e");
    
    #endregion
    
    #region Contents Preview Properties
    
    public string InsightsIncludedText => _selectedProfile != BundleProfile.Share 
        ? "✓ insights/insights.json" 
        : "○ insights/ (not in Share profile)";
    
    public Color InsightsIncludedColor => _selectedProfile != BundleProfile.Share 
        ? Color.FromArgb("#10B981") 
        : Color.FromArgb("#666");
    
    public string AssetsIncludedText => _selectedProfile != BundleProfile.Share 
        ? "✓ assets/ (screenshots, cards)" 
        : "○ assets/ (not in Share profile)";
    
    public Color AssetsIncludedColor => _selectedProfile != BundleProfile.Share 
        ? Color.FromArgb("#10B981") 
        : Color.FromArgb("#666");
    
    public string AuditIncludedText => "✓ audit/audit.json";
    public Color AuditIncludedColor => Color.FromArgb("#10B981");
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Initialize the panel with comparison data.
    /// </summary>
    public void Initialize(
        DeltaComputationResult? result,
        string? trajectory1Path,
        string? trajectory2Path,
        ImportPreset? preset = null,
        List<InsightEvent>? insights = null)
    {
        _comparisonResult = result;
        _trajectory1Path = trajectory1Path;
        _trajectory2Path = trajectory2Path;
        _preset = preset;
        _insights = insights;
    }
    
    /// <summary>
    /// Show the export panel.
    /// </summary>
    public void Show()
    {
        IsVisible = true;
    }
    
    /// <summary>
    /// Hide the export panel.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnCloseClicked(object? sender, EventArgs e)
    {
        Hide();
    }
    
    private void OnProfileChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) return; // Only handle when checked
        UpdateProfileVisuals();
    }
    
    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (_comparisonResult is null)
        {
            await ShowError("No comparison result to export");
            return;
        }
        
        // Show progress
        exportButton.IsEnabled = false;
        statusPanel.IsVisible = true;
        statusLabel.Text = "Creating bundle...";
        
        try
        {
            // Create the bundle
            var bundle = ComparisonBundleService.Instance.CreateBundle(
                _comparisonResult,
                _trajectory1Path,
                _trajectory2Path,
                _selectedProfile,
                _preset,
                _selectedProfile != BundleProfile.Share ? _insights : null,
                notesEditor.Text);
            
            statusLabel.Text = "Generating assets...";
            
            // Generate assets for Review/Audit profiles
            List<BundleAsset>? assets = null;
            if (_selectedProfile != BundleProfile.Share)
            {
                assets = await GenerateAssetsAsync();
            }
            
            // Generate audit bundle for Audit profile
            ReproAuditBundle? auditBundle = null;
            if (_selectedProfile == BundleProfile.Audit)
            {
                statusLabel.Text = "Creating audit bundle...";
                auditBundle = ReproAuditExportService.Instance.CreateBundle(
                    _comparisonResult,
                    _trajectory1Path,
                    _trajectory2Path,
                    _preset,
                    notesEditor.Text);
            }
            
            statusLabel.Text = "Exporting bundle...";
            
            // Pick save location
            var outputPath = await PickSaveLocationAsync();
            if (outputPath is null)
            {
                // User cancelled
                exportButton.IsEnabled = true;
                statusPanel.IsVisible = false;
                return;
            }
            
            // Export the bundle
            var result = await ComparisonBundleService.Instance.ExportAsync(
                bundle,
                outputPath,
                assets,
                auditBundle);
            
            if (result.Success)
            {
                statusLabel.Text = "Bundle exported successfully!";
                progressIndicator.IsRunning = false;
                
                // Show success toast with hash
                await ShowSuccess(result);
                
                Hide();
            }
            else
            {
                await ShowError(result.ErrorMessage ?? "Export failed");
            }
        }
        catch (Exception ex)
        {
            ErrorLoggingService.Instance.Log(ex, "BundleExport");
            await ShowError($"Export failed: {ex.Message}");
        }
        finally
        {
            exportButton.IsEnabled = true;
            statusPanel.IsVisible = false;
            progressIndicator.IsRunning = true;
        }
    }
    
    #endregion
    
    #region Private Methods
    
    private void UpdateProfileVisuals()
    {
        OnPropertyChanged(nameof(ShareProfileBackground));
        OnPropertyChanged(nameof(ShareProfileBorder));
        OnPropertyChanged(nameof(ReviewProfileBackground));
        OnPropertyChanged(nameof(ReviewProfileBorder));
        OnPropertyChanged(nameof(AuditProfileBackground));
        OnPropertyChanged(nameof(AuditProfileBorder));
        OnPropertyChanged(nameof(InsightsIncludedText));
        OnPropertyChanged(nameof(InsightsIncludedColor));
        OnPropertyChanged(nameof(AssetsIncludedText));
        OnPropertyChanged(nameof(AssetsIncludedColor));
    }
    
    private async Task<List<BundleAsset>> GenerateAssetsAsync()
    {
        var assets = new List<BundleAsset>();
        
        // For now, we'll add placeholder assets
        // In a full implementation, this would capture screenshots
        // or generate social cards using existing services
        
        // Placeholder: Add a text asset with comparison info
        var infoText = $"Comparison exported at {DateTime.UtcNow:O}";
        assets.Add(new BundleAsset
        {
            FileName = "export-info.txt",
            ContentType = "text/plain",
            Data = System.Text.Encoding.UTF8.GetBytes(infoText)
        });
        
        return assets;
    }
    
    private async Task<string?> PickSaveLocationAsync()
    {
        try
        {
            // Use default location in documents
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var scalarScopePath = Path.Combine(documentsPath, "ScalarScope", "Bundles");
            Directory.CreateDirectory(scalarScopePath);
            
            var fileName = $"comparison-{DateTime.UtcNow:yyyyMMdd-HHmmss}{ComparisonBundleService.BundleExtension}";
            return Path.Combine(scalarScopePath, fileName);
        }
        catch
        {
            // Fall back to temp directory
            return Path.Combine(Path.GetTempPath(), $"comparison-{Guid.NewGuid():N}{ComparisonBundleService.BundleExtension}");
        }
    }
    
    private async Task ShowSuccess(BundleExportResult result)
    {
        var page = GetParentPage();
        if (page is null) return;
        
        var message = $"Bundle exported successfully!\n\n" +
                      $"Location: {result.FilePath}\n" +
                      $"Files: {result.FileCount}\n" +
                      $"Hash: {result.BundleHash?[..16]}...";
        
        await page.DisplayAlert("Export Complete", message, "OK");
        
        // Also copy hash to clipboard
        if (!string.IsNullOrEmpty(result.BundleHash))
        {
            await Clipboard.SetTextAsync(result.BundleHash);
        }
    }
    
    private async Task ShowError(string message)
    {
        var page = GetParentPage();
        if (page is null) return;
        
        await page.DisplayAlert("Export Failed", message, "OK");
    }
    
    private Page? GetParentPage()
    {
        Element? parent = this;
        while (parent is not null)
        {
            if (parent is Page page) return page;
            parent = parent.Parent;
        }
        return null;
    }
    
    #endregion
    
    #region Events
    
    /// <summary>
    /// Raised when export is complete.
    /// </summary>
    public event EventHandler<BundleExportResult>? ExportCompleted;
    
    /// <summary>
    /// Raised when panel is closed.
    /// </summary>
    public event EventHandler? Closed;
    
    #endregion
}
