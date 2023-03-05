using Microsoft.AspNetCore.Authentication;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cloud
{
    public static class AutoStart
    {
        private static string FullAppName => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName);
        private static string AppName => AppDomain.CurrentDomain.FriendlyName;
        private static string TaskName => @"/tn AutoStart" + AppName  + Static.Port;


        public static void SetAutoStartByScript()
        {
            var startupFolder = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
            // automatic start of the application, with the start of the operating system
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var vbsFileName = Path.Combine(startupFolder.FullName, AppName + ".vbs");
                if (!File.Exists(vbsFileName))
                {
                    if (!startupFolder.Exists)
                        startupFolder.Create();
                    // https://www.informit.com/articles/article.aspx?p=1187429&seqNum=5
                    string vbs = "Set WshShell = WScript.CreateObject(\"WScript.Shell\")" + Environment.NewLine +
                                 "Dim exeName" + Environment.NewLine +
                                 "Dim statusCode" + Environment.NewLine +
                                 "exeName = \"" + FullAppName + "\"" + Environment.NewLine +
                                 "statusCode = WshShell.Run(exeName, 0, false)";
                    File.WriteAllText(vbsFileName, vbs);
                }
            }
        }

        public static void SetAutoStartByActivity()
        {
            if (Debugger.IsAttached)
                return;
            var xmlFileName = FullAppName + ".activity.xml";
            //if (!File.Exists(xmlFileName))
            //{
                // example and documentation: https://learn.microsoft.com/en-us/windows/win32/taskschd/boot-trigger-example--xml-/
                var xml = @"<?xml version=""1.0"" ?>
<!--
Schedules a task to start {0} when the system is booted.
-->
<Task xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
    <RegistrationInfo>
        <Date>2000-01-01T00:00:00-08:00</Date>
        <Author>{0}</Author>
        <Version>1.0.0</Version>
        <Description>Starts {0} on system boot.</Description>
    </RegistrationInfo>
    <Triggers>
        <LogonTrigger>
          <Enabled>true</Enabled>
        </LogonTrigger>
    </Triggers>
    <Principals>
        <Principal>
            <GroupId>S-1-5-32-545</GroupId>
            <RunLevel>HighestAvailable</RunLevel>
        </Principal>
    </Principals>
    <Settings>
        <Enabled>true</Enabled>
        <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
        <AllowStartOnDemand>true</AllowStartOnDemand>
        <AllowHardTerminate>true</AllowHardTerminate>
        <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    </Settings>
    <Actions>
        <Exec>
            <Command>{1}</Command>
        </Exec>
    </Actions>
</Task>
";
                var formatted = string.Format(xml, AppName, FullAppName);
                File.WriteAllText(xmlFileName, formatted);

                // schtasks /create /XML <path to the XML file containing the task definition> /tn <task name>
                var args = "/XML \"" + xmlFileName + "\" " + TaskName;
                //EncryptedMessaging.Functions.ExecuteCommand("schtasks", "/delete " + args);
                //EncryptedMessaging.Functions.ExecuteCommand("schtasks", "/create " + args);
                EncryptedMessaging.Functions.ExecuteCommand("cmd.exe", "/C " + "schtasks /delete " + TaskName + " /F", true);
                EncryptedMessaging.Functions.ExecuteCommand("cmd.exe", "/C " + "schtasks /create " + args, true);
            //}
        }

        public static void StopAutoStartByActivity()
        {
            EncryptedMessaging.Functions.ExecuteCommand("cmd.exe", "/C " + "schtasks /delete " + TaskName + " /F", true);
        }
    }
}
