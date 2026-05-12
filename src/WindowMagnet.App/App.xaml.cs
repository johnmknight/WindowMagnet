using System;
using System.IO;
using System.Windows;

namespace WindowMagnet.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // %APPDATA%\WindowMagnet — home for profiles.json and logs.
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowMagnet");
        Directory.CreateDirectory(appData);

        base.OnStartup(e);
    }
}
