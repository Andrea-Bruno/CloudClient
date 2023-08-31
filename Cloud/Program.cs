using Cloud;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;

AppDomain.CurrentDomain.UnhandledException += Util.UnhandledException; //it catches application errors in order to prepare a log of the events that cause the crash

Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // The UI fails if you launch the app from an external path without this command linee
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

var configuration = app.Configuration;

Static.CloudPath = CloudBox.CloudBox.GetCloudPath((string)configuration.GetValue(typeof(string), "CloudPath", null), false);

#if DEBUG
//Static.CloudPath =  @"C:\Test3";
Static.CloudPath = @"C:\Users\andre\OneDrive - Copy";
#endif

Static.EntryPoint = (string)configuration.GetValue(typeof(string), "EntryPoint", null); // Used for release
Static.Port = int.Parse((string)configuration.GetValue(typeof(string), "Port", null)); // Used for release
// Functions.ExecuteCommand("cmd.exe", "/C time " + "6:10", false);
var address = "http://localhost:" + Static.Port;
//if (!string.IsNullOrEmpty(Static.Port))
app.Urls.Add(address);
//Environment.SetEnvironmentVariable("ASPNETCORE_URLS", address);
//var url = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(";").First();
Static.Storage = new SecureStorage.Storage(address);

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    var cloudPath = new DirectoryInfo(Static.CloudPath);
    if (!cloudPath.Exists || cloudPath.LinkTarget != null)
    {
        var virtualDiskPepository = Path.Combine(new string[] { Environment.SystemDirectory, "$Sys" });
        var hash = CloudSync.Util.HashFileName(Static.CloudPath, true).GetBytes().ToHex();
        Static.VirtualDiskFullFileName = Path.Combine(virtualDiskPepository, hash + ".vhdx");
        if (!File.Exists(Static.VirtualDiskFullFileName))
        {
            SystemExtra.Util.CreateVirtualDisk(Static.VirtualDiskFullFileName, Static.CloudPath, true);
        }
        else
        {
            if (Static.LastMountVirtualDiskStatus)
            {
                SystemExtra.Util.MountVirtualDisk(Static.VirtualDiskFullFileName, Static.CloudPath);
            }
            //if (!SystemExtra.Util.IsMounted(Static.CloudPath, out bool _))
            //{

            //}
            //SystemExtra.Util.UnmountVirtualDisk(Static.VirtualDiskFullFileName);
        }
    }
}

var x = Environment.GetEnvironmentVariables();

#if RELEASE
if (SystemExtra.Util.GetAutoStart() == null)
    SystemExtra.Util.SetAutoStart(true, Static.Port);
if (Static.EntryPoint != null && Static.EntryPoint.Contains("test")) { Console.WriteLine("WARNING: Test entry point in use: Change entry point in application settings before deployment!"); };
#endif

var lastEntryPoint = CloudBox.CloudBox.LastEntryPoint();
if (lastEntryPoint != null)
{
    Static.CreateClient(lastEntryPoint);
}

BackupManager.Initialize(Static.CloudPath);

if (lastEntryPoint == null || Debugger.IsAttached)
{
    // Open the browser
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        //EncryptedMessaging.Functions.ExecuteCommand("cmd.exe", "/C " + "start /max " + address, true);
        EncryptedMessaging.Functions.ExecuteCommand("cmd.exe", "/C " + "start /max " + address);
    }
    if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)))
    {
        File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Cloud Settings " + Static.Port + ".htm"), @"<HEAD><META http-equiv=""refresh"" content=""1;" + address + @"""></HEAD>");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

Func<bool> PortIsAvailable = () =>
{
    IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
    TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
    foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
    {
        if (tcpi.LocalEndPoint.Port == Static.Port)
        {
            return false;
        }
    }
    return true;
};


if (!SpinWait.SpinUntil(PortIsAvailable, TimeSpan.FromSeconds(30)))
{
    Debugger.Break();
    throw new Exception("The port" + Static.Port + "is busy!");
}

app.Run();
