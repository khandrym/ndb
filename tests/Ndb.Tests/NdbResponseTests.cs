using System.Text.Json;
using Ndb.Models;
using Ndb.Json;

namespace Ndb.Tests;

public class NdbResponseTests
{
    [Fact]
    public void Success_SerializesToExpectedJson()
    {
        var response = NdbResponse.Ok("launch", new { pid = 1234 });
        var json = JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("launch", root.GetProperty("command").GetString());
        Assert.Equal(1234, root.GetProperty("data").GetProperty("pid").GetInt32());
    }

    [Fact]
    public void Error_SerializesToExpectedJson()
    {
        var response = NdbResponse.Fail("launch", "netcoredbg not found");
        var json = JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("launch", root.GetProperty("command").GetString());
        Assert.Equal("netcoredbg not found", root.GetProperty("error").GetString());
    }
}
