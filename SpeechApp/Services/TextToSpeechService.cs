using System.Net;
using System.Speech.Synthesis;
using NAudio.Wave;
using SoundTouch;

namespace SpeechApp.Services;

/// <summary>
/// Çift motorlu Text-to-Speech servisi:
/// 1) Yüksek kaliteli Türkçe/İngilizce Online TTS
/// 2) Çevrimdışı Windows SAPI5 (System.Speech.Synthesis.SpeechSynthesizer -> WaveStream)
/// Her iki motor da SoundTouch.Net + NAudio WaveOutEvent üzerinden çalınır;
/// böylece ses perdesi (pitch) bozulmadan gerçek zamanlı hız ve ses düzeyi kontrolü sağlanır.
/// </summary>
public class TextToSpeechService : IDisposable
{
    private readonly SpeechSynthesizer _sapiSynthesizer;
    private readonly HttpClient _httpClient;
    private WaveOutEvent? _waveOut;
    private TempoSampleProvider? _tempoProvider;
    private CancellationTokenSource? _cts;
    private bool _isSpeaking;
    private bool _isPaused;
    private int _rate;
    private int _volume = 100;
    private bool _disposed;

    public bool IsSpeaking => !_disposed && _isSpeaking;
    public bool IsPaused => !_disposed && _isPaused;

    public event Action<string>? StatusChanged; // "Ready", "Speaking", "Paused"
    public event Action<string>? ErrorOccurred;

    public TextToSpeechService()
    {
        _sapiSynthesizer = new SpeechSynthesizer();
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
        if (_tempoProvider != null)
        {
            _tempoProvider.Tempo = RateToTempo(_rate);
        }
    }

    public void SetVolume(int volume)
    {
        _volume = Math.Clamp(volume, 0, 100);
        if (_waveOut != null)
        {
            try { _waveOut.Volume = _volume / 100f; } catch { }
        }
    }

    private static double RateToTempo(int rate)
    {
        // -10 -> 0.50x, 0 -> 1.00x, +10 -> 1.80x (pitch korunur)
        return rate >= 0 ? 1.0 + rate * 0.08 : 1.0 + rate * 0.05;
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
            await SpeakSapiAsync(text, sapiName);
        }
    }

    private async Task SpeakOnlineAsync(string text, string langCode)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _isSpeaking = true;
        _isPaused = false;
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

                using var ms = new MemoryStream(mp3Bytes);
                using var reader = new Mp3FileReader(ms);
                await PlaySampleProviderAsync(reader.ToSampleProvider(), token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Seslendirme servisine bağlanılamadı (İnternet bağlantınızı kontrol edin veya '[Yerel SAPI]' sesi seçin).\nDetay: {ex.Message}");
        }
        finally
        {
            _isSpeaking = false;
            _isPaused = false;
            if (!_disposed)
                StatusChanged?.Invoke("Ready");
        }
    }

    private async Task SpeakSapiAsync(string text, string voiceName)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _isSpeaking = true;
        _isPaused = false;
        StatusChanged?.Invoke("Speaking");

        try
        {
            byte[] wavBytes = await Task.Run(() =>
            {
                lock (_sapiSynthesizer)
                {
                    if (_disposed) return Array.Empty<byte>();
                    using var wavStream = new MemoryStream();
                    if (!string.IsNullOrWhiteSpace(voiceName) && voiceName != "Varsayılan Sistem Sesi")
                        _sapiSynthesizer.SelectVoice(voiceName);

                    _sapiSynthesizer.Rate = 0; // Hız kontrolü SoundTouch tarafından anlık yapılır
                    _sapiSynthesizer.Volume = 100;
                    _sapiSynthesizer.SetOutputToWaveStream(wavStream);
                    _sapiSynthesizer.Speak(text);
                    _sapiSynthesizer.SetOutputToNull();
                    return wavStream.ToArray();
                }
            }, token);

            if (token.IsCancellationRequested || wavBytes.Length == 0) return;

            using var ms = new MemoryStream(wavBytes);
            using var reader = new WaveFileReader(ms);
            await PlaySampleProviderAsync(reader.ToSampleProvider(), token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Yerel SAPI seslendirme hatası: {ex.Message}");
        }
        finally
        {
            _isSpeaking = false;
            _isPaused = false;
            if (!_disposed)
                StatusChanged?.Invoke("Ready");
        }
    }

    private async Task PlaySampleProviderAsync(ISampleProvider source, CancellationToken token)
    {
        var tempoProvider = new TempoSampleProvider(source)
        {
            Tempo = RateToTempo(_rate)
        };
        _tempoProvider = tempoProvider;

        using var waveOut = new WaveOutEvent();
        _waveOut = waveOut;
        waveOut.Volume = _volume / 100f;
        waveOut.Init(tempoProvider);

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
            _tempoProvider = null;
        }
    }

    public void PauseOrResume()
    {
        if (!_isSpeaking || _waveOut == null) return;

        if (_isPaused)
        {
            _waveOut.Play();
            _isPaused = false;
            StatusChanged?.Invoke("Speaking");
        }
        else
        {
            _waveOut.Pause();
            _isPaused = true;
            StatusChanged?.Invoke("Paused");
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _waveOut?.Stop();
        }
        catch { }

        _isSpeaking = false;
        _isPaused = false;

        if (!_disposed)
        {
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
        try { _cts?.Cancel(); } catch { }
        try { _waveOut?.Stop(); } catch { }
        try { _httpClient.Dispose(); } catch { }
        try { _sapiSynthesizer.Dispose(); } catch { }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// SoundTouch.Net (WSOLA) kullanarak ses perdesini (pitch) bozmadan gerçek zamanlı oynatma hızını değiştirir.
    /// </summary>
    private sealed class TempoSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly SoundTouchProcessor _processor;
        private readonly float[] _sourceBuffer;
        private readonly int _channels;
        private bool _sourceEnded;
        private double _tempo = 1.0;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public double Tempo
        {
            get => _tempo;
            set
            {
                _tempo = Math.Clamp(value, 0.4, 2.2);
                _processor.Tempo = _tempo;
            }
        }

        public TempoSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            _processor = new SoundTouchProcessor
            {
                SampleRate = source.WaveFormat.SampleRate,
                Channels = _channels,
                Tempo = 1.0
            };
            _sourceBuffer = new float[source.WaveFormat.SampleRate * _channels / 5]; // 200ms buffer
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int framesRequested = count / _channels;
            float[] outTemp = offset == 0 ? buffer : new float[count];

            while (_processor.AvailableSamples < framesRequested && !_sourceEnded)
            {
                int samplesRead = _source.Read(_sourceBuffer, 0, _sourceBuffer.Length);
                if (samplesRead == 0)
                {
                    _sourceEnded = true;
                    _processor.Flush();
                    break;
                }
                _processor.PutSamples(_sourceBuffer, samplesRead / _channels);
            }

            int framesReceived = _processor.ReceiveSamples(outTemp, framesRequested);
            int samplesReceived = framesReceived * _channels;

            if (offset != 0 && samplesReceived > 0)
            {
                Array.Copy(outTemp, 0, buffer, offset, samplesReceived);
            }

            return samplesReceived;
        }
    }
}
