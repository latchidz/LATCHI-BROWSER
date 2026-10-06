using System.Text.Json.Nodes;

namespace LatchiBrowser.Core.Services;

/// <summary>
/// Gemini AI plumbing (§54-§58). Pure request-building + response-parsing — fully
/// unit-testable offline; the HTTP call lives in the App. The API key is NEVER a
/// parameter here (it travels as a header from secure storage only, §56/§57).
/// </summary>
public static class Gemini
{
    /// <summary>Latest stable Flash model at build time — user-overridable in Settings (§55).</summary>
    public const string DefaultModel = "gemini-2.5-flash";

    /// <summary>The LATCHI AI system prompt — spec §58, verbatim.</summary>
    public const string SystemPrompt =
        """
        You are LATCHI AI, the intelligent assistant built into LATCHI Browser.

        Your role is to help the user while browsing.

        You can:
        - Explain webpages.
        - Summarize user-provided page content.
        - Translate text.
        - Help write prompts.
        - Help with AI tools.
        - Explain technical problems.
        - Help with browser settings.
        - Help organize browsing tasks.
        - Answer questions clearly.
        - Maintain conversation context during the active session.

        You must:
        - Never invent facts.
        - Never claim that you performed a browser action unless the browser actually performed it.
        - Never request or expose passwords.
        - Never request authentication cookies.
        - Never expose API keys.
        - Never expose OAuth tokens.
        - Never pretend to be Google, Microsoft, or another service.
        - Clearly distinguish between information from the webpage and your own reasoning.

        You are an AI assistant inside a browser.
        """;

    /// <summary>REST endpoint for generateContent (v1beta stable API).</summary>
    public static string Endpoint(string model) =>
        $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

    /// <summary>Builds the JSON body: system instruction + full conversation + new user turn.</summary>
    public static string BuildRequestBody(string userText, IReadOnlyList<(string Role, string Text)> history)
    {
        var root = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = SystemPrompt }),
            },
            ["contents"] = new JsonArray(),
        };
        var contents = (JsonArray)root["contents"]!;
        foreach (var (role, text) in history)
            contents.Add(new JsonObject
            {
                ["role"] = role == "model" ? "model" : "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = text }),
            });
        contents.Add(new JsonObject
        {
            ["role"] = "user",
            ["parts"] = new JsonArray(new JsonObject { ["text"] = userText }),
        });
        return root.ToJsonString();
    }

    /// <summary>Parses a successful generateContent response into plain text.
    /// Throws a clear message on API errors (quota, bad key, bad model…).</summary>
    public static string ParseResponse(string json)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch { throw new InvalidOperationException("Invalid response from Gemini"); }

        var error = root!["error"]?["message"]?.ToString();
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException(error);

        var candidates = root["candidates"] as JsonArray;
        if (candidates is null || candidates.Count == 0)
            throw new InvalidOperationException("Gemini returned no content (try again or check the model name)");

        var parts = candidates[0]?["content"]?["parts"] as JsonArray;
        if (parts is null || parts.Count == 0)
            throw new InvalidOperationException("Gemini returned no content (try again or check the model name)");

        var text = string.Concat(parts.Select(p => p?["text"]?.ToString() ?? ""));
        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidOperationException("Gemini returned an empty answer")
            : text;
    }
}
