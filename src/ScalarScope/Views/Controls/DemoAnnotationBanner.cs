using Microsoft.Maui.Controls.Shapes;
using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// A banner that displays timed demo annotations during the guided demo.
/// Shows at the top of the Compare view with contextual messages.
/// </summary>
public class DemoAnnotationBanner : ContentView
{
    private readonly Label _titleLabel;
    private readonly Label _messageLabel;
    private readonly Button _dismissButton;
    private readonly Border _container;

    public DemoAnnotationBanner()
    {
        _titleLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };

        _messageLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#ccc"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _dismissButton = new Button
        {
            Text = "×",
            FontSize = 18,
            TextColor = Color.FromArgb("#888"),
            BackgroundColor = Colors.Transparent,
            WidthRequest = 30,
            HeightRequest = 30,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        _dismissButton.Clicked += OnDismissClicked;

        var textStack = new VerticalStackLayout
        {
            Spacing = 4,
            Children = { _titleLabel, _messageLabel },
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };

        var contentGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(15, 10)
        };
        contentGrid.Add(textStack, 0, 0);
        contentGrid.Add(_dismissButton, 1, 0);

        _container = new Border
        {
            BackgroundColor = Color.FromArgb("#1a2a4a"),
            Stroke = Color.FromArgb("#00d9ff"),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 1,
            Padding = 0,
            Content = contentGrid,
            IsVisible = false,
            Margin = new Thickness(20, 10)
        };

        Content = _container;

        // Subscribe to annotation events
        DemoAnnotationService.AnnotationTriggered += OnAnnotationTriggered;
        DemoAnnotationService.AnnotationDismissed += OnAnnotationDismissed;
    }

    private void OnAnnotationTriggered(DemoAnnotation annotation)
    {
        // Update on UI thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _titleLabel.Text = annotation.Title;
            _messageLabel.Text = annotation.Message;

            // Color based on emphasis
            var emphasisColor = annotation.Emphasis switch
            {
                DemoEmphasis.Introduction => Color.FromArgb("#00d9ff"),  // Blue
                DemoEmphasis.Observation => Color.FromArgb("#ffd93d"),   // Yellow
                DemoEmphasis.KeyInsight => Color.FromArgb("#4ecdc4"),    // Green/Teal
                DemoEmphasis.Conclusion => Color.FromArgb("#c56cf0"),    // Purple
                _ => Color.FromArgb("#00d9ff")
            };

            _container.Stroke = emphasisColor;
            _titleLabel.TextColor = emphasisColor;
            _container.IsVisible = true;
        });
    }

    private void OnAnnotationDismissed()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _container.IsVisible = false;
        });
    }

    private void OnDismissClicked(object? sender, EventArgs e)
    {
        DemoAnnotationService.DismissCurrent();
    }

    /// <summary>
    /// Clean up event subscriptions.
    /// </summary>
    public void Dispose()
    {
        DemoAnnotationService.AnnotationTriggered -= OnAnnotationTriggered;
        DemoAnnotationService.AnnotationDismissed -= OnAnnotationDismissed;
    }
}
