using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CloudSync;
namespace Cloud
{
    static public partial class Static
    {
        /// <summary>
        /// If used, the virtual disk path that underlies the cloud path
        /// </summary>
        public static string? VirtualDiskFullFileName { get; set; }

        /// <summary>
        /// Returns false if the path corresponds to an unmounted virtual disk, true in all other cases
        /// </summary>
        public static bool CloudPathIsReachable()
        {
            return VirtualDiskFullFileName == null ? Directory.Exists(CloudPath) : VirtualDiskIsMounted;
            //var directoryInfo = new DirectoryInfo(CloudPath);
            //if (!directoryInfo.Exists)
            //    return true;
            //else if (directoryInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
            //    return false;
            //var target = directoryInfo.ResolveLinkTarget(true);
            //return target == null ? directoryInfo.Exists : target.Exists;
        }

        /// <summary>
        /// Returns true if the virtual disk is mounted
        /// </summary>
        public static bool VirtualDiskIsMounted
        {
            get
            {
                return SystemExtra.Util.IsMounted(CloudPath);
            }
        }

        /// <summary>
        /// True if the virtual disk has been locked with a password
        /// </summary>
        public static bool VirtualDiskIsLocked
        {
            get { return !File.Exists(VirtualDiskFullFileName); }
        }

        /// <summary>
        /// Get or generate a virtual disk password
        /// </summary>
        /// <returns></returns>
        public static string? VirtualDiskPassword
        {
            get
            {
                string? vdPassword = null;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    vdPassword = Storage.Values.Get(nameof(vdPassword), null);
                    if (vdPassword == null)
                    {
                        vdPassword = Guid.NewGuid().ToByteArray().ToHex();
                        Storage.Values.Set(nameof(vdPassword), vdPassword);
                    }
                }
                return vdPassword;
            }
            set
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string? vdPassword = value;

                    if (value == null)
                    {
                        Static.Storage.Values.Delete(nameof(vdPassword), typeof(string));
                    }
                    else
                    {
                        Static.Storage.Values.Set(nameof(vdPassword), vdPassword);
                    }
                }

            }
        }


        // public static bool MountVirtualDiskStatus { get { return Storage.Values.Get(nameof(MountVirtualDiskStatus), true); } set { Storage.Values.Set(nameof(MountVirtualDiskStatus), value); } }

        private static int LockVirtualDiskRunning;

        /// <summary>
        /// Mount a password-locked disk
        /// </summary>
        /// <param name="password">Disk password</param>
        /// <returns>Description of the error if it occurs, or nothing</returns>
        public static string? UnlockVirtualDisk(string? password)
        {
            if (LockVirtualDiskRunning == 0)
            {
                LockVirtualDiskRunning++;
                if (string.IsNullOrEmpty(password))
                {
                    LockVirtualDiskRunning--;
                    return "null password";
                }
                else
                {
                    var hash = ParallelHash(Encoding.UTF8.GetBytes(password));
                    if (BitConverter.ToUInt64(hash) != Storage.Values.Get("vhdpw", 0ul))
                    {
                        LockVirtualDiskRunning--;
                        return "wrong password";
                    }
                    hash = ParallelHash(hash);
                    if (!SystemExtra.Util.IsMounted(CloudPath))
                    {
                        var sysFile = Path.ChangeExtension(VirtualDiskFullFileName, ".sys");

                        string vdPassword = null;
                        if (Directory.Exists(sysFile) || File.Exists(sysFile))
                        {
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                                Camouflage(sysFile, hash);
                                if (File.Exists(VirtualDiskFullFileName))
                                    File.Delete(VirtualDiskFullFileName);
                                Thread.CurrentThread.Priority = ThreadPriority.Normal;
                            }
                            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                            {
                                byte[] xorVDPass;
                                xorVDPass = Storage.Values.Get(nameof(xorVDPass), null).HexToBytes();
                                for (int i = 0; i < xorVDPass.Length; i++)
                                {
                                    xorVDPass[i] = (byte)(hash[i] ^ xorVDPass[i]);
                                }
                                vdPassword = xorVDPass.ToHex();
                                VirtualDiskPassword = vdPassword;
                                Storage.Values.Delete(nameof(xorVDPass), typeof(string));
                            }
                            if (File.GetAttributes(sysFile).HasFlag(FileAttributes.Directory))
                                Directory.Move(sysFile, VirtualDiskFullFileName);
                            else
                                File.Move(sysFile, VirtualDiskFullFileName);
                        }
                        new DirectoryInfo(CloudPath).Attributes &= ~FileAttributes.Hidden;
                        //new DirectoryInfo(CloudPath).Attributes &= ~FileAttributes.ReadOnly;
                        if (!SystemExtra.Util.MountVirtualDisk(VirtualDiskFullFileName, CloudPath, vdPassword))
                            Debugger.Break();
                        var isMounted = SystemExtra.Util.IsMounted(CloudPath);
                        if (!isMounted)
                        {
                            Console.WriteLine("Unable to mount Virtual Disk");
                            Debugger.Break();
                        }
                        else
                        {
                            Client?.SetSyncState(isMounted);
                        }
                        LockVirtualDiskRunning--;
                        return null;
                    }
                }
                LockVirtualDiskRunning--;
            }
            return "Operation already in progress, please wait!";
        }

        /// <summary>
        /// Unmount the virtual disk and assign a password for mounting
        /// </summary>
        /// <param name="password">Password assigned</param>
        /// <param name="error">Out error if any</param>
        public static void LockVirtualDisk(string password, out string? error)
        {
            if (LockVirtualDiskRunning == 0)
            {
                LockVirtualDiskRunning++;
                var hash = ParallelHash(Encoding.UTF8.GetBytes(password));
                Storage.Values.Set("vhdpw", BitConverter.ToUInt64(hash));
                hash = ParallelHash(hash);
                Client?.SetSyncState(false);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? SystemExtra.Util.UnmountVirtualDiskWindow(VirtualDiskFullFileName) : SystemExtra.Util.UnmountVirtualDisk(CloudPath))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        string vdPassword;
                        vdPassword = Storage.Values.Get(nameof(vdPassword), null);
                        if (vdPassword == null)
                        {
                            Debugger.Break();
                        }
                        var xorVDPass = vdPassword.HexToBytes();
                        for (int i = 0; i < xorVDPass.Length; i++)
                        {
                            xorVDPass[i] = (byte)(hash[i] ^ xorVDPass[i]);
                        }
                        Storage.Values.Set(nameof(xorVDPass), xorVDPass.ToHex());
                        VirtualDiskPassword = null;
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Camouflage(VirtualDiskFullFileName, hash);
                    }
                    if (File.GetAttributes(VirtualDiskFullFileName).HasFlag(FileAttributes.Directory))
                        Directory.Move(VirtualDiskFullFileName, Path.ChangeExtension(VirtualDiskFullFileName, ".sys"));
                    else
                        File.Move(VirtualDiskFullFileName, Path.ChangeExtension(VirtualDiskFullFileName, ".sys"));
                    new DirectoryInfo(CloudPath).Attributes |= FileAttributes.Hidden | FileAttributes.ReadOnly;
                }
                else
                {
                    Client?.SetSyncState(SystemExtra.Util.IsMounted(CloudPath));
                }
                LockVirtualDiskRunning--;
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
