namespace ReType.App;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        using var singleInstance = new Mutex(true, "Local\\ReType.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            return 0;
        }

        ApplicationConfiguration.Initialize();
        using var context = new TrayContext();
        Application.Run(context);
        return 0;
    }
}
