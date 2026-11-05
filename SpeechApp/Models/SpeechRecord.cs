namespace SpeechApp.Models;

/// <summary>
/// Gerçekleştirilen ses-metin veya metin-ses dönüşüm kaydını temsil eder.
/// </summary>
public class SpeechRecord
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Mode { get; set; } = "STT"; // "TTS" veya "STT"
    public string Text { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;

    public string ToDisplayString()
    {
        string detailPart = string.IsNullOrWhiteSpace(Detail) ? string.Empty : $" ({Detail})";
        return $"[{Timestamp:HH:mm:ss}] [{Mode}]{detailPart} {Text}";
    }
}
