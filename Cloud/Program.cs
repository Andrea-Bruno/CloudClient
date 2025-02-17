using Cloud;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;

AppDomain.CurrentDomain.UnhandledException += CloudSync.Util.UnhandledException; //it catches application errors in order to prepare a log of the events that cause the crash
AppDomain.CurrentDomain.ProcessExit += (s, e) => Static.SemaphoreCreateClient.Set(); // Unlock semaphore in exit request
//AppDomain.CurrentDomain.ProcessExit += new EventHandler(CloudSync.Util.RestartApplication); //restart application on end;
// SystemExtra.Util.Notify("Test", "Hello word!");
Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // The UI fails if you launch the app from an external path without this command line


if (!SystemExtra.Util.IsAdmin())
{
#if DEBUG
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        // From MacOS run VS or Rider by the follow command line:
        // sudo /Applications/Visual\ Studio.app/Contents/MacOS/VisualStudio
        // sudo /Applications/Rider.app/Contents/MacOS/rider
        Debugger.Break();
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        // From Linux in wsl edit the file /etc/wsl.conf: (nano /etc/wsl.conf) and add:
        // [user]
        // default = root
        Debugger.Break();
    }
#endif
    var errorRole = "The application requires the administrator role!";
    Console.WriteLine(errorRole);
    CloudSync.Util.RecordError(new Exception(errorRole));
    CloudSync.Util.DisallowRestartApplicationOnEnd = false;
    Environment.Exit(1);
    return;
}


if (!SpinWait.SpinUntil(() => !SystemExtra.Util.AppIsAlreadyRunning(), TimeSpan.FromSeconds(5)))
{
    Debugger.Break();
    Console.WriteLine("The application is already running!");
    CloudSync.Util.DisallowRestartApplicationOnEnd = false;
    Environment.Exit(1);
    return;
}


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
#endif

if (!new FileInfo(Static.CloudPath).Directory.Exists)
{
    CloudBox.CloudBox.ResetAppData();
    throw new Exception("ERROR: Invalid cloud path!"); // Restart!
}

Static.EntryPoint = (string)configuration.GetValue(typeof(string), "EntryPoint", null);

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(';');

if (!Debugger.IsAttached)
{
    var settingsUrls = (string)configuration.GetValue(typeof(string), "Urls", null); // Used for release
    if (urls == null)
    {
        urls = settingsUrls?.Split(';');
        if (urls != null)
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", string.Join(';', urls));
    }
}


Debug.Assert(urls != null, nameof(urls) + " != null");
var firstUrlParts = urls[0].Split(':');
Static.Port = int.Parse(firstUrlParts[2]);
//#if DEBUG
//// Increment port of +1 in debug mode
//Static.Port++;
//firstUrl[2] = Static.Port.ToString();
//urls[0] = string.Join(':', firstUrl);
//Environment.SetEnvironmentVariable("ASPNETCORE_URLS", string.Join(';', urls));
//#endif
Static.UIAddress = urls[0];
foreach (var url in urls)
{
    app.Urls.Add(url);
}
Static.Storage = new SecureStorage.Storage(Static.UIAddress);

var virtualDisk = (bool)configuration.GetValue(typeof(bool), "VirtualDisk", false);
var cloudPath = new DirectoryInfo(Static.CloudPath);
if (virtualDisk)
{
    var virtualDiskRepository = "";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        virtualDiskRepository = "/Volumes";
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        virtualDiskRepository = Environment.SystemDirectory;
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        virtualDiskRepository = "/var/lib";
    virtualDiskRepository = Path.Combine([virtualDiskRepository, ".$Sys"]);
    var hashPath = CloudSync.Util.HashFileName(Static.CloudPath, true).GetBytes().ToHex();
    var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".vhdx" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".sparsebundle" : ".encrypted";
    VirtualDiskManager.VirtualDiskFullFileName = Path.Combine(virtualDiskRepository, hashPath + extension);
    static bool Exists(string fullName) => File.Exists(fullName) || Directory.Exists(fullName);
    var virtualDiskFileInfo = new FileInfo(VirtualDiskManager.VirtualDiskFullFileName);
    var vdPassword = VirtualDiskManager.VirtualDiskPassword;
    if (!Exists(VirtualDiskManager.VirtualDiskFullFileName) && !Exists(Path.ChangeExtension(VirtualDiskManager.VirtualDiskFullFileName, ".sys")))
    {
        // Create Encrypted Virtual Disk
        VirtualDiskManager.CreateVirtualDisk(vdPassword);
    }
    else if (!VirtualDiskManager.VirtualDiskIsMounted && !VirtualDiskManager.VirtualDiskIsLocked)
    {
        // Mount Encrypted Virtual Disk
        if (vdPassword == null && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Debugger.Break(); // No password configured!  Investigate!
        else
            SystemExtra.Util.MountVirtualDisk(VirtualDiskManager.VirtualDiskFullFileName, Static.CloudPath, vdPassword);
    }
}
else
{
    cloudPath.Create();
    cloudPath.Refresh();
    if (!cloudPath.Exists)
    {
        throw new Exception("Invalid cloud path: '" + cloudPath.FullName + "'");
    }
}

#if RELEASE
if (Static.EntryPoint != null && Static.EntryPoint.Contains("test")) { Console.WriteLine("WARNING: Test entry point in use: Change entry point in application settings before deployment!"); };

if (SystemExtra.Util.IsService(Static.Port) ==  false)
{
    // is a first startup of application - then, set the service
    SystemExtra.Util.SetAutoStart(true, Static.Port);
    Static.NotifyUIAddress();
    Console.WriteLine(Static.NotifyUIAddressMessage);
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        CloudSync.Util.DisallowRestartApplicationOnEnd = false;
        Environment.Exit(0);
        return;
    }
}
#endif

string? lastEntryPoint = CloudBox.CloudBox.LastEntryPoint();
if (lastEntryPoint != null)
{
    Static.CreateClient(lastEntryPoint);
}

BackupManager.Initialize(Static.CloudPath);

Func<bool> portIsAvailable = () =>
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


if (!SpinWait.SpinUntil(portIsAvailable, TimeSpan.FromSeconds(180)))
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
            if (!SpinWait.SpinUntil(() => !portIsAvailable(), TimeSpan.FromSeconds(180)))
            {
                Debugger.Break();
                throw new Exception("Web interface not found on port " + Static.Port);
            }
            else
            {
                Static.OpenUI?.Invoke();
            }
        });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed


        if (Directory.Exists(CloudSync.Util.DesktopPath()))
        {
            try
            {
                var htmlFile = Path.Combine(CloudSync.Util.DesktopPath(), "Cloud Settings " + Static.Port + ".htm");
                var html = File.ReadAllText("redirect.html").Replace("{address}", Static.UIAddress);
                File.WriteAllText(htmlFile, html);
                SystemExtra.Util.Chmod(777, htmlFile);
                var desktopUser = SystemExtra.Util.CurrentUIUser();
                var userInfo = SystemExtra.Util.GetUserInfo(desktopUser);
                if (userInfo != null)
                {
                    SystemExtra.Util.Chown(htmlFile, userInfo.Value.pw_uid, userInfo.Value.pw_gid);
                }
            }
            catch (Exception ex)
            {
            }
        }
    }
}


new Thread(() => app.Run()).Start();

if (Static.Client == null)
    Static.SemaphoreCreateClient.WaitOne();
Static.Client?.EnableOSFeatures(Static.OpenUI);