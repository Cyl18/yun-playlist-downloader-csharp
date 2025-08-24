using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YunPlaylistDownloader.Services;
using YunPlaylistDownloader.Commands;
using YunPlaylistDownloader.Adapters;

namespace YunPlaylistDownloader;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        var rootCommand = new RootCommand("网易云音乐 歌单/专辑/电台 下载器");
        
        var downloadCommand = host.Services.GetRequiredService<DownloadCommand>();
        downloadCommand.ConfigureCommand(rootCommand);
        
        return await rootCommand.InvokeAsync(args);
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, services) =>
            {
                // HTTP Client
                services.AddHttpClient<NetEaseApiClient>(client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", 
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    client.Timeout = TimeSpan.FromMinutes(5);
                });

                // Services
                services.AddSingleton<NetEaseApiClient>();
                services.AddSingleton<DownloadService>();
                services.AddSingleton<ConfigService>();
                services.AddSingleton<FileNameService>();
                services.AddSingleton<CookieService>();
                services.AddSingleton<ProgressService>();

                // Commands
                services.AddSingleton<DownloadCommand>();

                // Adapters
                services.AddSingleton<PlaylistAdapter>();
                services.AddSingleton<AlbumAdapter>();
                services.AddSingleton<DjRadioAdapter>();
                services.AddSingleton<AdapterFactory>();
            });
}
