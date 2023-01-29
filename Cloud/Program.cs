using Cloud.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
//builder.Services.AddSingleton<WeatherForecastService>();

var app = builder.Build();

var configuration = app.Configuration;

string cloudPath = (string)configuration.GetValue(typeof(string), "CloudPath", null);
string entryPoint = (string)configuration.GetValue(typeof(string), "EntryPoint", null); ; // Used for release
#if RELEASE
if (entryPoint != null && entryPoint.Contains("test")) { Console.WriteLine("WARNING: Test entry point in use: Change entry point in application settings before deployment!"); };
#endif

var CloudClient = new CloudBox.CloudBox(entryPoint, cloudPath);
    
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
