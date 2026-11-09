using System.Net;
using System.Speech.Synthesis;
using NAudio.Wave;

namespace SpeechApp.Services;

/// <summary>
/// Çift motorlu Text-to-Speech servisi:
/// 1) Yüksek kaliteli Türkçe/İngilizce Online TTS (NAudio WaveOutEvent + anlık hız/ses kontrolü)
/// 2) Çevrimdışı Windows SAPI5 (System.Speech.Synthesis.SpeechSynthesizer)
/// </summary>
public class TextToSpeechService : IDisposable
{
    private readonly SpeechSynthesizer _sapiSynthesizer;
    private readonly HttpClient _httpClient;
    private WaveOutEvent? _waveOut;
    private SpeedSampleProvider? _speedProvider;
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
        SetRate(rate);
        SetVolume(volume);
    }

    public void SetRate(int rate)
    {
        _rate = Math.Clamp(rate, -10, 10);
        if (!_disposed)
        {
            try { _sapiSynthesizer.Rate = _rate; } catch { }
        }
        if (_speedProvider != null)
        {
            _speedProvider.SpeedFactor = RateToSpeedFactor(_rate);
        }
    }

    public void SetVolume(int volume)
    {
        _volume = Math.Clamp(volume, 0, 100);
        if (!_disposed)
        {
            try { _sapiSynthesizer.Volume = _volume; } catch { }
        }
        if (_waveOut != null)
        {
            try { _waveOut.Volume = _volume / 100f; } catch { }
        }
    }

    private static float RateToSpeedFactor(int rate)
    {
        // -10 -> 0.50x, 0 -> 1.00x, +10 -> 1.60x
        return rate >= 0 ? 1.0f + rate * 0.06f : 1.0f + rate * 0.05f;
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
                string url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={encoded}&tl={langCode}&ttsspeed=1&client=tw-ob";

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
            if (!_disposed)
                StatusChanged?.Invoke("Ready");
        }
    }

    private async Task PlayMp3BytesAsync(byte[] mp3Bytes, CancellationToken token)
    {
        using var ms = new MemoryStream(mp3Bytes);
        using var reader = new Mp3FileReader(ms);
        var sampleProvider = reader.ToSampleProvider();
        var speedProvider = new SpeedSampleProvider(sampleProvider)
        {
            SpeedFactor = RateToSpeedFactor(_rate)
        };
        _speedProvider = speedProvider;

        using var waveOut = new WaveOutEvent();
        _waveOut = waveOut;
        waveOut.Volume = _volume / 100f;
        waveOut.Init(speedProvider);

        var tcs = new TaskCompletionSource<bool>();
        waveOut.PlaybackStopped += (s, e) => tcs.TrySetResult(true);
        waveOut.Play();

        using var reg = token.Register(() =>
        {
            try { waveOut.Stop(); } catch { }
            tcs.TrySetCanceled();
        });

        try
        {
            await tcs.Task;
        }
        finally
        {
            _waveOut = null;
            _speedProvider = null;
        }
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

    /// <summary>
    /// Ses oynatılırken gerçek zamanlı hız değişimi (0.5x - 1.6x) sağlayan doğrusal enterpolasyonlu SampleProvider.
    /// </summary>
    private sealed class SpeedSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private float[] _sourceBuffer = Array.Empty<float>();
        private int _sourceCount;
        private double _readPos;

        public volatile float SpeedFactor = 1.0f;
        public WaveFormat WaveFormat => _source.WaveFormat;

        public SpeedSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            float speed = Math.Clamp(SpeedFactor, 0.4f, 2.0f);
            if (Math.Abs(speed - 1.0f) < 0.01f && _sourceCount == 0)
            {
                return _source.Read(buffer, offset, count);
            }

            int framesRequested = count / _channels;
            int maxSourceFramesNeeded = (int)Math.Ceiling(framesRequested * speed) + 4;
            int minSamplesNeeded = maxSourceFramesNeeded * _channels;

            if (_sourceBuffer.Length < minSamplesNeeded)
            {
                Array.Resize(ref _sourceBuffer, minSamplesNeeded * 2);
            }

            // Kalan örnekleri başa kaydır ve yeni örnekleri oku
            int availableFrames = _sourceCount / _channels;
            int intPos = (int)_readPos;
            if (intPos > 0 && intPos <= availableFrames)
            {
                int remainingSamples = (_sourceCount - intPos * _channels);
                if (remainingSamples > 0)
                    Array.Copy(_sourceBuffer, intPos * _channels, _sourceBuffer, 0, remainingSamples);
                _sourceCount = remainingSamples;
                _readPos -= intPos;
            }
            else if (intPos > availableFrames)
            {
                _sourceCount = 0;
                _readPos = 0;
            }

            int spaceAvailable = _sourceBuffer.Length - _sourceCount;
            if (spaceAvailable > 0)
            {
                int read = _source.Read(_sourceBuffer, _sourceCount, spaceAvailable);
                _sourceCount += read;
            }

            availableFrames = _sourceCount / _channels;
            if (availableFrames <= 1) return 0;

            int outSamples = 0;
            for (int f = 0; f < framesRequested; f++)
            {
                int idx0 = (int)_readPos;
                int idx1 = idx0 + 1;
                if (idx1 >= availableFrames) break;

                float frac = (float)(_readPos - idx0);
                for (int c = 0; c < _channels; c++)
                {
                    float s0 = _sourceBuffer[idx0 * _channels + c];
                    float s1 = _sourceBuffer[idx1 * _channels + c];
                    buffer[offset + outSamples++] = s0 + (s1 - s0) * frac;
                }
                _readPos += speed;
            }

            return outSamples;
        }
    }
}
