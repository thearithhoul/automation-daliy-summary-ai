

using System.Collections.ObjectModel;
using ChongReanProject.Domains;
using ChongReanProject.Func;
using Google.Rpc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChongReanProject.Controller;


[ApiController]
[Route("/api/v1/khmertime")]
// [Authorize]
public class KhmerTimeController(
    FirebaseStroing firebaseStroing,
    KhmerTimeParser khmerTimeParser,
    ILogger<KhmerTimeController> logger) : ControllerBase
{

    private readonly string collectionId = "Raw";
    private readonly string documentId = "www.khmertimeskh.com";

    [HttpGet("organiza_articles")]
    public async Task<ActionResult> GetOrganizaArticles()
    {
        var rawArticlesPath = FirebaseStroing.SubCollectionPath([collectionId, documentId, "articles"]);
        var rawArticles = await firebaseStroing.ReadAllAsync<Dictionary<string, object>>(rawArticlesPath);

        foreach (var articleMap in rawArticles)
        {
            if (articleMap.GetBool("is_summarize") == true) continue;

            var title = articleMap.GetString("title");
            if (title is null || string.IsNullOrWhiteSpace(title)) continue;

            List<string> list = khmerTimeParser.ParsingTitle(title);

            if (list.Count < 2) continue;
            string url = list[1];
            string? uniqueId = khmerTimeParser.ParsingUniqueId(url);
            if (uniqueId is null) continue;

            Dictionary<string, object?> organizeArticle = new()
            {
                ["uniqueId"] = uniqueId,
                ["title"] = list[0],
                ["url"] = url,
                ["detail"] = null,
                ["summrize"] = null,
            };

            var path = FirebaseStroing.SubCollectionPath(["organize_articles", documentId, "articles"]);

            try
            {
                await firebaseStroing.WriteAsync(path, uniqueId, organizeArticle);
                logger.LogInformation("Wrote article {UniqueId} to {Path}/{UniqueId}", uniqueId, path, uniqueId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write article {UniqueId} to {Path}/{UniqueId}", uniqueId, path, uniqueId);
                continue;
            }

            await firebaseStroing.UpdateAsync(rawArticlesPath, uniqueId, new Dictionary<string, object> { ["is_summarize"] = true });
        }

        return Ok();
    }


}


