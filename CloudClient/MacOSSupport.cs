using System;
using System.Diagnostics;
using MonoMac.AppKit;
using MonoMac.Foundation;
namespace CloudClient
{
    class MacOSSupport
    {
        private static NSStatusItem statusItem;
        static internal void SetOSXStatusIcon(string cloudPath, Action openUI)
        {
            try
            {

                if (statusItem != null)
                    return;
                NSApplication.Init();
                NSApplication.SharedApplication.InvokeOnMainThread(() =>
                {

                    NSApplication.SharedApplication.ActivationPolicy = NSApplicationActivationPolicy.Accessory;

                    // Crea l'icona di status
                    var icon = LoadImage(Client.IconStatus.Default);
                    statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(31);
                    statusItem.Image = icon;
                    statusItem.ToolTip = "Cloud storage";

                    // Crea il menu
                    var menu = new NSMenu();
                    if (openUI != null)
                        menu.AddItem(new NSMenuItem("Settings", (sender, e) => openUI.Invoke()));
                    menu.AddItem(new NSMenuItem("Open folder", (sender, e) =>
                    {
                        if (!System.IO.Directory.Exists(cloudPath) || System.IO.File.GetAttributes(cloudPath).HasFlag(System.IO.FileAttributes.ReadOnly))
                            ShowPopup("Cloud", "Cloud area has been blocked!");
                        else
                            NSWorkspace.SharedWorkspace.OpenFile(cloudPath);
                    }));
                    statusItem.Menu = menu;

                    try
                    {

                        NSApplication.SharedApplication.Run();
                    }
                    catch (Exception ex)
                    {

                    }
                });

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore: {ex.Message}");
            }
        }

        static void ShowPopup(string title, string message)
        {
            string appleScript = $"display dialog \"{message}\" with title \"{title}\" buttons {{\"OK\"}} default button \"OK\"";
            var processInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e \"{appleScript}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                process.WaitForExit();
            }
        }

        static public void UpdateStatusIcon(Client.IconStatus newStatus)
        {
            try
            {
                NSApplication.SharedApplication.InvokeOnMainThread(() =>
                {

                    var newIcon = LoadImage(newStatus);
                    if (newIcon != null)
                    {
                        statusItem.Image = newIcon;
                    }
                    else
                    { }
                });
            }
            catch (Exception ex)
            {
            }
        }
        static NSImage LoadImage(Client.IconStatus iconStatus)
        {
            var resourceName = "CloudClient.Resources.CloudIcon" + iconStatus.ToString() + ".png";
            return LoadImage(resourceName);
        }

        static NSImage LoadImage(string resourceName)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Resource '{resourceName}' not found.");

                var data = NSData.FromStream(stream);
                return new NSImage(data);
            }
        }
    }
}
