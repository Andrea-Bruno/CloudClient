using CloudSync;
using EncryptedMessaging;
using NBitcoin;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace CloudClient
{
    /// <summary>
    /// The cloud client objects based on the CloudBox project. Extends the functionality of the CloudBox client
    /// </summary>
    public partial class Client : CloudBox.CloudBox
    {
        /// <param name="cloudPath">Directory position of the cloud (a null value will be considered the default path)</param>
        /// <param name="syncIsEnabled"> False to suspend sync, or true. It is important to suspend synchronization if the path is not available (for example when using virtual disks)! Indicate true if the path to the cloud space is reachable (true), or unmounted virtual disk (false). Use IsReachableDiskStateIsChanged to notify that access to the cloud path has changed.</param>
        public Client(string cloudPath = null, bool syncIsEnabled = true) : base(cloudPath)
        {
            QrCodeDetector.DisallowDetectQrCode = true;
            OnRouterConnectionChangeEvent = OnRouterConnectionChange;
            OnCommandEvent = OnServerCommand;
            OnLocalSyncStatusChangesActionList.Add(OnLocalSyncStatusChanges);

            // EnableOSFeatures(null);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extra")))
            {
                string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extra.zip");
                if (File.Exists(zipPath))
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, AppDomain.CurrentDomain.BaseDirectory);
            }
        }

        public void EnableOSFeatures(Action openUI)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsSupport.SetWindowsStatusIcon(CloudPath, openUI);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                MacOSSupport.SetOSXStatusIcon(CloudPath, openUI);
            }
        }

        private void OnLocalSyncStatusChanges(Sync.SyncStatus syncStatus, int pendingFiles)
        {
            IconStatus currentStatus;
            if (syncStatus == Sync.SyncStatus.Undefined)
                currentStatus = IconStatus.Default;
            else if (syncStatus == Sync.SyncStatus.Pending)
                currentStatus = IconStatus.Synchronize;
            else if (syncStatus == Sync.SyncStatus.Monitoring)
                currentStatus = IconStatus.Synchronized;
            else
                currentStatus = IconStatus.Warning;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsSupport.UpdateStatusIcon(currentStatus);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                MacOSSupport.UpdateStatusIcon(currentStatus);
            };
        }
        internal enum IconStatus
        {
            Default,
            Synchronize,
            Synchronized,
            Warning
        }

        /// <summary>
        /// Connect to server and start sync
        /// </summary>
        /// <param name="serverPublicKey">It is the server to which this client must connect (the public key of the server)</param>
        /// <returns>Successful</returns>        
        public void ConnectToServer(string serverPublicKey = null)
        {
            if (EncryptedQR != null)
            {
                // If the login was partially done with an encrypted QR code, once the connection with the router has been established, it asks the cloud for the QR code in order to log in definitively
                SendCommand(EncryptedQR.ServerId, Command.GetEncryptedQR, null);
                return;
            }
            serverPublicKey ??= ServerPublicKey; // serverPublicKey = Context.SecureStorage.Values.Get("ServerPublicKey", null);
            SetServerCloudContact(serverPublicKey);
            LoginCredential credential = TmpLoginData?.Item1 == null ? null : new LoginCredential { Pin = TmpLoginData?.Item1, PublicKey = Context.My.GetPublicKeyBinary() };
            StartSync(credential, TmpLoginData?.Item2);
            return;
        }
        private string ServerPublicKey { get { return Context.SecureStorage.Values.Get(nameof(ServerPublicKey), null); } set { Context.SecureStorage.Values.Set(nameof(ServerPublicKey), value); } }
        private (string, byte[])? TmpLoginData;

        /// <summary>
        /// Placeholder for a method that will be called when the connection changes  
        /// Function that is called when the connection changes (connected or disconnected)
        /// </summary>
        /// <param name="isConnected">Notify the new connection status</param>
        private void OnRouterConnectionChange(bool isConnected)
        {
            if (Sync == null && isConnected)
                ConnectToServer();
        }

        /// <summary>
        /// Login the Client to the Cloud Server by entry QrCode and Pin of server
        /// </summary>
        /// <param name="qrCode">QR code generated by server cloud, in text format</param>
        /// <param name="pin">Pin</param>
        /// <param name="entryPoint">Router entry point, optional parameter for QR codes that do not contain the entry point</param>
        /// <param name="zeroKnowledgeEncryptionMasterKey">If set, zero knowledge proof is enabled, meaning files will be sent encrypted with keys derived from this, and once received, if encrypted, they will be decrypted.</param>
        /// <returns>Validated for Successful, or other result if QR code or PIN is not valid</returns>        
        public LoginResult Login(string qrCode, string pin, string entryPoint = null, byte[] zeroKnowledgeEncryptionMasterKey = null)
        {
            QrCodeDetector.DisallowDetectQrCode = true;
            var result = TryLogin(qrCode, pin, entryPoint, zeroKnowledgeEncryptionMasterKey);
            if (result != LoginResult.Successful)
                Logout();
            return result;
        }

        private ServerIdAndEncryptionKey EncryptedQR;

        private LoginResult TryLogin(string qrCode, string pin, string entryPoint = null, byte[] zeroKnowledgeEncryptionMasterKey = null)
        {
            Logout();
            if (string.IsNullOrEmpty(pin))
                return LoginResult.WrongPassword;
            if (SolveQRCode(qrCode, out string entry, out string serverPublicKey, out EncryptedQR) == false) return LoginResult.WrongQR;
            if (entry != null)
                entryPoint = entry;
            var context = CreateContext(entryPoint);
            // =================
            // NOTE: Login is performed when the context has established the connection with the router
            // =================
            TmpLoginData = (pin, zeroKnowledgeEncryptionMasterKey);
            ServerPublicKey = serverPublicKey; // context.SecureStorage.Values.Set("ServerPublicKey", serverPublicKey);
            try
            {
                OnSyncStart = new AutoResetEvent(false);
                if (OnSyncStart.WaitOne(60000))
                {
                    TmpLoginData = null;
                    Sync.OnLoginCompleted = new AutoResetEvent(false);
                    if (Sync.OnLoginCompleted.WaitOne(60000))
                        return Sync.LoginError ? LoginResult.WrongPassword : LoginResult.Successful;
                    else if (context.LicenseExpired)
                        return LoginResult.LicenseExpired;
                    else if (Sync == null)
                        return LoginResult.RemoteHostNotReachable;
                    else if (Sync.RemoteHostReachable)
                        return LoginResult.CloudNotResponding;
                    return LoginResult.RemoteHostNotReachable;
                }
                TmpLoginData = null;
                return LoginResult.TimeoutError;
            }
            catch (Exception)
            {
                TmpLoginData = null;
                return LoginResult.ErrorOccurred;
            }
        }

