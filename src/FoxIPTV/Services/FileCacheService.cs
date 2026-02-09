namespace FoxIPTV.Services;

using System.Text.Json;
using Microsoft.Extensions.Logging;

public sealed class FileCacheService : ICacheService
{
    private static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".foxiptv", "cache");

    private readonly ILogger<FileCacheService> _logger;

    public FileCacheService(ILogger<FileCacheService> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(CacheDir);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var path = GetPath(key);
        var metaPath = path + ".meta";

        if (!File.Exists(path) || !File.Exists(metaPath))
        {
            return null;
        }

        var expiryText = await File.ReadAllTextAsync(metaPath, ct);
        if (DateTimeOffset.TryParse(expiryText, out var expiry) && DateTimeOffset.UtcNow > expiry)
        {
            _logger.LogDebug("Cache expired for {Key}", key);
            File.Delete(path);
            File.Delete(metaPath);
            return null;
        }

        _logger.LogDebug("Cache hit for {Key}", key);
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default) where T : class
    {
        var path = GetPath(key);
        var metaPath = path + ".meta";

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, cancellationToken: ct);

        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        await File.WriteAllTextAsync(metaPath, expiresAt.ToString("O"), ct);

        _logger.LogDebug("Cached {Key} until {Expiry}", key, expiresAt);
    }

    private static string GetPath(string key)
    {
        var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(CacheDir, safeKey);
    }
}
