// Phase 6.3.5: Recovery Dialog - UI for restoring previous sessions
using Microsoft.Maui.Controls.Shapes;
using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 6.3: Dialog for recovering previous sessions.
/// Shows available auto-saves and checkpoints.
/// </summary>
public partial class RecoveryDialog : ContentView
{
    private AutoSaveSessionState? _selectedSession;
    private readonly List<Border> _sessionCards = new();
    
    /// <summary>
    /// Event raised when user selects a session to restore.
    /// </summary>
    public event EventHandler<RecoveryEventArgs>? SessionRestoreRequested;
    
    /// <summary>
    /// Event raised when user chooses to start fresh.
    /// </summary>
    public event EventHandler? StartFreshRequested;
    
    /// <summary>
    /// Event raised when dialog is closed.
    /// </summary>
    public event EventHandler? Closed;

    public RecoveryDialog()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// Show the recovery dialog with available sessions.
    /// </summary>
    public void Show()
    {
        LoadSessions();
        DialogContainer.IsVisible = true;
    }
    
    /// <summary>
    /// Hide the dialog.
    /// </summary>
    public void Hide()
    {
        DialogContainer.IsVisible = false;
        _selectedSession = null;
    }
    
    /// <summary>
    /// Check if dialog is visible.
    /// </summary>
    public bool IsShowing => DialogContainer.IsVisible;
    
    private void LoadSessions()
    {
        SessionsContainer.Children.Clear();
        _sessionCards.Clear();
        _selectedSession = null;
        RestoreButton.IsEnabled = false;
        
        var sessions = AutoSaveService.Instance.GetAvailableSaves();
        
        if (sessions.Count == 0)
        {
            NoSessionsLabel.IsVisible = true;
            SubtitleLabel.Text = "No sessions found";
            ClearAllButton.IsVisible = false;
            return;
        }
        
        NoSessionsLabel.IsVisible = false;
        ClearAllButton.IsVisible = true;
        SubtitleLabel.Text = sessions.Count == 1 
            ? "Found 1 session that can be restored"
            : $"Found {sessions.Count} sessions that can be restored";
        
        foreach (var session in sessions)
        {
            var card = CreateSessionCard(session);
            _sessionCards.Add(card);
            SessionsContainer.Children.Add(card);
        }
    }
    
    private Border CreateSessionCard(AutoSaveSessionState session)
    {
        var isRecent = DateTime.UtcNow - session.UpdatedAt < TimeSpan.FromHours(1);
        
        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#2A2A2A"),
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#3A3A3A"),
            Padding = new Thickness(12),
            BindingContext = session
        };
        card.StrokeShape = new RoundRectangle { CornerRadius = 8 };
        
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => SelectSession(session, card);
        card.GestureRecognizers.Add(tapGesture);
        
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        
        // Session name and indicator
        var headerStack = new HorizontalStackLayout { Spacing = 8 };
        headerStack.Children.Add(new Label
        {
            Text = session.SessionName,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        });
        
        if (isRecent)
        {
            headerStack.Children.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#2A4A2A"),
                Padding = new Thickness(6, 2),
                Content = new Label
                {
                    Text = "Recent",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#6BCB77")
                }
            });
        }
        
        Grid.SetRow(headerStack, 0);
        Grid.SetColumn(headerStack, 0);
        grid.Children.Add(headerStack);
        
        // Summary
        var summary = new Label
        {
            Text = session.GetSummary(),
            FontSize = 12,
            TextColor = Color.FromArgb("#888888"),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetRow(summary, 1);
        Grid.SetColumn(summary, 0);
        grid.Children.Add(summary);
        
        // Timestamp
        var timeAgo = GetTimeAgo(session.UpdatedAt);
        var timestamp = new Label
        {
            Text = timeAgo,
            FontSize = 11,
            TextColor = Color.FromArgb("#666666"),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetRow(timestamp, 0);
        Grid.SetRowSpan(timestamp, 2);
        Grid.SetColumn(timestamp, 1);
        grid.Children.Add(timestamp);
        
        card.Content = grid;
        return card;
    }
    
    private void SelectSession(AutoSaveSessionState session, Border card)
    {
        // Deselect previous
        foreach (var c in _sessionCards)
        {
            c.Stroke = Color.FromArgb("#3A3A3A");
            c.BackgroundColor = Color.FromArgb("#2A2A2A");
        }
        
        // Select new
        card.Stroke = Color.FromArgb("#6BCB77");
        card.BackgroundColor = Color.FromArgb("#2A3A2A");
        
        _selectedSession = session;
        RestoreButton.IsEnabled = true;
    }
    
    private static string GetTimeAgo(DateTime time)
    {
        var diff = DateTime.UtcNow - time;
        
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return time.ToLocalTime().ToString("MMM d");
    }
    
    private void OnCloseClicked(object? sender, EventArgs e)
    {
        Hide();
        Closed?.Invoke(this, EventArgs.Empty);
    }
    
    private async void OnClearAllClicked(object? sender, EventArgs e)
    {
        var page = Application.Current?.MainPage;
        if (page is null) return;
        
        var confirm = await page.DisplayAlert(
            "Clear All Sessions",
            "Are you sure you want to delete all saved sessions? This cannot be undone.",
            "Delete All",
            "Cancel");
        
        if (confirm)
        {
            AutoSaveService.Instance.ClearAllSaves();
            LoadSessions();
        }
    }
    
    private void OnStartFreshClicked(object? sender, EventArgs e)
    {
        Hide();
        StartFreshRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void OnRestoreClicked(object? sender, EventArgs e)
    {
        if (_selectedSession is null) return;
        
        Hide();
        SessionRestoreRequested?.Invoke(this, new RecoveryEventArgs
        {
            SessionId = _selectedSession.SessionId,
            Session = _selectedSession
        });
    }
}

/// <summary>
/// Event args for recovery events.
/// </summary>
public class RecoveryEventArgs : EventArgs
{
    public required string SessionId { get; init; }
    public AutoSaveSessionState? Session { get; init; }
}
