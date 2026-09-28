using System.Text.Json.Serialization;

namespace ParagensV2.Models;

public class UnirLine
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#4877ff";

    public Microsoft.Maui.Graphics.Color Color => Microsoft.Maui.Graphics.Color.FromArgb(ColorHex);

    public string Subtitle => !string.IsNullOrEmpty(Municipality) 
        ? $"Rede UNIR • {Municipality}" 
        : "Rede UNIR • Área Metropolitana do Porto";
}

public class UnirStop
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string Lines { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int Order { get; set; }
    public int Direction { get; set; } = 1;

    public string ZoneDisplay => !string.IsNullOrEmpty(Zone) ? $"Zona {Zone}" : "";
    public string SequenceDisplay => Order > 0 ? $"#{Order}" : "";
}

public class UnirDeparture
{
    public string Line { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string ScheduledTime { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public int MinutesUntil { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsTomorrow { get; set; }
    public bool IsRealtime { get; set; }
    public int? DelayMinutes { get; set; }

    public bool IsImminent => !IsTomorrow && MinutesUntil <= 2;
    public bool IsSoon => !IsTomorrow && MinutesUntil > 2 && MinutesUntil <= 15;

    public string EstimatedTimeDisplay
    {
        get
        {
            if (IsTomorrow)
            {
                return ScheduledTime;
            }
            if (MinutesUntil <= 0)
            {
                return IsRealtime ? "⚡ A chegar" : "A chegar";
            }
            if (MinutesUntil == 1)
            {
                return IsRealtime ? "⚡ 1 min" : "1 min";
            }
            if (MinutesUntil < 60)
            {
                return IsRealtime ? $"⚡ {MinutesUntil} min" : $"{MinutesUntil} min";
            }
            return ScheduledTime;
        }
    }

    public string TimeSubtitle
    {
        get
        {
            if (IsTomorrow)
            {
                return $"Amanhã às {ScheduledTime}";
            }
            if (IsRealtime)
            {
                if (DelayMinutes.HasValue && DelayMinutes.Value != 0)
                {
                    string sign = DelayMinutes.Value > 0 ? "+" : "";
                    return $"Programado: {ScheduledTime} • GPS: {sign}{DelayMinutes.Value} min";
                }
                return $"Programado: {ScheduledTime} • GPS em direto";
            }
            if (MinutesUntil < 60)
            {
                return $"Horário previsto: {ScheduledTime}";
            }
            int hours = MinutesUntil / 60;
            int mins = MinutesUntil % 60;
            return $"Em {hours}h {mins:D2}m • Horário: {ScheduledTime}";
        }
    }

    public Microsoft.Maui.Graphics.Color BadgeBackgroundColor => IsRealtime
        ? (IsImminent
            ? Microsoft.Maui.Graphics.Color.FromArgb("#DCFCE7") // Light green
            : Microsoft.Maui.Graphics.Color.FromArgb("#E0F2FE")) // Light cyan/sky
        : (IsImminent
            ? Microsoft.Maui.Graphics.Color.FromArgb("#DCFCE7") // Light green
            : IsSoon
                ? Microsoft.Maui.Graphics.Color.FromArgb("#FEF3C7") // Light amber
                : Microsoft.Maui.Graphics.Color.FromArgb("#E3F2FD")); // Light blue

    public Microsoft.Maui.Graphics.Color BadgeTextColor => IsRealtime
        ? (IsImminent
            ? Microsoft.Maui.Graphics.Color.FromArgb("#166534") // Dark green
            : Microsoft.Maui.Graphics.Color.FromArgb("#0369A1")) // Dark cyan/sky
        : (IsImminent
            ? Microsoft.Maui.Graphics.Color.FromArgb("#166534") // Dark green
            : IsSoon
                ? Microsoft.Maui.Graphics.Color.FromArgb("#92400E") // Dark amber
                : Microsoft.Maui.Graphics.Color.FromArgb("#1565C0")); // Dark blue
}

