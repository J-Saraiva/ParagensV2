using System.Text.Json.Serialization;

namespace ParagensV2.Models;

public class MetroStation
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Lat")]
    public double Lat { get; set; }

    [JsonPropertyName("Lng")]
    public double Lng { get; set; }

    [JsonPropertyName("Zone")]
    public string Zone { get; set; } = string.Empty;

    [JsonPropertyName("Lines")]
    public List<string> Lines { get; set; } = new();

    [JsonPropertyName("LinesDisplay")]
    public string LinesDisplay { get; set; } = string.Empty;

    [JsonPropertyName("Order")]
    public int Order { get; set; }

    public string ZoneDisplay => !string.IsNullOrEmpty(Zone) ? $"Zona {Zone}" : "";
    public string Subtitle => !string.IsNullOrEmpty(LinesDisplay) ? $"Metro • Linhas {LinesDisplay}" : "Metro do Porto";
}

public class MetroDirection
{
    [JsonPropertyName("Headsign")]
    public string Headsign { get; set; } = string.Empty;

    [JsonPropertyName("Stops")]
    public List<MetroStation> Stops { get; set; } = new();
}

public class MetroLine
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("ShortName")]
    public string ShortName { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ColorHex")]
    public string ColorHex { get; set; } = "#199FDA";

    [JsonPropertyName("Origin")]
    public string Origin { get; set; } = string.Empty;

    [JsonPropertyName("Destination")]
    public string Destination { get; set; } = string.Empty;

    [JsonPropertyName("Direction0")]
    public MetroDirection Direction0 { get; set; } = new();

    [JsonPropertyName("Direction1")]
    public MetroDirection Direction1 { get; set; } = new();

    public string BadgeText => ShortName;
    public string RouteDescription => !string.IsNullOrEmpty(Origin) && !string.IsNullOrEmpty(Destination) 
        ? $"{Origin} ➔ {Destination}" 
        : Name;
}

public class MetroDeparture
{
    public string Line { get; set; } = string.Empty;
    public string LineColor { get; set; } = "#199FDA";
    public string Destination { get; set; } = string.Empty;
    public string ScheduledTime { get; set; } = string.Empty;
    public int MinutesUntil { get; set; }
    public string Status { get; set; } = string.Empty;

    public string EstimatedTimeDisplay => MinutesUntil <= 0 
        ? "A chegar" 
        : (MinutesUntil == 1 ? "1 min" : $"{MinutesUntil} min");

    public string TimeSubtitle => $"Previsto às {ScheduledTime}";
}

public class MetroScheduleItem
{
    [JsonPropertyName("l")]
    public string Line { get; set; } = string.Empty;

    [JsonPropertyName("c")]
    public string Color { get; set; } = "#199FDA";

    [JsonPropertyName("d")]
    public string Destination { get; set; } = string.Empty;

    [JsonPropertyName("t")]
    public string Time { get; set; } = string.Empty;

    [JsonPropertyName("day")]
    public string Day { get; set; } = "wd";
}

public class MetroBundle
{
    [JsonPropertyName("Lines")]
    public List<MetroLine> Lines { get; set; } = new();

    [JsonPropertyName("Stops")]
    public List<MetroStation> Stops { get; set; } = new();

    [JsonPropertyName("Schedules")]
    public Dictionary<string, List<MetroScheduleItem>> Schedules { get; set; } = new();
}
