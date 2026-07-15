using System.Text;
using System.Text.Json;

namespace DanceManager.Api.Services;

/// <summary>One dancer to place, as supplied by the client.</summary>
public record FormationDancer(int StudentId, string? Gender, string? FirstName);

/// <summary>A stage position in 0–100 percentages (matches Formation.studentCoordinates).</summary>
public record FormationCoord(double X, double Y);

/// <summary>
/// Result of an AI formation suggestion. <c>Configured</c> is false when no Gemini
/// key is set; <c>Ok</c> is false when the call failed (the client then falls back
/// to a local template). Coordinates are keyed by studentId.
/// </summary>
public record FormationSuggestion(bool Configured, bool Ok, Dictionary<int, FormationCoord> Coordinates);

/// <summary>
/// Suggests a stage formation for a set of dancers using Google Gemini's free tier.
/// The API key lives server-side only. Gemini's output is always validated and
/// repaired (every dancer placed, in-bounds, spaced apart) so a bad/garbled model
/// response can never reach the client or break the formation.
/// </summary>
public class FormationAiService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string? _apiKey;
    private readonly string _model;

    // Stage margins + minimum spacing, in the same 0–100 space as the app's grid.
    private const double Margin = 6;
    private const double MinGap = 11;

    public FormationAiService(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _apiKey = config["Gemini:ApiKey"];
        _model = string.IsNullOrWhiteSpace(config["Gemini:Model"]) ? "gemini-2.5-flash" : config["Gemini:Model"]!;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<FormationSuggestion> SuggestAsync(
        IReadOnlyList<FormationDancer> dancers, string? description, CancellationToken ct = default)
    {
        if (dancers.Count == 0)
            return new FormationSuggestion(IsConfigured, true, new());

        if (!IsConfigured)
            return new FormationSuggestion(false, false, new());

        try
        {
            var raw = await CallGeminiAsync(dancers, description, ct);
            var coords = ParseCoords(raw, dancers);
            Repair(coords, dancers); // fill missing, clamp, space apart
            return new FormationSuggestion(true, true, coords);
        }
        catch
        {
            // Timeout, network, bad key, safety block, unparseable output — all
            // degrade to "not ok" so the client offers a template instead.
            return new FormationSuggestion(true, false, new());
        }
    }

    private async Task<string> CallGeminiAsync(
        IReadOnlyList<FormationDancer> dancers, string? description, CancellationToken ct)
    {
        var prompt = BuildPrompt(dancers, description);
        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            studentId = new { type = "INTEGER" },
                            x = new { type = "NUMBER" },
                            y = new { type = "NUMBER" },
                        },
                        required = new[] { "studentId", "x", "y" },
                    },
                },
            },
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(8));

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";
        var client = _httpFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);

        using var resp = await client.SendAsync(request, cts.Token);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
        // candidates[0].content.parts[0].text holds the JSON array (responseMimeType=json).
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? throw new InvalidOperationException("empty Gemini text");
    }

    private static string BuildPrompt(IReadOnlyList<FormationDancer> dancers, string? description)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You arrange dancers on a stage into a formation.");
        sb.AppendLine("Coordinates are percentages from 0 to 100 on a rectangular stage:");
        sb.AppendLine("- x: 0 = stage-left edge, 100 = stage-right edge, 50 = center.");
        sb.AppendLine("- y: 0 = upstage (back of stage, farthest from the audience), 100 = downstage (front, closest to the audience).");
        sb.AppendLine("Rules:");
        sb.AppendLine("- Give every dancer an (x, y).");
        sb.AppendLine("- Keep everyone within x 6–94 and y 6–94.");
        sb.AppendLine("- Space dancers apart so none overlap (at least ~12 units between any two).");
        sb.AppendLine("- Make it balanced and roughly symmetric about x=50 unless the description says otherwise.");
        sb.AppendLine("- Distribute boys and girls sensibly.");
        sb.AppendLine("- Shape the formation to fit the described number's style/mood.");
        sb.AppendLine();
        sb.AppendLine("Dancers:");
        foreach (var d in dancers)
        {
            var g = d.Gender == "Boys" ? "boy" : d.Gender == "Girls" ? "girl" : "dancer";
            var name = string.IsNullOrWhiteSpace(d.FirstName) ? $"#{d.StudentId}" : d.FirstName;
            sb.AppendLine($"- studentId {d.StudentId}: {g}, {name}");
        }
        sb.AppendLine();
        sb.Append("Choreographer's description: ");
        sb.AppendLine(string.IsNullOrWhiteSpace(description) ? "a balanced general formation." : description!.Trim());
        sb.AppendLine();
        sb.AppendLine("Return one entry per dancer.");
        return sb.ToString();
    }

    private static Dictionary<int, FormationCoord> ParseCoords(string raw, IReadOnlyList<FormationDancer> dancers)
    {
        // Be lenient: if the model wrapped the array in prose, grab the first [...] block.
        var json = raw.Trim();
        if (!json.StartsWith('['))
        {
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start >= 0 && end > start) json = json[start..(end + 1)];
        }

        var valid = new HashSet<int>(dancers.Select(d => d.StudentId));
        var result = new Dictionary<int, FormationCoord>();
        using var doc = JsonDocument.Parse(json);
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (!el.TryGetProperty("studentId", out var idEl)) continue;
            var id = idEl.GetInt32();
            if (!valid.Contains(id)) continue;
            var x = el.TryGetProperty("x", out var xe) ? xe.GetDouble() : 50;
            var y = el.TryGetProperty("y", out var ye) ? ye.GetDouble() : 50;
            result[id] = new FormationCoord(x, y);
        }
        return result;
    }

    /// <summary>Fill any missing dancers, clamp to the stage, and nudge apart overlaps.</summary>
    private static void Repair(Dictionary<int, FormationCoord> coords, IReadOnlyList<FormationDancer> dancers)
    {
        // Fill missing with a default grid spread.
        var missing = dancers.Where(d => !coords.ContainsKey(d.StudentId)).ToList();
        if (missing.Count > 0)
        {
            var spread = SpreadGrid(missing.Count);
            for (var i = 0; i < missing.Count; i++)
                coords[missing[i].StudentId] = spread[i];
        }

        // Work on a mutable array in a stable order.
        var ids = dancers.Select(d => d.StudentId).Where(coords.ContainsKey).ToList();
        var pts = ids.Select(id => coords[id]).Select(c => new double[] { c.X, c.Y }).ToArray();

        void Clamp()
        {
            foreach (var p in pts)
            {
                p[0] = Math.Clamp(p[0], Margin, 100 - Margin);
                p[1] = Math.Clamp(p[1], Margin, 100 - Margin);
            }
        }
        Clamp();

        // A few relaxation passes to separate overlapping dancers.
        for (var pass = 0; pass < 12; pass++)
        {
            var moved = false;
            for (var a = 0; a < pts.Length; a++)
                for (var b = a + 1; b < pts.Length; b++)
                {
                    var dx = pts[a][0] - pts[b][0];
                    var dy = pts[a][1] - pts[b][1];
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist >= MinGap) continue;
                    moved = true;
                    if (dist < 0.001) { dx = ((a % 3) - 1); dy = ((b % 3) - 1); dist = Math.Max(0.001, Math.Sqrt(dx * dx + dy * dy)); }
                    var push = (MinGap - dist) / 2 + 0.5;
                    var ux = dx / dist; var uy = dy / dist;
                    pts[a][0] += ux * push; pts[a][1] += uy * push;
                    pts[b][0] -= ux * push; pts[b][1] -= uy * push;
                }
            Clamp();
            if (!moved) break;
        }

        for (var i = 0; i < ids.Count; i++)
            coords[ids[i]] = new FormationCoord(Math.Round(pts[i][0], 1), Math.Round(pts[i][1], 1));
    }

    private static List<FormationCoord> SpreadGrid(int n)
    {
        var cols = (int)Math.Ceiling(Math.Sqrt(n));
        var rows = (int)Math.Ceiling((double)n / cols);
        var list = new List<FormationCoord>(n);
        for (var i = 0; i < n; i++)
        {
            var r = i / cols;
            var c = i % cols;
            var x = cols == 1 ? 50 : 15 + c * (70.0 / (cols - 1));
            var y = rows == 1 ? 50 : 15 + r * (70.0 / (rows - 1));
            list.Add(new FormationCoord(x, y));
        }
        return list;
    }
}
