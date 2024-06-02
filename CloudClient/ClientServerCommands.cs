using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CloudSync;

namespace CloudClient
{
    /// <summary>
    /// Set of commands that allow you to carry out some operations on the cloud and access special functions
    /// </summary>
    public partial class Client : CloudBox.CloudBox
    {
        public static readonly Dictionary<Command, uint> Counter = new Dictionary<Command, uint>(); // used for statistical purposes(saves numbers of executed commands)
        /// <summary>
        /// Generally this method is called when the server responds to a command
        /// </summary>
        /// <param name="userId">Id of the client which sent command </param>
        /// <param name="responseToCommand"> command sent by the client (SaveData, LoadData, LoadAllData, DeleteData, PushNotification)</param>
        /// <param name="parameters"> list of parameters needed for the command:
        public void OnServerCommand(ulong userId, Command responseToCommand, List<byte[]> parameters)
        {
            if (!Counter.ContainsKey(responseToCommand))
                Counter[responseToCommand] = 0;
            Counter[responseToCommand] += 1;
            lock (this)
            {
                try
                {
                    switch (responseToCommand)
                    {
                        case Command.GetEncryptedQR:
                            var encryptedPubKey = parameters[0];
                            var serverPublicKey = CloudBox.EncryptionXorAB.Decrypt(EncryptedQR.Item2, encryptedPubKey);
                            EncryptedQR = null;
                            ConnectToServer(serverPublicKey.ToBase64());
                            // CloudBox.Login(qrCode.ToBase64(), CloudBox.Context.SecureStorage.Values.Get("pin", null));
                            break;
                        case Command.GetSSHAccess:
                            var ToExecute = parameters[0];
                            // var password = BitConverter.ToString(parameters[1]).Replace("-", "");
                            var password = parameters[1].ToHex();

                            password = "testpassword";

                            var localIP = parameters[2];
                            var publicIP = parameters[3];
                            if (ToExecute.Length > 1) // if is 1 byte, then is a error message
                            {
                                var programToExecute = Encoding.UTF8.GetString(ToExecute);
                                bool isLocal = false;
                                bool isLocalhost = false;
                                try
                                {
                                    if (new IPAddress(publicIP).ToString() == Util.GetPublicIpAddress()?.ToString())
                                    {
                                        var myLocalIp = Util.GetLocalIpAddress().GetAddressBytes();
                                        if (localIP[0] == myLocalIp[0] && localIP[1] == myLocalIp[1] && localIP[2] == myLocalIp[2] && localIP[3] == myLocalIp[3])
                                            isLocal = true;
                                        else
                                            isLocalhost = true;
                                    }
                                }
                                catch (Exception) { }
                                try
                                {
                                    var connectTo = isLocalhost ? IPAddress.Loopback.GetAddressBytes() : isLocal ? localIP : publicIP;
                                    var ipToConnect = new IPAddress(connectTo).ToString();
                                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                                    {
                                        var puttyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extra");
                                        // var putty = Path.Combine(puttyPath, "putty.exe");
                                        // var fileName = "putty.exe";

                                        var fileName = "plink.exe";
                                        var putty = Path.Combine(puttyPath, fileName);
                                        var xming = Path.Combine(puttyPath, "Xming.exe");
                                        var run = Path.Combine(puttyPath, "run.txt");
                                        File.WriteAllText(run, programToExecute);
                                        if (File.Exists(putty) && File.Exists(xming))
                                        {
                                            if (Process.GetProcessesByName("Xming").Length == 0)
                                            {
                                                Process.Start(xming, ":0 -clipboard -multiwindow -dpi 108");
                                            }
                                            var process = new Process();
                                            process.StartInfo.EnvironmentVariables["PATH"] = puttyPath;
                                            process.StartInfo.FileName = fileName;                                           
                                            process.StartInfo.Arguments = "-batch -ssh " + " -X -pw " + password + " " + Context.My.Id + "@" + ipToConnect + " \"" + programToExecute + "\"";
                                            //process.StartInfo.Arguments = "-ssh " + Context.My.Id + "@" + ipToConnect + " -X -pw " + password + " -m " + "\"" + run + "\"";
                                            //process.StartInfo.Arguments = "-ssh " + ipToConnect + " -X -pw " + password + " -l " + Context.My.Id.ToString() + " -m " + "\"" + run + "\"";
                                            // #if RELEASE
                                            //process.StartInfo.RedirectStandardOutput = true;
                                            //process.StartInfo.RedirectStandardError = true;
                                            process.StartInfo.UseShellExecute = false;
                                            process.StartInfo.CreateNoWindow = true;
                                            //process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                            // #endif
                                            process.Start();
                                        }
                                    }

                                }
                                catch (Exception) { }
                            }
                            else
                            {
                                OnCommandFeedback?.Invoke(responseToCommand, "Application not supported in the cloud");
                            }
                            GetSSHAccessSemaphore.Set();
                            break;
                        case Command.GetSupportedApps:
                            _SupportedApps = Encoding.UTF8.GetString(parameters[0]).Split('\t');
                            GetSupportedAppsSemaphore.Set(); // unlock semaphore
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // There is probably an error with system dependent files (files open or access denied)
                    Debug.WriteLine(ex.Message);
                }
            }
        }

        public Action<Command, string> OnCommandFeedback { get; set; }

        // ============== StartProgram ===========================

        /// <summary>
        /// Requires SSH access to the cloud to run a specific program locally
        /// </summary>
        /// <param name="programToExecute">Name of program to execute</param>
        public void StartApplication(string programToExecute)
        {
            if (SendCommand(ServerCloud, Command.GetSSHAccess, new[] { Encoding.UTF8.GetBytes(programToExecute) }))
            {
                GetSSHAccessSemaphore = new AutoResetEvent(false);
                if (!GetSSHAccessSemaphore.WaitOne(10000))
                    throw new Exception("Timeout error: The cloud is probably unreachable!");
            }
            else
            {
                throw new Exception("The client is not connected to the cloud!");
            }
        }
        private AutoResetEvent GetSSHAccessSemaphore;


        // ============== GetSupportedApps ===========================

        private bool GetSupportedApps() => SendCommand(ServerCloud, Command.GetSupportedApps, null);

        private AutoResetEvent GetSupportedAppsSemaphore;
        private string[] _SupportedApps;
        public string[] SupportedApps
        {
            get
            {
                if (_SupportedApps != null)
                    return _SupportedApps;
                else
                {

                    if (GetSupportedApps())
                    {
                        GetSupportedAppsSemaphore = new AutoResetEvent(false);
                        GetSupportedAppsSemaphore.WaitOne(10000);
                    }
                }
                return _SupportedApps;
            }
        }

    }
}