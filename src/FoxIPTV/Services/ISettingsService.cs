namespace FoxIPTV.Services;

using FoxIPTV.Models;

public interface ISettingsService
{
    UserSettings Current { get; }
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
