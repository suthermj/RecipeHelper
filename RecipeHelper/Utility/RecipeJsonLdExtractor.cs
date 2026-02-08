using System.Net;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;

namespace RecipeHelper.Utility; 
public static class RecipeJsonLdExtractor
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<JsonElement?> ExtractRecipeNodeAsync(HttpClient http, string url, CancellationToken ct = default)
    {
        // Basic request headers help avoid some 403s
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; RecipeHelperBot/1.0)");
        req.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var html = await resp.Content.ReadAsStringAsync(ct);

        // Parse HTML
        var config = Configuration.Default;
        var ctx = BrowsingContext.New(config);
        var doc = await ctx.OpenAsync(r => r.Content(html).Address(url), ct);

        // Find all ld+json blocks
        var scripts = doc.QuerySelectorAll("script[type='application/ld+json']")
                         .Select(s => s.TextContent)
                         .Where(t => !string.IsNullOrWhiteSpace(t))
                         .ToList();

        foreach (var raw in scripts)
        {
            // Some sites include invalid JSON (e.g., multiple objects without an array),
            // so we attempt parsing carefully.
            foreach (var root in ParseJsonRootsLenient(raw))
            {
                var recipe = FindRecipeNode(root);
                if (recipe.HasValue)
                    return recipe.Value;
            }
        }

        return null;
    }

    // ---- Helpers ----

    /// <summary>
    /// Tries to parse a JSON-LD script. Handles cases where the script contains:
    /// - a single object
    /// - an array
    /// - an @graph
    /// If parsing fails, returns empty.
    /// </summary>
    private static List<JsonElement> ParseJsonRootsLenient(string json)
    {
        var results = new List<JsonElement>();

        json = json.Trim('\uFEFF', '\u200B', ' ', '\n', '\r', '\t');

        try
        {
            using var doc = JsonDocument.Parse(json);
            results.Add(doc.RootElement.Clone());
            return results;
        }
        catch
        {
            // ignore and try fallback
        }

        var wrapped = TryWrapAsArray(json);
        if (wrapped == null)
            return results;

        try
        {
            using var doc = JsonDocument.Parse(wrapped);
            results.Add(doc.RootElement.Clone());
        }
        catch
        {
            // swallow — invalid JSON-LD
        }

        return results;
    }

    private static string? TryWrapAsArray(string json)
    {
        // Very conservative: only wrap if it appears to be multiple top-level objects.
        // e.g. "{...}{...}" -> "[{...},{...}]"
        if (!json.StartsWith("{")) return null;

        // If it ends with "}" and has "}{", it's a decent hint.
        if (json.Contains("}{"))
        {
            return "[" + json.Replace("}{", "},{") + "]";
        }

        return null;
    }

    /// <summary>
    /// Returns the first node that has @type containing "Recipe"
    /// within:
    /// - object root
    /// - array root
    /// - object["@graph"]
    /// </summary>
    private static JsonElement? FindRecipeNode(JsonElement root)
    {
        // Root can be array
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var found = FindRecipeNode(item);
                if (found.HasValue) return found;
            }
            return null;
        }

        // Root can be object with @graph
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (IsRecipeType(root)) return root;

            if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in graph.EnumerateArray())
                {
                    if (node.ValueKind == JsonValueKind.Object && IsRecipeType(node))
                        return node;
                }
            }

            // Sometimes nested in "mainEntity" or similar
            foreach (var prop in root.EnumerateObject())
            {
                var val = prop.Value;
                if (val.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    var found = FindRecipeNode(val);
                    if (found.HasValue) return found;
                }
            }
        }

        return null;
    }

    private static bool IsRecipeType(JsonElement obj)
    {
        if (!obj.TryGetProperty("@type", out var typeProp))
            return false;

        if (typeProp.ValueKind == JsonValueKind.String)
            return string.Equals(typeProp.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase);

        if (typeProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in typeProp.EnumerateArray())
            {
                if (t.ValueKind == JsonValueKind.String &&
                    string.Equals(t.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
