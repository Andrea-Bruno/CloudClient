using Cloud.Pages;
using CloudSync;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Cloud
{
    static public class Static
    {
        public static string? EntryPoint;
        public static string? CloudPath;
        public static string? Port;
        public static readonly bool IsAdmin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) : Environment.UserName == "root";
        public static CloudBox.CloudBox? Client { get; private set; }
        public static void CreateClient(string? connectToEntryPoint = null)
        {
            Client = new CloudBox.CloudBox(CloudPath);
            if (connectToEntryPoint != null)
            {
                Client.CreateContext(connectToEntryPoint);
            }
        }
        /// <summary>
        /// Create a new account and login to cloud server
        /// </summary>
        /// <param name="qr"></param>
        /// <param name="pin"></param>
        /// <returns>True for Successful, or false if QR code is not valid (this routine don't check the pin)</returns>
        public static CloudBox.CloudBox.LoginResult Login(string qr, string pin)
        {
            if (Client == null)
                CreateClient();
            var result = Client == null ? CloudBox.CloudBox.LoginResult.WrongQR : Client.Login(qr, pin, EntryPoint);
            if (result == CloudBox.CloudBox.LoginResult.Validated)
            {
                Client?.Context.SecureStorage.Values.Set("QR", qr);
            } 
            return result;
        }

        /// <summary>
        /// Disconnect from the cloud
        /// </summary>
        public static void Logout()
        {
            Client?.Context.SecureStorage.Values.Set("QR", null);
            Client?.Logout();
        }

        /// <summary>
        /// Recover an existing account
        /// </summary>
        /// <param name="qr"></param>
        /// <param name="passphrase"></param>
        /// <returns></returns>
        public static CloudBox.CloudBox.LoginResult Restore(string qr, string passphrase)
        {
            Client = new CloudBox.CloudBox(CloudPath);
            return Client.CreateContext(qr, passphrase: passphrase) ? CloudBox.CloudBox.LoginResult.Validated : CloudBox.CloudBox.LoginResult.WrongQR;
        }
    }
}
