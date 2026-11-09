using System.Net;
using System.Speech.Synthesis;
using NAudio.Wave;

namespace SpeechApp.Services;

/// <summary>
/// Çift motorlu Text-to-Speech servisi:
/// 1) Yüksek kaliteli Türkçe/İngilizce Online TTS (NAudio WaveOutEvent + Mp3FileReader)
/// 2) Çevrimdışı Windows SAPI5 (System.Speech.Synthesis.SpeechSynthesizer)
/// </summary>
public class TextToSpeechService : IDisposable
{
    private readonly SpeechSynthesizer _sapiSynthesizer;
    private readonly HttpClient _httpClient;
    private WaveOutEvent? _waveOut;
    private CancellationTokenSource? _onlineCts;
    private bool _isOnlineSpeaking;
    private bool _isOnlinePaused;
    private int _rate;
    private int _volume = 100;
    private bool _disposed;

    public bool IsSpeaking => !_disposed && (_isOnlineSpeaking || _sapiSynthesizer.State == SynthesizerState.Speaking);
    public bool IsPaused => !_disposed && (_isOnlinePaused || _sapiSynthesizer.State == SynthesizerState.Paused);

    public event Action<string>? StatusChanged; // "Ready", "Speaking", "Paused"
    public event Action<string>? ErrorOccurred;

    public TextToSpeechService()
    {
        _sapiSynthesizer = new SpeechSynthesizer();
        _sapiSynthesizer.SetOutputToDefaultAudioDevice();
        _sapiSynthesizer.StateChanged += (s, e) =>
        {
            if (!_disposed && !_isOnlineSpeaking)
            {
                StatusChanged?.Invoke(e.State.ToString());
            }
        };
        _sapiSynthesizer.SpeakCompleted += (s, e) =>
        {
            if (_disposed) return;
            if (e.Error != null && !e.Cancelled && e.Error is not OperationCanceledException)
                ErrorOccurred?.Invoke($"Seslendirme hatası: {e.Error.Message}");
            if (!_isOnlineSpeaking)
                StatusChanged?.Invoke("Ready");
        };

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
    }

    public List<string> GetAvailableVoices()
    {
        var voices = new List<string>
        {
            "Türkçe (Yüksek Kalite - Doğal Ses)",
            "İngilizce (Yüksek Kalite - Doğal Ses)"
        };

        try
        {
            foreach (InstalledVoice voice in _sapiSynthesizer.GetInstalledVoices())
            {
                if (voice.Enabled)
                {
                    voices.Add($"[Yerel SAPI] {voice.VoiceInfo.Name}");
                }
            }
        }
        catch { }

        return voices;
    }

    public void Configure(int rate, int volume)
    {
        _rate = Math.Clamp(rate, -10, 10);
        _volume = Math.Clamp(volume, 0, 100);

        _sapiSynthesizer.Rate = _rate;
        _sapiSynthesizer.Volume = _volume;
        if (_waveOut != null)
        {
            _waveOut.Volume = _volume / 100f;
        }
    }

