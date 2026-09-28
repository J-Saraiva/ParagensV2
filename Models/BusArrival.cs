namespace ParagensV2.Models
{
    public enum VehicleType
    {
        Bus,
        Tram,
        Train
    }

    public class BusArrival
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string LineNumber { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string StopName { get; set; } = string.Empty;
        public List<int> EstimatedArrivalsMinutes { get; set; } = new();
        public bool IsRealTime { get; set; }
        public string LineColorHex { get; set; } = "#1E88E5";
        public Microsoft.Maui.Graphics.Color LineColor => Microsoft.Maui.Graphics.Color.FromArgb(LineColorHex);
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool HasLiveLocation => Latitude.HasValue && Longitude.HasValue;
        
        public VehicleType Type { get; set; } = VehicleType.Bus;
        public string VehicleIcon => Type switch
        {
            VehicleType.Tram => "🚋",
            VehicleType.Train => "🚆",
            _ => "🚌"
        };

        /// <summary>
        /// Formatted string showing upcoming ETAs (e.g. "5 min, 12 min")
        /// </summary>
        public string FormattedEta => EstimatedArrivalsMinutes.Count switch
        {
            0 => "Sem serviço",
            1 => $"{EstimatedArrivalsMinutes[0]} min",
            _ => string.Join(", ", EstimatedArrivalsMinutes.Select(m => $"{m} min"))
        };

        /// <summary>
        /// Immediate next arrival in minutes
        /// </summary>
        public int NextEtaMinutes => EstimatedArrivalsMinutes.FirstOrDefault();
    }
}

