using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FreeFlow_App;

/// <summary>
/// Native system-tray (notification area) icon with a right-click menu, giving
/// FreeFlow the always-available background presence of the macOS menu-bar app.
/// Built on <c>Shell_NotifyIcon</c> and a window-subclass that intercepts the
/// tray callback; the menu is shown synchronously via <c>TrackPopupMenu</c> with
/// <c>TPM_RETURNCMD</c> so no <c>WM_COMMAND</c> routing is needed.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int WM_APP = 0x8000;
    private const uint CallbackMessage = WM_APP + 1;
    private const int GWLP_WNDPROC = -4;

    private const uint NIM_ADD = 0x0;
    private const uint NIM_MODIFY = 0x1;
    private const uint NIM_DELETE = 0x2;
    private const uint NIF_MESSAGE = 0x1;
    private const uint NIF_ICON = 0x2;
    private const uint NIF_TIP = 0x4;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;

    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;

    private const uint MF_STRING = 0x0;
    private const uint MF_SEPARATOR = 0x800;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x10;
    private const uint LR_DEFAULTSIZE = 0x40;

    private const uint CmdOpen = 1;
    private const uint CmdSettings = 2;
    private const uint CmdQuit = 3;

    private readonly nint _hwnd;
    private readonly WndProc _newProc;
    private readonly nint _oldProc;
    private nint _hIcon;
    private bool _added;

    public event Action? OpenRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;

    public TrayIcon(nint hwnd, string tooltip = "FreeFlow")
    {
        _hwnd = hwnd;
        _hIcon = LoadAppIcon();

        // Subclass the host window so we receive the tray callback messages.
        _newProc = WindowProc;
        _oldProc = SetWindowLongPtrCompat(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newProc));

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = CallbackMessage,
            hIcon = _hIcon,
            szTip = tooltip,
        };

        _added = Shell_NotifyIcon(NIM_ADD, ref data);
        if (_added)
        {
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }
    }

    private nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == CallbackMessage)
        {
            var evt = (int)(lParam & 0xFFFF);
            switch (evt)
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    OpenRequested?.Invoke();
                    break;

                case WM_RBUTTONUP:
                case WM_CONTEXTMENU:
                    ShowMenu();
                    break;
            }

            return 0;
        }

        return CallWindowProc(_oldProc, hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == nint.Zero)
        {
            return;
        }

        try
        {
            AppendMenu(menu, MF_STRING, CmdOpen, "Open FreeFlow");
            AppendMenu(menu, MF_STRING, CmdSettings, "Settings");
            AppendMenu(menu, MF_SEPARATOR, 0, null);
            AppendMenu(menu, MF_STRING, CmdQuit, "Quit FreeFlow");

            GetCursorPos(out var pt);

            // Required so the menu dismisses correctly when focus moves away.
            SetForegroundWindow(_hwnd);

            var cmd = TrackPopupMenu(
                menu,
                TPM_RIGHTBUTTON | TPM_RETURNCMD,
                pt.X,
                pt.Y,
                0,
                _hwnd,
                nint.Zero);

            switch ((uint)cmd)
            {
                case CmdOpen:
                    OpenRequested?.Invoke();
                    break;
                case CmdSettings:
                    SettingsRequested?.Invoke();
                    break;
                case CmdQuit:
                    QuitRequested?.Invoke();
                    break;
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private static nint LoadAppIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(path))
            {
                return LoadImage(nint.Zero, path, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
            }
        }
        catch
        {
            // Fall through to no icon; the tray entry still works.
        }

        return nint.Zero;
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }

        if (_oldProc != nint.Zero)
        {
            SetWindowLongPtrCompat(_hwnd, GWLP_WNDPROC, _oldProc);
        }

        if (_hIcon != nint.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = nint.Zero;
        }
    }

    private delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    // 64-bit Windows exports SetWindowLongPtrW; 32-bit only has SetWindowLongW.
    private static nint SetWindowLongPtrCompat(nint hWnd, int nIndex, nint dwNewLong)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : SetWindowLong32(hWnd, nIndex, (int)dwNewLong);

    [DllImport("user32.dll")]
    private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(nint hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(nint hMenu, uint uFlags, int x, int y, int nReserved, nint hWnd, nint prcRect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadImage(nint hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);
}