    public async Task SpeakAsync(string text, string selectedVoice)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ErrorOccurred?.Invoke("Seslendirilecek metin boş olamaz.");
            return;
        }

        Stop();

        if (selectedVoice.StartsWith("Türkçe"))
        {
            await SpeakOnlineAsync(text, "tr");
        }
        else if (selectedVoice.StartsWith("İngilizce"))
        {
            await SpeakOnlineAsync(text, "en");
        }
        else
        {
            string sapiName = selectedVoice.Replace("[Yerel SAPI] ", "");
            try
            {
                if (!string.IsNullOrWhiteSpace(sapiName) && sapiName != "Varsayılan Sistem Sesi")
                    _sapiSynthesizer.SelectVoice(sapiName);
                _sapiSynthesizer.Rate = _rate;
                _sapiSynthesizer.Volume = _volume;
                _sapiSynthesizer.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Yerel seslendirme başlatılamadı: {ex.Message}");
            }
        }
    }

    private async Task SpeakOnlineAsync(string text, string langCode)
    {
        _onlineCts = new CancellationTokenSource();
        var token = _onlineCts.Token;
        _isOnlineSpeaking = true;
        _isOnlinePaused = false;
        StatusChanged?.Invoke("Speaking");

        try
        {
            var chunks = SplitTextIntoChunks(text, 180);
            foreach (string chunk in chunks)
            {
                if (token.IsCancellationRequested) break;

                string encoded = WebUtility.UrlEncode(chunk);
                string speedParam = _rate >= 0 ? "1" : "0.75";
                string url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={encoded}&tl={langCode}&ttsspeed={speedParam}&client=tw-ob";

                byte[] mp3Bytes = await _httpClient.GetByteArrayAsync(url, token);
                if (token.IsCancellationRequested) break;

                await PlayMp3BytesAsync(mp3Bytes, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Seslendirme servisine bağlanılamadı (İnternet bağlantınızı kontrol edin veya '[Yerel SAPI]' sesi seçin).\nDetay: {ex.Message}");
        }
        finally
        {
            _isOnlineSpeaking = false;
            _isOnlinePaused = false;
            StatusChanged?.Invoke("Ready");
        }
    }

    private async Task PlayMp3BytesAsync(byte[] mp3Bytes, CancellationToken token)
    {
        using var ms = new MemoryStream(mp3Bytes);
        using var reader = new Mp3FileReader(ms);
        using var waveOut = new WaveOutEvent();
        _waveOut = waveOut;
        waveOut.Volume = _volume / 100f;
        waveOut.Init(reader);

        var tcs = new TaskCompletionSource<bool>();
        waveOut.PlaybackStopped += (s, e) => tcs.TrySetResult(true);
        waveOut.Play();

        using var reg = token.Register(() =>
        {
            try { waveOut.Stop(); } catch { }
            tcs.TrySetCanceled();
        });

        await tcs.Task;
        _waveOut = null;
    }

    public void PauseOrResume()
    {
        if (_isOnlineSpeaking && _waveOut != null)
        {
            if (_isOnlinePaused)
            {
                _waveOut.Play();
                _isOnlinePaused = false;
                StatusChanged?.Invoke("Speaking");
            }
            else
            {
                _waveOut.Pause();
                _isOnlinePaused = true;
                StatusChanged?.Invoke("Paused");
            }
            return;
        }

        try
        {
            if (_sapiSynthesizer.State == SynthesizerState.Speaking)
                _sapiSynthesizer.Pause();
            else if (_sapiSynthesizer.State == SynthesizerState.Paused)
                _sapiSynthesizer.Resume();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Duraklatma/Devam işlemi başarısız: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            _onlineCts?.Cancel();
            _waveOut?.Stop();
        }
        catch { }

        _isOnlineSpeaking = false;
        _isOnlinePaused = false;

        if (!_disposed)
        {
            try
            {
                if (_sapiSynthesizer.State == SynthesizerState.Paused)
                    _sapiSynthesizer.Resume();
                _sapiSynthesizer.SpeakAsyncCancelAll();
            }
            catch { }

            StatusChanged?.Invoke("Ready");
        }
    }

    private static List<string> SplitTextIntoChunks(string text, int maxLen)
    {
        var chunks = new List<string>();
        var words = text.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var current = new System.Text.StringBuilder();

        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > maxLen && current.Length > 0)
            {
                chunks.Add(current.ToString().Trim());
                current.Clear();
            }
            current.Append(word).Append(' ');
        }

        if (current.Length > 0)
            chunks.Add(current.ToString().Trim());

        return chunks;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _onlineCts?.Cancel(); } catch { }
        try { _waveOut?.Stop(); } catch { }
        try { _sapiSynthesizer.SpeakAsyncCancelAll(); } catch { }
        try { _httpClient.Dispose(); } catch { }
        try { _sapiSynthesizer.Dispose(); } catch { }
        GC.SuppressFinalize(this);
    }
}
