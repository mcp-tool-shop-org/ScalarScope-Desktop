// Phase 7.2.5: Review Mode Banner
// Visual indicator when viewing a bundle in read-only mode.

using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 7.2: Banner shown when in review mode (viewing a bundle).
/// Indicates read-only state and provides exit action.
/// </summary>
public partial class ReviewModeBanner : ContentView
{
    /// <summary>
    /// Bindable property for the loaded bundle.
    /// </summary>
    public static readonly BindableProperty BundleProperty =
        BindableProperty.Create(
            nameof(Bundle),
            typeof(LoadedBundle),
            typeof(ReviewModeBanner),
            defaultValue: null,
            propertyChanged: OnBundleChanged);

    public LoadedBundle? Bundle
    {
        get => (LoadedBundle?)GetValue(BundleProperty);
        set => SetValue(BundleProperty, value);
    }

    /// <summary>
    /// Event raised when user clicks "Exit Review Mode".
    /// </summary>
    public event EventHandler? ExitRequested;

    public ReviewModeBanner()
    {
        InitializeComponent();
    }

    private static void OnBundleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ReviewModeBanner banner)
        {
            banner.UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        if (Bundle == null)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;

        // Update hash display (truncated)
        var hash = Bundle.BundleHash ?? "unknown";
        hashLabel.Text = hash.Length > 12 ? $"{hash[..12]}..." : hash;

        // Update profile badge
        var profile = Bundle.Manifest.Profile;
        profileLabel.Text = profile.ToString();
        
        // Color-code by profile
        profileBadge.BackgroundColor = profile switch
        {
            BundleProfile.Share => Color.FromArgb("#2a503a"),
            BundleProfile.Review => Color.FromArgb("#3a3a5a"),
            BundleProfile.Audit => Color.FromArgb("#4a3a3a"),
            _ => Color.FromArgb("#2a2a4e")
        };

        profileLabel.TextColor = profile switch
        {
            BundleProfile.Share => Color.FromArgb("#4ecdc4"),
            BundleProfile.Review => Color.FromArgb("#a29bfe"),
            BundleProfile.Audit => Color.FromArgb("#ff6b6b"),
            _ => Color.FromArgb("#888")
        };

        // Update subtitle with bundle info
        var createdDate = Bundle.Manifest.CreatedAt;
        var deltaCount = Bundle.Deltas.Count;
        subtitleLabel.Text = $"Created {createdDate:MMM d, yyyy} • {deltaCount} deltas • Read-only";
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        // Unload the bundle
        BundleImportService.Instance.Unload();
        
        // Raise event for parent to handle
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
