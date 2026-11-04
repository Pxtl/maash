namespace Maash.Core;

/// <summary>
/// A resolved maash operator: everything after <c>@@</c> in the text, mapped
/// to its module + operator definition.
/// </summary>
public sealed class ResolvedOperator {
    public required string Alias { get; init; }
    public required MaashOperatorDefinition Definition { get; init; }
    public required string Suffix { get; init; }

    /// <summary>Full operator string following <c>@@</c> (i.e. after the two @ signs).</summary>
    public required string OperatorString { get; init; }

    /// <summary>
    /// True when this operator is bracketed: the suffix (and therefore the
    /// operator string) ends with <c>[</c>, the open bracket of the operand.
    /// The operand runs to the next unescaped <c>]@@</c>.
    /// </summary>
    public bool IsBracketed => Suffix.EndsWith('[');

    /// <summary>The operator token emitted to <c>maash eval</c> (always starts with <c>@@</c>).</summary>
    public string EvalArg => "@@" + OperatorString;
}
