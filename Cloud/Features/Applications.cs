using System.Diagnostics;
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

        const string notConnected = "The client is not connected to the cloud!";
        const string timeoutError = "Timeout error: The cloud is currently unreachable!";


        /// <summary>
        /// Current status of the connection with the cloud
        /// </summary>
        [DebuggerHidden] // Don't break at throw
        public static string Status
        {
            get
            {
                if (timeout)
                    return Debugger.IsAttached ? timeoutError : throw new Exception(timeoutError);
                return Static.Client == null || !Static.Client.IsConnected ? Debugger.IsAttached ? notConnected : throw new Exception(notConnected) : "Connected";
            }
        }

        /// <summary>
        /// Select a cloud application to run in a local virtual environment
        /// </summary>
        public static string[]? ApplicationToRun
        {
            get
            {
                return Static.Client?.GetSupportedApps(out timeout);

            }
        }

        /// <summary>
        /// Run the selected application
        /// </summary>
        private static void OnSelectApplicationToRun(int id)
        {
            SelectedApplication = ApplicationToRun?[id];
        }

        /// <summary>
        /// Virtually run the application on the cloud. The execution leaves no traces on the local computer, technically you are working directly in the cloud, without performing any operations on the local machine.
        /// </summary>
        public static void ExecuteApplication()
        {
            if (SelectedApplication == null)
                throw new Exception("No applications selected!");
            if (Static.Client == null || !Static.Client.IsConnected)
                throw new Exception(notConnected);
            Static.Client.StartApplication(SelectedApplication);
        }
        private static string? SelectedApplication;
        private static bool timeout;
    }
}
