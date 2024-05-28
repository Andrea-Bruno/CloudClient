using System.Dynamic;

namespace Cloud.Features
{
    /// <summary>
    /// Panel for launching cloud applications in local terminal mode
    /// </summary>
    public static class Applications
    {
        static Applications()
        {

        }

        /// <summary>
        /// Select a cloud application to run in a local virtual environment
        /// </summary>
        public static string[]? TheApplicationToRun => Static.Client?.SupportedApps;

        /// <summary>
        /// Run the selected application
        /// </summary>
        private static void OnSelectApplicationToRun(int id)
        {
                SelectedApplication = TheApplicationToRun?[id];                
        }

        /// <summary>
        /// Virtually run the application on the cloud. The execution leaves no traces on the local computer, technically you are working directly in the cloud, without performing any operations on the local machine.
        /// </summary>
        public static void ExecuteApplication()
        {
            if (Static.Client == null || !Static.Client.IsConnected)
                throw new Exception("The client is not connected to the cloud!");
            Static.Client.StartApplication(SelectedApplication);
        }
        private static string? SelectedApplication;    
    }
}
