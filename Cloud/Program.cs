using Cloud;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;


AppDomain.CurrentDomain.UnhandledException += CloudSync.Util.UnhandledException; //it catches application errors in order to prepare a log of the events that cause the crash

if (!SystemExtra.Util.IsAdmin())
{
    // From MacOS run VS by the follow command line:
    // sudo /Applications/Visual\ Studio.app/Contents/MacOS/VisualStudio
    throw new Exception("The application requires administrator role to work!");
}

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
Static.UIAddress = "http://localhost:" + Static.Port;
app.Urls.Add(Static.UIAddress);
Static.Storage = new SecureStorage.Storage(Static.UIAddress);

var VirtualDisk = (bool)configuration.GetValue(typeof(bool), "VirtualDisk", false);
if (VirtualDisk && (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
{
    var cloudPath = new DirectoryInfo(Static.CloudPath);
    if (!cloudPath.Exists || cloudPath.LinkTarget != null)
    {
        var virtualDiskRepository = Path.Combine(new string[] { RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "/Volumes" : Environment.SystemDirectory, ".$Sys" });
        var hash = CloudSync.Util.HashFileName(Static.CloudPath, true).GetBytes().ToHex();
        var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".vhdx" : ".sparsebundle";
        Static.VirtualDiskFullFileName = Path.Combine(virtualDiskRepository, hash + extension);
        string? vdPassword = Static.VirtualDiskPassword();
        if (!Directory.Exists(virtualDiskRepository) || !File.Exists(Static.VirtualDiskFullFileName) && !File.Exists(Path.ChangeExtension(Static.VirtualDiskFullFileName, ".sys")))
        {
            SystemExtra.Util.CreateVirtualDisk(Static.VirtualDiskFullFileName, Static.CloudPath, vdPassword, true);
        }
        else
        {
            if (!Static.VirtualDiskIsMounted)
            {
                SystemExtra.Util.MountVirtualDisk(Static.VirtualDiskFullFileName, Static.CloudPath, vdPassword);
            }
        }
    }
}

#if RELEASE
if (SystemExtra.Util.GetAutoStart() == false)
    SystemExtra.Util.SetAutoStart(true, Static.Port);
if (Static.EntryPoint != null && Static.EntryPoint.Contains("test")) { Console.WriteLine("WARNING: Test entry point in use: Change entry point in application settings before deployment!"); };
#endif

string? lastEntryPoint = CloudBox.CloudBox.LastEntryPoint();
if (lastEntryPoint != null)
{
    Static.CreateClient(lastEntryPoint);
}

BackupManager.Initialize(Static.CloudPath);

Func<bool> PortIsAvailable = () =>
{
    try
    {
        using TcpClient client = new TcpClient("localhost", Static.Port);
        Thread.Sleep(500);
        return false;
    }
    catch (SocketException)
    {
        return true;
    }
};


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
    throw new Exception("The port " + Static.Port + " is busy!");
}
else
{
    if (lastEntryPoint == null || Debugger.IsAttached)
    {
        // Open the browser
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        Task.Run(() =>
        {
            if (!SpinWait.SpinUntil(() => !PortIsAvailable(), TimeSpan.FromSeconds(180)))
            {
                Debugger.Break();
                throw new Exception("Web interface not found on port " + Static.Port);
            }
            else
            {
                Static.OpenUI.Invoke();
            }
        });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed


        if (Directory.Exists(CloudSync.Util.DesktopPath()))
        {
            try
            {
                var html = File.ReadAllText("redirect.html").Replace("{address}", Static.UIAddress);
                File.WriteAllText(Path.Combine(CloudSync.Util.DesktopPath(), "Cloud Settings " + Static.Port + ".htm"), html);
            }
            catch (Exception ex)
            {
            }
        }
    }
}


new Thread(() =>
{
    app.Run();
}).Start();

if (Static.Client == null)
    Static.SemaphoreCreateClient.WaitOne();
Static.Client?.EnableOSFeatures(Static.OpenUI);

