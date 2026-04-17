using System.Text.Json.Serialization;

namespace Hi3Helper.Plugin.Endfield.Management.Api;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(EndfieldBatchRequest))]
[JsonSerializable(typeof(EndfieldBatchResponse))]
[JsonSerializable(typeof(EndfieldPatchManifest))]
public partial class EndfieldApiContext : JsonSerializerContext
{
}