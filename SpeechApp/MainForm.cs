using System.Speech.Recognition;
using System.Speech.Synthesis;
using SpeechApp.Models;
using SpeechApp.Services;

namespace SpeechApp;

public partial class MainForm : Form
{
    private readonly TextToSpeechService _ttsService = new();
    private readonly SpeechToTextService _sttService = new();
    private readonly TranscriptExportService _exportService = new();
    private readonly List<SpeechRecord> _records = new();
    private List<RecognizerInfo> _recognizers = new();

    public MainForm()
    {
        InitializeComponent();
        BindEvents();
    }

    private void BindEvents()
    {
        _ttsService.StateChanged += OnTtsStateChanged;
        _ttsService.ErrorOccurred += msg => ShowError("TTS Hatasi", msg);

        _sttService.TextRecognized += OnSttTextRecognized;
        _sttService.HypothesisUpdated += OnSttHypothesis;
        _sttService.ListeningStateChanged += OnSttListeningChanged;
        _sttService.ErrorOccurred += msg => ShowError("Mikrofon / STT Hatasi", msg);
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        // Yüklenmiş TTS seslerini doldur
        var voices = _ttsService.GetInstalledVoices();
        if (voices.Count > 0)
        {
            cmbVoice.Items.AddRange(voices.ToArray());
            cmbVoice.SelectedIndex = 0;
        }
        else
        {
            cmbVoice.Items.Add("Varsayilan Sistem Sesi");
            cmbVoice.SelectedIndex = 0;
        }

        // Yüklenmiş STT dil paketlerini doldur
        _recognizers = SpeechToTextService.GetInstalledRecognizers();
        if (_recognizers.Count > 0)
        {
            foreach (var r in _recognizers)
            {
                cmbRecognizer.Items.Add($"{r.Culture.DisplayName} ({r.Name})");
            }
            cmbRecognizer.SelectedIndex = 0;
        }
        else
        {
            cmbRecognizer.Items.Add("Yuklu tanima paketi bulunamadi");
            cmbRecognizer.SelectedIndex = 0;
        }
    }

    // ─── Text-to-Speech İşlemleri ─────────────────────────────────────────────

    private void btnSpeak_Click(object? sender, EventArgs e)
    {
        string text = rtbText.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("Lutfen seslendirilecek bir metin girin.", "Uyari", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string? selectedVoice = cmbVoice.SelectedItem?.ToString();
        if (selectedVoice == "Varsayilan Sistem Sesi") selectedVoice = null;

        _ttsService.Configure(selectedVoice, trkRate.Value, trkVolume.Value);
        _ttsService.Speak(text);

        AddRecord("TTS", text.Length > 60 ? text[..60] + "..." : text, $"Hiz: {trkRate.Value}");
    }

    private void btnPauseResume_Click(object? sender, EventArgs e)
    {
        _ttsService.PauseOrResume();
    }

    private void btnStopSpeak_Click(object? sender, EventArgs e)
    {
        _ttsService.Stop();
    }

    private void btnClear_Click(object? sender, EventArgs e)
    {
        rtbText.Clear();
    }

    private void OnTtsStateChanged(SynthesizerState state)
    {
        if (InvokeRequired) { Invoke(() => OnTtsStateChanged(state)); return; }

        switch (state)
        {
            case SynthesizerState.Speaking:
                SetActiveOperation("Text-to-Speech Okuyor...", Color.LimeGreen);
                btnSpeak.Enabled = false;
                btnPauseResume.Enabled = true;
                btnPauseResume.Text = "Duraklat";
                btnStopSpeak.Enabled = true;
                break;
            case SynthesizerState.Paused:
                SetActiveOperation("Text-to-Speech Duraklatildi", Color.Gold);
                btnPauseResume.Text = "Devam Et";
                break;
            case SynthesizerState.Ready:
            default:
                if (!_sttService.IsListening)
                    SetActiveOperation("Hazir (Beklemede)", Color.LightSkyBlue);
                btnSpeak.Enabled = true;
                btnPauseResume.Enabled = false;
                btnPauseResume.Text = "Duraklat";
                btnStopSpeak.Enabled = false;
                break;
        }
    }

    // ─── Speech-to-Text İşlemleri ─────────────────────────────────────────────

    private void btnStartListen_Click(object? sender, EventArgs e)
    {
        System.Globalization.CultureInfo? culture = null;
        if (_recognizers.Count > 0 && cmbRecognizer.SelectedIndex >= 0 && cmbRecognizer.SelectedIndex < _recognizers.Count)
        {
            culture = _recognizers[cmbRecognizer.SelectedIndex].Culture;
        }

        _sttService.StartListening(culture);
    }

    private void btnStopListen_Click(object? sender, EventArgs e)
    {
        _sttService.StopListening();
    }

    private void OnSttListeningChanged(bool listening)
    {
        if (InvokeRequired) { Invoke(() => OnSttListeningChanged(listening)); return; }

        btnStartListen.Enabled = !listening;
        btnStopListen.Enabled = listening;

        if (listening)
        {
            SetActiveOperation($"Speech-to-Text Dinliyor ({_sttService.ActiveCultureName})", Color.Cyan);
        }
        else
        {
            lblHypothesis.Text = "Anlik Algilama: -";
            if (_ttsService.CurrentState == SynthesizerState.Ready)
                SetActiveOperation("Hazir (Beklemede)", Color.LightSkyBlue);
        }
    }

    private void OnSttHypothesis(string text)
    {
        if (InvokeRequired) { Invoke(() => OnSttHypothesis(text)); return; }
        lblHypothesis.Text = $"Anlik Algilama: {text}";
    }

    private void OnSttTextRecognized(string text, float confidence)
    {
        if (InvokeRequired) { Invoke(() => OnSttTextRecognized(text, confidence)); return; }

        if (!string.IsNullOrWhiteSpace(rtbText.Text))
            rtbText.AppendText(" ");
        rtbText.AppendText(text);
        rtbText.ScrollToCaret();

        lblHypothesis.Text = "Anlik Algilama: -";
        AddRecord("STT", text, $"Guven: {confidence:P0}");
    }

    // ─── Dışa Aktarım & Yardımcılar ───────────────────────────────────────────

    private void btnExportTxt_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "Metin Dosyasi (*.txt)|*.txt",
            FileName = $"speech_metin_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            var result = _exportService.SaveToFile(dlg.FileName, rtbText.Text, _records);
            MessageBox.Show(result.Message, result.Success ? "Bilgi" : "Uyari", MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
    }

    private void AddRecord(string mode, string text, string detail)
    {
        var rec = new SpeechRecord
        {
            Timestamp = DateTime.Now,
            Mode = mode,
            Text = text,
            Detail = detail
        };
        _records.Add(rec);
        lstRecords.Items.Insert(0, rec.ToDisplayString());
        tsslRecordCount.Text = $"Kayit Sayisi: {_records.Count}";
    }

    private void SetActiveOperation(string statusText, Color color)
    {
        lblActiveMode.Text = $"Durum: {statusText}";
        lblActiveMode.ForeColor = color;
        tsslStatus.Text = $"Islem: {statusText}";
    }

    private void ShowError(string title, string message)
    {
        if (InvokeRequired) { Invoke(() => ShowError(title, message)); return; }
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _ttsService.Dispose();
        _sttService.Dispose();
    }
}
