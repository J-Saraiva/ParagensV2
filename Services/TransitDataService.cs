using System.Text.Json;
using System.Text.Json.Serialization;
using ParagensV2.Models;

namespace ParagensV2.Services
{
    public class TransitDataService
    {
        private readonly HttpClient _httpClient;
        private readonly STCPScraperService _stcpScraper = new();
        private StcpNetwork? _networkCache = null;

        public TransitDataService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://stcp.pt/"),
                Timeout = TimeSpan.FromSeconds(10)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        private class StcpApiResponse<T>
        {
            [JsonPropertyName("stops")]
            public List<StcpApiStopItem>? Stops { get; set; }

            [JsonPropertyName("coordinates")]
            public List<StcpApiCoordItem>? Coordinates { get; set; }
        }

        private class StcpApiStopItem
        {
            [JsonPropertyName("stop_id")]
            public string? StopId { get; set; }

            [JsonPropertyName("stop_code")]
            public string? StopCode { get; set; }

            [JsonPropertyName("stop_name")]
            public string? StopName { get; set; }

            [JsonPropertyName("zone_id")]
            public string? ZoneId { get; set; }

            [JsonPropertyName("stop_lat")]
            public double? StopLat { get; set; }

            [JsonPropertyName("stop_lon")]
            public double? StopLon { get; set; }
        }

        private class StcpApiCoordItem
        {
            [JsonPropertyName("lat")]
            public double? Lat { get; set; }

            [JsonPropertyName("lng")]
            public double? Lng { get; set; }
        }

        /// <summary>
        /// Fetches live arrivals for a specific stop code using the official STCP Realtime API.
        /// </summary>
        public async Task<List<BusArrival>> GetLiveArrivalsAsync(string stopCode)
        {
            var stcpArrivals = await _stcpScraper.ScrapeStopAsync(stopCode);
            return stcpArrivals.OrderByDescending(a => a.IsRealTime).ThenBy(a => a.NextEtaMinutes).ToList();
        }

        /// <summary>
        /// Fallback for widget.
        /// </summary>
        public async Task<List<BusArrival>> GetUpcomingArrivalsAsync()
        {
            return await GetLiveArrivalsAsync("AAL2");
        }

        public async Task<BusArrival?> GetNextArrivalAsync()
        {
            var arrivals = await GetUpcomingArrivalsAsync();
            return arrivals.FirstOrDefault();
        }

        private async Task<StcpNetwork> GetNetworkAsync()
        {
            if (_networkCache != null) return _networkCache;
            try
            {
                using var stream = await FileSystem.Current.OpenAppPackageFileAsync("stcp_network.json");
                _networkCache = await JsonSerializer.DeserializeAsync<StcpNetwork>(stream) ?? new StcpNetwork();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading network JSON: {ex.Message}");
                _networkCache = new StcpNetwork();
            }
            return _networkCache;
        }

        public async Task<List<StcpLine>> GetAllLinesAsync()
        {
            var network = await GetNetworkAsync();
            return network.Lines;
        }

        public async Task<List<StcpStop>> GetLineStopsRawAsync(string lineNumber, int directionId = 0)
        {
            var network = await GetNetworkAsync();
            var line = network.Lines.FirstOrDefault(l => l.LineNumber == lineNumber);
            
            var cachedStops = (directionId == 1) ? line?.ReturnStops : line?.Stops;
            if (cachedStops != null && cachedStops.Count > 0)
            {
                return cachedStops;
            }

            // Fallback: Query live STCP route stops endpoint
            try
            {
                var resp = await _httpClient.GetAsync($"api/route/{Uri.EscapeDataString(lineNumber)}/stops/direction?direction_id={directionId}");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<StcpApiResponse<StcpApiStopItem>>(json);
                    if (data?.Stops != null && data.Stops.Count > 0)
                    {
                        var stops = data.Stops.Select(s => new StcpStop
                        {
                            Code = !string.IsNullOrWhiteSpace(s.StopCode) ? s.StopCode : (s.StopId ?? ""),
                            Name = s.StopName ?? "",
                            Lat = s.StopLat.GetValueOrDefault(0),
                            Lng = s.StopLon.GetValueOrDefault(0),
                            Zone = s.ZoneId ?? "PRT1"
                        }).ToList();

                        if (line != null)
                        {
                            if (directionId == 1) line.ReturnStops = stops;
                            else line.Stops = stops;
                        }
                        return stops;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching live stops for line {lineNumber} (dir {directionId}): {ex.Message}");
            }

            return cachedStops ?? new List<StcpStop>();
        }

        /// <summary>
        /// Retrieves the exact street polyline coordinates for the bus route shape.
        /// </summary>
        public async Task<List<double[]>> GetRouteShapeAsync(string lineNumber, int directionId = 0)
        {
            var network = await GetNetworkAsync();
            var line = network.Lines.FirstOrDefault(l => l.LineNumber == lineNumber);
            
            var cachedShape = (directionId == 1) ? line?.ReturnRouteShape : line?.RouteShape;
            if (cachedShape != null && cachedShape.Count > 0)
            {
                return cachedShape;
            }

            // Fallback: Query live STCP route shape endpoint
            try
            {
                var resp = await _httpClient.GetAsync($"api/route/{Uri.EscapeDataString(lineNumber)}/shape?direction_id={directionId}");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<StcpApiResponse<StcpApiCoordItem>>(json);
                    if (data?.Coordinates != null && data.Coordinates.Count > 0)
                    {
                        var coords = data.Coordinates
                            .Where(c => c.Lat.HasValue && c.Lng.HasValue)
                            .Select(c => new double[] { c.Lat!.Value, c.Lng!.Value })
                            .ToList();

                        if (line != null)
                        {
                            if (directionId == 1) line.ReturnRouteShape = coords;
                            else line.RouteShape = coords;
                        }
                        return coords;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching live shape for line {lineNumber} (dir {directionId}): {ex.Message}");
            }

            return new List<double[]>();
        }
    }
}
