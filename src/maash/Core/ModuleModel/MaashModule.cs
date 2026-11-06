using System.Text.Json.Serialization;

namespace Maash.Core.ModuleModel;

/// <summary>
/// Parsed module definition (JSON schema from README §"Module JSON").
/// </summary>
public sealed record MaashModule {
    private const string Version = "v1";

    [JsonPropertyName("version")]
    public string VersionStr { get; init; } = Version;

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("interpreter")]
    public MaashInterpreter? Interpreter { get; init; }

    [JsonPropertyName("shell")]
    public string? Shell { get; init; } = null;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Default infix for operators when the user does not pass one (-x/--infix).
    /// Must be non-empty: a module is always imported on its infix, so a module
    /// should never have an empty default infix. Falls back to the module name
    /// when unset.
    /// </summary>
    [JsonPropertyName("defaultInfix")]
    public string? DefaultInfix { get; init; }

    [JsonPropertyName("operators")]
    public Dictionary<string, MaashOperatorDefinition> Operators { get; init; } = new();

    /// <summary>Directory the module was loaded from (for shell-script relative paths).</summary>
    [JsonPropertyName("sourceDir")]
    public string? SourceDir { get; init; }

    /// <summary>The file this module definition was loaded from (if any).</summary>
    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; init; }
}
