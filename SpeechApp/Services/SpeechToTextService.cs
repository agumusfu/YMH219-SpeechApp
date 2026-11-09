using System.Globalization;
using System.IO.Compression;
using System.Speech.Recognition;
using System.Text.Json;
using NAudio.Wave;
using Vosk;

namespace SpeechApp.Services;

/// <summary>
/// Çift motorlu Sesten Metne (Speech-to-Text) servisi:
/// 1) Vosk + NAudio (Türkçe & İngilizce çevrimdışı yüksek kaliteli konuşma tanıma)
/// 2) Windows SAPI (System.Speech.Recognition)
/// </summary>
public class SpeechToTextService : IDisposable
{
    private const string TrModelName = "vosk-model-small-tr-0.3";
    private const string TrModelUrl = "https://alphacephei.com/vosk/models/vosk-model-small-tr-0.3.zip";
    private const string EnModelName = "vosk-model-small-en-us-0.15";
    private const string EnModelUrl = "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip";

    private readonly string _modelsRoot;
    private Model? _voskModel;
    private VoskRecognizer? _voskRecognizer;
    private WaveInEvent? _waveIn;
    private SpeechRecognitionEngine? _sapiEngine;
    private bool _disposed;

    public bool IsListening { get; private set; }
    public string ActiveModeLabel { get; private set; } = string.Empty;

    public event Action<string, float>? TextRecognized;
    public event Action<string>? HypothesisUpdated;
    public event Action<bool>? ListeningStateChanged;
    public event Action<string>? StatusInfoChanged;
    public event Action<string>? ErrorOccurred;

    public SpeechToTextService()
    {
        _modelsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpeechApp", "models");
        Directory.CreateDirectory(_modelsRoot);
        Vosk.Vosk.SetLogLevel(-1);
    }

    public static List<string> GetAvailableEngines()
    {
        var list = new List<string>
        {
            "Türkçe (tr-TR - Vosk Akıllı Model)",
            "İngilizce (en-US - Vosk Akıllı Model)"
        };

        try
        {
            foreach (var r in SpeechRecognitionEngine.InstalledRecognizers())
            {
                list.Add($"[Yerel SAPI] {r.Culture.DisplayName}");
            }
        }
        catch { }

        return list;
    }

    public async Task<bool> StartListeningAsync(string selectedOption)
    {
        if (IsListening) return true;

        if (WaveInEvent.DeviceCount == 0)
        {
            ErrorOccurred?.Invoke("Sistemde aktif bir mikrofon aygıtı bulunamadı. Lütfen mikrofon bağlantınızı kontrol edin.");
            return false;
        }

        if (selectedOption.StartsWith("Türkçe"))
        {
            return await StartVoskAsync(TrModelName, TrModelUrl, "Türkçe (tr-TR)");
        }
        else if (selectedOption.StartsWith("İngilizce"))
        {
            return await StartVoskAsync(EnModelName, EnModelUrl, "İngilizce (en-US)");
        }
        else
        {
            string cultureDisplay = selectedOption.Replace("[Yerel SAPI] ", "");
            return StartSapi(cultureDisplay);
        }
    }

