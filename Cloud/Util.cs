﻿using AppSync;
using static System.Net.Mime.MediaTypeNames;

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
        public static readonly string CurrentPublicationPath = Path.Combine(new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.FullName, @"Release\net6.0\publish");

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

        /// <summary>
        /// Event that is executed when the application crashes, to create a log on file of the event, useful for diagnostics, and restarts the application after it has gone into error
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">Unhandled exception event args</param>
        public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
#if RELEASE
            if (e.ExceptionObject is Exception exception)
            {
                RecordError(exception);
            }
            // Restart application after crash
            Thread.Sleep(600000); // 10 minutes
            if (Environment.ProcessPath != null)
                System.Diagnostics.Process.Start(Environment.ProcessPath);
# endif
        }

        /// <summary>
        /// Record an error in the local log (thus creating an error log useful for diagnostics)
        /// </summary>
        /// <param name="error">Exception to log</param>
        public static void RecordError(Exception error)
        {
            lock (Environment.OSVersion)
            {
                try
                {
                    var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(RecordError)); ;
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    File.WriteAllText(Path.Combine(path, DateTime.UtcNow.Ticks.ToString("X") + "_" + error.HResult + ".txt"), error.ToString());
                    var files = (new DirectoryInfo(path)).GetFileSystemInfos("*.txt");
                    var orderedFiles = files.OrderBy(f => f.CreationTime).Reverse().ToArray();
                    // Keep 1024 errors
                    for (var index = 1024; index < orderedFiles.Length; index++)
                    {
                        var file = orderedFiles[index];
                        file.Delete();
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }
}
