using ScalarScope.Services;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 4: "Why did this fire?" panel for DeltaZone.
/// Explains delta justification with trigger type, parameters, confidence, and guardrails.
/// </summary>
public partial class DeltaWhyPanel : ContentView
{
    public static readonly BindableProperty DeltaProperty =
        BindableProperty.Create(nameof(Delta), typeof(CanonicalDelta), typeof(DeltaWhyPanel),
            defaultValue: null,
            propertyChanged: OnDeltaChanged);

    public static readonly BindableProperty IsExpandedProperty =
        BindableProperty.Create(nameof(IsExpanded), typeof(bool), typeof(DeltaWhyPanel),
            defaultValue: false,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty AccentColorProperty =
        BindableProperty.Create(nameof(AccentColor), typeof(Color), typeof(DeltaWhyPanel),
            defaultValue: Color.FromArgb("#00d9ff"));

    public CanonicalDelta? Delta
    {
        get => (CanonicalDelta?)GetValue(DeltaProperty);
        set => SetValue(DeltaProperty, value);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    // Computed properties for display
    public string DeltaName => Delta?.Name ?? "";
    public string TriggerType => GetTriggerType();
    public bool HasTriggerType => !string.IsNullOrEmpty(TriggerType);
    public string WhyFired => BuildWhyFired();
    public double Confidence => Delta?.Confidence ?? Delta?.ConvergenceConfidence ?? 0;
    public bool HasConfidence => Confidence > 0;
    public string? Guardrail => GetGuardrail();
    public bool HasGuardrail => !string.IsNullOrEmpty(Guardrail);
    public bool HasParameters => _parameters.Count > 0;
    
    /// <summary>
    /// Phase 5.3: Get confidence tier for display.
    /// </summary>
    public ConfidenceTokens.ConfidenceTier ConfidenceTier => 
        ConfidenceTokens.GetTierFromConfidence(Confidence);
    
    /// <summary>
    /// Phase 5.3: Get confidence tooltip prefix for user messaging.
    /// </summary>
    public string ConfidencePrefix => ConfidenceTokens.GetTooltipPrefix(ConfidenceTier);
    
    /// <summary>
    /// Phase 5.3: Get human-readable confidence label.
    /// </summary>
    public string ConfidenceLabel => ConfidenceTokens.GetLabel(ConfidenceTier);

    private readonly Dictionary<string, string> _parameters = new();

    /// <summary>
    /// Fired when user clicks "Show me" to navigate to anchor.
    /// </summary>
    public event Action<CanonicalDelta>? ShowMeRequested;

    /// <summary>
    /// Fired when panel is closed.
    /// </summary>
    public event Action? Closed;

    public DeltaWhyPanel()
    {
        InitializeComponent();
    }

    private static void OnDeltaChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DeltaWhyPanel panel)
        {
            panel.UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        _parameters.Clear();
        BuildParameters();
        UpdateParametersUI();

        OnPropertyChanged(nameof(DeltaName));
        OnPropertyChanged(nameof(TriggerType));
        OnPropertyChanged(nameof(HasTriggerType));
        OnPropertyChanged(nameof(WhyFired));
        OnPropertyChanged(nameof(Confidence));
        OnPropertyChanged(nameof(HasConfidence));
        OnPropertyChanged(nameof(ConfidenceTier));
        OnPropertyChanged(nameof(ConfidencePrefix));
        OnPropertyChanged(nameof(ConfidenceLabel));
        OnPropertyChanged(nameof(Guardrail));
        OnPropertyChanged(nameof(HasGuardrail));
        OnPropertyChanged(nameof(HasParameters));
    }

    private string GetTriggerType()
    {
        if (Delta == null) return "";

        return Delta.Id switch
        {
            "delta_td" => DetermineTdTriggerType(),
            "delta_a" => "persistence_weighted",
            "delta_o" => "area_episode",
            "delta_tc" => Delta.ConvergenceConfidence.HasValue ? "step_difference" : "one_run_converged",
            "delta_f" => DetermineFTriggerType(),
            _ => ""
        };
    }

    private string DetermineTdTriggerType()
    {
        // If dominance showed recurrence pattern
        // For now, default to "sustained" - could be enriched from diagnostics
        return "sustained";
    }

    private string DetermineFTriggerType()
    {
        if (Delta?.FailedA == true || Delta?.FailedB == true)
            return "event";
        return "proxy";
    }

    private string BuildWhyFired()
    {
        if (Delta == null) return "";
        
        // Phase 5.3: Prepend confidence prefix to explanation
        var baseExplanation = Delta.Id switch
        {
            "delta_f" => BuildFailureWhy(),
            "delta_tc" => BuildConvergenceWhy(),
            "delta_td" => BuildEmergenceWhy(),
            "delta_a" => BuildAlignmentWhy(),
            "delta_o" => BuildStabilityWhy(),
            _ => Delta.Explanation
        };
        
        // Only add prefix if we have measurable confidence
        if (HasConfidence && Confidence < 1.0)
        {
            return $"{ConfidencePrefix} {baseExplanation}";
        }
        
        return baseExplanation;
    }

    private string BuildFailureWhy()
    {
        if (Delta?.FailedA == true && Delta?.FailedB != true)
            return $"Path A failed at step {Delta.TFailA} ({Delta.FailureKindA ?? "unknown"}), Path B completed normally.";
        if (Delta?.FailedB == true && Delta?.FailedA != true)
            return $"Path B failed at step {Delta.TFailB} ({Delta.FailureKindB ?? "unknown"}), Path A completed normally.";
        if (Delta?.FailedA == true && Delta?.FailedB == true)
            return $"Both paths failed: A at step {Delta.TFailA}, B at step {Delta.TFailB}.";
        return "Failure detected via proxy (divergence or collapse pattern).";
    }

    private string BuildConvergenceWhy()
    {
        if (Delta == null) return "";

        if (Delta.TcA.HasValue && Delta.TcB.HasValue)
        {
            var diff = Delta.DeltaTcSteps ?? (Delta.TcB.Value - Delta.TcA.Value);
            var direction = diff > 0 ? "later" : "earlier";
            return $"Path A converged at step {Delta.TcA}, Path B at step {Delta.TcB}. Path B converged {Math.Abs(diff)} steps {direction}.";
        }
        if (Delta.TcA.HasValue)
            return $"Path A converged at step {Delta.TcA}. Path B did not converge.";
        if (Delta.TcB.HasValue)
            return $"Path A did not converge. Path B converged at step {Delta.TcB}.";
        return "Convergence timing difference detected.";
    }

    private string BuildEmergenceWhy()
    {
        if (Delta == null) return "";

        if (Delta.TdA.HasValue && Delta.TdB.HasValue)
        {
            var diff = Delta.TdB.Value - Delta.TdA.Value;
            var direction = diff > 0 ? "later" : "earlier";
            return $"Dominant structure emerged at step {Delta.TdA} (A) vs step {Delta.TdB} (B). Path B: {Math.Abs(diff)} steps {direction}.";
        }
        if (Delta.TdA.HasValue)
            return $"Path A developed dominant structure at step {Delta.TdA}. Path B did not.";
        if (Delta.TdB.HasValue)
            return $"Path A did not develop dominant structure. Path B did at step {Delta.TdB}.";
        return "Structural emergence difference detected.";
    }

    private string BuildAlignmentWhy()
    {
        if (Delta == null) return "";

        if (Delta.MeanAlignA.HasValue && Delta.MeanAlignB.HasValue)
        {
            var better = Delta.MeanAlignB > Delta.MeanAlignA ? "B" : "A";
            return $"Mean evaluator alignment: A={Delta.MeanAlignA:F3}, B={Delta.MeanAlignB:F3}. Path {better} showed higher agreement. (Persistence-weighted: final 25% at 2× importance)";
        }
        return "Evaluator alignment difference detected.";
    }

    private string BuildStabilityWhy()
    {
        if (Delta == null) return "";

        if (Delta.ScoreA.HasValue && Delta.ScoreB.HasValue)
        {
            var higher = Delta.ScoreB > Delta.ScoreA ? "B" : "A";
            return $"Oscillation score: A={Delta.ScoreA:F2}, B={Delta.ScoreB:F2}. Path {higher} showed more instability. (Area-above-θ scoring)";
        }
        return "Stability difference detected.";
    }

    private void BuildParameters()
    {
        if (Delta == null) return;

        switch (Delta.Id)
        {
            case "delta_tc":
                _parameters["ResolutionSteps"] = "3";
                if (Delta.EpsilonUsed.HasValue)
                    _parameters["Epsilon"] = Delta.EpsilonUsed.Value.ToString("F4");
                if (Delta.ConvergenceConfidence.HasValue)
                    _parameters["Confidence"] = $"{Delta.ConvergenceConfidence.Value:P0}";
                break;

            case "delta_td":
                _parameters["RecurrenceWindow"] = "7";
                if (Delta.DominanceRatioK.HasValue)
                    _parameters["DominanceK"] = $"{Delta.DominanceRatioK.Value:F2}";
                break;

            case "delta_a":
                _parameters["DualGate"] = "persist<0.05, raw<0.10";
                _parameters["Weighting"] = "final 25% @ 2×";
                break;

            case "delta_o":
                _parameters["MinDuration"] = "4";
                _parameters["ThetaSigmaMultiplier"] = "1.0";
                if (Delta.ThresholdUsed.HasValue)
                    _parameters["Threshold"] = Delta.ThresholdUsed.Value.ToString("F3");
                break;

            case "delta_f":
                _parameters["PersistenceWindow"] = "3";
                break;
        }
    }

    private void UpdateParametersUI()
    {
        parametersContainer.Children.Clear();

        foreach (var kvp in _parameters)
        {
            var chip = new Border
            {
                BackgroundColor = Color.FromArgb("#252540"),
                StrokeThickness = 0,
                Padding = new Thickness(6, 3),
                Margin = new Thickness(0, 0, 4, 4),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 3 }
            };

            var label = new Label
            {
                Text = $"{kvp.Key}={kvp.Value}",
                TextColor = Color.FromArgb("#888"),
                FontSize = 9
            };

            chip.Content = label;
            parametersContainer.Children.Add(chip);
        }
    }

