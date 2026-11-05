using System.IO;
using System.Windows;
using Plc.Engine;

namespace Learning.Shell;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // PLCPlus.exe --export-ladders <folder>: every program in the content package drawn as a PNG, with the same
        // drawing as the window. The instructors' review pages on softwareplus.ai show them (tools/make-review-site.py).
        if (e.Args.Length == 2 && e.Args[0] == "--export-ladders")
        {
            var code = 0;
            try
            {
                Directory.CreateDirectory(e.Args[1]);
                foreach (var file in Directory.GetFiles(Path.Combine(PlcContent.DefaultFolder, "programs"), "*.json"))
                {
                    var project = ProjectJson.Deserialize(File.ReadAllText(file));
                    LadderPrinter.SavePng(project, project.Name, Path.Combine(e.Args[1], Path.GetFileNameWithoutExtension(file) + ".png"));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                Console.Error.WriteLine(ex.Message);
                code = 1;
            }
            Shutdown(code);
            return;
        }

        new MainWindow().Show();
    }
}
