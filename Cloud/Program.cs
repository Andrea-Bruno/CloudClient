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
Static.EntryPoint = (string)configuration.GetValue(typeof(string), "EntryPoint", null); ; // Used for release
#if RELEASE
if (Static.EntryPoint != null && Static.EntryPoint.Contains("test")) { Console.WriteLine("WARNING: Test entry point in use: Change entry point in application settings before deployment!"); };
#endif

//var h1 = CloudSync.Util.HashFileName("Download/Sorgenti/Cloud/ClientMobile/cloud-storage-main/node_modules/unimodules-app-loader/android/build/intermediates/aapt_friendly_merged_manifests/debug/aapt/AndroidManifest.xml", false);
//var h2 = CloudSync.Util.HashFileName("Download/Sorgenti/Cloud/ClientMobile/cloud-storage-main/node_modules/unimodules-app-loader/android/build/intermediates/compiled_local_resources/debug/out", true);

var lastEntryPoint = CloudBox.CloudBox.LastEntryPoint();
if (lastEntryPoint != null)
{
    Static.CreateClient(lastEntryPoint);
}

BackupManager.Initialize(CloudBox.CloudBox.GetCloudPath(Static.CloudPath, false));


// Functions.ExecuteCommand("cmd.exe", "/C time " + "6:10", false);

#if RELEASE
// Open the browser
var url = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(";").First();
if (url != null)
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))

        EncryptedMessaging.Functions.ExecuteCommand("cmd.exe", "/C " + "start /max " + url, true);
#endif

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
