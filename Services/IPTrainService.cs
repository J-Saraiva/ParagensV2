using System.Net.Http.Json;
using System.Text.Json;
using ParagensV2.Models;

namespace ParagensV2.Services
{
    public class IPTrainService
    {
        private readonly HttpClient _client;
        private static List<TrainStation>? _cachedStations = null;

        public IPTrainService()
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri("https://comboios.ruicosta.pt/"),
                Timeout = TimeSpan.FromSeconds(10)
            };
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            _client.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<List<TrainStation>> GetAllStationsAsync()
        {
            if (_cachedStations != null && _cachedStations.Count > 0)
            {
                return _cachedStations;
            }

            try
            {
                using var stream = await FileSystem.Current.OpenAppPackageFileAsync("train_stations.json");
                _cachedStations = await JsonSerializer.DeserializeAsync<List<TrainStation>>(stream) ?? new List<TrainStation>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading local train stations: {ex.Message}");
                _cachedStations = GetPopularStations();
            }

            return _cachedStations;
        }

        public static List<TrainStation> GetPopularStations()
        {
            return new List<TrainStation>
            {
                new() { Code = "94-2006", Name = "Porto Campanhã" },
                new() { Code = "94-1008", Name = "Porto São Bento" },
                new() { Code = "94-29157", Name = "Braga" },
                new() { Code = "94-29405", Name = "Guimarães" },
                new() { Code = "94-38000", Name = "Aveiro" },
                new() { Code = "94-36004", Name = "Coimbra - B" },
                new() { Code = "94-41020", Name = "Coimbra" },
                new() { Code = "94-31039", Name = "Lisboa Oriente" },
                new() { Code = "94-30007", Name = "Lisboa Santa Apolónia" },
                new() { Code = "94-59006", Name = "Lisboa Rossio" },
                new() { Code = "94-73007", Name = "Faro" },
                new() { Code = "94-18002", Name = "Viana do Castelo" },
                new() { Code = "94-49007", Name = "Guarda" },
                new() { Code = "94-75002", Name = "Beja" },
                new() { Code = "94-74203", Name = "Évora" }
            };
        }

        public async Task<List<TrainStation>> SearchStationsAsync(string query)
        {
            var stations = await GetAllStationsAsync();
            if (string.IsNullOrWhiteSpace(query))
            {
                return stations;
            }

            return stations
                .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            s.Code.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<List<TrainDeparture>> GetStationDeparturesAsync(string stationCode)
        {
            var results = new List<TrainDeparture>();

            if (string.IsNullOrWhiteSpace(stationCode)) return results;

            // Ensure format like "94-2006"
            var formattedCode = NormalizeStationCode(stationCode);

            try
            {
                var response = await _client.GetFromJsonAsync<ComboiosUpcomingResponse>($"api/station/{Uri.EscapeDataString(formattedCode)}/upcoming");
                if (response?.Upcoming != null && response.Upcoming.Count > 0)
                {
                    foreach (var item in response.Upcoming)
                    {
                        var rawType = item.Type?.ToString() ?? "";
                        var (serviceLabel, serviceColor) = ResolveServiceInfo(rawType);

                        // Departure or Arrival time
                        var time = !string.IsNullOrEmpty(item.EstimatedDeparture)
                            ? item.EstimatedDeparture
                            : (!string.IsNullOrEmpty(item.ScheduledDeparture)
                                ? item.ScheduledDeparture
                                : (!string.IsNullOrEmpty(item.EstimatedArrival)
                                    ? item.EstimatedArrival
                                    : (!string.IsNullOrEmpty(item.ScheduledArrival) ? item.ScheduledArrival : "--:--")));

                        // Calculate delay
                        var delayMin = (int)Math.Round(item.Delay.GetValueOrDefault(0));
                        var isDelayed = delayMin > 1;

                        string obs;
                        string obsColor = "#2E7D32"; // Green by default
                        string obsBgColor = "#E8F5E9"; // Light green by default
                        
                        if (isDelayed)
                        {
                            obs = $"+{delayMin} min atraso";
                            obsColor = "#C62828"; // Red
                            obsBgColor = "#FFEBEE"; // Light red
                        }
                        else if (delayMin < -1)
                        {
                            obs = $"{Math.Abs(delayMin)} min adiantado";
                            obsColor = "#1565C0"; // Blue
                            obsBgColor = "#E3F2FD"; // Light blue
                        }
                        else
                        {
                            obs = "À hora";
                        }

                        var trainNum = item.TrainNumber?.ToString() ?? item.TrainId ?? "Comboio";
                        var dest = !string.IsNullOrWhiteSpace(item.Destination) ? item.Destination : "Destino";
                        var orig = !string.IsNullOrWhiteSpace(item.Origin) ? item.Origin : "";

                        results.Add(new TrainDeparture
                        {
                            TrainId = item.TrainId ?? "",
                            DepartureTime = time,
                            TrainNumber = trainNum,
                            ServiceType = serviceLabel,
                            Origin = orig,
                            Destination = dest,
                            Platform = item.Platform ?? "",
                            Operator = serviceLabel.Contains("Fertagus", StringComparison.OrdinalIgnoreCase) ? "Fertagus" : "CP",
                            Observations = obs,
                            IsDelayed = isDelayed,
                            ServiceColorHex = serviceColor,
                            ObservationColorHex = obsColor,
                            ObservationBgColorHex = obsBgColor
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching upcoming departures for {stationCode}: {ex.Message}");
            }

            return results;
        }

        public async Task<string?> GetTrainDetailsRawAsync(string trainId)
        {
            try
            {
                var response = await _client.GetAsync($"api/train/{Uri.EscapeDataString(trainId)}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching train details: {ex.Message}");
            }
            return null;
        }

        public async Task<List<TrainDeparture>> GetStationDeparturesAsync(long nodeId)
        {
            var stations = await GetAllStationsAsync();
            var matched = stations.FirstOrDefault(s => s.NodeId == nodeId);
            var code = matched?.Code ?? nodeId.ToString();
            return await GetStationDeparturesAsync(code);
        }

        private static string NormalizeStationCode(string code)
        {
            code = code.Trim();
            if (code.Contains('-')) return code;

            // e.g. 9402006 -> 94-2006
            if (code.StartsWith("94") && code.Length >= 6)
            {
                return "94-" + code[2..];
            }

            return code;
        }

        private static (string Label, string ColorHex) ResolveServiceInfo(string type)
        {
            type = type.Trim().ToLowerInvariant();

            return type switch
            {
                "1" or "ap" or "alfa" => ("Alfa Pendular", "#C62828"), // Red
                "2" or "ic" or "intercidades" => ("Intercidades", "#1565C0"), // Blue
                "3" or "ir" or "interregional" => ("InterRegional", "#00838F"), // Cyan
                "4" or "40" or "r" or "regional" => ("Regional", "#2E7D32"), // Green
                "5" or "45" or "55" or "u" or "urbano" => ("Urbano", "#EF6C00"), // Orange
                "uf" or "fertagus" => ("Fertagus", "#00897B"), // Teal
                "12" or "in" or "internacional" => ("Internacional", "#F57F17"), // Amber
                "14" or "es" or "especial" => ("Especial", "#546E7A"),
                _ => ("CP", "#37474F")
            };
        }
    }
}
