using System.Net;
using System.Text;
using System.Web;
using Npgsql;

namespace HttpListenerServer;

public class AdminPanel
{
    private readonly string _connString;

    public AdminPanel(string connectionString)
    {
        _connString = connectionString;
    }

    public async Task ServeDashboard(HttpListenerContext ctx)
    {
        var resp = ctx.Response;
        var sb = new StringBuilder();

        sb.Append(@"<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='UTF-8'>
    <title>Админка — Туры</title>
    <style>
        body {font-family: Arial, sans-serif; margin:40px; background:#f9f9f9;}
        table {width:100%; border-collapse:collapse; margin:20px 0;}
        th, td {border:1px solid #ddd; padding:12px; text-align:left;}
        th {background:#f0f0f0;}
        button, a.btn {padding:8px 16px; margin:0 5px; text-decoration:none; border:none; border-radius:4px; cursor:pointer;}
        .btn-edit {background:#007bff; color:white;}
        .btn-delete {background:#dc3545; color:white;}
        .btn-add {background:#28a745; color:white; padding:12px 24px; font-size:16px;}
        a {color:#007bff;}
    </style>
</head>
<body>
<h1>Управление турами</h1>
<a href='/admin/logout' style='float:right; color:red;'>Выйти</a>
<hr>

<h2>Добавить тур</h2>
<form method='post' action='/admin/add'>
    <p><input name='title' placeholder='Название тура' required style='width:500px'></p>
    <p><input name='country' placeholder='Страна' required></p>
    <p><textarea name='description' placeholder='Описание' rows='3' style='width:600px'></textarea></p>
    <p><input name='price' type='number' placeholder='Цена ₽' required></p>
    <p><input name='discount' type='number' step='0.01' placeholder='Скидка %'></p>
    <p><input name='days' type='number' placeholder='Дней' required> <input name='nights' type='number' placeholder='Ночей' required></p>
    <p><input name='image_path' placeholder='images/tour.jpg' value='images/default.jpg' style='width:500px'></p>
    <p>Комфорт (1-5): <input name='comfort_level' type='number' min='1' max='5' value='3' style='width:80px'>
       Активность (1-5): <input name='activity_level' type='number' min='1' max='5' value='3' style='width:80px'></p>
    <button type='submit' class='btn-add'>Добавить тур</button>
</form>

<hr>
<h2>Все туры</h2>
<table>
<tr><th>ID</th><th>Название</th><th>Страна</th><th>Цена</th><th>Скидка</th><th>Дни/Ночи</th><th>Фото</th><th>Действия</th></tr>");

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT id, title, country, price, discount, days, nights, image_path FROM tours ORDER BY id DESC", conn);
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            int id = r.GetInt32(0);
            string title = HttpUtility.HtmlEncode(r.GetString(1));
            string country = HttpUtility.HtmlEncode(r.GetString(2));
            decimal price = r.GetDecimal(3);
            string discount = r.IsDBNull(4) ? "" : r.GetDouble(4).ToString("F1") + "%";
            int days = r.GetInt32(5);
            int nights = r.GetInt32(6);
            string img = r.GetString(7);

            sb.Append($@"
<tr>
    <td>{id}</td>
    <td>{title}</td>
    <td>{country}</td>
    <td>{price:N0} ₽</td>
    <td>{(string.IsNullOrEmpty(discount) ? "—" : discount)}</td>
    <td>{days}/{nights}</td>
    <td>{(string.IsNullOrEmpty(img) ? "нет" : "есть")}</td>
    <td>
        <a href='/admin/edit/{id}' class='btn btn-edit'>Изменить</a>
        <form method='post' action='/admin/delete' style='display:inline' onsubmit='return confirm(""Удалить тур {title}?"");'>
            <input type='hidden' name='id' value='{id}'>
            <button type='submit' class='btn-delete'>Удалить</button>
        </form>
    </td>
</tr>");
        }

        sb.Append("</table></body></html>");
        SendHtml(resp, sb.ToString());
    }

    public async Task ServeEditPage(HttpListenerContext ctx, int tourId)
    {
        var resp = ctx.Response;
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT title,country,description,price,discount,days,nights,image_path,comfort_level,activity_level
            FROM tours WHERE id = $1", conn);
        cmd.Parameters.AddWithValue(tourId);
        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync())
        {
            SendHtml(resp, "<h1>Тур не найден</h1><a href='/admin'>← Назад</a>");
            return;
        }

        string title = HttpUtility.HtmlAttributeEncode(r.GetString(0));
        string country = HttpUtility.HtmlAttributeEncode(r.GetString(1));
        string desc = HttpUtility.HtmlEncode(r.GetString(2));
        decimal price = r.GetDecimal(3);
        string discount = r.IsDBNull(4) ? "" : r.GetDouble(4).ToString();
        int days = r.GetInt32(5);
        int nights = r.GetInt32(6);
        string img = HttpUtility.HtmlAttributeEncode(r.GetString(7));
        int comfort = r.GetInt32(8);
        int activity = r.GetInt32(9);

        var sb = new StringBuilder();
        sb.Append($@"<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='UTF-8'>
    <title>Редактировать тур #{tourId}</title>
    <style>
        body {{font-family: Arial; margin:40px; background:#f9f9f9;}}
        input, textarea {{width:100%; max-width:600px; padding:10px; margin:10px 0; font-size:16px;}}
        button {{padding:12px 24px; font-size:16px; margin-right:10px;}}
        .btn-save {{background:#007bff; color:white;}}
        .btn-back {{background:#6c757d; color:white; text-decoration:none; padding:12px 24px;}}
    </style>
</head>
<body>
<h1>Редактирование тура #{tourId}</h1>
<a href='/admin' class='btn-back'>← Назад к списку</a>
<hr>
<form method='post' action='/admin/save'>
    <input type='hidden' name='id' value='{tourId}'>
    <p><input name='title' value='{title}' required placeholder='Название'></p>
    <p><input name='country' value='{country}' required placeholder='Страна'></p>
    <p><textarea name='description' rows='5'>{desc}</textarea></p>
    <p><input name='price' type='number' value='{price}' required></p>
    <p><input name='discount' type='number' step='0.01' value='{discount}' placeholder='Скидка %'></p>
    <p><input name='days' type='number' value='{days}' required style='width:100px'> дней  
       <input name='nights' type='number' value='{nights}' required style='width:100px'> ночей</p>
    <p><input name='image_path' value='{img}' style='width:100%'></p>
    <p>Комфорт (1-5): <input name='comfort_level' type='number' min='1' max='5' value='{comfort}' style='width:100px'>
       Активность (1-5): <input name='activity_level' type='number' min='1' max='5' value='{activity}' style='width:100px'></p>
    <button type='submit' class='btn-save'>Сохранить изменения</button>
</form>
</body></html>");

        SendHtml(resp, sb.ToString());
    }

    public async Task HandleCrud(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "";
        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        var data = HttpUtility.ParseQueryString(body);

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        if (path == "/admin/add")
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO tours (title,country,description,price,discount,days,nights,image_path,comfort_level,activity_level,rating,reviews_count)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,5.0,0)", conn);
            cmd.Parameters.AddWithValue(data["title"] ?? "");
            cmd.Parameters.AddWithValue(data["country"] ?? "");
            cmd.Parameters.AddWithValue(data["description"] ?? "");
            cmd.Parameters.AddWithValue(decimal.Parse(data["price"] ?? "0"));
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(data["discount"]) ? DBNull.Value : double.Parse(data["discount"]!));
            cmd.Parameters.AddWithValue(int.Parse(data["days"] ?? "0"));
            cmd.Parameters.AddWithValue(int.Parse(data["nights"] ?? "0"));
            cmd.Parameters.AddWithValue(data["image_path"] ?? "images/default.jpg");
            cmd.Parameters.AddWithValue(int.Parse(data["comfort_level"] ?? "3"));
            cmd.Parameters.AddWithValue(int.Parse(data["activity_level"] ?? "3"));
            await cmd.ExecuteNonQueryAsync();
        }
        else if (path == "/admin/delete" && int.TryParse(data["id"], out int delId))
        {
            await using var cmd = new NpgsqlCommand("DELETE FROM tours WHERE id = $1", conn);
            cmd.Parameters.AddWithValue(delId);
            await cmd.ExecuteNonQueryAsync();
        }
        else if (path == "/admin/save" && int.TryParse(data["id"], out int saveId))
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE tours SET title=$1, country=$2, description=$3, price=$4, discount=$5,
                days=$6, nights=$7, image_path=$8, comfort_level=$9, activity_level=$10 WHERE id=$11", conn);
            cmd.Parameters.AddWithValue(data["title"] ?? "");
            cmd.Parameters.AddWithValue(data["country"] ?? "");
            cmd.Parameters.AddWithValue(data["description"] ?? "");
            cmd.Parameters.AddWithValue(decimal.Parse(data["price"] ?? "0"));
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(data["discount"]) ? DBNull.Value : double.Parse(data["discount"]!));
            cmd.Parameters.AddWithValue(int.Parse(data["days"] ?? "0"));
            cmd.Parameters.AddWithValue(int.Parse(data["nights"] ?? "0"));
            cmd.Parameters.AddWithValue(data["image_path"] ?? "");
            cmd.Parameters.AddWithValue(int.Parse(data["comfort_level"] ?? "3"));
            cmd.Parameters.AddWithValue(int.Parse(data["activity_level"] ?? "3"));
            cmd.Parameters.AddWithValue(saveId);
            await cmd.ExecuteNonQueryAsync();
        }

        ctx.Response.Redirect("/admin");
    }

    private static void SendHtml(HttpListenerResponse resp, string html)
    {
        var buffer = Encoding.UTF8.GetBytes(html);
        resp.ContentType = "text/html; charset=utf-8";
        resp.ContentLength64 = buffer.Length;
        resp.OutputStream.Write(buffer, 0, buffer.Length);
    }
}