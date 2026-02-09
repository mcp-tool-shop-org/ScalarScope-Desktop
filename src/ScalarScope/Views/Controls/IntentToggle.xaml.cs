namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 2: Intent-first toggle control.
/// Shows user-facing intent question with observable behavior explanation.
/// Technical term available but de-emphasized.
/// </summary>
public partial class IntentToggle : ContentView
{
    #region Bindable Properties

    public static readonly BindableProperty IsToggledProperty =
        BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(IntentToggle), false,
            BindingMode.TwoWay);

    public static readonly BindableProperty IntentLabelProperty =
        BindableProperty.Create(nameof(IntentLabel), typeof(string), typeof(IntentToggle), "Show this?");

    public static readonly BindableProperty ExplanationProperty =
        BindableProperty.Create(nameof(Explanation), typeof(string), typeof(IntentToggle), "");

    public static readonly BindableProperty TechnicalTermProperty =
        BindableProperty.Create(nameof(TechnicalTerm), typeof(string), typeof(IntentToggle), "");

    public static readonly BindableProperty AccentColorProperty =
        BindableProperty.Create(nameof(AccentColor), typeof(Color), typeof(IntentToggle), Colors.Cyan);

    public static readonly BindableProperty ShowTechnicalProperty =
        BindableProperty.Create(nameof(ShowTechnical), typeof(bool), typeof(IntentToggle), false);

    #endregion

    #region Properties

    /// <summary>
    /// Whether the toggle is on or off.
    /// </summary>
    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set => SetValue(IsToggledProperty, value);
    }

    /// <summary>
    /// Intent-focused label (e.g., "Show motion speed?").
    /// Answers: what question does this control address?
    /// </summary>
    public string IntentLabel
    {
        get => (string)GetValue(IntentLabelProperty);
        set => SetValue(IntentLabelProperty, value);
    }

    /// <summary>
    /// Brief explanation of what changes when toggled (≤12 words).
    /// Answers: what will I see differently?
    /// </summary>
    public string Explanation
    {
        get => (string)GetValue(ExplanationProperty);
        set => SetValue(ExplanationProperty, value);
    }

    /// <summary>
    /// Technical term for advanced users (de-emphasized).
    /// Only shown when ShowTechnical is true.
    /// </summary>
    public string TechnicalTerm
    {
        get => (string)GetValue(TechnicalTermProperty);
        set => SetValue(TechnicalTermProperty, value);
    }

    /// <summary>
    /// Accent color for the toggle and label.
    /// </summary>
    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    /// <summary>
    /// Whether to show the technical term.
    /// Often bound to a global "show technical details" setting.
    /// </summary>
    public bool ShowTechnical
    {
        get => (bool)GetValue(ShowTechnicalProperty);
        set => SetValue(ShowTechnicalProperty, value);
    }

    #endregion

    public IntentToggle()
    {
        InitializeComponent();
    }
}
