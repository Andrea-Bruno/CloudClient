using Cloud;
using System.Diagnostics;
using System.Runtime.InteropServices;

if (!Debugger.IsAttached)
{
    // AutoStart.SetAutoStartByScript();
    AutoStart.SetAutoStartByActivity();
}

Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory); // The UI fails if you launch the app from an external path without this command linee
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
//builder.Services.AddSingleton<WeatherForecastService>();

var app = builder.Build();

var configuration = app.Configuration;

Static.CloudPath = (string)configuration.GetValue(typeof(string), "CloudPath", null);
Static.EntryPoint = (string)configuration.GetValue(typeof(string), "EntryPoint", null); // Used for release
var port = (string)configuration.GetValue(typeof(string), "Port", null); // Used for release

#if RELEASE
if (Static.EntryPoint != null && Static.EntryPoint.Contains("test")) { Console.WriteLine("WARNING: Test entry point in use: Change entry point in application settings before deployment!"); };
#endif

var lastEntryPoint = CloudBox.CloudBox.LastEntryPoint();
if (lastEntryPoint != null)
{
    Static.CreateClient(lastEntryPoint);
}

BackupManager.Initialize(CloudBox.CloudBox.GetCloudPath(Static.CloudPath, false));


// Functions.ExecuteCommand("cmd.exe", "/C time " + "6:10", false);

if (!string.IsNullOrEmpty(port))
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:" + port);
var url = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(";").First();
//#if RELEASE
if (lastEntryPoint == null || Debugger.IsAttached)
{
    // Open the browser
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        EncryptedMessaging.Functions.ExecuteCommand("cmd.exe", "/C " + "start /max " + url, true);
    if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Desktop))){
        File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Cloud Settings.htm"), @"<HEAD><META http-equiv=""refresh"" content=""1;" + url + @"""></HEAD>");
    }
}

//#endif

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
