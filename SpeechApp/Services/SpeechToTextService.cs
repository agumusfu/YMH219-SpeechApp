using System.Globalization;
using System.Speech.Recognition;

namespace SpeechApp.Services;

/// <summary>
/// System.Speech.Recognition.SpeechRecognitionEngine tabanlı Sesten Metne (Speech-to-Text) servisi.
/// Mikrofon ve yüklü konuşma tanıma paketlerini denetleyerek güvenli şekilde dinleme başlatır/durdurur.
/// </summary>
public class SpeechToTextService : IDisposable
{
    private SpeechRecognitionEngine? _engine;
    private bool _disposed;

    public bool IsListening { get; private set; }
    public string ActiveCultureName { get; private set; } = string.Empty;

    public event Action<string, float>? TextRecognized;
    public event Action<string>? HypothesisUpdated;
    public event Action<bool>? ListeningStateChanged;
    public event Action<string>? ErrorOccurred;

    public static List<RecognizerInfo> GetInstalledRecognizers()
    {
        try
        {
            return SpeechRecognitionEngine.InstalledRecognizers().ToList();
        }
        catch
        {
            return new List<RecognizerInfo>();
        }
    }

    public bool StartListening(CultureInfo? preferredCulture = null)
    {
        if (IsListening) return true;

        try
        {
            var recognizers = GetInstalledRecognizers();
            if (recognizers.Count == 0)
            {
                ErrorOccurred?.Invoke(
                    "Sistemde yüklü Windows Konuşma Tanıma (Speech Recognition) dil paketi bulunamadı.\n" +
                    "Windows Ayarları > Zaman ve Dil > Konuşma bölümünden bir dil paketi yükleyebilirsiniz.");
                return false;
            }

            RecognizerInfo? selected = preferredCulture != null
                ? recognizers.FirstOrDefault(r => r.Culture.Equals(preferredCulture)) ?? recognizers[0]
                : recognizers[0];

            DisposeEngine();

            _engine = new SpeechRecognitionEngine(selected);
            ActiveCultureName = selected.Culture.DisplayName;

            _engine.LoadGrammar(new DictationGrammar());
            _engine.SetInputToDefaultAudioDevice();

            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.SpeechHypothesized += OnSpeechHypothesized;
            _engine.RecognizeCompleted += OnRecognizeCompleted;

            _engine.RecognizeAsync(RecognizeMode.Multiple);
            IsListening = true;
            ListeningStateChanged?.Invoke(true);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            DisposeEngine();
            ErrorOccurred?.Invoke($"Mikrofon aygıtına erişilemedi veya varsayılan ses girişi bulunamadı.\nDetay: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            DisposeEngine();
            ErrorOccurred?.Invoke($"Konuşma tanıma başlatılamadı: {ex.Message}");
            return false;
        }
    }

    public void StopListening()
    {
        if (!IsListening && _engine == null) return;

        try
        {
            _engine?.RecognizeAsyncStop();
        }
        catch { }
        finally
        {
            IsListening = false;
            ListeningStateChanged?.Invoke(false);
        }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (e.Result != null && !string.IsNullOrWhiteSpace(e.Result.Text))
        {
            TextRecognized?.Invoke(e.Result.Text, e.Result.Confidence);
        }
    }

    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
    {
        if (e.Result != null && !string.IsNullOrWhiteSpace(e.Result.Text))
        {
            HypothesisUpdated?.Invoke(e.Result.Text);
        }
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            ErrorOccurred?.Invoke($"Konuşma tanıma sırasında hata: {e.Error.Message}");
        }
        IsListening = false;
        ListeningStateChanged?.Invoke(false);
    }

    private void DisposeEngine()
    {
        if (_engine == null) return;
        try
        {
            _engine.SpeechRecognized -= OnSpeechRecognized;
            _engine.SpeechHypothesized -= OnSpeechHypothesized;
            _engine.RecognizeCompleted -= OnRecognizeCompleted;
            _engine.RecognizeAsyncCancel();
            _engine.Dispose();
        }
        catch { }
        _engine = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsListening = false;
        DisposeEngine();
        GC.SuppressFinalize(this);
    }
}
