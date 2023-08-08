using Cloud.Pages;
using CloudSync;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Cloud
{
    static public class Static
    {
        public static string? EntryPoint;
        public static string? CloudPath;
        public static int Port;
        public static SecureStorage.Storage Storage;
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

        public static string? VirtualDiskFullFileName;

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

        public static bool LastMountVirtualDiskStatus { get { return Storage.Values.Get(nameof(LastMountVirtualDiskStatus), true); } set { Storage.Values.Set(nameof(LastMountVirtualDiskStatus), value); } }

        public static void MountVirtualDisk(string password)
        {
            LastMountVirtualDiskStatus = true;
            if (!SystemExtra.Util.IsMounted(CloudPath, out bool _))
            {
                var sysFile = VirtualDiskFullFileName?.Substring(0, VirtualDiskFullFileName.Length - 4) + ".sys";
                if (File.Exists(sysFile))
                {
                    Camouflage(sysFile, password);
                    Path.ChangeExtension(sysFile, ".vhdx");
                }
                SystemExtra.Util.MountVirtualDisk(VirtualDiskFullFileName, CloudPath);
            }
        }


        public static void UnmountVirtualDisk(string password)
        {
            LastMountVirtualDiskStatus = false;
            SystemExtra.Util.UnmountVirtualDisk(VirtualDiskFullFileName);
            Camouflage(VirtualDiskFullFileName, password);
            Path.ChangeExtension(VirtualDiskFullFileName, ".sys");
        }

        private static void Camouflage(string file, string password, int len = 1048576)
        {
            using var fs = new FileStream(file, FileMode.Open);
            if (fs.Length < len)
                len = (int)fs.Length;
            var data = new byte[len];
            fs.Read(data, 0, len);
            data = Xor(data, GetRandomByteArray(data.Length, password));
            fs.Write(data, 0, data.Length);
            fs.Close();
        }
        private static byte[] GetRandomByteArray(int size, string seed)
        {
            using HashAlgorithm algorithm = SHA256.Create();
            var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(seed));
            var hl = hash.Length;
            var parts = (int)Math.Ceiling((double)size / hl);
            byte[] b = new byte[parts * hl];
            var p = 0;
            for (int i = 0; i < parts; i++)
            {
                p = i * 32;
                hash = algorithm.ComputeHash(hash);
                Array.Copy(hash, 0, b, p, hl);
            }
            Array.Resize(ref b, size);
            return b;
        }
        public static byte[] Xor(byte[] key, byte[] PAN)
        {
            byte[] result = new byte[key.Length];
            for (int i = 0; i < key.Length; i++)
            {
                result[i] = (byte)(key[i] ^ PAN[i]);
            }
            return result;
        }
    }
}
