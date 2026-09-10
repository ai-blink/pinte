using System.Configuration;
using System.Data;
using System.Windows;

using System.IO;
using System.Text.Json;
using Magnifier.Infrastructure;

namespace Magnifier.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--diagnose-input-transform"))
        {
            // MainWindow 생성 전 실행하여 기존 hook 중계와 진단을 겹치지 않는다.
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "Magnifier", "diagnostics");
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, $"input-transform-{Environment.ProcessId}.json");
                using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                var result = WindowsInputTransformDiagnostic.Run();
                JsonSerializer.Serialize(output, result, new JsonSerializerOptions { WriteIndented = true });
                Shutdown(result.Result == "API_ACCEPTED_NOT_UI_VERIFIED" && result.Restored ? 0 : 2);
            }
            catch (Exception)
            {
                Shutdown(3);
            }
            return;
        }
#if INPUT_TRANSFORM_TRIAL
        // 서명 실험본으로 기존 SendInput 엔진을 실수로 시작하지 않는다.
        Shutdown(4);
#else
        StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        base.OnStartup(e);
#endif
    }
}
