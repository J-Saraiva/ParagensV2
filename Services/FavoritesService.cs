using System.Text.Json;
using Microsoft.Maui.Storage;
using ParagensV2.Models;

namespace ParagensV2.Services;

public class FavoritesService
{
    private const string FavoritesStorageKey = "paragens_user_favorites_v1";
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private List<FavoriteItem>? _cachedFavorites;

    public static FavoritesService Instance { get; } = new();

    public event EventHandler? FavoritesChanged;

    private async Task<List<FavoriteItem>> LoadFavoritesInternalAsync()
    {
        if (_cachedFavorites != null)
        {
            return _cachedFavorites;
        }

        await _semaphore.WaitAsync();
        try
        {
            if (_cachedFavorites != null)
            {
                return _cachedFavorites;
            }

            var json = Preferences.Default.Get<string>(FavoritesStorageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                _cachedFavorites = new List<FavoriteItem>();
            }
            else
            {
                try
                {
                    _cachedFavorites = JsonSerializer.Deserialize<List<FavoriteItem>>(json) ?? new List<FavoriteItem>();
                }
                catch
                {
                    _cachedFavorites = new List<FavoriteItem>();
                }
            }

            return _cachedFavorites;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SaveFavoritesInternalAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_cachedFavorites ?? new List<FavoriteItem>());
            Preferences.Default.Set(FavoritesStorageKey, json);
        }
        finally
        {
            _semaphore.Release();
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            FavoritesChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public async Task<bool> IsFavoriteAsync(string id)
    {
        var list = await LoadFavoritesInternalAsync();
        return list.Any(f => f.Id == id);
    }

    public async Task AddFavoriteAsync(FavoriteItem item)
    {
        var list = await LoadFavoritesInternalAsync();
        if (!list.Any(f => f.Id == item.Id))
        {
            item.AddedAt = DateTime.UtcNow;
            list.Insert(0, item);
            await SaveFavoritesInternalAsync();
        }
    }

    public async Task RemoveFavoriteAsync(string id)
    {
        var list = await LoadFavoritesInternalAsync();
        var removedCount = list.RemoveAll(f => f.Id == id);
        if (removedCount > 0)
        {
            await SaveFavoritesInternalAsync();
        }
    }

    public async Task<bool> ToggleFavoriteAsync(FavoriteItem item)
    {
        var isFav = await IsFavoriteAsync(item.Id);
        if (isFav)
        {
            await RemoveFavoriteAsync(item.Id);
            return false;
        }
        else
        {
            await AddFavoriteAsync(item);
            return true;
        }
    }

    public async Task<List<FavoriteItem>> GetFavoritesAsync(FavoriteType? type = null)
    {
        var list = await LoadFavoritesInternalAsync();
        var query = list.AsEnumerable();
        if (type.HasValue)
        {
            query = query.Where(f => f.Type == type.Value);
        }
        return query.OrderByDescending(f => f.AddedAt).ToList();
    }
}
