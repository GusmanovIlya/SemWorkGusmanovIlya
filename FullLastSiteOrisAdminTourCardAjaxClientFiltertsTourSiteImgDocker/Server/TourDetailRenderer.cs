using Npgsql;
using System.Text;
using System.Web;

namespace HttpListenerServer;

public class TourDetailRenderer
{
    private readonly string _connString;
    private readonly string _templatePath;

    public TourDetailRenderer(string connString, string staticFolder)
    {
        _connString = connString;
        _templatePath = Path.Combine(staticFolder, "templates", "tour_detail.html");
    }

    public async Task<string?> RenderTourAsync(int tourId)
    {
        if (!File.Exists(_templatePath))
            return $"<h1>Шаблон не найден</h1><p>{HttpUtility.HtmlEncode(_templatePath)}</p>";

        string template = await File.ReadAllTextAsync(_templatePath);

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        string title = "", description = "";
        decimal price = 0;
        double? discount = null;
        int days = 7;
        double rating = 4.9;
        int reviews = 0;
        int? organizerId = null;

        await using (var cmd = new NpgsqlCommand(@"
            SELECT title, description, price, discount, days, rating, reviews_count, organizer_id
            FROM tours WHERE id = $1", conn))
        {
            cmd.Parameters.AddWithValue(tourId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return null;

            title = r.GetString(0);
            description = r.IsDBNull(1) ? "" : r.GetString(1);
            price = r.GetDecimal(2);
            discount = r.IsDBNull(3) ? null : r.GetDouble(3);
            days = r.GetInt32(4);
            rating = r.GetDouble(5);
            reviews = r.GetInt32(6);
            organizerId = r.IsDBNull(7) ? null : r.GetInt32(7);
        }

        string organizerName = "Организатор";
        string organizerRating = "4.9";
        string organizerReviewsCount = "156";
        string organizerToursCount = "0";
        string organizerJoined = "01.01.2024";
        string organizerDescription = "Описание отсутствует.";

        if (organizerId.HasValue)
        {
            await using (var cmd = new NpgsqlCommand(@"
                SELECT name, rating, reviews_count, TO_CHAR(joined_at, 'DD.MM.YYYY'),
                       COALESCE(self_description, '')
                FROM organizers WHERE id = $1", conn))
            {
                cmd.Parameters.AddWithValue(organizerId.Value);
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    organizerName = r.GetString(0);
                    organizerRating = r.GetDouble(1).ToString("F1");
                    organizerReviewsCount = r.GetInt32(2).ToString();
                    organizerJoined = r.GetString(3);
                    organizerDescription = r.GetString(4);
                }
            }

            await using (var cmdCount = new NpgsqlCommand(
                "SELECT COUNT(*) FROM tours WHERE organizer_id = $1", conn))
            {
                cmdCount.Parameters.AddWithValue(organizerId.Value);
                organizerToursCount = (await cmdCount.ExecuteScalarAsync())?.ToString() ?? "0";
            }
        }

        string mainImage = "/static/images/default.jpg";
        var smallImages = new StringBuilder();

        await using (var cmd = new NpgsqlCommand(@"
            SELECT image_path FROM tour_images 
            WHERE tour_id = $1 ORDER BY sort_order LIMIT 5", conn))
        {
            cmd.Parameters.AddWithValue(tourId);
            await using var r = await cmd.ExecuteReaderAsync();
            int count = 0;
            while (await r.ReadAsync())
            {
                string path = "/" + r.GetString(0).Replace("\\", "/");
                if (count == 0) mainImage = path;
                else smallImages.Append($"<img src=\"{path}\" alt=\"Фото тура\">");
                count++;
            }
            while (count < 5)
            {
                if (count > 0)
                    smallImages.Append("<img src=\"/static/images/default.jpg\" alt=\"\">");
                count++;
            }
        }

        string[] dayDescriptions = new string[7];
        for (int i = 0; i < 7; i++) dayDescriptions[i] = "";

        try
        {
            await using (var cmd = new NpgsqlCommand(@"
                SELECT day1_description, day2_description, day3_description,
                       day4_description, day5_description, day6_description, day7_description
                FROM day_descriptions WHERE tour_id = $1", conn))
            {
                cmd.Parameters.AddWithValue(tourId);
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    for (int i = 0; i < 7; i++)
                    {
                        dayDescriptions[i] = r.IsDBNull(i)
                            ? ""
                            : HttpUtility.HtmlEncode(r.GetString(i)).Replace("\n", "<br>");
                    }
                }
            }
        }
        catch { }

        decimal finalPrice = discount.HasValue
            ? price * (decimal)(1 - discount.Value / 100)
            : price;

        decimal pricePerDay = days > 0 ? Math.Round(finalPrice / days, 0) : finalPrice;

        return template
            .Replace("{TITLE}", HttpUtility.HtmlEncode(title))
            .Replace("{RATING}", rating.ToString("F1"))
            .Replace("{REVIEWS}", reviews.ToString("N0"))
            .Replace("{DAYS}", days.ToString())
            .Replace("{FINAL_PRICE}", finalPrice.ToString("N0"))
            .Replace("{PRICE_PER_DAY}", pricePerDay.ToString("N0"))
            .Replace("{DESCRIPTION}", HttpUtility.HtmlEncode(description).Replace("\n", "<br>"))
            .Replace("{MAIN_IMAGE}", mainImage)
            .Replace("{SMALL_IMAGES}", smallImages.ToString())
            .Replace("{ORGANIZER_NAME}", HttpUtility.HtmlEncode(organizerName))
            .Replace("{ORGANIZER_RATING}", organizerRating)
            .Replace("{ORGANIZER_REVIEWS_COUNT}", organizerReviewsCount)
            .Replace("{ORGANIZER_TOURS_COUNT}", organizerToursCount)
            .Replace("{ORGANIZER_JOINED}", organizerJoined)
            .Replace("{ORGANIZER_DESCRIPTION}", HttpUtility.HtmlEncode(organizerDescription).Replace("\n", "<br>"))
            .Replace("{DAY1}", dayDescriptions[0])
            .Replace("{DAY2}", dayDescriptions[1])
            .Replace("{DAY3}", dayDescriptions[2])
            .Replace("{DAY4}", dayDescriptions[3])
            .Replace("{DAY5}", dayDescriptions[4])
            .Replace("{DAY6}", dayDescriptions[5])
            .Replace("{DAY7}", dayDescriptions[6]);
    }
}
