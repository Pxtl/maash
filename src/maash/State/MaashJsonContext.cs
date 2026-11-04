using System.Text.Json;
using System.Text.Json.Serialization;
using Maash.Core;

namespace Maash.State;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/>. maash publishes with
/// Native AOT (<c>PublishAot</c>), where reflection-based JSON serialization
/// is unavailable; a source-generated context lets the state store serialize
/// without runtime reflection or trimmer warnings.
/// </summary>
[JsonSerializable(typeof(LoadedModuleEntry))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(MaashRegistryState))]
[JsonSerializable(typeof(MaashModule))]
public partial class MaashJsonContext : JsonSerializerContext {
}
