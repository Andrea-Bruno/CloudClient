namespace Cloud.Features
{
    /// <summary>
    /// File sharing
    /// </summary>
    public static class Share
    {
        /// <summary>
        /// Locate a file to share
        /// </summary>
        /// <param name="searchPattern">Partial of the file name to find. The characters * and ? are allowed as a joker.</param>
        /// <returns>Search result</returns>
        public static void Search(string searchPattern)
        {
            FileSelected = null;
            if (string.IsNullOrEmpty(searchPattern))
            {
                SearchResult = null;
            }
            else if (!".?*".ToArray().Any(x => searchPattern.Contains(x)))
            {
                searchPattern = "*" + searchPattern + "*";
            }
#if release
            if (Static.Client == null)
            {
                throw new Exception("The cloud has not been initialized!");
            }
#endif
            if (Static.CloudPath == null)
            {
                SearchResult = null;
            }
            else
            {
                var files = new List<string>();
                foreach (var item in Directory.EnumerateFiles(Static.CloudPath, searchPattern, new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                {
                    var relative = Path.GetRelativePath(Static.CloudPath, item);
                    files.Add(relative);
                };
                SearchResult = files.ToArray();
            }
        }

        /// <summary>
        /// Select a file to sharing from the search results
        /// </summary>
        public static string[]? SearchResult { get; private set; }


        /// <summary>
        /// Event that is executed when the user selects an item in the list from his UI
        /// </summary>
        /// <param name="recordIndex">The array index of the selected item</param>
        internal static void OnSelectSearchResult(int recordIndex)
        {
            FileSelected = SearchResult?[recordIndex];
        }

        /// <summary>
        /// File selected
        /// </summary>
        private static string? FileSelected { get; set; }

        /// <summary>
        /// Select the group to which you want to add the file to share (if the group already exists, or create a new group)
        /// </summary>
        public static string[]? Groups
        {
            get
            {
                return CloudSync.Share.GetGroups(Static.CloudPath);
            }
        }

        /// <summary>
        /// Event that is executed when the user selects an item in the list from his UI
        /// </summary>
        /// <param name="recordIndex">The array index of the selected item</param>
        internal static void OnSelectGroups(int recordIndex)
        {
            SharingGroup = Groups?[recordIndex];
        }

        /// <summary>
        /// If you want to create a new file group, indicate the group name here
        /// </summary>
        public static string? SharingGroup { get; set; }

        /// <summary>
        /// Add the file to the sharing group
        /// </summary>
        public static void ShareTheFile()
        {
            if (FileSelected == null && SearchResult?.Length == 1)
            {
                FileSelected = SearchResult.First();
            }
            CloudSync.Share.AddShareFile(Static.CloudPath, SharingGroup, FileSelected);
        }

        /// <summary>
        /// List of shared files: Click on the individual file to remove it
        /// </summary>
        public static string[]? SharedFiles
        {
            get
            {
                return CloudSync.Share.GetSharedFiles(Static.CloudPath, SharingGroup).ToArray();
            }
        }

        internal static void OnSelectSharedFiles(int recordIndex)
        {
            var toRemove = SharedFiles?[recordIndex];
            CloudSync.Share.RemoveSharedFile(Static.CloudPath, SharingGroup, toRemove);
        }

        /// <summary>
        /// Generate the file sharing link inherent to the group you have selected.
        /// Give this link to the person with whom you would like to share files belonging to this group!
        /// </summary>
        /// <returns></returns>
        public static string? GenerateSharingLink()
        {
            return CloudBox.Share.GenerateSharingLink(SharingGroup, Static.Client);
        }
    }
}
