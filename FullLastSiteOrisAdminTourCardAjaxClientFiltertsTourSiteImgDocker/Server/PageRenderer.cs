using System.IO;
using System.Threading.Tasks;

namespace HttpListenerServer;

public class PageRenderer
{
    private readonly string _connString;
    private readonly string _staticFolder;
    private readonly TourCardRenderer _cardRenderer;
    private readonly TourDetailRenderer _detailRenderer;

    public PageRenderer(string connectionString, string staticFolder)
    {
        _connString = connectionString;
        _staticFolder = Path.GetFullPath(staticFolder);

        _cardRenderer = new TourCardRenderer(_connString, _staticFolder);
        _detailRenderer = new TourDetailRenderer(_connString, _staticFolder);
    }

    public async Task<string> RenderMainPageAsync()
    {
        var (cardsHtml, totalTours) = await _cardRenderer.RenderAllCardsAsync(new Dictionary<string, string>());

        string templatePath = Path.Combine(_staticFolder, "index.html");
        string template = await File.ReadAllTextAsync(templatePath);

        return template
            .Replace("{{CARDS}}", cardsHtml)
            .Replace("{{TOTAL_TOURS}}", totalTours.ToString());
    }

    public async Task<string?> RenderTourDetailAsync(int id)
        => await _detailRenderer.RenderTourAsync(id);

    public async Task<(string CardsHtml, int TotalCount)> RenderFilteredCardsAsync(Dictionary<string, string> filters)
        => await _cardRenderer.RenderAllCardsAsync(filters);
}
