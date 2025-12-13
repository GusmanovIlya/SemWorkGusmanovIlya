using HttpListenerServer;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("settings.json", optional: false, reloadOnChange: false)
    .Build();

string staticFolder = config["StaticDirectoryPath"] ?? "public";
string domain = config["Domain"] ?? "localhost";
int port = int.Parse(config["Port"] ?? "1234");
string prefix = $"http://{domain}:{port}/";
string connString = "Host=localhost;Port=5432;Username=postgres;Password=1212;Database=tours_db";

var server = new HttpServer(prefix, staticFolder, connString);

Console.WriteLine($"Сервер запущен: {prefix}");
Console.WriteLine($"Статические файлы: {Path.GetFullPath(staticFolder)}");
Console.WriteLine("Для остановки введите: quit");

await server.RunAsync();