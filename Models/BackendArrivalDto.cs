using System.Text.Json.Serialization;

namespace ParagensV2.Models
{
    public class BackendArrivalDto
    {
        [JsonPropertyName("line")]
        public string Line { get; set; } = string.Empty;

        [JsonPropertyName("destination")]
        public string Destination { get; set; } = string.Empty;

        [JsonPropertyName("etaMinutes")]
        public int EtaMinutes { get; set; }

        [JsonPropertyName("isRealTime")]
        public bool IsRealTime { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }
    }
}

