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
        /// Login to cloud server
        /// </summary>
        /// <param name="qr"></param>
        /// <param name="pin"></param>
        /// <returns>True for Successful, or false if QR code is not valid (this routine don't check the pin)</returns>
        public static bool Login(string qr, string pin)
        {
            if (Client == null)
                CreateClient();
          return Client != null && Client.Login(qr, pin, EntryPoint);
        }
    }
}
