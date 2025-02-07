using AppSync;
using System.Runtime.InteropServices;

namespace Cloud
{
    public static class Util
    {
        /// <summary>
        /// The address of the server that acts as a repository for new versions of applications (new versions are published to this address and apps that need to be updated get the new version from here)
        /// </summary>
        private const string DefaultUpdateUrl = "http://update.tc0.it:5050";

        /// <summary>
        /// The local location of the application package ready to be published.
        /// Explanation: The developer can publish updates of this app to a public repository in order to distribute them. This is the location of the application ready for distribution.
        /// </summary>
        public static readonly string CurrentPublicationPath = Path.Combine(new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.FullName, "Release", "net" + GetFrameworkVersion(), "publish");


        static string GetFrameworkVersion()
        {
            string frameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            string version = frameworkDescription.Split(' ')[1]; 
            string majorMinorVersion = string.Join('.', version.Split('.').Take(2));
            return majorMinorVersion;
        }

        /// <summary>
        /// Returns true if a new package was recently created.
        /// </summary>
        /// <returns>True if the package for publishing is ready</returns>
        public static bool PackageIsReady()
        {
            if (Directory.Exists(CurrentPublicationPath))
            {
                var exe = Directory.GetFiles(CurrentPublicationPath, "*.exe").First();
                var exeInfo = new FileInfo(exe);
                if (exeInfo.Exists)
                {
                    if ((DateTime.UtcNow - exeInfo.LastAccessTimeUtc).TotalMinutes < 30) // Check if the last build is recent
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check for updates & update the current application with the latest version published in the current store if is necessary
        /// </summary>
        /// <returns></returns>
        static public string UpdateApplication() => Update.CheckAndUpdate(DefaultUpdateUrl, Static.CanUpdate);

        /// <summary>
        /// Start a timer that periodically checks for app updates
        /// </summary>
        static public void MonitorUpdates() => Update.MonitoringUpdates(DefaultUpdateUrl, Static.CanUpdate);

        /// <summary>
        /// Publish the current application package in the private store in order to distribute an update
        /// </summary>
        public static void PublishCurrentApplication() => Prepare.PublishCurrentApplication(DefaultUpdateUrl, publicationPath: CurrentPublicationPath);
    }
}
