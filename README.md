# ProcessMonitor

#### Description:
Process Monitor is a Windows x64 console application that will launch and monitor a specified process and
periodically, with a configurable time interval, collect the following data about it:
- CPU usage (in percent)
- Memory consumption: Working Set and Private Bytes (in MB)
- Number of open handles


#### Additional information:
- Data collection is performed while the process is running, the application automatically exits afterwards.
- Path to the executable file for the process, time interval between data collection iterations and
    an output directory are to be be provided by the user.
    - Example File Path: C:\Program Files\Notepad++\notepad++.exe
    - Example Output Path: C:\Users\Default\Desktop
- Collected data will be stored in the specified directory in CSV format, allowing for automated parsing.
