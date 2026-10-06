using System.Text.Json.Nodes;
using LatchiBrowser.Core.Services;

namespace LatchiBrowser.Tests;

/// <summary>Gemini request/response plumbing (§54-§58) — pure, offline, no network.</summary>
public class GeminiTests
{
    [Fact]
    public void Endpoint_UsesV1BetaGenerateContent()
    {
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent",
            Gemini.Endpoint("gemini-2.5-flash"));
    }

    [Fact]
    public void SystemPrompt_IsTheSpecText_WithAllSafetyRules()
    {
        // §58 verbatim guards — these MUST all be present
        Assert.Contains("LATCHI AI", Gemini.SystemPrompt);
        Assert.Contains("Never invent facts", Gemini.SystemPrompt);
        Assert.Contains("Never claim that you performed a browser action", Gemini.SystemPrompt);
        Assert.Contains("Never request or expose passwords", Gemini.SystemPrompt);
        Assert.Contains("Never expose API keys", Gemini.SystemPrompt);
        Assert.Contains("Never expose OAuth tokens", Gemini.SystemPrompt);
        Assert.Contains("Never pretend to be Google, Microsoft", Gemini.SystemPrompt);
    }

    [Fact]
    public void BuildRequestBody_ContainsSystemInstruction_History_AndNewTurn()
    {
        var history = new List<(string, string)>
        {
            ("user", "hello"),
            ("model", "hi!"),
        };
        var json = Gemini.BuildRequestBody("what is 2+2?", history);
        var root = JsonNode.Parse(json)!;

        Assert.Equal(Gemini.SystemPrompt, root["systemInstruction"]!["parts"]![0]!["text"]!.ToString());

        var contents = (JsonArray)root["contents"]!;
        Assert.Equal(3, contents.Count);
        Assert.Equal("user", contents[0]!["role"]!.ToString());
        Assert.Equal("hello", contents[0]!["parts"]![0]!["text"]!.ToString());
        Assert.Equal("model", contents[1]!["role"]!.ToString());
        Assert.Equal("hi!", contents[1]!["parts"]![0]!["text"]!.ToString());
        Assert.Equal("user", contents[2]!["role"]!.ToString());
        Assert.Equal("what is 2+2?", contents[2]!["parts"]![0]!["text"]!.ToString());
    }

    [Fact]
    public void BuildRequestBody_NoHistory_StillValid()
    {
        var root = JsonNode.Parse(Gemini.BuildRequestBody("hi", Array.Empty<(string, string)>()))!;
        var contents = (JsonArray)root["contents"]!;
        Assert.Single(contents);
        Assert.Equal("user", contents[0]!["role"]!.ToString());
    }

    [Fact]
    public void ParseResponse_JoinsParts()
    {
        var json = """
        {
          "candidates": [
            { "content": { "parts": [ { "text": "Hello " }, { "text": "world" } ] } }
          ]
        }
        """;
        Assert.Equal("Hello world", Gemini.ParseResponse(json));
    }

    [Fact]
    public void ParseResponse_SurfacesApiErrorMessages()
    {
        var json = """{ "error": { "message": "API key not valid. Please pass a valid API key." } }""";
        var ex = Assert.Throws<InvalidOperationException>(() => Gemini.ParseResponse(json));
        Assert.Contains("API key not valid", ex.Message);
    }

    [Fact]
    public void ParseResponse_MissingCandidates_ThrowsClearError()
    {
        Assert.Throws<InvalidOperationException>(() => Gemini.ParseResponse("""{"foo":1}"""));
        Assert.Throws<InvalidOperationException>(() => Gemini.ParseResponse("""{"candidates":[]}"""));
        Assert.Throws<InvalidOperationException>(() => Gemini.ParseResponse("not json at all"));
        // blocked/empty parts → clear error too
        Assert.Throws<InvalidOperationException>(
            () => Gemini.ParseResponse("""{"candidates":[{"content":{"parts":[{"text":""}]}}]}"""));
    }
}
