namespace Payment.Infrastructure.Monitoring;

/// <summary>
/// Configuration for alert rules.
/// Stateless configuration class following 12-Factor App principles.
/// </summary>
public class AlertRulesConfiguration
{
    /// <summary>
    /// Gets or sets payment failure alert rules.
    /// </summary>
    public PaymentFailureAlertRules? PaymentFailure { get; set; }

    /// <summary>
    /// Gets or sets security incident alert rules.
    /// </summary>
    public SecurityIncidentAlertRules? SecurityIncident { get; set; }
}

/// <summary>
/// Alert rules for payment failures.
/// </summary>
public class PaymentFailureAlertRules
{
    /// <summary>
    /// Gets or sets critical severity rules.
    /// </summary>
    public AlertRule? Critical { get; set; }

    /// <summary>
    /// Gets or sets high severity rules.
    /// </summary>
    public AlertRule? High { get; set; }

    /// <summary>
    /// Gets or sets medium severity rules.
    /// </summary>
    public AlertRule? Medium { get; set; }

    /// <summary>
    /// Gets or sets low severity rules.
    /// </summary>
    public AlertRule? Low { get; set; }
}

/// <summary>
/// Alert rules for security incidents.
/// </summary>
public class SecurityIncidentAlertRules
{
    /// <summary>
    /// Gets or sets critical severity rules.
    /// </summary>
    public AlertRule? Critical { get; set; }

    /// <summary>
    /// Gets or sets high severity rules.
    /// </summary>
    public AlertRule? High { get; set; }

    /// <summary>
    /// Gets or sets medium severity rules.
    /// </summary>
    public AlertRule? Medium { get; set; }

    /// <summary>
    /// Gets or sets low severity rules.
    /// </summary>
    public AlertRule? Low { get; set; }
}

/// <summary>
/// Individual alert rule configuration.
/// </summary>
public class AlertRule
{
    /// <summary>
    /// Gets or sets the threshold expression (e.g., "> 10 failures in 5 minutes").
    /// </summary>
    public string Threshold { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification channels to use.
    /// </summary>
    public List<string> Channels { get; set; } = new();
}