    private string? GetGuardrail()
    {
        return Delta?.Id switch
        {
            "delta_a" => "Agreement ≠ correctness.",
            "delta_td" => "Dominance ≠ collapse.",
            "delta_o" => "Instability ≠ failure.",
            _ => null
        };
    }

    private void OnCloseClicked(object? sender, EventArgs e)
    {
        IsExpanded = false;
        Closed?.Invoke();
    }

    private async void OnCopyFindingClicked(object? sender, EventArgs e)
    {
        if (Delta == null) return;

        var finding = BuildCopyableFinding();
        await Clipboard.SetTextAsync(finding);

        // Brief visual feedback
        if (sender is Button btn)
        {
            var originalText = btn.Text;
            btn.Text = "✓ Copied";
            await Task.Delay(1500);
            btn.Text = originalText;
        }
    }

    private string BuildCopyableFinding()
    {
        if (Delta == null) return "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"**{Delta.Name}**");
        sb.AppendLine();
        sb.AppendLine(WhyFired);
        sb.AppendLine();

        if (HasConfidence)
            sb.AppendLine($"Confidence: {Confidence:P0}");

        if (_parameters.Count > 0)
        {
            sb.AppendLine($"Parameters: {string.Join(", ", _parameters.Select(p => $"{p.Key}={p.Value}"))}");
        }

        sb.AppendLine();
        sb.AppendLine($"_Delta Spec v3.2.0_");

        return sb.ToString();
    }

    private void OnShowMeClicked(object? sender, EventArgs e)
    {
        if (Delta != null)
        {
            ShowMeRequested?.Invoke(Delta);
        }
    }
}
