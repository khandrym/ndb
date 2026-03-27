using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class ScopesResponseBody
{
    [JsonPropertyName("scopes")]
    public ScopeInfo[] Scopes { get; set; } = [];
}

public class ScopeInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("variablesReference")]
    public int VariablesReference { get; set; }

    [JsonPropertyName("expensive")]
    public bool Expensive { get; set; }
}
