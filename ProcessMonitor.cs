//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// PROCESS MONITOR
// Created By: Kalopsia
// Created On: 26.08.2022
// Copyright (c) 2022, all rights reserved.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////// LICENSE /////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the License,
// or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
// for more details.
//
// You should have received a copy of the GNU General Public License along with this program.
// If not, see <https://www.gnu.org/licenses/>.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////// DESCRIPTION ///////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Process Monitor is a Windows x64 console application that will launch and monitor a specified process and
// periodically, with a configurable time interval, collect the following data about it:
//   • CPU usage (in percent)
//   • Memory consumption: Working Set and Private Bytes (in MB)
//   • Number of open handles
//
// Additional information:
//   • Data collection is performed while the process is running, the application automatically exists afterwards.
//   • Path to the executable file for the process, time interval between data collection iterations and
//     an output directory are to be be provided by user.
//     -> Example File Path: C:\Program Files\Notepad++\notepad++.exe
//     -> Example Output Path: C:\Users\Default\Desktop
//   • Collected data will be stored in the specified directory in CSV format, allowing for automated parsing.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Diagnostics;

namespace ProcessMonitor
{
    class Application
    {
        public static void Main()
        {
            Console.WriteLine("Initialising Process Monitor ...");
            Application procMon = new();

            // Prompt the user to input required parameters.
            string sFilePath = procMon.SetFilePath();
            if (sFilePath == null) goto ERROR;

            int iUpdateInterval = procMon.SetUpdateInterval();
            if (iUpdateInterval < 1) goto ERROR;

            string sOutputFile = procMon.SetOutputFile();
            if (sOutputFile == null) goto ERROR;

            // Configuration successful! Check for existing processes with this name.
            string sProcessName  = Path.GetFileNameWithoutExtension(sFilePath);
            Process[] pProcesses = Process.GetProcessesByName(sProcessName);
            if (pProcesses.Length > 0)
            {
                Console.WriteLine("Found an existing instance of " + sProcessName + ". Terminating ...\n");
                foreach (Process pProcess in pProcesses)
                {
                    pProcess.CloseMainWindow();
                    pProcess.Close();
                }
            }
            Thread.Sleep(500);

            // Now launch a new instance of the process
            Console.WriteLine("Configuration complete. Launching " + sProcessName + "...\n");
            Process pMonitor = Process.Start(sFilePath);

            Thread.Sleep(500);
            Console.WriteLine("Initializing performance metrics ...\n");
            procMon.RunHeartbeat(iUpdateInterval, pMonitor, sOutputFile);
            return;

        ERROR:
            Console.WriteLine("Initialisation failed.");
        }

        // Triggers a prompt to configure the path to the EXE.
        private string SetFilePath()
        {
            Console.WriteLine("Enter a file path:");
            string? sFilePath = Console.ReadLine();

            // Validate the input.
            if (!File.Exists(sFilePath))
            {
                Console.WriteLine("File not found.\n");
                return SetFilePath();
            }
            else if (!FileIsExecutable(sFilePath))
            {
                Console.WriteLine("File must be executable.\n");
                return SetFilePath();
            }
            else
            {
                // Apply the change.
                Console.WriteLine("Selected File Path: " + sFilePath + "\n");
                return sFilePath;
            }
        }

        // Returns true if the file is an EXE.
        private static bool FileIsExecutable(string sFilePath)
        {
            string sFileType = sFilePath.Substring(sFilePath.Length - 3, 3).ToLower();
            return sFileType == "exe";
        }

        // Triggers a prompt to configure the directory of the output CSV file.
        private string SetOutputFile()
        {
            Console.WriteLine("Specify Output Directory:");
            string? sDirectory = Console.ReadLine();
            if (!Directory.Exists(sDirectory))
            {
                Console.WriteLine("Directory not found.\n");
                return SetOutputFile();
            }
            else
            {
                // Attempt to create a new file with CSV column titles
                string sOutputFile = sDirectory + "\\PerfLog_" + DateTime.UtcNow.ToString("yyMMdd_HH-mm-ss") + ".csv";
                try
                {
                    string cListSeparator = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;
                    File.WriteAllText(sOutputFile, "Timestamp (UTC)"      + cListSeparator
                                                 + "CPU Usage (%)"        + cListSeparator
                                                 + "Physical Memory (MB)" + cListSeparator
                                                 + "Private Memory (MB)"  + cListSeparator
                                                 + "Open Handles\n");
                    File.SetAttributes(sOutputFile, FileAttributes.ReadOnly);

                    Console.WriteLine("Writing Process Data to: " + sOutputFile + "\n");
                    return sOutputFile;
                }
                catch
                {
                    Console.WriteLine("Write access denied.\n");
                    return SetOutputFile();
                }
            }
        }

