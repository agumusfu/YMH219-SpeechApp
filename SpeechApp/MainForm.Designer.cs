namespace SpeechApp;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.pnlHeader = new Panel();
        this.lblTitle = new Label();
        this.lblActiveMode = new Label();

        this.pnlTtsControls = new Panel();
        this.lblVoice = new Label();
        this.cmbVoice = new ComboBox();
        this.lblRate = new Label();
        this.trkRate = new TrackBar();
        this.lblRateVal = new Label();
        this.lblVolume = new Label();
        this.trkVolume = new TrackBar();
        this.lblVolumeVal = new Label();

        this.splitMain = new SplitContainer();
        this.grpText = new GroupBox();
        this.rtbText = new RichTextBox();
        this.pnlTextButtons = new Panel();
        this.btnSpeak = new Button();
        this.btnPauseResume = new Button();
        this.btnStopSpeak = new Button();
        this.btnClear = new Button();

        this.grpStt = new GroupBox();
        this.pnlSttTop = new Panel();
        this.lblRecognizer = new Label();
        this.cmbRecognizer = new ComboBox();
        this.btnStartListen = new Button();
        this.btnStopListen = new Button();
        this.btnExportTxt = new Button();
        this.lblHypothesis = new Label();
        this.lstRecords = new ListBox();

        this.statusStrip = new StatusStrip();
        this.tsslStatus = new ToolStripStatusLabel();
        this.tsslRecordCount = new ToolStripStatusLabel();

        // pnlHeader
        pnlHeader.Dock = DockStyle.Top;
        pnlHeader.Height = 52;
        pnlHeader.BackColor = Color.FromArgb(30, 32, 44);
        pnlHeader.Padding = new Padding(12, 8, 12, 8);

        lblTitle.Text = "YMH 219 - SpeechApp (Text-to-Speech & Speech-to-Text)";
        lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(12, 13);
        lblTitle.AutoSize = true;
        pnlHeader.Controls.Add(lblTitle);

        lblActiveMode.Text = "Durum: Hazır (Beklemede)";
        lblActiveMode.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        lblActiveMode.ForeColor = Color.LightSkyBlue;
        lblActiveMode.BackColor = Color.FromArgb(45, 50, 68);
        lblActiveMode.Padding = new Padding(10, 5, 10, 5);
        lblActiveMode.Location = new Point(620, 10);
        lblActiveMode.AutoSize = true;
        lblActiveMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pnlHeader.Controls.Add(lblActiveMode);

        // pnlTtsControls
        pnlTtsControls.Dock = DockStyle.Top;
        pnlTtsControls.Height = 64;
        pnlTtsControls.BackColor = Color.FromArgb(38, 40, 54);
        pnlTtsControls.Padding = new Padding(10);

        lblVoice.Text = "Ses Seçimi:";
        lblVoice.ForeColor = Color.LightGray;
        lblVoice.Location = new Point(12, 22);
        lblVoice.AutoSize = true;
        pnlTtsControls.Controls.Add(lblVoice);

        cmbVoice.Location = new Point(108, 18);
        cmbVoice.Width = 240;
        cmbVoice.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbVoice.FlatStyle = FlatStyle.Flat;
        cmbVoice.BackColor = Color.FromArgb(55, 58, 74);
        cmbVoice.ForeColor = Color.White;
        pnlTtsControls.Controls.Add(cmbVoice);

        lblRate.Text = "Hız:";
        lblRate.ForeColor = Color.LightGray;
        lblRate.Location = new Point(366, 22);
        lblRate.AutoSize = true;
        pnlTtsControls.Controls.Add(lblRate);

        trkRate.Location = new Point(402, 14);
        trkRate.Width = 135;
        trkRate.Minimum = -10;
        trkRate.Maximum = 10;
        trkRate.Value = 0;
        trkRate.TickFrequency = 2;
        trkRate.ValueChanged += (s, e) => lblRateVal.Text = trkRate.Value.ToString();
        pnlTtsControls.Controls.Add(trkRate);

        lblRateVal.Text = "0";
        lblRateVal.ForeColor = Color.Cyan;
        lblRateVal.Location = new Point(542, 22);
        lblRateVal.AutoSize = true;
        pnlTtsControls.Controls.Add(lblRateVal);

        lblVolume.Text = "Ses Düzeyi:";
        lblVolume.ForeColor = Color.LightGray;
        lblVolume.Location = new Point(580, 22);
        lblVolume.AutoSize = true;
        pnlTtsControls.Controls.Add(lblVolume);

        trkVolume.Location = new Point(672, 14);
        trkVolume.Width = 135;
        trkVolume.Minimum = 0;
        trkVolume.Maximum = 100;
        trkVolume.Value = 100;
        trkVolume.TickFrequency = 10;
        trkVolume.ValueChanged += (s, e) => lblVolumeVal.Text = $"{trkVolume.Value}%";
        pnlTtsControls.Controls.Add(trkVolume);

        lblVolumeVal.Text = "100%";
        lblVolumeVal.ForeColor = Color.Cyan;
        lblVolumeVal.Location = new Point(812, 22);
        lblVolumeVal.AutoSize = true;
        pnlTtsControls.Controls.Add(lblVolumeVal);

        // splitMain
        splitMain.Dock = DockStyle.Fill;
        splitMain.Orientation = Orientation.Vertical;
        splitMain.Size = new Size(1120, 520);
        splitMain.Panel1MinSize = 440;
        splitMain.Panel2MinSize = 510;
        splitMain.SplitterDistance = 520;
        splitMain.Padding = new Padding(8);

        // grpText (Left: TTS + Metin Alani)
        grpText.Text = "Metin Alani & Text-to-Speech (Metinden Sese)";
        grpText.ForeColor = Color.LightGray;
        grpText.Dock = DockStyle.Fill;
        grpText.Padding = new Padding(8);

        rtbText.Dock = DockStyle.Fill;
        rtbText.BackColor = Color.FromArgb(24, 26, 36);
        rtbText.ForeColor = Color.White;
        rtbText.Font = new Font("Segoe UI", 11f);
        rtbText.BorderStyle = BorderStyle.None;
        rtbText.Text = "Merhaba! Bu metni sesli olarak okutabilir veya mikrofonu baslatarak konusmanizi metne donusturebilirsiniz.";

        pnlTextButtons.Dock = DockStyle.Bottom;
        pnlTextButtons.Height = 50;
        pnlTextButtons.Padding = new Padding(0, 8, 0, 0);

        btnSpeak.Text = "Sesli Oku";
        btnSpeak.Location = new Point(0, 8);
        btnSpeak.Size = new Size(118, 36);
        btnSpeak.BackColor = Color.FromArgb(0, 150, 100);
        btnSpeak.ForeColor = Color.White;
        btnSpeak.FlatStyle = FlatStyle.Flat;
        btnSpeak.Click += btnSpeak_Click;
        pnlTextButtons.Controls.Add(btnSpeak);

        btnPauseResume.Text = "Duraklat";
        btnPauseResume.Location = new Point(126, 8);
        btnPauseResume.Size = new Size(108, 36);
        btnPauseResume.BackColor = Color.FromArgb(200, 140, 30);
        btnPauseResume.ForeColor = Color.White;
        btnPauseResume.FlatStyle = FlatStyle.Flat;
        btnPauseResume.Enabled = false;
        btnPauseResume.Click += btnPauseResume_Click;
        pnlTextButtons.Controls.Add(btnPauseResume);

        btnStopSpeak.Text = "Durdur";
        btnStopSpeak.Location = new Point(242, 8);
        btnStopSpeak.Size = new Size(95, 36);
        btnStopSpeak.BackColor = Color.FromArgb(180, 50, 50);
        btnStopSpeak.ForeColor = Color.White;
        btnStopSpeak.FlatStyle = FlatStyle.Flat;
        btnStopSpeak.Enabled = false;
        btnStopSpeak.Click += btnStopSpeak_Click;
        pnlTextButtons.Controls.Add(btnStopSpeak);

        btnClear.Text = "Temizle";
        btnClear.Location = new Point(345, 8);
        btnClear.Size = new Size(95, 36);
        btnClear.BackColor = Color.FromArgb(75, 80, 95);
        btnClear.ForeColor = Color.White;
        btnClear.FlatStyle = FlatStyle.Flat;
        btnClear.Click += btnClear_Click;
        pnlTextButtons.Controls.Add(btnClear);

        grpText.Controls.Add(rtbText);
        grpText.Controls.Add(pnlTextButtons);
        splitMain.Panel1.Controls.Add(grpText);

        // grpStt (Right: STT + Zaman Damgali Kayitlar)
        grpStt.Text = "Speech-to-Text (Sesten Metne) & Zaman Damgali Kayitlar";
        grpStt.ForeColor = Color.LightGray;
        grpStt.Dock = DockStyle.Fill;
        grpStt.Padding = new Padding(8);

        pnlSttTop.Dock = DockStyle.Top;
        pnlSttTop.Height = 112;

        lblRecognizer.Text = "Tanıma Dili:";
        lblRecognizer.ForeColor = Color.LightGray;
        lblRecognizer.Location = new Point(0, 8);
        lblRecognizer.AutoSize = true;
        pnlSttTop.Controls.Add(lblRecognizer);

        cmbRecognizer.Location = new Point(100, 4);
        cmbRecognizer.Width = 340;
        cmbRecognizer.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbRecognizer.FlatStyle = FlatStyle.Flat;
        cmbRecognizer.BackColor = Color.FromArgb(55, 58, 74);
        cmbRecognizer.ForeColor = Color.White;
        pnlSttTop.Controls.Add(cmbRecognizer);

        btnStartListen.Text = "Mikrofonu Dinle";
        btnStartListen.Location = new Point(0, 38);
        btnStartListen.Size = new Size(165, 38);
        btnStartListen.BackColor = Color.FromArgb(50, 110, 200);
        btnStartListen.ForeColor = Color.White;
        btnStartListen.FlatStyle = FlatStyle.Flat;
        btnStartListen.Click += btnStartListen_Click;
        pnlSttTop.Controls.Add(btnStartListen);

        btnStopListen.Text = "Dinlemeyi Durdur";
        btnStopListen.Location = new Point(173, 38);
        btnStopListen.Size = new Size(175, 38);
        btnStopListen.BackColor = Color.FromArgb(180, 50, 50);
        btnStopListen.ForeColor = Color.White;
        btnStopListen.FlatStyle = FlatStyle.Flat;
        btnStopListen.Enabled = false;
        btnStopListen.Click += btnStopListen_Click;
        pnlSttTop.Controls.Add(btnStopListen);

        btnExportTxt.Text = ".txt Kaydet";
        btnExportTxt.Location = new Point(356, 38);
        btnExportTxt.Size = new Size(115, 38);
        btnExportTxt.BackColor = Color.FromArgb(0, 140, 130);
        btnExportTxt.ForeColor = Color.White;
        btnExportTxt.FlatStyle = FlatStyle.Flat;
        btnExportTxt.Click += btnExportTxt_Click;
        pnlSttTop.Controls.Add(btnExportTxt);

        lblHypothesis.Text = "Anlik Algilama: -";
        lblHypothesis.ForeColor = Color.Gold;
        lblHypothesis.Location = new Point(0, 82);
        lblHypothesis.AutoSize = true;
        pnlSttTop.Controls.Add(lblHypothesis);

        lstRecords.Dock = DockStyle.Fill;
        lstRecords.BackColor = Color.FromArgb(24, 26, 36);
        lstRecords.ForeColor = Color.LightCyan;
        lstRecords.BorderStyle = BorderStyle.None;
        lstRecords.Font = new Font("Consolas", 9.5f);

        grpStt.Controls.Add(lstRecords);
        grpStt.Controls.Add(pnlSttTop);
        splitMain.Panel2.Controls.Add(grpStt);

        // statusStrip
        statusStrip.BackColor = Color.FromArgb(30, 32, 44);
        tsslStatus.Text = "Islem: Beklemede";
        tsslStatus.ForeColor = Color.LightGray;
        tsslRecordCount.Text = "Kayit Sayisi: 0";
        tsslRecordCount.ForeColor = Color.LightGray;
        statusStrip.Items.Add(tsslStatus);
        statusStrip.Items.Add(new ToolStripSeparator());
        statusStrip.Items.Add(tsslRecordCount);

        // MainForm
        this.Text = "YMH 219 - SpeechApp (TTS & STT)";
        this.Size = new Size(1140, 680);
        this.MinimumSize = new Size(1020, 560);
        this.BackColor = Color.FromArgb(28, 30, 42);
        this.Controls.Add(splitMain);
        this.Controls.Add(pnlTtsControls);
        this.Controls.Add(pnlHeader);
        this.Controls.Add(statusStrip);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.Load += MainForm_Load;
        this.FormClosing += MainForm_FormClosing;
    }

    private Panel pnlHeader, pnlTtsControls, pnlTextButtons, pnlSttTop;
    private Label lblTitle, lblActiveMode, lblVoice, lblRate, lblRateVal, lblVolume, lblVolumeVal, lblRecognizer, lblHypothesis;
    private ComboBox cmbVoice, cmbRecognizer;
    private TrackBar trkRate, trkVolume;
    private SplitContainer splitMain;
    private GroupBox grpText, grpStt;
    private RichTextBox rtbText;
    private Button btnSpeak, btnPauseResume, btnStopSpeak, btnClear, btnStartListen, btnStopListen, btnExportTxt;
    private ListBox lstRecords;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel tsslStatus, tsslRecordCount;

    #endregion
}
