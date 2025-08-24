using Microsoft.Extensions.Logging;

namespace YunPlaylistDownloader.Services;

public class CookieService
{
    private readonly ILogger<CookieService> _logger;
    private string? _cookieContent;

    public CookieService(ILogger<CookieService> logger)
    {
        _logger = logger;
    }

    public void LoadCookie(string cookieFile)
    {
        if (string.IsNullOrEmpty(cookieFile))
            return;

        var file = Path.GetFullPath(cookieFile);
        
        if (!File.Exists(file))
        {
            _logger.LogWarning("[cookie] cookie 文件不存在: {File}", file);
            return;
        }

        _logger.LogInformation("[cookie] 使用 cookie 文件: {File}", file);

        try
        {
            var content = File.ReadAllText(file);
            
            // 去除注释和空行
            var lines = content.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//"))
                .Select(line => line.Trim());

            _cookieContent = string.Join("", lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read cookie file: {File}", file);
        }
    }

    public string? GetCookieString()
    {
        return _cookieContent;
    }

    public bool HasCookie()
    {
        return !string.IsNullOrEmpty(_cookieContent);
    }
}
