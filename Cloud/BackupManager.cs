namespace Cloud
{
    public static class BackupManager
    {
        //static BackupManager()
        //{
        //    AntiGithub.SourceDir = CloudBox.CloudBox.LastInstance.CloudPath;
        //}
        public static void Initialize(string sourceDir)
        {
            AntiGithub.SourceDir = sourceDir;
        }
        public static Action<string> Alert;
        static private AntiGitLibrary.Context AntiGithub = new AntiGitLibrary.Context(Alert, false);
        // static private AntiGitLibrary.Context AntiGithub = new AntiGitLibrary.Context(Alert, File.CreateSymbolicLink, false);
        public static string Source { get => AntiGithub.SourceDir; set => AntiGithub.SourceDir = value; }
        public static string Target { get => AntiGithub.TargetDir; set => AntiGithub.TargetDir = value; }
        public static string Git { get => AntiGithub.GitDir; set => AntiGithub.GitDir = value; }
        public static string StartBackup()
        {
            var outcome = AntiGithub.StartBackup(true);
            return outcome.ToString();
        }

        public static void StopSyncGit() => AntiGithub.StopSyncGit();

    }
}
