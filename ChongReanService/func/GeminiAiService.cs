using System.Text.Json;
using ChongReanProject.Config;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using GenAiType = Google.GenAI.Types.Type;

namespace ChongReanProject.Func;

public class GeminiAiService
{
    private const string DefaultModel = "gemini-3.6-flash";

    // Keep in sync with docs/article-filter-instruction.md - that file is the
    // source of truth for this task's rules; this is its content as the
    // Gemini SystemInstruction for FilterArticle.
    private const string ArticleFilterInstruction = """
        Task: filter a list of scraped page sections down to only the real
        articles/content sections, dropping site navigation clusters that got
        parsed the same way (heading + list of links).

        Input shape (each item in the array you're given):
            title: string | null
            subtitle: string | null
            links: list of { text: string, url: string }
            metadata: object
            other: string

        How to tell them apart:

        Primary rule - is this one coherent topic, or a grab-bag of site links?
        - Real article: the title names a specific topic, and every link is a
          deeper/related page about that same topic (sub-sections, "read more"
          links, next steps) - the links read like a table of contents for the
          title's topic.
        - Not an article: the title is a generic site section (About, Support,
          Contact, Sponsors, Team, Legal, Privacy, Community, Resources,
          footer/utility sections), and the links are unrelated to each other
          beyond "things in this site area" (e.g. an FAQ, a privacy policy, a
          YouTube video, and a code of conduct all under "About").

        Supporting hint (not a hard rule) - the `other` field:
        Non-empty `other` is a mild positive signal, empty is a mild negative
        signal, but this depends on incidental source-page formatting. Don't
        reject an otherwise-clearly-real article just because `other` is
        empty, and don't keep an otherwise-clearly-navigational section just
        because `other` has text.

        Other supporting signals:
        - Navigation sections are more likely to mix in an external/social
          link (YouTube, Twitter, a sponsor site); a real article's links
          usually stay within the same site/doc section.
        - A real article set usually comes with several sibling sections at
          the same heading level; a page-level nav block is often a single
          isolated section.

        Examples:
        - NOT an article - "About": links = FAQ, Team, Releases, Community
          Guide, Code of Conduct, Privacy Policy, The Documentary (YouTube).
          Why: the links share nothing beyond "stuff under About" - a policy
          page, a video, a releases page, a contributor guide are not
          sub-topics of one article.
        - NOT an article - "Support": links = Sponsor, Partners.
          Why: two unrelated funding/utility links, not article content.
        - REAL article - "Scaling Up": links = Single-File Components,
          Tooling, Routing, State Management, Testing, Server-Side Rendering
          (SSR). Why: every link is a specific technique for scaling up a Vue
          app - all sub-topics of exactly what the title describes.
        - REAL article - "Best Practices": links = Production Deployment,
          Performance, Accessibility, Security. Why: same pattern - every
          link is a specific best practice under a title that promises them.

        Output: respond with a single JSON object { "keep_indices": [0, 3, 4] }
        - the 0-based indices, into the input array, of entries that are real
        articles. Do not re-emit the article content itself.
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