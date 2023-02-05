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
        public static void Login(string qr, string pin)
        {
            if (Client == null)
                CreateClient();
            Client?.Login(qr, pin, EntryPoint);
        }
    }
}
