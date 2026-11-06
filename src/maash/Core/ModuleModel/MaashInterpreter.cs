using System.Text.Json.Serialization;

namespace Maash.Core.ModuleModel;

/// <summary>How to spawn the persistent interpreter process for a module.</summary>
public sealed record MaashInterpreter {
    [JsonPropertyName("command")]
    public string? Command { get; init; }

    [JsonPropertyName("args")]
    public string[] Args { get; init; } = System.Array.Empty<string>();

    /// <summary>true if the interpreter command and args are relative to the module's SourceDir.</summary>
    public bool? RelativePaths { get; init; }
}
