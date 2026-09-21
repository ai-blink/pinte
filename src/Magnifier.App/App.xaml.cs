using System.Configuration;
using System.Data;
using System.Windows;

using System.IO;
using System.Text.Json;
using System.Threading;
using Magnifier.Infrastructure;

namespace Magnifier.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // One product instance owns the global mouse relay hook. A second instance would install a
    // competing WH_MOUSE_LL hook, and both churning the low-level chain is what let the hook time
    // out and reinstall in a storm. Per-session name so different users never block each other.
    private const string SingleInstanceName = @"Local\Pinte.Magnifier.SingleInstance";
    private const string SingleInstanceActivationEventName = @"Local\Pinte.Magnifier.ActivateExisting";
    private Mutex? _singleInstance;
    private EventWaitHandle? _activationSignal;
    private RegisteredWaitHandle? _activationRegistration;

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
        _activationSignal = new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstanceActivationEventName);
        _singleInstance = new Mutex(true, SingleInstanceName, out var createdNew);
        if (!createdNew)
        {
            _activationSignal.Set();
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown(5);
            return;
        }
        StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        base.OnStartup(e);
        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationSignal, static (state, _) => ((App)state!).RequestExistingWindowRestore(), this,
            Timeout.Infinite, executeOnlyOnce: false);
#endif
    }

    private void RequestExistingWindowRestore()
    {
        try
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (MainWindow is Magnifier.App.MainWindow main)
                    _ = main.RestoreForActivationAsync();
            });
        }
        catch (InvalidOperationException)
        {
            // Shutdown can race an activation request; the next launch owns the new instance.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationRegistration?.Unregister(null);
        _activationRegistration = null;
        _activationSignal?.Dispose();
        _activationSignal = null;
        if (_singleInstance is not null)
        {
            try { _singleInstance.ReleaseMutex(); } catch { }
            _singleInstance.Dispose();
            _singleInstance = null;
        }
        base.OnExit(e);
    }
}
