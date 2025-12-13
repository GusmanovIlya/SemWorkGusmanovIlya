using Npgsql;
using System.Text;
using System.Web;

namespace HttpListenerServer;

public class TourCardRenderer
{
    private readonly string _connString;
    private readonly string _templatePath;

    public TourCardRenderer(string connString, string? templatePath = null)
    {
        _connString = connString;
        _templatePath = templatePath ?? "public/templates/card_template.html";
    }

    public async Task<(string CardsHtml, int TotalCount)> RenderAllCardsAsync(Dictionary<string, string> filters)
    {
        var sb = new StringBuilder();
        int count = 0;
        string template = await File.ReadAllTextAsync(_templatePath);

        var sql = new StringBuilder();
        sql.Append("SELECT id, title, country, description, price, discount, days, nights, image_path, comfort_level, activity_level, rating, reviews_count ");
        sql.Append("FROM tours WHERE 1=1");

        var parameters = new List<NpgsqlParameter>();
        int paramIndex = 1;

        if (!string.IsNullOrWhiteSpace(filters.GetValueOrDefault("country")))
        {
            sql.Append($" AND (title ILIKE ${paramIndex} OR country ILIKE ${paramIndex})");
            parameters.Add(new NpgsqlParameter { Value = "%" + filters["country"].Trim() + "%" });
            paramIndex++;
        }


        if (int.TryParse(filters.GetValueOrDefault("price_min"), out int priceMin) && priceMin > 0)
        {
            sql.Append($" AND price >= ${paramIndex}");
            parameters.Add(new NpgsqlParameter { Value = priceMin });
            paramIndex++;
        }

        if (int.TryParse(filters.GetValueOrDefault("price_max"), out int priceMax) && priceMax > 0)
        {
            sql.Append($" AND price <= ${paramIndex}");
            parameters.Add(new NpgsqlParameter { Value = priceMax });
            paramIndex++;
        }


        if (!string.IsNullOrEmpty(filters.GetValueOrDefault("duration")))
        {
            var dur = filters["duration"];
            if (dur == "7-10")
                sql.Append(" AND days BETWEEN 7 AND 10");
            else if (dur == "11-14")
                sql.Append(" AND days BETWEEN 11 AND 14");
            else if (dur == "15+")
                sql.Append(" AND days >= 15");
        }


        if (int.TryParse(filters.GetValueOrDefault("comfort"), out int comfort) && comfort > 0)
        {
            sql.Append($" AND comfort_level = ${paramIndex}");
            parameters.Add(new NpgsqlParameter { Value = comfort });
            paramIndex++;
        }


        if (int.TryParse(filters.GetValueOrDefault("activity"), out int activity) && activity > 0)
        {
            sql.Append($" AND activity_level = ${paramIndex}");
            parameters.Add(new NpgsqlParameter { Value = activity });
            paramIndex++;
        }


        if (filters.GetValueOrDefault("discount") == "1")
        {
            sql.Append(" AND discount IS NOT NULL AND discount > 0");
        }

        var sort = filters.GetValueOrDefault("sort") ?? "popularity";
        sql.Append(sort switch
        {
            "price_asc" => " ORDER BY price ASC",
            "price_desc" => " ORDER BY price DESC",
            "rating_desc" => " ORDER BY rating DESC",
            "duration_asc" => " ORDER BY days ASC",
            _ => " ORDER BY rating DESC, reviews_count DESC"
        });

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql.ToString(), conn);
        foreach (var param in parameters)
        {
            cmd.Parameters.Add(param);
        }

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            count++;
            int id = reader.GetInt32(0);
            string title = HttpUtility.HtmlEncode(reader.GetString(1));
            string country = HttpUtility.HtmlEncode(reader.GetString(2));
            string desc = HttpUtility.HtmlEncode(reader.GetString(3));
            decimal price = reader.GetDecimal(4);
            double? discount = reader.IsDBNull(5) ? null : reader.GetDouble(5);
            int days = reader.GetInt32(6);
            int nights = reader.GetInt32(7);
            string img = reader.GetString(8);
            int comfortLvl = reader.GetInt32(9);
            int activityLvl = reader.GetInt32(10);
            double rating = reader.GetDouble(11);
            int reviews = reader.GetInt32(12);

            decimal finalPrice = discount.HasValue ? price * (decimal)(1 - discount.Value / 100) : price;

            string comfortStr = string.Concat(Enumerable.Repeat("●", comfortLvl)) + string.Concat(Enumerable.Repeat("○", 5 - comfortLvl));
            string activityStr = string.Concat(Enumerable.Repeat("●", activityLvl)) + string.Concat(Enumerable.Repeat("○", 5 - activityLvl));

            string oldPriceHtml = discount.HasValue ? $"<div class='old'>₽ {price:N0}</div>" : "";

            string card = template
                .Replace("{ID}", id.ToString())
                .Replace("{TITLE}", title)
                .Replace("{COUNTRY}", country)
                .Replace("{DESCRIPTION}", desc)
                .Replace("{IMAGE}", img)
                .Replace("{RATING}", rating.ToString("F1"))
                .Replace("{REVIEWS}", reviews.ToString())
                .Replace("{ACTIVITY}", activityStr)
                .Replace("{COMFORT}", comfortStr)
                .Replace("{DAYS}", days.ToString())
                .Replace("{NIGHTS}", nights.ToString())
                .Replace("{FINAL_PRICE}", finalPrice.ToString("N0"))
                .Replace("{OLD_PRICE}", oldPriceHtml);

            sb.Append(card);
        }

        if (count == 0)
            sb.Append("<div style='text-align:center;padding:100px;color:#888;font-size:18px;'>Туров не найдено</div>");

        return (sb.ToString(), count);
    }
}