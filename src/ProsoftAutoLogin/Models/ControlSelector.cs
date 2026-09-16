namespace ProsoftAutoLogin.Models;

public sealed class ControlSelector
{
    public string? AutomationId { get; set; }

    public string? NameContains { get; set; }

    public string? ClassNameContains { get; set; }

    public string? ControlType { get; set; }

    public bool? IsPassword { get; set; }

    public bool? ExcludeComboBoxChildren { get; set; }

    public int Index { get; set; }

    public override string ToString()
    {
        return $"AutomationId={AutomationId ?? "*"}, " +
               $"Name~={NameContains ?? "*"}, " +
               $"Class~={ClassNameContains ?? "*"}, " +
               $"Type={ControlType ?? "*"}, Index={Index}";
    }
}
