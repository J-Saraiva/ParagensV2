using System.Text.Json.Serialization;

namespace ParagensV2.Models
{
    public class TrainStation
    {
        [JsonPropertyName("Code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Lat")]
        public double Lat { get; set; }

        [JsonPropertyName("Lon")]
        public double Lon { get; set; }

        public long NodeId
        {
            get
            {
                if (string.IsNullOrEmpty(Code)) return 0;
                var cleaned = Code.Replace("-", "");
                return long.TryParse(cleaned, out var id) ? id : 0;
            }
            set
            {
                if (string.IsNullOrEmpty(Code))
                {
                    Code = value.ToString();
                }
            }
        }
    }

    public class TrainDeparture
    {
        public string TrainId { get; set; } = string.Empty;
        public string DepartureTime { get; set; } = string.Empty;
        public string TrainNumber { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string Operator { get; set; } = "CP";
        public string Observations { get; set; } = "À hora";
        public bool IsDelayed { get; set; }
        public string ServiceColorHex { get; set; } = "#1976D2";
        public string ObservationColorHex { get; set; } = "#2E7D32";
        public string ObservationBgColorHex { get; set; } = "#E8F5E9";

        public string PlatformDisplay => string.IsNullOrEmpty(Platform) ? "" : $"Linha {Platform}";
        public string DisplayTitle => $"{ServiceType} • {Destination}";
    }

    public class ComboiosUpcomingResponse
    {
        [JsonPropertyName("stationCode")]
        public string? StationCode { get; set; }

        [JsonPropertyName("upcoming")]
        public List<ComboiosUpcomingTrainItem> Upcoming { get; set; } = new();
    }

    public class ComboiosUpcomingTrainItem
    {
        [JsonPropertyName("trainId")]
        public string? TrainId { get; set; }

        [JsonPropertyName("trainNumber")]
        public object? TrainNumber { get; set; }

        [JsonPropertyName("type")]
        public object? Type { get; set; }

        [JsonPropertyName("origin")]
        public string? Origin { get; set; }

        [JsonPropertyName("destination")]
        public string? Destination { get; set; }

        [JsonPropertyName("scheduledArrival")]
        public string? ScheduledArrival { get; set; }

        [JsonPropertyName("estimatedArrival")]
        public string? EstimatedArrival { get; set; }

        [JsonPropertyName("scheduledDeparture")]
        public string? ScheduledDeparture { get; set; }

        [JsonPropertyName("estimatedDeparture")]
        public string? EstimatedDeparture { get; set; }

        [JsonPropertyName("delay")]
        public double? Delay { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }
    }
}
