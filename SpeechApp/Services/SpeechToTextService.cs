using System.IO.Compression;
using System.Speech.Recognition;
using System.Text.Json;
using NAudio.Wave;
using Vosk;

namespace SpeechApp.Services;

/// <summary>
/// Çok dilli Sesten Metne (Speech-to-Text) servisi:
/// 1) Vosk + NAudio (Türkçe, İngilizce veya Türkçe+İngilizce Otomatik Çift Model)
/// 2) Windows SAPI (System.Speech.Recognition)
/// </summary>
public class SpeechToTextService : IDisposable
{
    private const string TrModelName = "vosk-model-small-tr-0.3";
    private const string TrModelUrl = "https://alphacephei.com/vosk/models/vosk-model-small-tr-0.3.zip";
    private const string EnModelName = "vosk-model-small-en-us-0.15";
    private const string EnModelUrl = "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip";

    private readonly string _modelsRoot;
    private Model? _trModel;
    private Model? _enModel;
    private VoskRecognizer? _trRecognizer;
    private VoskRecognizer? _enRecognizer;
    private WaveInEvent? _waveIn;
    private SpeechRecognitionEngine? _sapiEngine;
    private bool _dualMode;
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
            "Türkçe + İngilizce (Otomatik - Vosk)",
            "Türkçe (tr-TR - Vosk)",
            "İngilizce (en-US - Vosk)"
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

        if (selectedOption.StartsWith("Türkçe + İngilizce"))
        {
            return await StartVoskEnginesAsync(loadTr: true, loadEn: true, label: "TR + EN Otomatik");
        }
        else if (selectedOption.StartsWith("Türkçe"))
        {
            return await StartVoskEnginesAsync(loadTr: true, loadEn: false, label: "Türkçe (tr-TR)");
        }
        else if (selectedOption.StartsWith("İngilizce"))
        {
            return await StartVoskEnginesAsync(loadTr: false, loadEn: true, label: "İngilizce (en-US)");
        }
        else
        {
            string cultureDisplay = selectedOption.Replace("[Yerel SAPI] ", "");
            return StartSapi(cultureDisplay);
        }
    }

    private async Task<bool> StartVoskEnginesAsync(bool loadTr, bool loadEn, string label)
    {
        try
        {
            string trPath = Path.Combine(_modelsRoot, TrModelName);
            string enPath = Path.Combine(_modelsRoot, EnModelName);

            if (loadTr && !Directory.Exists(trPath))
            {
                if (!await DownloadModelAsync(TrModelName, TrModelUrl, trPath, "Türkçe")) return false;
            }
            if (loadEn && !Directory.Exists(enPath))
            {
                if (!await DownloadModelAsync(EnModelName, EnModelUrl, enPath, "İngilizce")) return false;
            }

            StatusInfoChanged?.Invoke($"{label} modeli yükleniyor...");
            await Task.Run(() =>
            {
                CleanupVosk();
                if (loadTr)
                {
                    _trModel = new Model(trPath);
                    _trRecognizer = new VoskRecognizer(_trModel, 16000.0f);
                    _trRecognizer.SetWords(true);
                }
                if (loadEn)
                {
                    _enModel = new Model(enPath);
                    _enRecognizer = new VoskRecognizer(_enModel, 16000.0f);
                    _enRecognizer.SetWords(true);
                }
            });

            _dualMode = loadTr && loadEn;
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
            ErrorOccurred?.Invoke($"Konuşma tanıma başlatılamadı: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> DownloadModelAsync(string modelName, string url, string targetDir, string langLabel)
    {
        string zipPath = Path.Combine(_modelsRoot, $"{modelName}.zip");
        try
        {
            StatusInfoChanged?.Invoke($"İlk kullanım için {langLabel} modeli indiriliyor...");
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
                        StatusInfoChanged?.Invoke($"{langLabel} konuşma modeli indiriliyor: %{percent}");
                    }
                }
            }

            fileStream.Close();
            StatusInfoChanged?.Invoke($"{langLabel} model dosyaları çıkarılıyor...");
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, _modelsRoot, overwriteFiles: true));

            if (File.Exists(zipPath)) File.Delete(zipPath);
            return Directory.Exists(targetDir);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            ErrorOccurred?.Invoke($"{langLabel} konuşma modeli indirilemedi (İnternet bağlantınızı kontrol edin).\nDetay: {ex.Message}");
            return false;
        }
    }

    private void OnWaveDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!IsListening) return;

        try
        {
            if (_dualMode && _trRecognizer != null && _enRecognizer != null)
            {
                bool trReady = _trRecognizer.AcceptWaveform(e.Buffer, e.BytesRecorded);
                bool enReady = _enRecognizer.AcceptWaveform(e.Buffer, e.BytesRecorded);

                if (trReady || enReady)
                {
                    string trJson = trReady ? _trRecognizer.Result() : _trRecognizer.FinalResult();
                    string enJson = enReady ? _enRecognizer.Result() : _enRecognizer.FinalResult();

                    var (trText, trConf) = ParseVoskResult(trJson);
                    var (enText, enConf) = ParseVoskResult(enJson);

                    if (!string.IsNullOrWhiteSpace(trText) || !string.IsNullOrWhiteSpace(enText))
                    {
                        if (enConf > trConf && !string.IsNullOrWhiteSpace(enText))
                            TextRecognized?.Invoke(enText, enConf);
                        else if (!string.IsNullOrWhiteSpace(trText))
                            TextRecognized?.Invoke(trText, trConf);
                        else if (!string.IsNullOrWhiteSpace(enText))
                            TextRecognized?.Invoke(enText, enConf);
                    }
                }
                else
                {
                    string trPart = ExtractJsonField(_trRecognizer.PartialResult(), "partial");
                    string enPart = ExtractJsonField(_enRecognizer.PartialResult(), "partial");
                    string combined = string.IsNullOrWhiteSpace(enPart) ? trPart : $"{trPart} / {enPart}";
                    if (!string.IsNullOrWhiteSpace(combined))
                        HypothesisUpdated?.Invoke(combined);
                }
                return;
            }

            var active = _trRecognizer ?? _enRecognizer;
            if (active == null) return;

            if (active.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                var (text, conf) = ParseVoskResult(active.Result());
                if (!string.IsNullOrWhiteSpace(text))
                    TextRecognized?.Invoke(text, conf);
            }
            else
            {
                string partial = ExtractJsonField(active.PartialResult(), "partial");
                if (!string.IsNullOrWhiteSpace(partial))
                    HypothesisUpdated?.Invoke(partial);
            }
        }
        catch { }
    }

    private static (string Text, float AvgConfidence) ParseVoskResult(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement.TryGetProperty("text", out var t) ? (t.GetString()?.Trim() ?? "") : "";
            float avgConf = 0f;
            if (doc.RootElement.TryGetProperty("result", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                int count = 0;
                float sum = 0;
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.TryGetProperty("conf", out var c))
                    {
                        sum += (float)c.GetDouble();
                        count++;
                    }
                }
                if (count > 0) avgConf = sum / count;
            }
            return (text, avgConf);
        }
        catch
        {
            return (string.Empty, 0f);
        }
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

        try { _trRecognizer?.Dispose(); } catch { }
        try { _enRecognizer?.Dispose(); } catch { }
        try { _trModel?.Dispose(); } catch { }
        try { _enModel?.Dispose(); } catch { }
        _trRecognizer = null;
        _enRecognizer = null;
        _trModel = null;
        _enModel = null;
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
