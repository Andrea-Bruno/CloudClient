using Cloud;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

AppDomain.CurrentDomain.UnhandledException += CloudSync.Util.UnhandledException; //it catches application errors in order to prepare a log of the events that cause the crash

var currentFileInstance = Process.GetCurrentProcess()?.MainModule?.FileName;
var currentProcessId = Process.GetCurrentProcess()?.Id;

// It doesn't work with Macintosh, it hasn't been tested with Linux
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).ToList().ForEach(process =>
    {
        if (string.Equals(process.MainModule?.FileName, currentFileInstance, StringComparison.InvariantCultureIgnoreCase))
        {
            if (process.Id != currentProcessId)
            {
                Debugger.Break();
                Console.WriteLine("The application is already running");
                Environment.Exit(1);
                return;
            }
        }
    });
}


Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // The UI fails if you launch the app from an external path without this command line
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

var configuration = app.Configuration;

Static.CloudPath = CloudBox.CloudBox.GetCloudPath((string)configuration.GetValue(typeof(string), "CloudPath", null), false);

#if DEBUG
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    Static.CloudPath = @"C:\Test4";
//else
//    Static.CloudPath = @"/home/user/test";
//Static.CloudPath = @"C:\Users\andre\OneDrive";
//Static.CloudPath = @"C:\Users\andre\OneDrive - Copy";
#endif

Static.EntryPoint = (string)configuration.GetValue(typeof(string), "EntryPoint", null); // Used for release
Static.Port = int.Parse((string)configuration.GetValue(typeof(string), "Port", null)); // Used for release
var address = "http://localhost:" + Static.Port;
app.Urls.Add(address);
Static.Storage = new SecureStorage.Storage(address);

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    var cloudPath = new DirectoryInfo(Static.CloudPath);
    if (!cloudPath.Exists || cloudPath.LinkTarget != null)
    {
        var virtualDiskRepository = Path.Combine(new string[] { Environment.SystemDirectory, "$Sys" });
        var hash = CloudSync.Util.HashFileName(Static.CloudPath, true).GetBytes().ToHex();
        Static.VirtualDiskFullFileName = Path.Combine(virtualDiskRepository, hash + ".vhdx");
        if (!Directory.Exists(virtualDiskRepository) || !File.Exists(Static.VirtualDiskFullFileName) && !File.Exists(Path.ChangeExtension(Static.VirtualDiskFullFileName, ".sys")))
        {
            SystemExtra.Util.CreateVirtualDisk(Static.VirtualDiskFullFileName, Static.CloudPath, true);
        }
        else
        {
            if (!Static.VirtualDiskIsMounted)
            {
                SystemExtra.Util.MountVirtualDisk(Static.VirtualDiskFullFileName, Static.CloudPath);
            }
        }
    }
}

#if RELEASE
if (SystemExtra.Util.GetAutoStart() == false)
    SystemExtra.Util.SetAutoStart(true, Static.Port);
if (Static.EntryPoint != null && Static.EntryPoint.Contains("test")) { Console.WriteLine("WARNING: Test entry point in use: Change entry point in application settings before deployment!"); };
#endif

var lastEntryPoint = CloudBox.CloudBox.LastEntryPoint();
if (lastEntryPoint != null)
{
    Static.CreateClient(lastEntryPoint);
}

BackupManager.Initialize(Static.CloudPath);

Func<bool> PortIsAvailable = () =>
{
    IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
    TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
    foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
    {
        if (tcpi.LocalEndPoint.Port == Static.Port)
        {
            Thread.Sleep(1000);
            return false;
        }
    }
    return true;
};

Func<bool> NotPortIsAvailable = () => !PortIsAvailable();

if (lastEntryPoint == null || Debugger.IsAttached)
{
    // Open the browser
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    Task.Run(() =>
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SystemExtra.Util.ExecuteCommand("cmd.exe", "/C " + "start /max " + address);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (!SpinWait.SpinUntil(NotPortIsAvailable, TimeSpan.FromSeconds(180)))
            {
                Debugger.Break();
                throw new Exception("The web interface is not working!");
            }
            SystemExtra.Util.ExecuteCommand("open", address);
        }
    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed


    if (Directory.Exists(CloudSync.Util.DesktopPath()))
    {
        File.WriteAllText(Path.Combine(CloudSync.Util.DesktopPath(), "Cloud Settings " + Static.Port + ".htm"), @"<HEAD><META http-equiv=""refresh"" content=""1;" + address + @"""></HEAD>");
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



if (!SpinWait.SpinUntil(PortIsAvailable, TimeSpan.FromSeconds(180)))
{
    Debugger.Break();
    throw new Exception("The port" + Static.Port + "is busy!");
}

app.Run();