#pragma warning disable CS1591
        /// <summary>
        /// Result of login validation
        /// </summary>
        public enum LoginResult
        {
            Successful,
            LicenseExpired,
            WrongPassword,
            CloudNotResponding,
            WrongQR,
            RemoteHostNotReachable,
            ErrorOccurred,
            TimeoutError
        }
#pragma warning restore CS1591

        /// <summary>
        /// Close socket connection to the router and stop syncing, stops transmitting with the cloud server, but the connection with the router remains active
        /// </summary>
        /// <returns>False if already logged out, true otherwise</returns>
        public bool Logout()
        {
            QrCodeDetector.DisallowDetectQrCode = false;
            SetStaticValue(nameof(LastEntryPoint), null);
            StopSync();
            if (Context != null)
            {
                TmpLoginData = null;
                ServerPublicKey = null;
                Context.Dispose();
                Context = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the cloud is logged in to the server. The returned value is persistent, that is, if the client is logged in it will remain logged in even after a reboot, and this value will return true (after a reboot it is not necessary to log in again)
        /// </summary>
        /// <returns>True if the client is logged in to the server</returns>
        public bool LoginStatus() => StaticValueExists(nameof(LastEntryPoint));

        private void SetServerCloudContact(string serverPublicKey)
        {
#if DEBUG || DEBUG_AND
            if (serverPublicKey == null)
            {
                var mnemonic = new Mnemonic(TestServerPassphrase, Wordlist.AutoDetect(TestServerPassphrase));
                var hdRoot = mnemonic.DeriveExtKey();
                var privateKey = hdRoot.PrivateKey;
                serverPublicKey = Convert.ToBase64String(privateKey.PubKey.ToBytes());
            }
#endif
#pragma warning disable
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (serverPublicKey == null)
                // ReSharper disable once HeuristicUnreachableCode
                serverPublicKey = ServerPublicKey; // serverPublicKey = Context.SecureStorage.Values.Get("ServerPublicKey", null);
            else
                ServerPublicKey = serverPublicKey; // Context.SecureStorage.Values.Set("ServerPublicKey", serverPublicKey);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (serverPublicKey != null)
            {
                ServerCloud = Context.Contacts.AddContact(serverPublicKey, "Server cloud", Modality.Server, Contacts.SendMyContact.None);
            }
#pragma warning restore            
        }

        /// <summary>
        /// Execute when Garbage Collector remove it
        /// </summary>
        ~Client()
        {
        }

    }

}
