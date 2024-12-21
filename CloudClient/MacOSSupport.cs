using System;
using MonoMac.AppKit;
using MonoMac.Foundation;
namespace CloudClient
{
    class MacOSSupport
    {
        private static NSStatusItem statusItem;
        static public void SetOSXStatusIcon(string cloudPath, Action openUI)
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
                    menu.AddItem(new NSMenuItem("Open folder", (sender, e) => NSWorkspace.SharedWorkspace.OpenFile(cloudPath)));
                    statusItem.Menu = menu;



                    try
                    {

                        NSApplication.SharedApplication.Run();
                    }
                    catch (Exception ex)
                    {

                    }

                    // NSApplication.SharedApplication.InvokeOnMainThread(() => NSApplication.SharedApplication.Run());

                });




            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore: {ex.Message}");
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
