using System.Text.Json.Serialization;

namespace Maash.Core.ModuleModel;

/// <summary>
/// A single operator within a module. Here-bracketed operators declare
/// hereBracketed=true; by convention their suffix ends with `[`, the open
/// bracket of the operand (see README), e.g. suffixes `[` and `+[`.
/// </summary>
public sealed record MaashOperatorDefinition {
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// True when the operator's operand is here-bracketed: maash scopes it to
    /// the next unescaped ']@@' closer. False when the module owns the operand's
    /// extent and reports a parsed length (M1 parseLength / render).
    /// </summary>
    [JsonPropertyName("hereBracketed")]
    public bool HereBracketed { get; init; }
}
