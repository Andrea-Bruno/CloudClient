﻿using CloudBox;
using CloudSync;
using EncryptedMessaging;
using NBitcoin;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CloudClient
{
    /// <summary>
    /// The cloud client objects based on the CloudBox project. Extends the functionality of the CloudBox client
    /// </summary>
    public partial class Client : CloudBox.CloudBox
    {
        public Client(string cloudPath = null, bool isReachable = true) : base( cloudPath, isReachable: isReachable)
        {
            OnRouterConnectionChangeEvent = OnRouterConnectionChange;
            OnCommandEvent = OnServerCommand;
        }
        /// <summary>
        /// Connect to server and start sync
        /// </summary>
        /// <param name="serverPublicKey">It is the server to which this client must connect (the public key of the server)</param>
        /// <param name="pin">If the client is running, this is the Pin on the server that is required to log in and then connect the client to the server</param>       
        /// <returns>Successful</returns>        
        public void ConnectToServer(string serverPublicKey = null, string pin = null)
        {
            if (!IsServer)
            {
                if (EncryptedQR != null)
                {
                    // If the login was partially done with an encrypted QR code, once the connection with the router has been established, it asks the cloud for the QR code in order to log in definitively
                    SendCommand(EncryptedQR.Item1, Command.GetEncryptedQR, null);
                    return;
                }
                if (pin == null)
                    pin = Context.SecureStorage.Values.Get("pin", null);
                if (!string.IsNullOrEmpty(pin))
                    Context.SecureStorage.Values.Set("pin", pin);
                if (serverPublicKey == null)
                    serverPublicKey = Context.SecureStorage.Values.Get("ServerPublicKey", null);
                SetServerCloudContact(serverPublicKey);
            }
            var credential = IsServer ? null : new LoginCredential { Pin = pin, PublicKey = Context.My.GetPublicKeyBinary() };
            StartSync(credential);
            return;
        }

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
        /// <returns>Validated for Successful, or other result if QR code or PIN is not valid</returns>        
        public LoginResult Login(string qrCode, string pin, string entryPoint = null)
        {
            var result = TryLogin(qrCode, pin, entryPoint);
            if (result != LoginResult.Successful)
                Logout();
            return result;
        }

        private LoginResult TryLogin(string qrCode, string pin, string entryPoint = null)
        {
            if (IsServer)
                Debugger.Break(); // non sense for server                     
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
            context.SecureStorage.Values.Set("pin", pin);
            context.SecureStorage.Values.Set("ServerPublicKey", serverPublicKey);
            if (SpinWait.SpinUntil(() => Sync != null && (Sync.IsLogged || Sync.LoginError), 60000))
                return Sync.LoginError ? LoginResult.WrongPassword : LoginResult.Successful;
            else if (context.LicenseExpired)
                return LoginResult.LicenseExpired;
            else if (Sync == null)
                return LoginResult.RemoteHostNotReachable;
            else if (Sync.RemoteHostReachable)
                return LoginResult.CloudNotResponding;
            return LoginResult.RemoteHostNotReachable;
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
        }
#pragma warning restore CS1591

        /// <summary>
        /// Close socket connection to the router and stop syncing, stops transmitting with the cloud server, but the connection with the router remains active
        /// </summary>
        /// <returns>False if already logged out, true otherwise</returns>
        public bool Logout()
        {
            if (File.Exists(FileLastEntryPoint))
                File.Delete(FileLastEntryPoint);
            StopSync();
            if (Context != null)
            {
                Context.SecureStorage.Values.Set("pin", null);
                Context.SecureStorage.Values.Set("ServerPublicKey", null);
                Context.Dispose();
                Context = null;
                return true;
            }
            return false;
        }

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
                serverPublicKey = Context.SecureStorage.Values.Get("ServerPublicKey", null);
            else
                Context.SecureStorage.Values.Set("ServerPublicKey", serverPublicKey);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (serverPublicKey != null)
            {
                ServerCloud = Context.Contacts.AddContact(serverPublicKey, "Server cloud", Modality.Server, Contacts.SendMyContact.None);
            }
#pragma warning restore            
        }
    }
}