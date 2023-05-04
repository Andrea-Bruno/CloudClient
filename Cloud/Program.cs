using Cloud;
using System.Diagnostics;
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
Static.CloudPath = @"C:\Test";
#endif


Static.EntryPoint = (string)configuration.GetValue(typeof(string), "EntryPoint", null); // Used for release
Static.Port = (string)configuration.GetValue(typeof(string), "Port", null); // Used for release
                                                                          
AutoStart.SetAutoStartByActivity();

#if RELEASE
if (Static.EntryPoint != null && Static.EntryPoint.Contains("test")) { Console.WriteLine("WARNING: Test entry point in use: Change entry point in application settings before deployment!"); };
#endif

var lastEntryPoint = CloudBox.CloudBox.LastEntryPoint();
if (lastEntryPoint != null)
{
    Static.CreateClient(lastEntryPoint);
}

BackupManager.Initialize(Static.CloudPath);


// Functions.ExecuteCommand("cmd.exe", "/C time " + "6:10", false);

if (!string.IsNullOrEmpty(Static.Port))
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:" + Static.Port);
var url = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(";").First();
if (lastEntryPoint == null || Debugger.IsAttached)
{
    // Open the browser
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        EncryptedMessaging.Functions.ExecuteCommand("cmd.exe", "/C " + "start /max " + url, true);
    if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)))
    {
        File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Cloud Settings " + Static.Port + ".htm"), @"<HEAD><META http-equiv=""refresh"" content=""1;" + url + @"""></HEAD>");
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

app.Run(url);
