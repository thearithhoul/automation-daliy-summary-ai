

using System.Text.Json;
using ChongReanProject.Config;
using System.Linq;
using ChongReanProject.Func;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChongReanProject.Domains;

namespace ChongReanProject.Controller;

[ApiController]
[Route("/api/v1/article")]
// [Authorize]
public class ScrapingArticleController(
    ArticleScrapingClient scrapingClient,
    FirebaseStroing firebaseStroing
    ) : ControllerBase
{
    [HttpGet("get-scraping-article")]
    public async Task<ActionResult<JsonElement>> getArticleScraping([FromQuery] string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest("Invalid url.");
        }

        string domain = uri.Host;
        var result = await scrapingClient.ScrapeAsync(url);

        // One document per article under Raw/{domain}/articles/{articleId}, instead of a
        // single growing `articles` array field on the Raw/{domain} document - avoids the
        // 1 MiB per-document limit, lets is_summarize be queried/updated per article, and
        // means re-scraping only touches the specific articles that changed.
        var articlesPath = FirebaseStroing.SubCollectionPath(["Raw", domain, "articles"]);

        int i = 0;
        foreach (var articleElement in result.GetProperty("articles").EnumerateArray())
        {
            var article = (Dictionary<string, object?>)ToFirestoreValue(articleElement)!;
            article["is_summarize"] = false;
            i++;
            await firebaseStroing.WriteAsync(articlesPath, i.ToString(), article);
        }

        // var list = await geminiAiService.FilterArticle(result.GetProperty("articles"));
        return Ok();
    }

    private static object? ToFirestoreValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => ToFirestoreValue(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(ToFirestoreValue).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    // [HttpGet("summarize-article")]
    // public async Task<ActionResult> SummarizeArticle()
    // {

    // }

}