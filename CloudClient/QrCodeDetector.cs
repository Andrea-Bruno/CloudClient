using CloudSync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace CloudClient
{
    public static class QrCodeDetector
    {
        public static string QrDetected { get; private set; }
        private static readonly List<Action<string>> OnDetected = new List<Action<string>>();
        private static bool DetectQrCodeInProgress;
        public static bool _StopDetectQrCode;
        public static bool DisallowDetectQrCode { get { return _StopDetectQrCode; } set { _StopDetectQrCode = value; if (value == true) OnDetected.Clear(); } }
        public static void DetectQrCode(Action<string> onDetected)
        {
            if (DisallowDetectQrCode) return;
            lock (OnDetected)
            {
                OnDetected.Add(onDetected);
            }
            if (DetectQrCodeInProgress == false)
            {
                DetectQrCodeInProgress = true;
                if (QrDetected != null)
                {
                    lock (OnDetected)
                    {
                        OnDetected.ToList().ForEach(o => o.Invoke(QrDetected));
                        OnDetected.Clear();
                    }
                    return;
                }
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var localIp = host.AddressList.ToList().Find(ip => ip.IsLocalIPAddress());
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
                                                    lock (OnDetected)
                                                    {
                                                        OnDetected.ForEach(o => o.Invoke(QrDetected));
                                                        OnDetected.Clear();
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
                        } while (!DisallowDetectQrCode && string.IsNullOrEmpty(QrDetected));
                        DetectQrCodeInProgress = false;
                    }).Start();
                }
            }
        }
    }
}
