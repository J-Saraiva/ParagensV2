using System.Text.Json;
using ParagensV2.Models;

namespace ParagensV2.Services;

public class UnirDataService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private List<UnirLine>? _cachedLines;

    public UnirDataService()
    {
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }
    }

    /// <summary>
    /// Loads all 439 UNIR lines from local bundle
    /// </summary>
    public async Task<List<UnirLine>> GetAllLinesAsync()
    {
        if (_cachedLines != null && _cachedLines.Count > 0)
            return _cachedLines;

        try
        {
            using var stream = await FileSystem.Current.OpenAppPackageFileAsync("unir_lines.json");
            var lines = await JsonSerializer.DeserializeAsync<List<UnirLine>>(stream);
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    line.Name = CleanMojibake(line.Name);
                    line.Origin = CleanMojibake(line.Origin);
                    line.Destination = CleanMojibake(line.Destination);
                    line.Municipality = CleanMojibake(line.Municipality);
                }

                _cachedLines = lines;
                return _cachedLines;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load unir_lines.json: {ex.Message}");
        }

        return new List<UnirLine>();
    }

    /// <summary>
    /// Fetches all stops for a specific UNIR line in route order from AMP GeoServer (plim:paragens_linha_amp)
    /// </summary>
    public async Task<List<UnirStop>> GetStopsForLineAsync(string lineCode, int direction = 1, string? lineName = null)
    {
        var stops = new List<UnirStop>();
        if (string.IsNullOrWhiteSpace(lineCode)) return stops;

        try
        {
            // Primary layer: plim:paragens_linha_amp provides official stop sequence ("ordem") and travel direction ("sentidotroco")
            var filter = Uri.EscapeDataString($"cod={lineCode}");
            var url = $"https://paragens.amp.pt/geoserver/wfs?service=WFS&version=1.0.0&request=GetFeature&typeName=plim:paragens_linha_amp&cql_filter={filter}&srsName=EPSG:4326&outputFormat=application/json";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var rawBytes = await response.Content.ReadAsByteArrayAsync();
                var json = System.Text.Encoding.UTF8.GetString(rawBytes);

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("features", out var featuresElement) &&
                    featuresElement.ValueKind == JsonValueKind.Array)
                {
                    var rawFeatures = new List<(UnirStop Stop, int GidLinha, string Netex, string LineDesig)>();

                    foreach (var feature in featuresElement.EnumerateArray())
                    {
                        double lng = 0, lat = 0;
                        if (feature.TryGetProperty("geometry", out var geom) &&
                            geom.TryGetProperty("coordinates", out var coords) &&
                            coords.ValueKind == JsonValueKind.Array &&
                            coords.GetArrayLength() >= 2)
                        {
                            lng = coords[0].GetDouble();
                            lat = coords[1].GetDouble();
                        }

                        if (feature.TryGetProperty("properties", out var props))
                        {
                            var cod = props.TryGetProperty("cod_paragem", out var c) ? c.GetString() ?? "" : "";
                            var desig = props.TryGetProperty("desig_paragem", out var d) ? d.GetString() ?? "" : "";
                            var loc = props.TryGetProperty("localizacao", out var l) ? l.GetString() ?? "" : "";
                            var zona = props.TryGetProperty("zona_andante", out var z) ? z.GetString() ?? "" : "";
                            var ordem = props.TryGetProperty("ordem", out var o) && o.TryGetInt32(out var ord) ? ord : 0;
                            var sentido = props.TryGetProperty("sentidotroco", out var s) && s.TryGetInt32(out var snt) ? snt : 1;
                            var gidLinha = props.TryGetProperty("gid_linha", out var gl) && gl.TryGetInt32(out var gVal) ? gVal : 0;
                            var netex = props.TryGetProperty("l_cod_netex", out var nx) ? nx.GetString() ?? "" : "";
                            var lineDesig = props.TryGetProperty("desig", out var ld) ? ld.GetString() ?? "" : "";

                            rawFeatures.Add((
                                new UnirStop
                                {
                                    Code = cod,
                                    Name = CleanMojibake(desig),
                                    Address = CleanMojibake(loc),
                                    Zone = zona,
                                    Lat = lat,
                                    Lng = lng,
                                    Order = ordem,
                                    Direction = sentido
                                },
                                gidLinha,
                                netex,
                                lineDesig
                            ));
                        }
                    }

                    if (rawFeatures.Count > 0)
                    {
                        var targetDir = direction > 0 ? direction : 1;
                        var dirFeatures = rawFeatures.Where(f => f.Stop.Direction == targetDir).ToList();
                        if (dirFeatures.Count == 0)
                        {
                            dirFeatures = rawFeatures;
                        }

                        // Group by route variant (gid_linha) to prevent merging multiple branch variants into double stops
                        var variantGroups = dirFeatures.GroupBy(f => f.GidLinha).ToList();

                        List<(UnirStop Stop, int GidLinha, string Netex, string LineDesig)> chosenVariant;
                        if (variantGroups.Count == 1)
                        {
                            chosenVariant = variantGroups[0].ToList();
                        }
                        else
                        {
                            // If lineName is provided, find the variant matching line designation
                            var cleanedLineName = CleanMojibake(lineName);
                            var matchedGroup = !string.IsNullOrWhiteSpace(cleanedLineName)
                                ? variantGroups.FirstOrDefault(g =>
                                {
                                    var desig = CleanMojibake(g.First().LineDesig);
                                    return !string.IsNullOrEmpty(desig) &&
                                           (cleanedLineName.Contains(desig, StringComparison.OrdinalIgnoreCase) ||
                                            desig.Contains(cleanedLineName, StringComparison.OrdinalIgnoreCase));
                                })
                                : null;

                            if (matchedGroup != null)
                            {
                                chosenVariant = matchedGroup.ToList();
                            }
                            else
                            {
                                // Prefer base variant (:0) or variant with highest stop count
                                chosenVariant = variantGroups.FirstOrDefault(g => g.First().Netex.EndsWith(":0"))?.ToList()
                                                ?? variantGroups.OrderByDescending(g => g.Count()).First().ToList();
                            }
                        }

                        // Deduplicate stops by Code and maintain order
                        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var item in chosenVariant.OrderBy(f => f.Stop.Order))
                        {
                            if (!string.IsNullOrEmpty(item.Stop.Code) && seenCodes.Add(item.Stop.Code))
                            {
                                stops.Add(item.Stop);
                            }
                        }

                        if (stops.Count > 0) return stops;
                    }
                }
            }

            // Fallback to paragens_geoserver if plim layer has no records for this line
            var fallbackFilter = Uri.EscapeDataString($"linhas LIKE '%{lineCode}%'");
            var fallbackUrl = $"https://paragens.amp.pt/geoserver/wfs?service=WFS&version=1.0.0&request=GetFeature&typeName=paragens:paragens_geoserver&cql_filter={fallbackFilter}&srsName=EPSG:4326&outputFormat=application/json";

            var fbResponse = await _httpClient.GetAsync(fallbackUrl);
            if (fbResponse.IsSuccessStatusCode)
            {
                var rawBytes = await fbResponse.Content.ReadAsByteArrayAsync();
                var json = System.Text.Encoding.UTF8.GetString(rawBytes);

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("features", out var featuresElement) &&
                    featuresElement.ValueKind == JsonValueKind.Array)
                {
                    int autoOrder = 1;
                    var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var feature in featuresElement.EnumerateArray())
                    {
                        double lng = 0, lat = 0;
                        if (feature.TryGetProperty("geometry", out var geom) &&
                            geom.TryGetProperty("coordinates", out var coords) &&
                            coords.ValueKind == JsonValueKind.Array &&
                            coords.GetArrayLength() >= 2)
                        {
                            lng = coords[0].GetDouble();
                            lat = coords[1].GetDouble();
                        }

                        if (feature.TryGetProperty("properties", out var props))
                        {
                            var cod = props.TryGetProperty("codparagem", out var c) ? c.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(cod) && seenCodes.Add(cod))
                            {
                                var desig = props.TryGetProperty("designacao", out var d) ? d.GetString() ?? "" : "";
                                var loc = props.TryGetProperty("localizaca", out var l) ? l.GetString() ?? "" : "";
                                var zona = props.TryGetProperty("zona_andante", out var z) ? z.GetString() ?? "" : "";

                                stops.Add(new UnirStop
                                {
                                    Code = cod,
                                    Name = CleanMojibake(desig),
                                    Address = CleanMojibake(loc),
                                    Zone = zona,
                                    Lat = lat,
                                    Lng = lng,
                                    Order = autoOrder++
                                });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get stops for UNIR line {lineCode}: {ex.Message}");
        }

        return stops;
    }

    /// <summary>
    /// Fetches upcoming departures for a UNIR stop from AMP Q.Horas API
    /// </summary>
    public async Task<List<UnirDeparture>> GetStopDeparturesAsync(string stopCode, DateTime? targetDate = null)
    {
        var departures = new List<UnirDeparture>();
        if (string.IsNullOrWhiteSpace(stopCode)) return departures;

        var cleanCode = stopCode.Trim().ToLowerInvariant();
        if (cleanCode.StartsWith("prg:"))
        {
            cleanCode = cleanCode.Substring(4);
        }

        try
        {
            var today = DateTime.Today;
            var dateToQuery = targetDate ?? today;
            var dateStr = dateToQuery.ToString("yyyy-MM-dd");
            var url = $"https://paragens.amp.pt/acarto2/get_horarios_prg?dia={dateStr}&id={Uri.EscapeDataString(cleanCode)}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var rawBytes = await response.Content.ReadAsByteArrayAsync();
                var json = System.Text.Encoding.UTF8.GetString(rawBytes);

                using var outerDoc = JsonDocument.Parse(json);
                JsonElement root = outerDoc.RootElement;
                if (root.ValueKind == JsonValueKind.String)
                {
                    using var innerDoc = JsonDocument.Parse(root.GetString() ?? "{}");
                    root = innerDoc.RootElement.Clone();
                }

                if (root.TryGetProperty("horarios", out var horariosElement) &&
                    horariosElement.ValueKind == JsonValueKind.Array)
                {
                    var now = DateTime.Now;
                    var currentMinutesFromMidnight = now.Hour * 60 + now.Minute;

                    foreach (var item in horariosElement.EnumerateArray())
                    {
                        var linha = item.TryGetProperty("linha", out var l) ? l.GetString() ?? "" : "";
                        var destino = item.TryGetProperty("destino", out var d) ? d.GetString() ?? "" : "";
                        var chegada = item.TryGetProperty("chegada", out var c) ? c.GetString() ?? "" : "";
                        var hora = item.TryGetProperty("hora", out var h) && h.TryGetInt32(out var hh) ? hh : 0;
                        var minuto = item.TryGetProperty("minuto", out var m) && m.TryGetInt32(out var mm) ? mm : 0;
                        var sentido = item.TryGetProperty("sentido", out var s) ? s.GetString() ?? "" : "";

                        var scheduledMinutesFromMidnight = hora * 60 + minuto;
                        var diffMinutes = scheduledMinutesFromMidnight - currentMinutesFromMidnight;

                        // Handle midnight rollover (e.g. at 23:50, bus scheduled at 00:15)
                        if (diffMinutes < -1200 && now.Hour >= 20 && hora <= 4)
                        {
                            diffMinutes += 1440;
                        }

                        if (diffMinutes >= -5)
                        {
                            var timeStr = !string.IsNullOrEmpty(chegada) && chegada.Length >= 5 
                                ? chegada.Substring(0, 5) 
                                : $"{hora:D2}:{minuto:D2}";

                            departures.Add(new UnirDeparture
                            {
                                Line = linha,
                                Destination = CleanMojibake(destino),
                                ScheduledTime = timeStr,
                                Direction = sentido,
                                MinutesUntil = Math.Max(0, diffMinutes),
                                IsTomorrow = false,
                                Status = diffMinutes <= 0 ? "A chegar" : $"{diffMinutes} min"
                            });
                        }
                    }
                }
            }

            // If today has no upcoming departures (or fewer than 3), also fetch tomorrow's early departures
            if (!targetDate.HasValue && departures.Count < 3)
            {
                try
                {
                    var tomorrow = today.AddDays(1);
                    var tomorrowStr = tomorrow.ToString("yyyy-MM-dd");
                    var tomorrowUrl = $"https://paragens.amp.pt/acarto2/get_horarios_prg?dia={tomorrowStr}&id={Uri.EscapeDataString(cleanCode)}";

                    var tmResponse = await _httpClient.GetAsync(tomorrowUrl);
                    if (tmResponse.IsSuccessStatusCode)
                    {
                        var tmBytes = await tmResponse.Content.ReadAsByteArrayAsync();
                        var tmJson = System.Text.Encoding.UTF8.GetString(tmBytes);

                        using var tmOuterDoc = JsonDocument.Parse(tmJson);
                        JsonElement tmRoot = tmOuterDoc.RootElement;
                        if (tmRoot.ValueKind == JsonValueKind.String)
                        {
                            using var tmInnerDoc = JsonDocument.Parse(tmRoot.GetString() ?? "{}");
                            tmRoot = tmInnerDoc.RootElement.Clone();
                        }

                        if (tmRoot.TryGetProperty("horarios", out var tmHorariosElement) &&
                            tmHorariosElement.ValueKind == JsonValueKind.Array)
                        {
                            var now = DateTime.Now;
                            var currentMinutesFromMidnight = now.Hour * 60 + now.Minute;

                            foreach (var item in tmHorariosElement.EnumerateArray())
                            {
                                var linha = item.TryGetProperty("linha", out var l) ? l.GetString() ?? "" : "";
                                var destino = item.TryGetProperty("destino", out var d) ? d.GetString() ?? "" : "";
                                var chegada = item.TryGetProperty("chegada", out var c) ? c.GetString() ?? "" : "";
                                var hora = item.TryGetProperty("hora", out var h) && h.TryGetInt32(out var hh) ? hh : 0;
                                var minuto = item.TryGetProperty("minuto", out var m) && m.TryGetInt32(out var mm) ? mm : 0;
                                var sentido = item.TryGetProperty("sentido", out var s) ? s.GetString() ?? "" : "";

                                var scheduledMinutes = hora * 60 + minuto;
                                var minutesUntilTomorrow = (1440 - currentMinutesFromMidnight) + scheduledMinutes;

                                var timeStr = !string.IsNullOrEmpty(chegada) && chegada.Length >= 5 
                                    ? chegada.Substring(0, 5) 
                                    : $"{hora:D2}:{minuto:D2}";

                                departures.Add(new UnirDeparture
                                {
                                    Line = linha,
                                    Destination = CleanMojibake(destino),
                                    ScheduledTime = timeStr,
                                    Direction = sentido,
                                    MinutesUntil = minutesUntilTomorrow,
                                    IsTomorrow = true,
                                    Status = $"Amanhã {timeStr}"
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load tomorrow departures for stop {cleanCode}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load departures for stop {stopCode}: {ex.Message}");
        }

        await EnrichWithMoovitRealtimeAsync(departures, cleanCode);

        return departures.OrderBy(d => d.MinutesUntil).ToList();
    }

    /// <summary>
    /// Queries the local Moovit proxy microservice for live GPS delay updates.
    /// Fails gracefully and silently in &lt;1.5s if proxy is offline.
    /// </summary>
    private async Task EnrichWithMoovitRealtimeAsync(List<UnirDeparture> departures, string cleanCode)
    {
        if (departures.Count == 0) return;

        try
        {
            var proxyBaseUrl = Preferences.Default.Get("MoovitProxyUrl", "http://localhost:5000");
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
            var url = $"{proxyBaseUrl.TrimEnd('/')}/arrivals?stop={Uri.EscapeDataString(cleanCode)}";

            var response = await _httpClient.GetAsync(url, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("arrivals", out var arrElement) && arrElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rtItem in arrElement.EnumerateArray())
                    {
                        var line = rtItem.TryGetProperty("line", out var l) ? l.GetString() : null;
                        if (string.IsNullOrEmpty(line)) continue;

                        var rtMinutes = rtItem.TryGetProperty("realtimeMinutes", out var rm) && rm.TryGetInt32(out var mins) ? mins : -1;
                        var delay = rtItem.TryGetProperty("delayMinutes", out var dm) && dm.TryGetInt32(out var dly) ? dly : (int?)null;

                        // Match the next departure for this line
                        var match = departures.FirstOrDefault(d => !d.IsTomorrow && d.Line == line && !d.IsRealtime);
                        if (match != null && rtMinutes >= 0)
                        {
                            match.MinutesUntil = rtMinutes;
                            match.IsRealtime = true;
                            match.DelayMinutes = delay;
                            match.Status = rtMinutes <= 0 ? "⚡ A chegar" : $"⚡ {rtMinutes} min";
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently fallback to scheduled departures
        }
    }

    /// <summary>
    /// Corrects UTF-8 mojibake (e.g. "EstaÃ§Ã£o" -> "Estação", "AntÃ³nio" -> "António")
    /// </summary>
    public static string CleanMojibake(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var current = text;
        while (current.Contains('Ã') || current.Contains('Â'))
        {
            try
            {
                var bytes = System.Text.Encoding.GetEncoding("iso-8859-1").GetBytes(current);
                var fixedStr = System.Text.Encoding.UTF8.GetString(bytes);
                if (fixedStr == current) break;
                current = fixedStr;
            }
            catch
            {
                break;
            }
        }

        return current;
    }
}
