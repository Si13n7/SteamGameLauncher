using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SteamGameLauncher
{
    public partial class MainWindow : Window
    {
        List<string> cmdLineArg = new List<string>();

        bool headerSizePlus = false;

        List<string> steamApps = new List<string>();
        List<string> steamGameId = new List<string>();
        List<string> steamGameName = new List<string>();

        string path = Directory.GetCurrentDirectory();
        string fullPath = string.Empty;

        private BackgroundWorker runGameBkWorker = new BackgroundWorker();

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        protected override void OnClosed(EventArgs e)
        {
            cleanCache();
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        private void wndMain_Loaded(object sender, RoutedEventArgs e)
        {
            if (SilDev.Reg.SubKeyExist("HKLM", "SOFTWARE\\Valve\\Steam"))
                path = SilDev.Reg.ReadValue("HKLM", "SOFTWARE\\Valve\\Steam", "InstallPath");

            fullPath = Path.Combine(path, "Steam.exe");
            if (!File.Exists(fullPath))
            {
                MessageBox.Show("Steam not found. Please reinstall Steam or put this tool in your Steam install folder.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                Environment.Exit(1);
            }

            string[] cmdLineArgs = Environment.GetCommandLineArgs();
            if (cmdLineArgs.Length > 1)
            {
                for (int i = 1; i < cmdLineArgs.Length; i++)
                    cmdLineArg.Add(cmdLineArgs[i]);

                if (cmdLineArg.Count != 2 || !cmdLineArg[0].Any(x => !char.IsLetter(x)))
                {
                    using (var cmdInfo = new Process())
                    {
                        cmdInfo.StartInfo.Arguments = "/C @ECHO OFF && TITLE SteamGameLauncher && CLS && ECHO Steam Game Launcher - Developed by Si13n7(tm) && ECHO. && ECHO. && ECHO USAGE: && ECHO \"...\\SteamGameLauncher.exe\" \"GameID\" \"GameExecute\" && ECHO. && ECHO EXAMPLE: && ECHO \"C:\\Program Files\\Steam\\SteamGameLauncher.exe\" \"206420\" \"SaintsRowIV.exe\" && ECHO. && ECHO. && pause";
                        cmdInfo.StartInfo.FileName = Path.Combine(Environment.GetEnvironmentVariable("WinDir"), "System32\\cmd.exe");
                        cmdInfo.StartInfo.WorkingDirectory = Path.Combine(Environment.GetEnvironmentVariable("WinDir"), "System32");
                        cmdInfo.Start();
                    }
                    Environment.Exit(1);
                }
                else
                {
                    runGameBkWorker.DoWork += runGameBkWorker_DoWork;
                    runGameBkWorker.RunWorkerCompleted += runGameBkWorker_RunWorkerCompleted;
                    runGameBkWorker.RunWorkerAsync();
                }

                ShowInTaskbar = false;
                Visibility = Visibility.Hidden;
            }
            else
            {
                steamApps.Add(Path.Combine(path, "SteamApps"));
                if (Directory.Exists(steamApps[0]))
                {
                    string libPath = Path.Combine(steamApps[0], "libraryfolders.vdf");
                    if (File.Exists(libPath))
                    {
                        using (var acf = new StreamReader(libPath))
                        {
                            var keyword = string.Empty;
                            var appid = string.Empty;
                            foreach (var line in acf.ReadToEnd().Split((char)34))
                            {
                                try
                                {
                                    string dir = Path.Combine(line, "SteamApps");
                                    if (Directory.Exists(dir))
                                        steamApps.Add(dir);
                                }
                                catch {  /* DO NOTHING */ }
                            }
                        }
                    }
                    foreach (var dir in steamApps)
                    {
                        foreach (var AppState in Directory.GetFiles(dir, "*.acf"))
                        {
                            using (var acf = new StreamReader(AppState))
                            {
                                var keyword = string.Empty;
                                var appid = string.Empty;
                                foreach (var line in acf.ReadToEnd().Split((char)34))
                                {
                                    if (!string.IsNullOrWhiteSpace(line) && line != string.Empty && !line.Contains((char)34))
                                    {
                                        if (keyword != string.Empty)
                                        {
                                            var done = false;
                                            switch (keyword)
                                            {
                                                case "appid":
                                                    steamGameId.Add(line);
                                                    break;
                                                case "name":
                                                    steamGameName.Add(line);
                                                    done = true;
                                                    break;
                                                default:
                                                    done = true;
                                                    break;
                                            }
                                            keyword = string.Empty;
                                            if (done)
                                                break;
                                        }
                                        if (line.ToLower() == "appid" || line.ToLower() == "name")
                                            keyword = line.ToLower();
                                    }
                                }
                            }
                        }
                    }
                }

                if (steamGameId.Count <= 0 || steamGameName.Count <= 0)
                {
                    MessageBox.Show("Sorry, no Steam games found.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Environment.Exit(1);
                }

                cb1.ItemsSource = steamGameName.OrderBy(x => x).ToList();
                cb1.SelectedIndex = 0;

                DispatcherTimer headerFx = new DispatcherTimer();
                headerFx.Tick += new EventHandler(headerSize_Tick);
                headerFx.Interval = TimeSpan.FromMilliseconds(10);
                headerFx.Start();

                SilDev.Source.LoadAssemblies(Properties.Resources._SteamGameLauncher);
                SilDev.Source.IncludeAssemblies();
                if (!SilDev.Reg.SubKeyExist("HKCU", "Software\\Si13n7 Dev.\\SteamGameLauncher"))
                {
                    SilDev.Reg.CreateNewSubKey("HKCU", "Software\\Si13n7 Dev.");
                    SilDev.Reg.CreateNewSubKey("HKCU", "Software\\Si13n7 Dev.\\SteamGameLauncher");
                    SilDev.Reg.WriteValue("HKCU", "Software\\Si13n7 Dev.\\SteamGameLauncher", "Music", "True");
                }
                if (SilDev.Reg.SubKeyExist("HKCU", "Software\\Si13n7 Dev.\\SteamGameLauncher"))
                {
                    if (SilDev.Reg.ReadValue("HKCU", "Software\\Si13n7 Dev.\\SteamGameLauncher", "Music") == "True")
                        SilDev.IrrKlangLib.Play(SilDev.Source.GetFilePath(SilDev.Source.files[0]), true, 25);
                }
                else
                    SilDev.IrrKlangLib.Play(SilDev.Source.GetFilePath(SilDev.Source.files[0]), true, 25);
            }
        }

        private void headerSize_Tick(object sender, EventArgs e)
        {
            if (headLabel.FontSize < 16)
                headerSizePlus = true;
            if (headLabel.FontSize > 17)
                headerSizePlus = false;

            if (!headerSizePlus)
            {
                headLabel.Opacity += 0.005;
                headLabel.FontSize -= 0.005;
                headLabel2.Opacity += 0.005;
            }
            else
            {
                headLabel.Opacity -= 0.005;
                headLabel.FontSize += 0.005;
                headLabel2.Opacity -= 0.005;
            }
        }

        private void wndMain_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MoveWindow_Mouse(e);
        }

        private void MoveWindow_Mouse(MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Cursor curCursor = Cursor;
                Cursor = Cursors.None;
                ReleaseCapture();
                SendMessage(new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle, 0xA1, 0x2, 0);
                Cursor = curCursor;
            }
        }

        private void btn1_Click(object sender, RoutedEventArgs e)
        {
            int index = 0;
            for (index = 0; index < steamGameName.Count; index++)
                if (steamGameName[index] == cb1.SelectedItem.ToString())
                    break;

            string curSteamGameId = steamGameId[index];

            string shortcutName = string.Join(string.Empty, cb1.SelectedItem.ToString().Split(Path.GetInvalidFileNameChars()));
            string curSteamGameDir = string.Format("{0}\\SteamApps\\common\\{1}", path, shortcutName);

            if (!string.IsNullOrEmpty(curSteamGameId))
            {
                OpenFileDialog getGameExe = new OpenFileDialog();
                getGameExe.DefaultExt = ".exe";
                getGameExe.Filter = "Executable File (*.exe) | *.exe";
                if (Directory.Exists(curSteamGameDir))
                    getGameExe.InitialDirectory = curSteamGameDir;
                else
                    getGameExe.InitialDirectory = Path.Combine(path, "SteamApps");

                if (SilDev.Reg.SubKeyExist("HKLM", string.Format("SOFTWARE\\Valve\\Steam\\Apps\\{0}", curSteamGameId)))
                {
                    string uplayGame = SilDev.Reg.ReadValue("HKLM", string.Format("SOFTWARE\\Valve\\Steam\\Apps\\{0}", curSteamGameId), "UplayLauncher");
                    if (!string.IsNullOrEmpty(uplayGame))
                    {
                        if (SilDev.Reg.SubKeyExist("HKLM", "SOFTWARE\\Ubisoft\\Launcher"))
                        {
                            string uplayPath = SilDev.Reg.ReadValue("HKLM", "SOFTWARE\\Ubisoft\\Launcher", "InstallDir");
                            if (!string.IsNullOrEmpty(uplayPath))
                            {
                                if (Directory.Exists(uplayPath))
                                    getGameExe.InitialDirectory = uplayPath;
                                if (File.Exists(Path.Combine(uplayPath, "Uplay.exe")))
                                    getGameExe.FileName = "Uplay.exe";
                            }
                        }
                    }
                }

                getGameExe.Title = ("Select the game executable file").ToUpper();
                bool? gameExeResult = getGameExe.ShowDialog();
                if (gameExeResult == true)
                {
                    string curSteamGameName = Path.GetFileNameWithoutExtension(getGameExe.FileName);
                    string shortcutIconPath = getGameExe.FileName;
                    if (curSteamGameName.ToLower() == "uplay")
                    {
                        OpenFileDialog getGameIcon = new OpenFileDialog();
                        getGameIcon.DefaultExt = ".exe|.ico";
                        getGameIcon.Filter = "Icon (*.exe, *.ico) | *.exe; *.ico"; ;
                        getGameIcon.InitialDirectory = Path.Combine(path, "steam\\games");
                        getGameIcon.Title = ("Select the game icon").ToUpper();
                        Nullable<bool> getGameIconResult = getGameIcon.ShowDialog();
                        if (getGameIconResult == true)
                            shortcutIconPath = getGameIcon.FileName;
                    }
                    try
                    {
                        IWshRuntimeLibrary.WshShell wshShell = new IWshRuntimeLibrary.WshShell();
                        IWshRuntimeLibrary.IWshShortcut desktopShortcut;
                        desktopShortcut = (IWshRuntimeLibrary.IWshShortcut)wshShell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), string.Format("{0}.lnk", shortcutName)));
                        desktopShortcut.Arguments = string.Format("\"{0}\" \"{1}\"", curSteamGameId, Path.GetFileNameWithoutExtension(getGameExe.FileName));
                        desktopShortcut.IconLocation = shortcutIconPath;
                        desktopShortcut.TargetPath = Environment.GetCommandLineArgs()[0];
                        desktopShortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
                        desktopShortcut.WindowStyle = 7;
                        desktopShortcut.Save();
                        MessageBox.Show(string.Format("Desktop shortcut for {0} created.", shortcutName), "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch
                    {
                        MessageBox.Show("Something was wrong, no shortcut created.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void btn2_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btn3_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void btn4_Click(object sender, RoutedEventArgs e)
        {
            if (!SilDev.IrrKlangLib.MusicIsRunning)
            {
                SilDev.IrrKlangLib.Play(SilDev.Source.GetFilePath(SilDev.Source.files[0]), true, 25);
                SilDev.Reg.WriteValue("HKCU", "Software\\Si13n7 Dev.\\SteamGameLauncher", "Music", "True");
            }
            else
            {
                SilDev.IrrKlangLib.Stop();
                SilDev.Reg.WriteValue("HKCU", "Software\\Si13n7 Dev.\\SteamGameLauncher", "Music", "False");
            }
        }

        private void runGameBkWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (File.Exists(fullPath))
            {
                string gameId = cmdLineArg[0];
                using (var steam = new Process())
                {
                    steam.StartInfo.Arguments = string.Format("-applaunch {0}", gameId);
                    steam.StartInfo.FileName = fullPath;
                    steam.StartInfo.WorkingDirectory = path;
                    steam.Start();
                }
                string gameExe = (cmdLineArg[1].ToLower().EndsWith(".exe") ? cmdLineArg[1].ToLower().Replace(".exe", string.Empty) : cmdLineArg[1]);

                int count = 0;
                while (Process.GetProcessesByName(gameExe).Length < 1)
                {
                    count++;
                    Thread.Sleep(100);
                    if (count == 600)
                    {
                        MessageBoxResult dialog = MessageBox.Show(string.Format("Process for \"{0}\" not found. Would you like to wait?", gameExe), "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (dialog == MessageBoxResult.Yes)
                            count = 0;
                        else
                            break;
                    }
                }
                if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fullPath)).Length > 0 && count == 600)
                {
                    MessageBoxResult dialog = MessageBox.Show("Would you like to close \"Steam\"?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dialog == MessageBoxResult.No)
                        Environment.Exit(1);
                }

                for (int i = 0; i < 10; i++)
                {
                    foreach (var p in Process.GetProcessesByName(gameExe))
                        p.WaitForExit();

                    Thread.Sleep(200);
                }

                killSteam();
            }
            else
                MessageBox.Show("\"Steam.exe\" not found!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void runGameBkWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Environment.Exit(1);
        }

        private void killSteam()
        {
            var runningSteam = Process.GetProcessesByName("Steam");
            if (runningSteam.Length > 0)
            {
                foreach (var p in runningSteam)
                {
                    p.CloseMainWindow();
                    p.WaitForExit(200);
                    if (!p.HasExited)
                        p.Kill();

                    p.Close();
                }
            }
        }

        private void cleanCache()
        {
            try
            {
                string cleanerFile = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "SteamGameLauncherCleaner.bat");
                string cleanerContent = SilDev.Crypt.DecryptFromBase64("QEVDSE8gT0ZGDQpUSVRMRSBDTEVBTkVSDQpQSU5HIDE3Mi4wLjAuMSAtbiAxIC13IDIwMA0KUk1ESVIgL1MgL1EgIiVURU1QJVxTdGVhbUdhbWVMYXVuY2hlciINCkRFTCAvRiAvUSAiJVRFTVAlXFN0ZWFtR2FtZUxhdW5jaGVyQ2xlYW5lci5iYXQiDQpFWElU");
                if (File.Exists(cleanerFile))
                    File.Delete(cleanerFile);
                using (StreamWriter createCleanerFile = File.CreateText(cleanerFile))
                    createCleanerFile.Write(cleanerContent);
                using (Process cleanerProcess = new Process())
                {
                    cleanerProcess.StartInfo.FileName = cleanerFile;
                    cleanerProcess.StartInfo.WorkingDirectory = Environment.GetEnvironmentVariable("TEMP");
                    cleanerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    cleanerProcess.Start();
                }
            }
            catch { /* DO NOTHING */ }
        }
    }
}
