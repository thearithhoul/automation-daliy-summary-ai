using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChongReanProject.Config;
using Microsoft.Extensions.Options;

namespace ChongReanProject.Func;

// Talks to the artical-web-scraping FastAPI service (POST /scrape). That
// service has its own JWT auth (src/auth.py) - unrelated to this API's own
// AuthService, hence the separate ApiToken here.
public class ArticleScrapingClient
{
    private readonly HttpClient _httpClient;

    public ArticleScrapingClient(HttpClient httpClient, IOptions<ScrapingServiceSetting> settings)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(settings.Value.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", settings.Value.ApiToken);
    }

    public async Task<JsonElement> ScrapeAsync(string url)
    {
        var response = await _httpClient.PostAsJsonAsync("/scrape", new { url });
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
