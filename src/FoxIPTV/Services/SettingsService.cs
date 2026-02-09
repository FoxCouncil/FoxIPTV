namespace FoxIPTV.Services;

using System.Text.Json;
using FoxIPTV.Models;
using Microsoft.Extensions.Logging;

public sealed class SettingsService : ISettingsService
{
    private static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".foxiptv");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<SettingsService> _logger;

    public UserSettings Current { get; private set; } = new();

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(SettingsDir);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(SettingsPath))
            {
                _logger.LogInformation("No settings file found, using defaults");
                return;
            }

            await using var stream = File.OpenRead(SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync(stream, IptvJsonContext.Default.UserSettings, ct);

            if (settings is not null)
            {
                Current = settings;
                _logger.LogInformation("Settings loaded");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var stream = File.Create(SettingsPath);
            await JsonSerializer.SerializeAsync(stream, Current, IptvJsonContext.Default.UserSettings, ct);

            _logger.LogDebug("Settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save settings");
        }
        finally
        {
            _lock.Release();
        }
    }
}
