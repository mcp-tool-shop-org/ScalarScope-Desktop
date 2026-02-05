using Microsoft.Maui.Controls.Shapes;
using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// A dismissible inline hint for first-run guidance.
/// Shows contextual tips that users can permanently dismiss.
/// </summary>
public class InlineHint : ContentView
{
    public static readonly BindableProperty HintIdProperty =
        BindableProperty.Create(nameof(HintId), typeof(string), typeof(InlineHint), "",
            propertyChanged: OnHintIdChanged);

    public static readonly BindableProperty HintTextProperty =
        BindableProperty.Create(nameof(HintText), typeof(string), typeof(InlineHint), "");

    public static readonly BindableProperty AccentColorProperty =
        BindableProperty.Create(nameof(AccentColor), typeof(Color), typeof(InlineHint), Color.FromArgb("#00d9ff"));

    public string HintId
    {
        get => (string)GetValue(HintIdProperty);
        set => SetValue(HintIdProperty, value);
    }

    public string HintText
    {
        get => (string)GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    private Border _container = null!;
    private Label _textLabel = null!;
    private Button _dismissButton = null!;

    public InlineHint()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        _textLabel = new Label
        {
            FontSize = 12,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };
        _textLabel.SetBinding(Label.TextProperty, new Binding(nameof(HintText), source: this));

        _dismissButton = new Button
        {
            Text = "×",
            FontSize = 14,
            TextColor = Colors.White,
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(8, 2),
            HeightRequest = 24,
            WidthRequest = 24,
            VerticalOptions = LayoutOptions.Center
        };
        _dismissButton.Clicked += OnDismissClicked;

        var hintIcon = new Label
        {
            Text = "💡",
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var contentGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 4
        };
        contentGrid.Add(hintIcon, 0);
        contentGrid.Add(_textLabel, 1);
        contentGrid.Add(_dismissButton, 2);

        _container = new Border
        {
            BackgroundColor = Color.FromArgb("#16213e"),
            StrokeThickness = 1,
            Padding = new Thickness(10, 6),
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Content = contentGrid
        };
        _container.SetBinding(Border.StrokeProperty, new Binding(nameof(AccentColor), source: this));

        Content = _container;

        // Check if already dismissed
        UpdateVisibility();
    }

    private static void OnHintIdChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is InlineHint hint)
        {
            hint.UpdateVisibility();
        }
    }

    private void UpdateVisibility()
    {
        if (string.IsNullOrEmpty(HintId))
        {
            IsVisible = false;
            return;
        }

        IsVisible = !UserPreferencesService.IsHintDismissed(HintId);
    }

    private void OnDismissClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(HintId))
        {
            UserPreferencesService.DismissHint(HintId);
        }

        // Animate out
        this.FadeTo(0, 200, Easing.CubicOut).ContinueWith(_ =>
        {
            MainThread.BeginInvokeOnMainThread(() => IsVisible = false);
        });
    }
}
