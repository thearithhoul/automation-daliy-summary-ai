using System.Text.Json;
using ChongReanProject.Config;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using GenAiType = Google.GenAI.Types.Type;

namespace ChongReanProject.Func;

public class GeminiAiService
{
    private const string DefaultModel = "gemini-3.8-flash";

    // Keep in sync with docs/article-filter-instruction.md - that file is the
    // source of truth for this task's rules; this is its content as the
    // Gemini SystemInstruction for FilterArticle.
    private const string ArticleFilterInstruction = """
    
        """;

    private readonly Client _client;

    public GeminiAiService(IOptions<GeminiSetting> settings)
    {
        var apiKey = settings.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Missing 'Gemini:ApiKey' configuration value.");
        }

        // The SDK makes exactly one attempt per call unless RetryOptions is set
        // explicitly - without this, a transient 503/429 ("high demand")
        // from Gemini surfaces straight to the caller instead of being retried.
        _client = new Client(apiKey: apiKey, httpOptions: new HttpOptions
        {
            RetryOptions = new HttpRetryOptions
            {
                Attempts = 5,
                InitialDelay = 1,
                MaxDelay = 20,
                ExpBase = 2,
                Jitter = 1,
                HttpStatusCodes = [408, 429, 500, 502, 503, 504],
            },
        });
    }

    public async Task<string> GenerateTextAsync(string prompt, string model = DefaultModel)
    {
        var response = await _client.Models.GenerateContentAsync(model: model, contents: prompt);
        return response.Candidates?[0].Content?.Parts?[0].Text ?? string.Empty;
    }

    // Keep batches well under Gemini's input limits and away from the point
    // where accuracy degrades on long inputs.
    private const int MaxArticlesPerBatch = 20;

    // articles: the scraped `articles` array (see docs/article-filter-instruction.md
    // §1). Returns the same array's entries, filtered down to the ones
    // classified as real articles - Gemini only decides which indices to
    // keep per batch (§5 of the instruction doc); the filtering itself
    // happens here, deterministically, against the original JSON. Large
    // inputs are split into batches so a single call never gets too long.
    public async Task<List<JsonElement>> FilterArticle(JsonElement articles, string model = DefaultModel)
    {
        var all = articles.EnumerateArray().ToList();
        var results = new List<JsonElement>();
        foreach (var batch in all.Chunk(MaxArticlesPerBatch))
        {
            results.AddRange(await FilterBatch(batch, model));
        }

        return results;
    }

    private async Task<List<JsonElement>> FilterBatch(IReadOnlyList<JsonElement> articles, string model)
    {
        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = [new Part { Text = ArticleFilterInstruction }]
            },
            Temperature = 0.1,
            ResponseMimeType = "application/json",
            ResponseSchema = new Schema
            {
                Type = GenAiType.Object,
                Properties = new Dictionary<string, Schema>
                {
                    ["keep_indices"] = new Schema
                    {
                        Type = GenAiType.Array,
                        Items = new Schema { Type = GenAiType.Integer },
                        Description = "0-based indices, into the input array, of entries that are real articles.",
                    },
                },
                Required = ["keep_indices"],
            },
        };

        var articlesJson = JsonSerializer.Serialize(articles);
        var response = await _client.Models.GenerateContentAsync(model: model, contents: articlesJson, config: config);
        var resultText = response.Candidates?[0].Content?.Parts?[0].Text;
        if (string.IsNullOrWhiteSpace(resultText))
        {
            return [];
        }

        var keepIndices = JsonDocument.Parse(resultText).RootElement
            .GetProperty("keep_indices")
            .EnumerateArray()
            .Select(i => i.GetInt32())
            .ToHashSet();

        return keepIndices
            .Where(i => i >= 0 && i < articles.Count)
            .Select(i => articles[i])
            .ToList();
    }
}