using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 2: Progressive disclosure container.
/// Hides advanced controls behind an expandable section.
/// State is persisted per section ID.
/// </summary>
public partial class AdvancedSection : ContentView
{
    private static readonly Dictionary<string, bool> _expandedStates = new();

    #region Bindable Properties

    public static readonly BindableProperty SectionIdProperty =
        BindableProperty.Create(nameof(SectionId), typeof(string), typeof(AdvancedSection), "",
            propertyChanged: OnSectionIdChanged);

    public static readonly BindableProperty HeaderTextProperty =
        BindableProperty.Create(nameof(HeaderText), typeof(string), typeof(AdvancedSection), "More options");

    public static readonly BindableProperty IsExpandedProperty =
        BindableProperty.Create(nameof(IsExpanded), typeof(bool), typeof(AdvancedSection), false,
            BindingMode.TwoWay, propertyChanged: OnIsExpandedChanged);

    public static readonly BindableProperty AdvancedContentProperty =
        BindableProperty.Create(nameof(AdvancedContent), typeof(View), typeof(AdvancedSection));

    #endregion

    #region Properties

    /// <summary>
    /// Unique ID for persisting expand state.
    /// </summary>
    public string SectionId
    {
        get => (string)GetValue(SectionIdProperty);
        set => SetValue(SectionIdProperty, value);
    }

    /// <summary>
    /// Header text shown when collapsed.
    /// </summary>
    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    /// <summary>
    /// Whether the section is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Content to show when expanded.
    /// </summary>
    public View? AdvancedContent
    {
        get => (View?)GetValue(AdvancedContentProperty);
        set => SetValue(AdvancedContentProperty, value);
    }

    #endregion

    public AdvancedSection()
    {
        InitializeComponent();
    }

    private static void OnSectionIdChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AdvancedSection section && newValue is string id && !string.IsNullOrEmpty(id))
        {
            // Restore persisted state
            if (_expandedStates.TryGetValue(id, out var expanded))
            {
                section.IsExpanded = expanded;
            }
        }
    }

    private static void OnIsExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AdvancedSection section)
        {
            // Update icon
            section.expandIcon.Text = section.IsExpanded ? "▾" : "▸";

            // Persist state
            if (!string.IsNullOrEmpty(section.SectionId))
            {
                _expandedStates[section.SectionId] = section.IsExpanded;
            }

            // Animate (using centralized motion tokens)
            if (section.IsExpanded && MotionTokens.ShouldAnimate("section.expand"))
            {
                section.contentBorder.Opacity = 0;
                var duration = MotionTokens.GetDuration("section.expand");
                section.contentBorder.FadeTo(1, (uint)duration, MotionTokens.EaseEnter);
            }
        }
    }

    private void OnHeaderTapped(object? sender, TappedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }
}
