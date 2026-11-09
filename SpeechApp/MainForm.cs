using SpeechApp.Models;
using SpeechApp.Services;

namespace SpeechApp;

public partial class MainForm : Form
{
    private readonly TextToSpeechService _ttsService = new();
    private readonly SpeechToTextService _sttService = new();
    private readonly TranscriptExportService _exportService = new();
    private readonly List<SpeechRecord> _records = new();

    public MainForm()
    {
        InitializeComponent();
        try
        {
            if (!string.IsNullOrEmpty(Environment.ProcessPath))
                this.Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
        }
        catch { }
        BindEvents();
    }

    private void BindEvents()
    {
        _ttsService.StatusChanged += OnTtsStatusChanged;
        _ttsService.ErrorOccurred += msg => ShowError("TTS Hatası", msg);

        _sttService.TextRecognized += OnSttTextRecognized;
        _sttService.HypothesisUpdated += OnSttHypothesis;
        _sttService.ListeningStateChanged += OnSttListeningChanged;
        _sttService.StatusInfoChanged += info =>
        {
            if (InvokeRequired) { Invoke(() => SetActiveOperation(info, Color.Gold)); return; }
            SetActiveOperation(info, Color.Gold);
        };
        _sttService.ErrorOccurred += msg => ShowError("Mikrofon / STT Hatası", msg);
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        var voices = _ttsService.GetAvailableVoices();
        cmbVoice.Items.AddRange(voices.ToArray());
        if (cmbVoice.Items.Count > 0) cmbVoice.SelectedIndex = 0;

        var engines = SpeechToTextService.GetAvailableEngines();
        cmbRecognizer.Items.AddRange(engines.ToArray());
        if (cmbRecognizer.Items.Count > 0) cmbRecognizer.SelectedIndex = 0;

        cmbRecognizer.SelectedIndexChanged += async (s, ev) =>
        {
            if (_sttService.IsListening)
            {
                _sttService.StopListening();
                string selectedEngine = cmbRecognizer.SelectedItem?.ToString() ?? "Türkçe + İngilizce (Otomatik - Vosk)";
                await _sttService.StartListeningAsync(selectedEngine);
            }
        };
    }

    // ─── Text-to-Speech İşlemleri ─────────────────────────────────────────────

    private async void btnSpeak_Click(object? sender, EventArgs e)
    {
        string text = rtbText.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("Lütfen seslendirilecek bir metin girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string selectedVoice = cmbVoice.SelectedItem?.ToString() ?? "Türkçe (Yüksek Kalite - Doğal Ses)";
        _ttsService.Configure(trkRate.Value, trkVolume.Value);
        AddRecord("TTS", text.Length > 60 ? text[..60] + "..." : text, selectedVoice.Split(' ')[0]);

        await _ttsService.SpeakAsync(text, selectedVoice);
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
        _records.Clear();
        lstRecords.Items.Clear();
        lblHypothesis.Text = "Anlık Algılama: -";
        tsslRecordCount.Text = "Kayıt Sayısı: 0";
    }

    private void OnTtsStatusChanged(string state)
    {
        if (InvokeRequired) { Invoke(() => OnTtsStatusChanged(state)); return; }

        switch (state)
        {
            case "Speaking":
                SetActiveOperation("Text-to-Speech Okuyor...", Color.LimeGreen);
                btnSpeak.Enabled = false;
                btnPauseResume.Enabled = true;
                btnPauseResume.Text = "Duraklat";
                btnStopSpeak.Enabled = true;
                break;
            case "Paused":
                SetActiveOperation("Text-to-Speech Duraklatıldı", Color.Gold);
                btnPauseResume.Text = "Devam Et";
                break;
            case "Ready":
            default:
                if (!_sttService.IsListening)
                    SetActiveOperation("Hazır (Beklemede)", Color.LightSkyBlue);
                btnSpeak.Enabled = true;
                btnPauseResume.Enabled = false;
                btnPauseResume.Text = "Duraklat";
                btnStopSpeak.Enabled = false;
                break;
        }
    }

    // ─── Speech-to-Text İşlemleri ─────────────────────────────────────────────

    private async void btnStartListen_Click(object? sender, EventArgs e)
    {
        btnStartListen.Enabled = false;
        string selectedEngine = cmbRecognizer.SelectedItem?.ToString() ?? "Türkçe (tr-TR - Vosk Akıllı Model)";

        bool ok = await _sttService.StartListeningAsync(selectedEngine);
        if (!ok)
        {
            btnStartListen.Enabled = true;
            if (!_ttsService.IsSpeaking)
                SetActiveOperation("Hazır (Beklemede)", Color.LightSkyBlue);
        }
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
            SetActiveOperation($"Speech-to-Text Dinliyor ({_sttService.ActiveModeLabel})", Color.Cyan);
        }
        else
        {
            lblHypothesis.Text = "Anlık Algılama: -";
            if (!_ttsService.IsSpeaking)
                SetActiveOperation("Hazır (Beklemede)", Color.LightSkyBlue);
        }
    }

    private void OnSttHypothesis(string text)
    {
        if (InvokeRequired) { Invoke(() => OnSttHypothesis(text)); return; }
        lblHypothesis.Text = $"Anlık Algılama: {text}";
    }

    private void OnSttTextRecognized(string text, float confidence)
    {
        if (InvokeRequired) { Invoke(() => OnSttTextRecognized(text, confidence)); return; }

        if (!string.IsNullOrWhiteSpace(rtbText.Text))
            rtbText.AppendText(" ");
        rtbText.AppendText(text);
        rtbText.ScrollToCaret();

        lblHypothesis.Text = "Anlık Algılama: -";
        AddRecord("STT", text, _sttService.ActiveModeLabel);
    }

    // ─── Dışa Aktarım & Yardımcılar ───────────────────────────────────────────

    private void btnExportTxt_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "Metin Dosyası (*.txt)|*.txt",
            FileName = $"speech_metin_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            var result = _exportService.SaveToFile(dlg.FileName, rtbText.Text, _records);
            MessageBox.Show(result.Message, result.Success ? "Bilgi" : "Uyarı", MessageBoxButtons.OK,
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
        tsslRecordCount.Text = $"Kayıt Sayısı: {_records.Count}";
    }

    private void SetActiveOperation(string statusText, Color color)
    {
        lblActiveMode.Text = $"Durum: {statusText}";
        lblActiveMode.ForeColor = color;
        tsslStatus.Text = $"İşlem: {statusText}";
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
