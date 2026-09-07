

using System.Text.Json;
using ChongReanProject.Func;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChongReanProject.Controller;

[ApiController]
[Route("/api/v1/article")]
// [Authorize]
public class ScrapingArticleController(ArticleScrapingClient scrapingClient, GeminiAiService geminiAiService) : ControllerBase
{
    [HttpGet("get-scraping-article")]
    public async Task<ActionResult<JsonElement>> getArticleScraping([FromQuery] string url)
    {
        var result = await scrapingClient.ScrapeAsync(url);
        var list = await geminiAiService.FilterArticle(result.GetProperty("articles"));
        return Ok(list);
    }
}