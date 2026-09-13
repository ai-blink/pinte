using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace Magnifier.App.Tests;

// 앱 시작 코드의 실제 엔진 생성을 차단한다. 일반 상태 테스트는 HWND도 생성하지 않는다.
// 표시창 실패 테스트만 Show 이전 실패 경로의 숨은 HWND를 사용한다.
internal static class StaTest
{
    private static readonly Lazy<Task<Dispatcher>> SharedDispatcher = new(CreateDispatcher);

    public static async Task Run(Func<Task> test)
    {
        var dispatcher = await SharedDispatcher.Value;
        await dispatcher.InvokeAsync(test).Task.Unwrap();
    }

    private static Task<Dispatcher> CreateDispatcher()
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                // 파생 테스트 타입으로 XAML을 로드하면 pack URI의 component가 테스트
                // 어셈블리로 해석된다. 제품 App 타입으로 리소스만 초기화하고 Run은 하지 않는다.
                var application = new global::Magnifier.App.App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.InitializeComponent();
                ready.SetResult(Dispatcher.CurrentDispatcher);
                Dispatcher.Run();
            }
            catch (Exception exception) { ready.TrySetException(exception); }
        }) { IsBackground = true, Name = "Magnifier App 테스트 STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task;
    }
}

internal static class PrivateAccess
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static T Get<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, Flags)?.GetValue(target)
            ?? throw new MissingFieldException(target.GetType().FullName, name));

    public static void Set(object target, string name, object? value) =>
        (target.GetType().GetField(name, Flags)
            ?? throw new MissingFieldException(target.GetType().FullName, name)).SetValue(target, value);

    public static Task CallAsync(object target, string name, params object[] arguments) =>
        (Task)(target.GetType().GetMethod(name, Flags)?.Invoke(target, arguments)
            ?? throw new MissingMethodException(target.GetType().FullName, name));
}
