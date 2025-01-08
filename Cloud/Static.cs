using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
namespace Cloud
{
    static public partial class Static
    {
        static Static()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                OpenUI = () => SystemExtra.Util.ExecuteCommand("cmd.exe", "/C " + "start /max " + UIAddress);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                OpenUI = () => SystemExtra.Util.ExecuteCommand("open", UIAddress);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string? desktopEnvironment = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
                if (!string.IsNullOrEmpty(desktopEnvironment))
                {
                    OpenUI = () => SystemExtra.Util.ExecuteCommand("xdg-open", UIAddress);
                }
            }
        }
        /// <summary>
        /// Calling this function starts the application's graphical interface (basically the browser with the app's settings page)
        /// </summary>
        public readonly static Action? OpenUI;

        /// <summary>
        /// Router entry point
        /// </summary>
        public static string? EntryPoint;
        /// <summary>
        /// Local cloud directory position (Path)
        /// </summary>
        public static string? CloudPath;
        /// <summary>
        /// Address of User Interface
        /// </summary>
        public static string? UIAddress;
        /// <summary>
        /// Port of Iser Interface
        /// </summary>
        public static int Port;
        public static SecureStorage.Storage Storage;
        public static readonly bool IsAdmin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) : Environment.UserName == "root";
        private static CloudClient.Client? _client;


        public static CloudClient.Client? Client
        {
            get { return _client; }
            private set
            {
                _client = value;
                Util.MonitorUpdates(); // Start automatic updating of executables (checks for new updates and proceeds with the update)
            }
        }

        /// <summary>
        /// Returns true if queried when is a suitable time to perform an update
        /// </summary>
        /// <returns></returns>
        public static bool CanUpdate()
        {
            return Client != null && Client.CanRestart() && !BackupManager.BackupIsRunning;
        }

        /// <summary>
        /// Create the client instance 
        /// </summary>
        /// <param name="connectToEntryPoint"></param>
        public static void CreateClient(string? connectToEntryPoint = null)
        {
            Client = new CloudClient.Client(CloudPath, CloudPathIsReachable());
            if (connectToEntryPoint != null)
            {
                Client.CreateContext(connectToEntryPoint);
            }
            SemaphoreCreateClient.Set();
        }

        public static ManualResetEvent SemaphoreCreateClient = new ManualResetEvent(false);


        /// <summary>
        /// Create a new account and login to cloud server
        /// </summary>
        /// <param name="qr"></param>
        /// <param name="pin"></param>
        /// <returns>True for Successful, or false if QR code is not valid (this routine don't check the pin)</returns>
        public static CloudClient.Client.LoginResult Login(string qr, string pin)
        {
            if (Client == null)
                CreateClient();
            var result = Client == null ? CloudClient.Client.LoginResult.WrongQR : Client.Login(qr, pin, EntryPoint);
            if (result == CloudClient.Client.LoginResult.Successful)
            {
                AutoStart = true;
                Client?.Context.SecureStorage.Values.Set("QR", qr);
            }
            return result;
        }

        /// <summary>
        /// The QR code you logged in with, or null if you are not logged in to the cloud
        /// </summary>
        public static string? LoggedQr => Client?.Context?.SecureStorage.Values.Get("QR", null);

        /// <summary>
        /// Set to true, starts automatically when the operating system starts
        /// </summary>
        public static bool AutoStart { get { return SystemExtra.Util.AutoStart; } set { SystemExtra.Util.SetAutoStart(value, Static.Port); } }

        /// <summary>
        /// Disconnect from the cloud
        /// </summary>
        public static void Logout()
        {
            Client?.Context?.SecureStorage.Values.Set("QR", null);
            Client?.Logout();
            Client = null;            
        }

        /// <summary>
        /// Recover an existing account
        /// </summary>
        /// <param name="qr"></param>
        /// <param name="passphrase"></param>
        /// <returns></returns>
        public static CloudClient.Client.LoginResult Restore(string qr, string passphrase)
        {
            if (Client != null)
                CreateClient();
            Client?.Logout();
            return Client?.CreateContext(qr, passphrase: passphrase) != null ? CloudClient.Client.LoginResult.Successful : CloudClient.Client.LoginResult.WrongQR;
        }
#if DEBUG
        public const bool IsDebug = true;
#else
        public const bool IsDebug = false;
#endif
    }
}
