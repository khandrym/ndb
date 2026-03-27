using System.Text.Json;
using System.Text.Json.Serialization;
using Ndb.Models;

namespace Ndb.Json;

[JsonSerializable(typeof(NdbResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class NdbJsonContext : JsonSerializerContext;