        // Triggers a prompt to configure the update interval.
        private int SetUpdateInterval()
        {
            Console.WriteLine("Specify an update interval in seconds: ");
            string? sInput = Console.ReadLine();
            int iUpdateInterval;

            // Convert the input into an int and validate it.
            try   { iUpdateInterval = (sInput != null) ? Int32.Parse(sInput) : 0; }
            catch { iUpdateInterval = 0; }

            if (iUpdateInterval < 1)
            {
                Console.WriteLine("Invalid input. Enter a number greater than 0.\n");
                return SetUpdateInterval();
            }
            else
            {
                if (iUpdateInterval > 30)
                {
                    Console.WriteLine("Input value too high. Limiting to 30 seconds ...");
                    iUpdateInterval = 30;
                }
                // Apply the change.
                Console.WriteLine("Update Interval is " + iUpdateInterval + (iUpdateInterval > 1 ? " seconds.\n" : " second.\n"));
            }
            return iUpdateInterval;
        }

        // Run a recursive heartbeat as long as the EXE has an associated service.
        private void RunHeartbeat(int iUpdateInterval, Process pMonitor, string sOutputFile)
        {
            // Check if process has been terminated.
            if (!pMonitor.HasExited)
            {
                // Process is still running. Collect data, then retrigger this method as long as the process exists.
                if (WriteData(new ProcessData(pMonitor, iUpdateInterval), sOutputFile))
                    RunHeartbeat(iUpdateInterval, pMonitor, sOutputFile);
            }
            else
            {
                // Process is no longer running. End the recursion and remove the write protection.
                Console.WriteLine("\nProcess has been terminated.\n\nPress any key to exit ...");
                Console.ReadKey();
            }
        }

        // Writes process data to a CSV file in the specified directory.
        public static bool WriteData(ProcessData oData, string sOutputFile)
        {
            // Append the data to the existing output file
            try
            {
                Console.WriteLine(DateTime.UtcNow + ": Writing data ...");

                // Use the correct list separator for CSV files
                string cListSeparator = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;

                // Briefly remove the write protection to add current data.
                File.SetAttributes(sOutputFile, ~FileAttributes.ReadOnly);
                File.AppendAllText(sOutputFile, oData.utcTime  + cListSeparator 
                                              + oData.iCPU     + cListSeparator
                                              + oData.iMemW    + cListSeparator
                                              + oData.iMemP    + cListSeparator
                                              + oData.iHandles + "\n");
                File.SetAttributes(sOutputFile, FileAttributes.ReadOnly);
                return true;
            }
            catch
            {
                if (!File.Exists(sOutputFile))
                {
                    Console.WriteLine("ERROR: Output file not found.");
                    return false; // This will end the recursion.
                }
                else
                {
                    Console.WriteLine("WARNING: Write access denied. Data collection will be paused until write access is restored.");
                    return true;
                }
            }
        }
    }

    class ProcessData
    {
        public DateTime utcTime; // Current UTC time
        public int iCPU;         // CPU Usage (in %)
        public int iMemW;        // Physical Memory (in MB)
        public int iMemP;        // Private Memory (in MB)
        public int iHandles;     // Open Handles

        public ProcessData(Process pMonitor, int iUpdateInterval)
        {
            // Calculate CPU usage first. This also triggers a Thread.Sleep command.
            iCPU     = GetCpuUsage(pMonitor, iUpdateInterval * 1000);
            utcTime  = DateTime.UtcNow;

            // Calculate total memory usage by combining all instances with pMonitor's name.
            Process[] pProcesses = Process.GetProcessesByName(pMonitor.ProcessName);
            foreach (Process pProcess in pProcesses)
            {
                iMemW    += Convert.ToInt32(pProcess.WorkingSet64 / 1024 / 1024);
                iMemP    += Convert.ToInt32(pProcess.PrivateMemorySize64 / 1024 / 1024);
                iHandles += pProcess.HandleCount;
            }
        }

        private static int GetCpuUsage(Process pProcess, int iDelay)
        {
            // Store the current processor time, then observe CPU usage for [iDelay] milliseconds.
            DateTime StartTime   = DateTime.UtcNow;
            TimeSpan CpuUsageOld = pProcess.TotalProcessorTime;
            Thread.Sleep(iDelay);

            // Read the new processor time and calculate CPU usage within this period of time.
            TimeSpan CpuUsageNew = pProcess.TotalProcessorTime;
            double TimePassed    = (DateTime.UtcNow - StartTime).TotalMilliseconds;
            double CpuTime       = (CpuUsageNew - CpuUsageOld).TotalMilliseconds;
            double CpuUsage      = (CpuTime * 100) / (Environment.ProcessorCount * TimePassed);
            return Convert.ToInt32(CpuUsage);
        }
    }
}
