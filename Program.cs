namespace HdrBrightness;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool startMinimized = args.Contains("--minimized");
        Application.Run(new MainForm(startMinimized));
    }
}
