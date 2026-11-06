using System.Speech.Synthesis;

namespace SpeechApp.Services;

/// <summary>
/// System.Speech.Synthesis.SpeechSynthesizer tabanlı Metinden Sese (Text-to-Speech) servisi.
/// Ses seçimi, okuma hızı, ses düzeyi, duraklatma, devam ettirme ve durdurma işlemlerini yönetir.
/// </summary>
public class TextToSpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synthesizer;
    private bool _disposed;

    public SynthesizerState CurrentState => _synthesizer.State;

    public event Action<SynthesizerState>? StateChanged;
    public event Action<string>? ErrorOccurred;

    public TextToSpeechService()
    {
        _synthesizer = new SpeechSynthesizer();
        _synthesizer.SetOutputToDefaultAudioDevice();

        _synthesizer.StateChanged += (s, e) => StateChanged?.Invoke(e.State);
        _synthesizer.SpeakCompleted += (s, e) =>
        {
            if (e.Error != null)
            {
                ErrorOccurred?.Invoke($"Seslendirme sırasında hata oluştu: {e.Error.Message}");
            }
            StateChanged?.Invoke(_synthesizer.State);
        };
    }

    public List<string> GetInstalledVoices()
    {
        var voices = new List<string>();
        try
        {
            foreach (InstalledVoice voice in _synthesizer.GetInstalledVoices())
            {
                if (voice.Enabled)
                {
                    voices.Add(voice.VoiceInfo.Name);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Yüklü sesler alınamadı: {ex.Message}");
        }
        return voices;
    }

    public void Configure(string? voiceName, int rate, int volume)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(voiceName))
            {
                _synthesizer.SelectVoice(voiceName);
            }
            _synthesizer.Rate = Math.Clamp(rate, -10, 10);
            _synthesizer.Volume = Math.Clamp(volume, 0, 100);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Ses ayarları uygulanamadı: {ex.Message}");
        }
    }

    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ErrorOccurred?.Invoke("Seslendirilecek metin boş olamaz.");
            return;
        }

        try
        {
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Seslendirme başlatılamadı: {ex.Message}");
        }
    }

    public void PauseOrResume()
    {
        try
        {
            if (_synthesizer.State == SynthesizerState.Speaking)
            {
                _synthesizer.Pause();
            }
            else if (_synthesizer.State == SynthesizerState.Paused)
            {
                _synthesizer.Resume();
            }
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
            if (_synthesizer.State == SynthesizerState.Paused)
            {
                _synthesizer.Resume();
            }
            _synthesizer.SpeakAsyncCancelAll();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Seslendirme durdurulamadı: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _synthesizer.SpeakAsyncCancelAll(); } catch { }
        _synthesizer.Dispose();
        GC.SuppressFinalize(this);
    }
}
