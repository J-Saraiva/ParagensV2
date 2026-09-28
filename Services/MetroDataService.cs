using System.Text.Json;
using ParagensV2.Models;

namespace ParagensV2.Services;

public class MetroDataService
{
    private static MetroBundle? _cachedBundle;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    private async Task<MetroBundle> EnsureLoadedAsync()
    {
        if (_cachedBundle != null) return _cachedBundle;

        await _semaphore.WaitAsync();
        try
        {
            if (_cachedBundle != null) return _cachedBundle;

            using var stream = await FileSystem.Current.OpenAppPackageFileAsync("metro_network.json");
            var bundle = await JsonSerializer.DeserializeAsync<MetroBundle>(stream);
            _cachedBundle = bundle ?? new MetroBundle();
            return _cachedBundle;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load metro_network.json: {ex.Message}");
            return new MetroBundle();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<MetroLine>> GetAllLinesAsync()
    {
        var bundle = await EnsureLoadedAsync();
        return bundle.Lines;
    }

    public async Task<List<MetroStation>> GetAllStationsAsync()
    {
        var bundle = await EnsureLoadedAsync();
        return bundle.Stops;
    }

    public async Task<List<MetroStation>> GetStopsForLineAsync(string lineId, int direction = 0)
    {
        var bundle = await EnsureLoadedAsync();
        var line = bundle.Lines.FirstOrDefault(l => l.Id.Equals(lineId, StringComparison.OrdinalIgnoreCase) ||
                                                    l.ShortName.Equals(lineId, StringComparison.OrdinalIgnoreCase));
        if (line == null) return new List<MetroStation>();

        return direction == 0 
            ? line.Direction0.Stops 
            : line.Direction1.Stops;
    }

    public async Task<List<MetroDeparture>> GetStationDeparturesAsync(string stationId)
    {
        var departures = new List<MetroDeparture>();
        if (string.IsNullOrWhiteSpace(stationId)) return departures;

        var bundle = await EnsureLoadedAsync();
        if (!bundle.Schedules.TryGetValue(stationId, out var items) || items == null)
            return departures;

        var now = DateTime.Now;
        var currentMinutes = now.Hour * 60 + now.Minute;

        var dayType = now.DayOfWeek switch
        {
            DayOfWeek.Saturday => "sat",
            DayOfWeek.Sunday => "sun",
            _ => "wd"
        };

        foreach (var item in items)
        {
            if (item.Day != dayType) continue;

            var parts = item.Time.Split(':');
            if (parts.Length < 2) continue;

            if (int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m))
            {
                var depMinutes = h * 60 + m;
                var diff = depMinutes - currentMinutes;

                // Include departures from 2 minutes ago (may be boarding) up to 90 minutes ahead
                if (diff >= -2 && diff <= 90)
                {
                    departures.Add(new MetroDeparture
                    {
                        Line = item.Line,
                        LineColor = item.Color,
                        Destination = item.Destination,
                        ScheduledTime = item.Time,
                        MinutesUntil = Math.Max(0, diff),
                        Status = diff <= 0 ? "A chegar" : $"{diff} min"
                    });
                }
            }
        }

        return departures.OrderBy(d => d.MinutesUntil).Take(25).ToList();
    }
}
