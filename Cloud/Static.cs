using Cloud.Pages;
using CloudSync;
using System.Buffers;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

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
            Client = new CloudBox.CloudBox(CloudPath, isReachable: IsReachable());
            if (connectToEntryPoint != null)
            {
                Client.CreateContext(connectToEntryPoint);
            }
        }
        /// <summary>
        /// This function returns true if the specified path is IsReachable or matches a virtual disk path that has unmounted.
        /// </summary>
        public static bool IsReachable()
        {
            var directoryInfo = new DirectoryInfo(CloudPath);
            var target = directoryInfo.ResolveLinkTarget(true);
            return target == null ? directoryInfo.Exists : target.Exists;
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
            Client = new CloudBox.CloudBox(CloudPath, isReachable: IsReachable());
            return Client.CreateContext(qr, passphrase: passphrase) ? CloudBox.CloudBox.LoginResult.Validated : CloudBox.CloudBox.LoginResult.WrongQR;
        }

        public static bool LastMountVirtualDiskStatus { get { return Storage.Values.Get(nameof(LastMountVirtualDiskStatus), true); } set { Storage.Values.Set(nameof(LastMountVirtualDiskStatus), value); } }

        private static int MountRunning;
        public static bool MountVirtualDisk(string password)
        {
            if (MountRunning == 0 && password != null)
            {
                MountRunning++;
                var hash = ParallelHash(Encoding.UTF8.GetBytes(password));
                if (BitConverter.ToUInt64(hash) != Static.Storage.Values.Get("vhdpw", 0ul))
                {
                    MountRunning--;
                    return false;
                }
                hash = ParallelHash(hash);
                LastMountVirtualDiskStatus = true;
                if (!SystemExtra.Util.IsMounted(CloudPath, out bool _))
                {
                    var sysFile = VirtualDiskFullFileName?.Substring(0, VirtualDiskFullFileName.Length - 4) + ".sys";
                    if (File.Exists(sysFile))
                    {
                        Camouflage(sysFile, hash);
                        Path.ChangeExtension(sysFile, ".vhdx");
                    }
                    new DirectoryInfo(CloudPath).Attributes &= ~FileAttributes.Hidden;
                    SystemExtra.Util.MountVirtualDisk(VirtualDiskFullFileName, CloudPath);
                    Client?.IsReachableDiskStateIsChanged(SystemExtra.Util.IsMounted(CloudPath, out bool _));
                    MountRunning--;
                    return true;
                }
                MountRunning--;
            }
            return false;
        }

        public static void UnmountVirtualDisk(string password)
        {
            if (MountRunning == 0)
            {
                MountRunning++;
                var hash = ParallelHash(Encoding.UTF8.GetBytes(password));
                Static.Storage.Values.Set("vhdpw", BitConverter.ToUInt64(hash));
                hash = ParallelHash(hash);
                LastMountVirtualDiskStatus = false;
                SystemExtra.Util.UnmountVirtualDisk(VirtualDiskFullFileName);
                Camouflage(VirtualDiskFullFileName, hash);
                Path.ChangeExtension(VirtualDiskFullFileName, ".sys");
                new DirectoryInfo(CloudPath).Attributes |= FileAttributes.Hidden;
                Client?.IsReachableDiskStateIsChanged(SystemExtra.Util.IsMounted(CloudPath, out bool _));
                MountRunning--;
            }
        }

        /// <summary>
        /// Anty brute force attac!
        /// </summary>
        /// <param name="data"></param>
        /// <param name="interactions"></param>
        /// <param name="threads"></param>
        /// <returns></returns>
        private static byte[] ParallelHash(byte[] data, int interactions = 2000000, int threads = 8)
        {
            var seeds = new byte[threads][];
            var sha256 = SHA256.Create();
            for (byte i = 0; i < threads; i++)
            {
                seeds[i] = sha256.ComputeHash(new byte[i].Concat(data));
            }
            var hashes = new byte[threads][];
            var x = new Stopwatch();
            x.Start();

            Parallel.For(0, threads, thread =>
            {
                hashes[thread] = RecursiceHash(seeds[thread], interactions);
            });
            var result = new byte[hashes[0].Length];
            for (int i = 0; i < threads; i++)
            {
                result = Xor(result, hashes[i]);
            }
            x.Stop();
            return result;
        }

        private static byte[] RecursiceHash(byte[] data, int interactions)
        {
            var sha256 = SHA256.Create();
            byte[] hash = data;
            for (int i = 0; i < interactions; i++)
            {
                hash = sha256.ComputeHash(hash);
            }
            return hash;
        }

        /// <summary>
        /// 1 Mega Byte
        /// </summary>
        private const int MB = 1048576;
        private static void Camouflage(string file, byte[] password, int len = MB * 10)
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
        private static byte[] GetRandomByteArray(int size, byte[] seed)
        {
            using HashAlgorithm algorithm = SHA256.Create();
            var hash = seed;
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

        public static string? QrDetected { get; private set; }
        private static List<Action<string>> OnDetecteds = new List<Action<string>>();
        private static bool LockDetectQrCode;
        public static void DetectQrCode(Action<string> onDetected)
        {
            lock (OnDetecteds)
            {
                OnDetecteds.Add(onDetected);
            }
            if (LockDetectQrCode == false)
            {
                LockDetectQrCode = true;
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
                                            if (QrDetected != null)
                                                return;
                                            PingReply reply = pinger.Send(ipAddress, 2000);
                                            if (reply.Status == IPStatus.Success)
                                            {
                                                string url = "http://" + ipAddress.ToString() + ":5000/qr";
                                                if (QrDetected != null)
                                                    return;
                                                using HttpClient client = new();
                                                try
                                                {
                                                    client.Timeout = TimeSpan.FromSeconds(2);
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
                        } while (Client == null && QrDetected == null);
                        LockDetectQrCode = false;
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