    private async Task<bool> StartVoskAsync(string modelName, string modelUrl, string label)
    {
        try
        {
            string modelPath = Path.Combine(_modelsRoot, modelName);
            if (!Directory.Exists(modelPath))
            {
                bool downloaded = await DownloadModelAsync(modelName, modelUrl, modelPath);
                if (!downloaded) return false;
            }

            StatusInfoChanged?.Invoke($"{label} konuşma modeli yükleniyor...");
            await Task.Run(() =>
            {
                CleanupVosk();
                _voskModel = new Model(modelPath);
                _voskRecognizer = new VoskRecognizer(_voskModel, 16000.0f);
            });

            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 250
            };

            _waveIn.DataAvailable += OnWaveDataAvailable;
            _waveIn.RecordingStopped += (s, e) =>
            {
                if (e.Exception != null)
                    ErrorOccurred?.Invoke($"Mikrofon kaydı durdu: {e.Exception.Message}");
            };

            _waveIn.StartRecording();
            IsListening = true;
            ActiveModeLabel = label;
            ListeningStateChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            CleanupVosk();
            ErrorOccurred?.Invoke($"Vosk konuşma tanıma başlatılamadı: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> DownloadModelAsync(string modelName, string url, string targetDir)
    {
        string zipPath = Path.Combine(_modelsRoot, $"{modelName}.zip");
        try
        {
            StatusInfoChanged?.Invoke($"İlk kullanım için {modelName} indiriliyor...");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            long totalRead = 0;
            int read;
            int lastPercent = -1;

            while ((read = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;
                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    int percent = (int)(totalRead * 100 / totalBytes.Value);
                    if (percent != lastPercent && percent % 5 == 0)
                    {
                        lastPercent = percent;
                        StatusInfoChanged?.Invoke($"Türkçe/İngilizce konuşma modeli indiriliyor: %{percent}");
                    }
                }
            }

            fileStream.Close();
            StatusInfoChanged?.Invoke("Model dosyaları çıkarılıyor...");
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, _modelsRoot, overwriteFiles: true));

            if (File.Exists(zipPath)) File.Delete(zipPath);
            return Directory.Exists(targetDir);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            ErrorOccurred?.Invoke($"Konuşma modeli indirilemedi (İnternet bağlantınızı kontrol edin).\nDetay: {ex.Message}");
            return false;
        }
    }

    private void OnWaveDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_voskRecognizer == null || !IsListening) return;

        try
        {
            if (_voskRecognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                string json = _voskRecognizer.Result();
                string text = ExtractJsonField(json, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    TextRecognized?.Invoke(text, 1.0f);
                }
            }
            else
            {
                string json = _voskRecognizer.PartialResult();
                string partial = ExtractJsonField(json, "partial");
                if (!string.IsNullOrWhiteSpace(partial))
                {
                    HypothesisUpdated?.Invoke(partial);
                }
            }
        }
        catch { }
    }

    private static string ExtractJsonField(string json, string field)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(field, out var prop))
                return prop.GetString()?.Trim() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    private bool StartSapi(string cultureDisplay)
    {
        try
        {
            var recognizers = SpeechRecognitionEngine.InstalledRecognizers().ToList();
            if (recognizers.Count == 0)
            {
                ErrorOccurred?.Invoke("Sistemde yüklü Windows SAPI konuşma tanıma paketi bulunamadı.");
                return false;
            }

            var selected = recognizers.FirstOrDefault(r => r.Culture.DisplayName == cultureDisplay) ?? recognizers[0];
            CleanupSapi();

            _sapiEngine = new SpeechRecognitionEngine(selected);
            _sapiEngine.LoadGrammar(new DictationGrammar());
            _sapiEngine.SetInputToDefaultAudioDevice();

            _sapiEngine.SpeechRecognized += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Result?.Text))
                    TextRecognized?.Invoke(e.Result.Text, e.Result.Confidence);
            };
            _sapiEngine.SpeechHypothesized += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Result?.Text))
                    HypothesisUpdated?.Invoke(e.Result.Text);
            };
            _sapiEngine.RecognizeCompleted += (s, e) =>
            {
                IsListening = false;
                ListeningStateChanged?.Invoke(false);
            };

            _sapiEngine.RecognizeAsync(RecognizeMode.Multiple);
            IsListening = true;
            ActiveModeLabel = selected.Culture.DisplayName;
            ListeningStateChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            CleanupSapi();
            ErrorOccurred?.Invoke($"Windows SAPI başlatılamadı: {ex.Message}");
            return false;
        }
    }

    public void StopListening()
    {
        IsListening = false;
        CleanupVosk();
        CleanupSapi();
        ListeningStateChanged?.Invoke(false);
    }

    private void CleanupVosk()
    {
        try
        {
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnWaveDataAvailable;
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }
        }
        catch { }

        try { _voskRecognizer?.Dispose(); } catch { }
        try { _voskModel?.Dispose(); } catch { }
        _voskRecognizer = null;
        _voskModel = null;
    }

    private void CleanupSapi()
    {
        try
        {
            _sapiEngine?.RecognizeAsyncCancel();
            _sapiEngine?.Dispose();
        }
        catch { }
        _sapiEngine = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopListening();
        GC.SuppressFinalize(this);
    }
}
