using static System.Net.Mime.MediaTypeNames;

namespace Cloud
{
    public static class Util
    {
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
            Thread.Sleep(60000);
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
