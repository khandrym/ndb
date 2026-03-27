using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class InitializeArguments
{
    [JsonPropertyName("clientID")]
    public string ClientId { get; set; } = "ndb";

    [JsonPropertyName("clientName")]
    public string ClientName { get; set; } = "ndb";

    [JsonPropertyName("adapterID")]
    public string AdapterId { get; set; } = "coreclr";

    [JsonPropertyName("linesStartAt1")]
    public bool LinesStartAt1 { get; set; } = true;

    [JsonPropertyName("columnsStartAt1")]
    public bool ColumnsStartAt1 { get; set; } = true;

    [JsonPropertyName("pathFormat")]
    public string PathFormat { get; set; } = "path";
}
