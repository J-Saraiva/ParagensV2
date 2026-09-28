using System.Text.Json;
using System.Text.Json.Serialization;
using ParagensV2.Models;

namespace ParagensV2.Services
{
    public class STCPScraperService
    {
        private readonly HttpClient _client;

        public STCPScraperService()
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri("https://stcp.pt/"),
                Timeout = TimeSpan.FromSeconds(10)
            };
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _client.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        private class StcpRealtimeResponse
        {
            [JsonPropertyName("stop_id")]
            public string? StopId { get; set; }

            [JsonPropertyName("stop_name")]
            public string? StopName { get; set; }

            [JsonPropertyName("arrivals")]
            public List<StcpRealtimeArrival>? Arrivals { get; set; }
        }

        private class StcpRealtimeArrival
        {
            [JsonPropertyName("route_short_name")]
            public string? RouteShortName { get; set; }

            [JsonPropertyName("route_color")]
            public string? RouteColor { get; set; }

            [JsonPropertyName("trip_headsign")]
            public string? TripHeadsign { get; set; }

            [JsonPropertyName("estimated_arrival_time")]
            public string? EstimatedArrivalTime { get; set; }

            [JsonPropertyName("arrival_minutes")]
            public int? ArrivalMinutes { get; set; }

            [JsonPropertyName("status")]
            public string? Status { get; set; }
        }

        private class StcpRoutesResponse
        {
            [JsonPropertyName("display_routes")]
            public List<StcpRouteInfo>? DisplayRoutes { get; set; }
        }

        private class StcpRouteInfo
        {
            [JsonPropertyName("route_id")]
            public string? RouteId { get; set; }

            [JsonPropertyName("route_short_name")]
            public string? RouteShortName { get; set; }

            [JsonPropertyName("route_long_name")]
            public string? RouteLongName { get; set; }

            [JsonPropertyName("route_color")]
            public string? RouteColor { get; set; }
        }

        public async Task<List<BusArrival>> ScrapeStopAsync(string stopCode)
        {
            var arrivals = new List<BusArrival>();

            try
            {
                // Call official STCP Realtime API
                var response = await _client.GetAsync($"api/stops/{Uri.EscapeDataString(stopCode)}/realtime");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<StcpRealtimeResponse>(json);

                    if (data?.Arrivals != null && data.Arrivals.Count > 0)
                    {
                        foreach (var item in data.Arrivals)
                        {
                            int eta = item.ArrivalMinutes.GetValueOrDefault(0);
                            arrivals.Add(new BusArrival
                            {
                                Id = Guid.NewGuid().ToString(),
                                LineNumber = item.RouteShortName ?? "?",
                                Destination = !string.IsNullOrWhiteSpace(item.TripHeadsign) ? item.TripHeadsign : "Destino",
                                StopName = data.StopName ?? stopCode,
                                EstimatedArrivalsMinutes = new List<int> { eta },
                                IsRealTime = true,
                                LineColorHex = !string.IsNullOrWhiteSpace(item.RouteColor) ? item.RouteColor : "#2196F3",
                                Type = VehicleType.Bus
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"STCP Realtime API call failed: {ex.Message}");
            }

            // If no active real-time vehicles, query the routes serving this stop so user sees the actual lines
            if (arrivals.Count == 0)
            {
                try
                {
                    var routesResp = await _client.GetAsync($"api/stops/{Uri.EscapeDataString(stopCode)}/routes");
                    if (routesResp.IsSuccessStatusCode)
                    {
                        var json = await routesResp.Content.ReadAsStringAsync();
                        var routesData = JsonSerializer.Deserialize<StcpRoutesResponse>(json);
                        if (routesData?.DisplayRoutes != null && routesData.DisplayRoutes.Count > 0)
                        {
                            foreach (var route in routesData.DisplayRoutes)
                            {
                                arrivals.Add(new BusArrival
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    LineNumber = route.RouteShortName ?? route.RouteId ?? "?",
                                    Destination = route.RouteLongName ?? "Em serviço",
                                    StopName = stopCode,
                                    EstimatedArrivalsMinutes = new List<int>(), // No active arrivals right now
                                    IsRealTime = false,
                                    LineColorHex = route.RouteColor ?? "#2196F3",
                                    Type = VehicleType.Bus
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"STCP Routes API call failed: {ex.Message}");
                }
            }

            return arrivals.OrderByDescending(a => a.IsRealTime).ThenBy(a => a.NextEtaMinutes).ToList();
        }
    }
}
