using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
namespace Cloud
{
    static public partial class Static
    {
        public static string? EntryPoint;
        public static string? CloudPath;
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

        public static bool CanUpdate()
        {
            return Client != null && Client.CanRestart() && !BackupManager.BackupIsRunning;
        }

        public static void CreateClient(string? connectToEntryPoint = null)
        {
            Client = new CloudClient.Client(CloudPath, isReachable: IsReachable());
            if (connectToEntryPoint != null)
            {
                Client.CreateContext(connectToEntryPoint);
            }
        }
        /// <summary>
        /// Returns false if the path corresponds to an unmounted virtual disk, true in all other cases
        /// </summary>
        public static bool IsReachable()
        {
            var directoryInfo = new DirectoryInfo(CloudPath);
            if (!directoryInfo.Exists)
                return true;
            var target = directoryInfo.ResolveLinkTarget(true);
            return target == null ? directoryInfo.Exists : target.Exists;
        }

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
            Client = new CloudClient.Client(CloudPath, isReachable: IsReachable());
            return Client.CreateContext(qr, passphrase: passphrase) != null ? CloudClient.Client.LoginResult.Successful : CloudClient.Client.LoginResult.WrongQR;
        }
#if DEBUG
        public const bool IsDebug = true;
#else
        public const bool IsDebug = false;
#endif
    }
}
