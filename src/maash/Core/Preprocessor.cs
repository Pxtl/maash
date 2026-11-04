using System.Text;
using Maash.Core;

namespace Maash.Core;

/// <summary>
/// The maash preprocessor. Converts maash operators (@@...) in bash text
/// into escaped invocations of <c>maash eval</c>. This is syntactic sugar only:
/// it never evaluates operators itself.
/// </summary>
public static class Preprocessor {
    /// <summary>Finds the next unescaped <c>@@</c> at or after pos.</summary>
    public static int FindOperator(string text, int pos) {
        for (var i = pos; i + 1 < text.Length; i++) {
            if (text[i] == '@' && text[i + 1] == '@' &&
                (i == 0 || text[i - 1] != '\\')) {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Index of the <c>]</c> starting the next unescaped <c>]@@</c> closer at or after pos, or -1.</summary>
    public static int IndexOfHereBracketClose(string text, int pos) {
        for (var i = pos; i + 2 < text.Length; i++) {
            if (text[i] == ']' && text[i + 1] == '@' && text[i + 2] == '@' &&
                (i == 0 || text[i - 1] != '\\')) {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Greedy longest-operator-wins match at a <c>@@</c> position.
    /// Returns null on no match.
    /// </summary>
    public static ResolvedOperator? TryResolve(ModuleRegistry modules, int position, string text) {
        ResolvedOperator? best = null;
        var primaryAlias = modules.PrimaryAlias;
        foreach (var loaded in modules.Modules) {
            foreach (var (suffix, opString) in ModuleRegistry.BuildOperatorStrings(loaded.Module, loaded.Infix, loaded.Alias == primaryAlias, loaded.AllowEmptySuffix)) {
                if (text.Length - (position + 2) < opString.Length) {
                    continue;
                }

                if (!text.AsSpan(position + 2, opString.Length).StartsWith(opString, StringComparison.Ordinal)) {
                    continue;
                }

                if (best is null || opString.Length > best.OperatorString.Length) {
                    best = new ResolvedOperator {
                        Alias = loaded.Alias,
                        Definition = loaded.Module.Operators[suffix],
                        Suffix = suffix,
                        OperatorString = opString
                    };
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Runs the preprocessor over the whole text. Replaces each maash operator
    /// invocation with a bash command substitution
    /// <c>$(maash eval '@@OPT' operand)</c>.
    /// </summary>
    /// <param name="evaluate">Optional render-mode callback. When supplied, each
    /// resolved operator is fully evaluated now: the callback receives the
    /// resolved operator and the operand text and must return an
    /// <see cref="EvalOutcome"/> (result text plus how many chars of the operand
    /// the module consumed). The result is spliced into the output as a bash
    /// single-quoted literal instead of a <c>maash eval</c> wrapper. Used by
    /// <c>maash render</c> (hotkey-eval form). For here-bracketed operators the
    /// operand is maash-scoped (up to the <c>']@@'</c> closer); for non-here-
    /// bracketed operators it is the rest of the text from the operand start to
    /// end-of-text.
    /// </param>
    /// <param name="parseLength">Callback for non-here-bracketed operators when
    /// <paramref name="evaluate"/> is not supplied: receives the resolved operator
    /// and the rest of the text from the operand start to end-of-text and must
    /// return how many chars the module consumed as its operand (M1
    /// <c>parseLength</c>). Without it a non-here-bracketed operator cannot be
    /// scoped and Run throws.
    /// </param>
    public static PreprocessResult Run(ModuleRegistry modules, string text,
        Func<ResolvedOperator, string, EvalOutcome>? evaluate = null,
        Func<ResolvedOperator, string, int>? parseLength = null) {
        var warnings = new List<string>();
        var output = new StringBuilder(text.Length + 64);
        var pos = 0;

        while (pos < text.Length) {
            var outLenAtLoopTop = output.Length;
            var idx = FindOperator(text, pos);
            if (idx < 0) {
                output.Append(Unescape(text, pos, text.Length - pos));
                break;
            }
            output.Append(Unescape(text, pos, idx - pos));

            var resolved = TryResolve(modules, idx, text)
                ?? throw new MaashPreprocessorException(
                    $"No registered maash operator matches the '@@' sequence at index {idx}.");

            var afterToken = idx + 2 + resolved.OperatorString.Length;

            // Optional whitespace after operator before operand.
            while (afterToken < text.Length && char.IsWhiteSpace(text[afterToken])) {
                afterToken++;
            }

            string operand;
            string? evalOut = null;
            var end = afterToken;
            if (resolved.IsHereBracketed) {
                // Here-bracketed: maash owns the scoping. The operator string
                // already ends with the open bracket '[', so the operand starts
                // right at afterToken and runs to the next unescaped ']@@'
                // closer.
                var close = IndexOfHereBracketClose(text, afterToken);
                if (close < 0) {
                    throw new MaashPreprocessorException(
                        $"Unterminated here-bracketed operand for operator '@@{resolved.OperatorString}': no ']@@' closer.");
                }

                operand = text[afterToken..close];
                end = close + 3;                    // past the ']@@' closer
                if (evaluate is not null) {
                    evalOut = evaluate(resolved, operand).Output;
                }
            } else {
                // Non-here-bracketed: maash is dumb about the operand's extent.
                // The module owns it: the module receives the rest of the text
                // (from afterToken to end-of-text) and reports how many chars it
                // consumed (M1 parseLength / render parsed-length).
                var rest = text[afterToken..];
                int n;
                if (evaluate is not null) {
                    var outcome = evaluate(resolved, rest);
                    evalOut = outcome.Output;
                    n = outcome.ParsedLength;
                } else if (parseLength is not null) {
                    n = parseLength(resolved, rest);
                } else {
                    throw new MaashPreprocessorException(
                        $"Non-here-bracketed operator '@@{resolved.OperatorString}' has a module-owned operand extent; maash cannot scope it without a module (use 'maash render' or supply a parseLength callback).");
                }

                if (n < 0 || n > rest.Length) {
                    throw new MaashPreprocessorException(
                        $"Module reported parsed length {n} outside the operand range [0, {rest.Length}] for operator '@@{resolved.OperatorString}'.");
                }

                operand = rest[..n];
                end = afterToken + n;
            }

            // If the char right after the operand extent is the ')' closer of a
            // '$(' opened in the current literal chunk, the operator sits inside
            // a user $( ): emit the invocation bare (no $( ) wrapper) so the
            // user's own closer closes it, and consume that closer.
            var dOpen = text.LastIndexOf("$(", idx, StringComparison.Ordinal);
            var insideParen = dOpen >= pos && end < text.Length && text[end] == ')';
            if (insideParen) {
                end++;                                 // consume the user's ')'
            }

            // Convention: operators are invoked without leading whitespace.
            var t = operand.TrimStart();

            if (evalOut is not null) {
                // Render mode: splice the result as a bash literal. No maash eval
                // wrapper is emitted.
                if (insideParen) {
                    // If the user's $( ) wraps only the op (no command prefix),
                    // drop the $( ) entirely and splice the literal value in its
                    // place. Otherwise the result becomes the substitution body
                    // and we re-emit the closer.
                    // The enclosing opener is the nearest '$(' at or before the
                    // operator start (insideParen guarantees it exists).
                    var prefixEmpty = IsAllWhitespace(text, dOpen + 2, idx - (dOpen + 2));
                    if (prefixEmpty) {
                        // Truncate to just before the $( (accounting for any
                        // unescaping in the chunk text that preceded it).
                        var keepLen = Unescape(text, pos, dOpen - pos).Length;
                        output.Length = outLenAtLoopTop + keepLen;
                        output.Append(QuoteBash(evalOut));
                    } else {
                        output.Append(QuoteBash(evalOut));
                        output.Append(")");
                    }
                } else {
                    output.Append(QuoteBash(evalOut));
                }
            } else if (insideParen) {
                // When the operator sits inside a user-provided $(), emit the maash eval
                // invocation bare so it becomes the body of that $(), consuming the closer
                // (already consumed above).
                output.Append("maash eval ");
                output.Append(QuoteBash("@@" + resolved.OperatorString));
                output.Append(' ');
                output.Append(QuoteBash(t));
                output.Append(")");
            } else {
                // When it is NOT inside $(), wrap the invocation in $( ) so the
                // substitution result is captured inline in the bash output.
                output.Append("$(maash eval ");
                output.Append(QuoteBash("@@" + resolved.OperatorString));
                output.Append(' ');
                output.Append(QuoteBash(t));
                output.Append(")");
            }

            pos = end;
        }

        return new PreprocessResult(output.ToString(), warnings);
    }

    /// <summary>
    /// Emit a run of source text verbatim, but unescape any <c>\@@</c>
    /// sequences (a backslash immediately before an operator token) into a
    /// bare, literal <c>@@</c>. Implements the README backslash rule:
    /// a leading backslash on an operator means "literal, no operator".
    /// </summary>
    public static string Unescape(string text, int start, int length) {
        var slice = text.Substring(start, length);
        var outp = new StringBuilder(slice.Length);
        var i = 0;
        while (i < slice.Length) {
            if (slice[i] == '\\' && i + 2 < slice.Length && slice[i + 1] == '@' && slice[i + 2] == '@') {
                outp.Append("@@");
                i += 3;
            } else {
                outp.Append(slice[i]);
                i++;
            }
        }
        return outp.ToString();
    }

    static bool IsAllWhitespace(string s, int start, int length) {
        for (var i = start; i < start + length; i++) {
            if (!char.IsWhiteSpace(s[i])) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Bash single-quote quoting, escaping embedded single quotes using the
    /// classic <c>'\''</c> splice.
    /// </summary>
    public static string QuoteBash(string s) {
        var builder = new StringBuilder(s.Length + 5);
        builder.Append('\'');
        for (var i = 0; i < s.Length; i++) {
            if (s[i] == '\'') {
                builder.Append("'\\''");
            } else {
                builder.Append(s[i]);
            }
        }
        builder.Append('\'');
        return builder.ToString();
    }
}

/// <summary>Result of a preprocess pass.</summary>
public sealed record PreprocessResult(string Text, IReadOnlyList<string> Warnings);

/// <summary>
/// Result of a module render: the result text and how many chars of the
/// operand the module consumed.
/// </summary>
public sealed record EvalOutcome(string Output, int ParsedLength);

public class MaashPreprocessorException : MaashException {
    public MaashPreprocessorException(string message) : base(message) { }
}
