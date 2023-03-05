namespace Cloud
{
    public static class Util
    {
        public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                RecordError(exception);
            }
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
