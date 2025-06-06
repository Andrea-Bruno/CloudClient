using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace CloudClient
{
    static internal class WindowsSupport
    {
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int WM_USER = 0x0400;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_COMMAND = 0x0111;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, [In] ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, string lpIconName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WNDCLASS
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hWnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        private const int WM_DESTROY = 0x0002;
        private const int WM_CLOSE = 0x0010;

        static private NOTIFYICONDATA _notifyIconData;
        static private IntPtr _windowHandle;
        static private WNDCLASS _wndClass;
        static private Action _openUI;
        static private string _cloudPath;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        static private WndProcDelegate _wndProcDelegate;

        public static void SetWindowsStatusIcon(string cloudPath, Action openUI)
        {
            _cloudPath = cloudPath;
            _openUI = openUI;

            _wndProcDelegate = new WndProcDelegate(WndProc);

            _wndClass = new WNDCLASS
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                lpszClassName = "HiddenWindowClass",
                hInstance = Marshal.GetHINSTANCE(typeof(WindowsSupport).Module)
            };

            if (RegisterClass(ref _wndClass) == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Exception($"Failed to register window class. Error: {error}");
            }

            _windowHandle = CreateWindowEx(0, "HiddenWindowClass", "Cloud client windows support", 0x00000000U, 100, 100, 300, 200, IntPtr.Zero, IntPtr.Zero, _wndClass.hInstance, IntPtr.Zero);

            if (_windowHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Exception($"Failed to create window. Error: {error}");
            }

            _notifyIconData = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _windowHandle,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_USER + 1,
                hIcon = LoadIconFromResources(Client.IconStatus.Default),
                szTip = "Cloud storage"
            };

            if (!Shell_NotifyIcon(NIM_ADD, ref _notifyIconData))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Exception($"Failed to add notify icon. Error: {error}");
            }

            Debug.WriteLine("Notify icon added");

            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        public static void Quit() => PostQuitMessage(0);

        public static void Dispose()
        {
            Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);
            DestroyWindow(_windowHandle);
            UnregisterClass(_wndClass.lpszClassName, _wndClass.hInstance);
        }

        static private IntPtr LoadIconFromResources(Client.IconStatus iconStatus)
        {
            lock (_lock)
            {
                var resourceName = "CloudClient.Resources.CloudIcon" + iconStatus.ToString() + ".png";

                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    throw new Exception($"Resource '{resourceName}' not found.");

                using var bitmap = new Bitmap(stream);
                try
                {
                    Thread.Sleep(100);
                    return bitmap.GetHicon();
                }
                catch (Exception ex)
                {
#if DEBUG
                    throw ex;
#endif
                }

            }
        }

        static private object _lock = new object();

        static private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            Debug.WriteLine($"WndProc called with msg: {msg}");

            if (msg == WM_USER + 1)
            {
                if (lParam.ToInt32() == WM_LBUTTONDOWN)
                {
                    _openUI?.Invoke();
                }
                else if (lParam.ToInt32() == WM_RBUTTONDOWN)
                {
                    OpenFolder(_cloudPath);
                }
            }
            else if (msg == WM_DESTROY)
            {
                PostQuitMessage(0);
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        static private void OpenFolder(string path)
        {
            try
            {
                var dir = new DirectoryInfo(path);
                var files = dir.GetFileSystemInfos();
                if (files.Length > 0)
                {
                    Process.Start("explorer.exe", path);
                    return;
                }
            }
            catch (Exception)
            {
            }
            ShowPopup("Cloud", "Cloud area has been blocked!");
        }

        static public void ShowPopup(string title, string message)
        {
            string powershellCommand = $"powershell -command \"[System.Windows.MessageBox]::Show('{message}', '{title}')\"";

            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {powershellCommand}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            process.WaitForExit();
        }

        static public void UpdateStatusIcon(Client.IconStatus newStatus)
        {
            _notifyIconData.hIcon = LoadIconFromResources(newStatus);
        }
    }
}