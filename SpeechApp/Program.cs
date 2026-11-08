namespace SpeechApp;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kritik Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
