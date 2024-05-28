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

        public static string? QrDetected { get; private set; }
        private static List<Action<string>> OnDetecteds = new List<Action<string>>();
        private static bool DetectQrCodeInProgress;
        public static void DetectQrCode(Action<string> onDetected)
        {
            lock (OnDetecteds)
            {
                OnDetecteds.Add(onDetected);
            }
            if (DetectQrCodeInProgress == false)
            {
                DetectQrCodeInProgress = true;
                if (QrDetected != null)
                {
                    lock (OnDetecteds)
                    {
                        OnDetecteds.ForEach(o => o.Invoke(QrDetected));
                        OnDetecteds.Clear();
                    }
                    return;
                }
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var localIp = host.AddressList.ToList().Find(ip => ip.IsIntranet());
                if (localIp != null)
                {
                    var my = localIp.GetAddressBytes().Last();
                    new Task(() =>
                    {
                        int attempt = 0;
                        do
                        {
                            attempt++;
                            if (attempt != 1)
                                Thread.Sleep(60000); //pause for one minute for each attempt to locate the QR code (the device may have been connected in the meantime)
                            var tasks = new List<Task>();
                            for (byte i = 0; i < 255; i++)
                            {
                                if (i != my)
                                {
                                    var task = new Task((obj) =>
                                    {
                                        try
                                        {
                                            byte index = (byte)obj;
                                            using var pinger = new Ping();
                                            var address = localIp.GetAddressBytes();
                                            address[address.Length - 1] = index;
                                            var ipAddress = new IPAddress(address);
                                            if (!string.IsNullOrEmpty(QrDetected))
                                                return;
                                            PingReply reply = pinger.Send(ipAddress, 5000);
                                            if (reply.Status == IPStatus.Success)
                                            {
                                                string url = "http://" + ipAddress.ToString() + ":5000/qr";
                                                if (!string.IsNullOrEmpty(QrDetected))
                                                    return;
                                                using HttpClient client = new();
                                                try
                                                {
                                                    client.Timeout = TimeSpan.FromSeconds(4);
                                                    var b64 = client.GetStringAsync(url).Result;
                                                    Convert.FromBase64String(b64);
                                                    QrDetected = b64;
                                                    lock (OnDetecteds)
                                                    {
                                                        OnDetecteds.ForEach(o => o.Invoke(QrDetected));
                                                        OnDetecteds.Clear();
                                                    }
                                                }
                                                catch (Exception) { }
                                            }

                                        }
                                        catch (PingException)
                                        {
                                        }
                                    }, i);
                                    tasks.Add(task);
                                    task.Start();
                                }
                            }
                            Task.WaitAll(tasks.ToArray());
                        } while (Client == null && string.IsNullOrEmpty(QrDetected));
                        DetectQrCodeInProgress = false;
                    }).Start();
                }
            }
        }

        /// <summary>
        /// An extension method to determine if an IP address is internal, as specified in RFC1918
        /// </summary>
        /// <param name="toTest">The IP address that will be tested</param>
        /// <returns>Returns true if the IP is internal, false if it is external</returns>
        private static bool IsIntranet(this IPAddress toTest)
        {
            if (IPAddress.IsLoopback(toTest)) return true;
            if (toTest.ToString() == "::1") return false;
            var bytes = toTest.GetAddressBytes();
            if (bytes.Length != 4) return false;
            uint A(byte[] bts) { Array.Reverse(bts); return BitConverter.ToUInt32(bts, 0); }
            bool Ir(uint ipReverse, byte[] start, byte[] end) { return (ipReverse >= A(start) && ipReverse <= A(end)); } // Check if is in range
            var ip = A(bytes);
            // IP for special use: https://en.wikipedia.org/wiki/Reserved_IP_addresses             
            if (Ir(ip, new byte[] { 0, 0, 0, 0 }, new byte[] { 0, 255, 255, 255 })) return true;
            if (Ir(ip, new byte[] { 10, 0, 0, 0 }, new byte[] { 10, 255, 255, 255 })) return true;
            if (Ir(ip, new byte[] { 100, 64, 0, 0 }, new byte[] { 100, 127, 255, 255 })) return true;
            if (Ir(ip, new byte[] { 127, 0, 0, 0 }, new byte[] { 127, 255, 255, 255 })) return true;
            if (Ir(ip, new byte[] { 169, 254, 0, 0 }, new byte[] { 169, 254, 255, 255 })) return true;
            if (Ir(ip, new byte[] { 172, 16, 0, 0 }, new byte[] { 172, 31, 255, 255 })) return true;
            if (Ir(ip, new byte[] { 192, 0, 0, 0 }, new byte[] { 192, 0, 0, 255 })) return true;
            if (Ir(ip, new byte[] { 192, 0, 2, 0 }, new byte[] { 192, 0, 2, 255 })) return true;
            if (Ir(ip, new byte[] { 192, 88, 99, 0 }, new byte[] { 192, 88, 99, 255 })) return true;
            if (Ir(ip, new byte[] { 192, 168, 0, 0 }, new byte[] { 192, 168, 255, 255 })) return true;
            if (Ir(ip, new byte[] { 198, 18, 0, 0 }, new byte[] { 198, 19, 255, 255 })) return true;
            if (Ir(ip, new byte[] { 198, 51, 100, 0 }, new byte[] { 198, 51, 100, 255 })) return true;
            if (Ir(ip, new byte[] { 203, 0, 113, 0 }, new byte[] { 203, 0, 113, 255 })) return true;
            if (Ir(ip, new byte[] { 224, 0, 0, 0 }, new byte[] { 239, 255, 255, 255 })) return true;
            if (Ir(ip, new byte[] { 233, 252, 0, 0 }, new byte[] { 233, 252, 0, 255 })) return true;
            if (Ir(ip, new byte[] { 240, 0, 0, 0 }, new byte[] { 255, 255, 255, 254 })) return true;
            return false;
        }
#if DEBUG
        public const bool IsDebug = true;
#else
        public const bool IsDebug = false;
#endif
    }
}
