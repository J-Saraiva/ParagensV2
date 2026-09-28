using System.Text.Json.Serialization;

namespace ParagensV2.Models;

public enum FavoriteType
{
    Line,
    Stop,
    Station
}

public enum FavoriteTransportMode
{
    Stcp,
    Unir,
    Metro,
    Train
}

public class FavoriteItem
{
    public string Id { get; set; } = string.Empty;
    public FavoriteType Type { get; set; }
    public FavoriteTransportMode Mode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string BadgeText { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#28d8c3";
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public Microsoft.Maui.Graphics.Color Color => Microsoft.Maui.Graphics.Color.FromArgb(ColorHex);

    [JsonIgnore]
    public string ModeDisplay => Mode switch
    {
        FavoriteTransportMode.Stcp => "STCP",
        FavoriteTransportMode.Unir => "Rede UNIR",
        FavoriteTransportMode.Metro => "Metro do Porto",
        FavoriteTransportMode.Train => "CP Comboios",
        _ => ""
    };

    [JsonIgnore]
    public string TypeDisplay => Type switch
    {
        FavoriteType.Line => "Linha",
        FavoriteType.Stop => "Paragem",
        FavoriteType.Station => "Estação",
        _ => ""
    };

    [JsonIgnore]
    public string TagDisplay => $"{ModeDisplay} • {TypeDisplay}";
}
