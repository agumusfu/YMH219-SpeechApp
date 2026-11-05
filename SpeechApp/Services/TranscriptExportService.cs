using System.Text;
using SpeechApp.Models;

namespace SpeechApp.Services;

/// <summary>
/// Dönüştürülen metinleri ve zaman damgalı konuşma kayıtlarını StreamWriter ile .txt dosyasına aktaran servis.
/// </summary>
public class TranscriptExportService
{
    public (bool Success, string Message) SaveToFile(string filePath, string plainText, IEnumerable<SpeechRecord> records)
    {
        try
        {
            var recordList = records.ToList();
            if (string.IsNullOrWhiteSpace(plainText) && recordList.Count == 0)
                return (false, "Kaydedilecek herhangi bir metin veya kayıt bulunmuyor.");

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("=================================================================");
            writer.WriteLine("  YMH 219 - SpeechApp (TTS & STT) Metin Çıktısı");
            writer.WriteLine($"  Kaydedilme Tarihi: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine("=================================================================");
            writer.WriteLine();

            if (!string.IsNullOrWhiteSpace(plainText))
            {
                writer.WriteLine("--- METİN ALANI ---");
                writer.WriteLine(plainText.Trim());
                writer.WriteLine();
            }

            if (recordList.Count > 0)
            {
                writer.WriteLine($"--- ZAMAN DAMGALI KAYITLAR ({recordList.Count}) ---");
                foreach (var rec in recordList)
                {
                    writer.WriteLine(rec.ToDisplayString());
                }
            }

            return (true, $"Metin ve kayıtlar başarıyla kaydedildi:\n{filePath}");
        }
        catch (Exception ex)
        {
            return (false, $"Dosya kaydedilirken hata oluştu: {ex.Message}");
        }
    }
}
