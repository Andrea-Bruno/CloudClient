using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CloudSync;
namespace Cloud
{
    static public partial class Static
    {
        public static string? VirtualDiskFullFileName;

        public static bool VirtualDiskIsMounted
        {
            get
            {
                var path = new DirectoryInfo(CloudPath);
                if (path.LinkTarget == null)
                    Debugger.Break(); // Virtual disk not in this location
                try
                {
                    path.GetFileSystemInfos();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Get or generate a virtual disk password
        /// </summary>
        /// <returns></returns>
        public static string? VirtualDiskPassword()
        {
            string? vdPassword = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                vdPassword = Static.Storage.Values.Get(nameof(vdPassword), null);
                if (vdPassword == null)
                {
                    vdPassword = Guid.NewGuid().ToByteArray().ToHex();
                    Static.Storage.Values.Set(nameof(vdPassword), vdPassword);
                }
            }
            return vdPassword;
        }


        // public static bool MountVirtualDiskStatus { get { return Storage.Values.Get(nameof(MountVirtualDiskStatus), true); } set { Storage.Values.Set(nameof(MountVirtualDiskStatus), value); } }

        private static int MountRunning;

        /// <summary>
        /// Mount a password-locked disk
        /// </summary>
        /// <param name="password">Disk password</param>
        /// <returns>Description of the error if it occurs, or nothing</returns>
        public static string? MountVirtualDisk(string? password)
        {
            if (MountRunning == 0)
            {
                MountRunning++;
                if (string.IsNullOrEmpty( password))
                {
                    MountRunning--;
                    return "null password";
                }
                else
                {
                    var hash = ParallelHash(Encoding.UTF8.GetBytes(password));
                    if (BitConverter.ToUInt64(hash) != Storage.Values.Get("vhdpw", 0ul))
                    {
                        MountRunning--;
                        return "wrong password";
                    }
                    hash = ParallelHash(hash);
                    if (!SystemExtra.Util.IsMounted(CloudPath, out bool _))
                    {
                        var sysFile = Path.ChangeExtension(VirtualDiskFullFileName, ".sys");
                        if (File.Exists(sysFile))
                        {
                            Thread.CurrentThread.Priority = ThreadPriority.Highest;
                            Camouflage(sysFile, hash);
                            if (File.Exists(VirtualDiskFullFileName))
                                File.Delete(VirtualDiskFullFileName);
                            File.Move(sysFile, VirtualDiskFullFileName);
                            Thread.CurrentThread.Priority = ThreadPriority.Normal;
                        }
                        new DirectoryInfo(CloudPath).Attributes &= ~FileAttributes.Hidden;
                        SystemExtra.Util.MountVirtualDisk(VirtualDiskFullFileName, CloudPath, password);
                        Client?.IsReachableDiskStateIsChanged(SystemExtra.Util.IsMounted(CloudPath, out bool _));
                        MountRunning--;
                        return null;
                    }
                }
                MountRunning--;
            }
            return "Operation already in progress, please wait!";
        }

        public static void UnmountVirtualDisk(string password, out string? error)
        {
            if (MountRunning == 0)
            {
                MountRunning++;
                var hash = ParallelHash(Encoding.UTF8.GetBytes(password));
                Storage.Values.Set("vhdpw", BitConverter.ToUInt64(hash));
                hash = ParallelHash(hash);
                SystemExtra.Util.UnmountVirtualDisk(VirtualDiskFullFileName);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string vdPassword = Storage.Values.Get(nameof(vdPassword), null);
                    if (vdPassword == null)
                    {
                        Debugger.Break();
                    }
                    string newPassword = hash.ToHex();
                    // Comando per cambiare la password
                    string command = $"hdiutil chpass {VirtualDiskFullFileName}";
                    SystemExtra.Util.ExecuteSystemCommand(command, vdPassword + Environment.NewLine + newPassword + Environment.NewLine + newPassword);
                    Static.Storage.Values.Set(nameof(vdPassword), newPassword);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Camouflage(VirtualDiskFullFileName, hash);
                    File.Move(VirtualDiskFullFileName, Path.ChangeExtension(VirtualDiskFullFileName, ".sys"));
                }
                new DirectoryInfo(CloudPath).Attributes |= FileAttributes.Hidden;
                Client?.IsReachableDiskStateIsChanged(SystemExtra.Util.IsMounted(CloudPath, out bool _));
                MountRunning--;
                error = null;
                return;
            }
            error = "Operation already in progress, please wait!";
            return;
        }

        /// <summary>
        /// Anti brute force attach!
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

            Parallel.For(0, threads, thread => hashes[thread] = RecursiveHash(seeds[thread], interactions));
            var result = new byte[hashes[0].Length];
            for (int i = 0; i < threads; i++)
            {
                result = Xor(result, hashes[i]);
            }
            x.Stop();
            return result;
        }

        private static byte[] RecursiveHash(byte[] data, int interactions)
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

    }

}
