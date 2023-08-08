namespace Cloud
{
    public static class BackupManager
    {
        public static void Initialize(string sourceDir)
        {
            AntiGithub = new AntiGitLibrary.Context(SetAntiGitAlert, false, Static.Port, sourceDir);
        }
        public static Action<Exception?>? AntiGitAlertAction { get; set; }
        public static string? AntiGitAlert { get; set; }
        public static void SetAntiGitAlert(Exception Alert)
        {
            // Check if is antivirus alert
            if (Alert != null)
            {
                if (Alert.HResult == -2147024671)
                    Static.Client?.AddAntivirusWarning(Alert.Message, null);
                else
                    Static.Client?.AddFileError(Alert.Message, null);
            }
            AntiGitAlert = Alert == null ? null : Alert.Message;
            AntiGitAlertAction?.Invoke(Alert);
        }
        static private AntiGitLibrary.Context AntiGithub;
        public static string Source { get => AntiGithub.SourceDir; set => AntiGithub.SourceDir = value; }
        public static string Target { get => AntiGithub.TargetDir; set => AntiGithub.TargetDir = value; }
        public static string Git { get => AntiGithub.GitDir; set => AntiGithub.GitDir = value; }
        public static string StartBackup()
        {
            var outcome = AntiGithub.StartBackup(true);
            return outcome.ToString();
        }
        public static bool BackupIsRunning => AntiGithub.DailyBckupIsRunning;
        public static void StopSyncGit() => AntiGithub.StopSyncGit();

    }
}
