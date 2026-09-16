using System.Runtime.InteropServices;

namespace Magnifier.Infrastructure;

public sealed partial class WindowsLivePointerRelay
{
    private const int WhKeyboardLl = 13;
    private const uint WmKeyDown = 0x0100;
    private const uint WmSysKeyDown = 0x0104;
    private const uint VkEscape = 0x1B;
    private const uint LlkhfInjected = 0x10;

    // Escape remains an explicit physical cancel, but virtual keyboards and other SendInput
    // producers must not be able to cancel a mouse-only relay through global key state.
    private nint KeyboardHook(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var key = Marshal.PtrToStructure<KeyboardHookData>(data);
            if (_state.IsRequested && IsPhysicalEscapeKeyDown((uint)message, key.VirtualKey, key.Flags))
                _physicalEscapeRequested = true;
        }
        return CallNextHookEx(0, code, message, data);
    }

    private void InstallKeyboardHook()
    {
        if (_keyboardHook != 0) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = 0; }
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, GetModuleHandle(null), 0);
    }

    internal static bool IsPhysicalEscapeKeyDown(uint message, uint virtualKey, uint flags) =>
        message is WmKeyDown or WmSysKeyDown && virtualKey == VkEscape && (flags & LlkhfInjected) == 0;
}
