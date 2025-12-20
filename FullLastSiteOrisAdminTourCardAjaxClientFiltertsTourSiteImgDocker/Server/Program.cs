using HttpListenerServer;
using Microsoft.Extensions.Configuration;

var env = Environment.GetEnvironmentVariable("APP_ENV") ?? "local";

var configBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("settings.json", optional: false, reloadOnChange: false);

if (env.Equals("docker", StringComparison.OrdinalIgnoreCase))
{
    configBuilder.AddJsonFile("settings.docker.json", optional: false, reloadOnChange: false);
}

configBuilder.AddEnvironmentVariables();

var config = configBuilder.Build();

string staticFolder = config["StaticDirectoryPath"] ?? "public";
string domain = config["Domain"] ?? "localhost";
int port = int.TryParse(config["Port"], out var p) ? p : 1234;

string prefix = $"http://{domain}:{port}/";

string connString =
    config.GetSection("ConnectionStrings")["Postgres"]
    ?? throw new Exception("ConnectionStrings:Postgres not found");

var server = new HttpServer(prefix, staticFolder, connString);

Console.WriteLine($"ENV: {env}");
Console.WriteLine($"Сервер запущен: {prefix}");
Console.WriteLine($"Статические файлы: {Path.GetFullPath(staticFolder)}");
Console.WriteLine("Для остановки введите: quit");

await server.RunAsync();
