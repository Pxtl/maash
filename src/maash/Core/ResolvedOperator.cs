using Maash.Core.ModuleModel;

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
    /// True when this operator is here-bracketed: maash scopes the operand to
    /// the next unescaped <c>]@@</c> closer. Declared by the module JSON
    /// (hereBracketed); by convention the suffix (and operator string) ends
    /// with the open bracket <c>[</c>. False: the module owns the operand's
    /// extent and reports a parsed length.
    /// </summary>
    public bool IsHereBracketed => Definition.HereBracketed;

    /// <summary>The operator token emitted to <c>maash eval</c> (always starts with <c>@@</c>).</summary>
    public string EvalArg => "@@" + OperatorString;
}
