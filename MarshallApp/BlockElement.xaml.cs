using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MarshallApp;

public partial class BlockElement
{
    private readonly string _iconsPath;
    private CancellationTokenSource? _cts;
    private readonly Action<BlockElement>? _onRemove;
    public string? PythonFilePath;
    public bool IsLooping;
    public double LoopInterval { get; set; } = 5.0;
    private DispatcherTimer? _loopTimer;
    private Process? _activeProcess;
    private bool _isInputVisible;
    
    // --- JOB OBJECT KILL TREE ---
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo, int cbJobObjectInfoLength);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    private IntPtr _jobHandle;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public int LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public int ActiveProcessLimit;
        public long Affinity;
        public int PriorityClass;
        public int SchedulingClass;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private const int JOB_OBJECT_EXTENDED_LIMIT_INFORMATION = 9;
    private const int JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    public BlockElement(Action<BlockElement>? onRemove)
    {
        InitializeComponent();
        _onRemove = onRemove;
        _iconsPath = Path.Combine(Environment.CurrentDirectory + "/Resource/Icons/");
        UpdateLoopIcon(IsLooping);
        
        Application.Current.Exit += (_, _) => StopActiveProcess();
        this.Unloaded += (_, _) => StopActiveProcess();
    }
    
    public void RunPythonScript()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        
        if (string.IsNullOrEmpty(PythonFilePath) || !File.Exists(PythonFilePath))
        {
            OutputText.Text = "File not found or not selected!";
            return;
        }
        
        if (!IsPythonInstalled())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.python.org/downloads/",
                UseShellExecute = true
            });
        }

        SetFileNameText();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"-u \"{PythonFilePath}\"",
                
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                EnvironmentVariables =
                {
                    ["PYTHONUTF8"] = "1"
                }
            };

            _activeProcess = new Process { StartInfo = psi };
            _activeProcess.EnableRaisingEvents = true;
            _activeProcess.Start();
            
            // Create JobObject if not created yet
            if (_jobHandle == IntPtr.Zero)
            {
                _jobHandle = CreateJobObject(IntPtr.Zero, null);

                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

                SetInformationJobObject(
                    _jobHandle,
                    JOB_OBJECT_EXTENDED_LIMIT_INFORMATION,
                    ref info,
                    System.Runtime.InteropServices.Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()
                );
            }
            
            AssignProcessToJobObject(_jobHandle, _activeProcess.Handle);
            
            var hasNewOutputStarted = false;
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && 
                       !_activeProcess.HasExited && 
                       await _activeProcess.StandardOutput.ReadLineAsync(token) is { } line)
                {
                    var capturedLine = line;

                    Dispatcher.Invoke(() =>
                    {
                        if (!hasNewOutputStarted)
                        {
                            hasNewOutputStarted = true;
                            OutputText.Text = string.Empty;
                        }

                        OutputText.Text += capturedLine + "\n";
                        Scroll.ScrollToEnd();
                    });
                }
            }, token);

            _ = Task.Run(async () =>
            {
                while (await _activeProcess.StandardError.ReadLineAsync(token) is { } line)
                {
                    if (line.Contains("No module named"))
                    {
                        var missingModule = ParseMissingModule(line);
                        if (string.IsNullOrEmpty(missingModule)) continue;
                        Dispatcher.Invoke(() => OutputText.Text += $"\n[AutoFix] Installing missing module: {missingModule}...\n");
                        var installed = await InstallPythonPackage(missingModule);
                        if (installed)
                        {
                            Dispatcher.Invoke(() => OutputText.Text += $"[AutoFix] Successfully installed {missingModule}. Restarting script...\n");
                            Dispatcher.Invoke(RunPythonScript);
                        }
                        else
                        {
                            Dispatcher.Invoke(() => OutputText.Text += $"[AutoFix] Failed to install {missingModule}.\n");
                        }
                    }
                    else
                    {
                        var line1 = line;
                        Dispatcher.Invoke(() => OutputText.Text += "\n[Error] " + line1);
                    }
                }
            }, token);

            _activeProcess.Exited += (_, _) =>
            {
                if (!hasNewOutputStarted && !IsLooping)
                {
                    //OutputText.Text += "\n[Готово] Скрипт завершился без вывода OWO";
                }
            };
        }
        catch (Exception ex)
        {
            OutputText.Text = $"\nPython startup error:\n{ex.Message}";
        }
    }

    private static async Task<bool> InstallPythonPackage(string package)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"-m pip install {package}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = Encoding.UTF8,
            };
            
            using var process = new Process();
            process.StartInfo = psi;
            process.Start();
            //var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            var success = !error.Contains("ERROR", StringComparison.OrdinalIgnoreCase);
            
            return success;
        }
        catch
        {
            return false;
        }
    }

    private static string? ParseMissingModule(string errorText)
    {
        var start = errorText.IndexOf("No module named '", StringComparison.Ordinal);
        if (start == -1)
            return null;
        start += "No module named '".Length;

        var end = errorText.IndexOf('\'', start);
        return end == -1 ? null : errorText[start..end];
    }
    
    private static bool IsPythonInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(2000);

            return process is { ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }

    private void StopActiveProcess(bool forceKill = false)
    {
        if (_activeProcess == null) return;

        try
        {
            _cts?.Cancel();

            try { _activeProcess.StandardInput.Close(); } catch { /* ignored */ }

            if (_activeProcess.HasExited) return;
            
            if (forceKill)
            {
                if (!_activeProcess.HasExited)
                {
                    _activeProcess.Kill(); 
                }
        
                if (_jobHandle != IntPtr.Zero)
                {
                    CloseHandle(_jobHandle);
                    _jobHandle = IntPtr.Zero;
                }
            }
        }
        catch { /* ignored */ }
        finally
        {
            _activeProcess?.Dispose();
            _activeProcess = null;
        }
        
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var currentSessionId = currentProcess.SessionId;

            foreach (var proc in Process.GetProcessesByName("python"))
            {
                if (proc.SessionId != currentSessionId) continue;
                try
                {
                    if (proc.HasExited) continue;
                    proc.Kill();
                    proc.WaitForExit(2000);
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    proc.Dispose();
                }
            }

            foreach (var proc in Process.GetProcessesByName("pythonw"))
            {
                if (proc.SessionId != currentSessionId) continue;
                try { if (!proc.HasExited) proc.Kill(); }
                catch
                {
                    // ignored
                }
                finally { proc.Dispose(); }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    #region top menu buttons
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        StopLoop();
        _onRemove?.Invoke(this);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (EditBlockButton.ContextMenu == null) return;
        EditBlockButton.ContextMenu.PlacementTarget = EditBlockButton;
        EditBlockButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        EditBlockButton.ContextMenu.IsOpen = true;
    }

    private void SelectPythonFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Python files (*.py)|*.py|All files (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;
        PythonFilePath = dlg.FileName;
        SetFileNameText();
        RunPythonScript();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(OutputText.Text);
    }

    #endregion

    #region loop button behaviour

    private void ToggleLoop_Click(object sender, RoutedEventArgs e)
    {
        IsLooping = !IsLooping;

        if (IsLooping)
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox("Interval in seconds:", "Loop Settings", LoopInterval.ToString(CultureInfo.InvariantCulture));
            if (double.TryParse(input, out var sec) && sec > 0)
                LoopInterval = sec;

            _loopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(LoopInterval) };
            _loopTimer.Tick += (_, _) => RunPythonScript();
            _loopTimer.Start();

            UpdateLoopIcon(true);
        }
        else
        {
            StopLoop();
            UpdateLoopIcon(false); 
        }

        UpdateLoopStatus();
    }
    private void UpdateLoopIcon(bool isLooping)
    {
        var iconName = isLooping ? "loopGreen.png" : "loop.png";
        var fullPath = Path.Combine(_iconsPath, iconName);

        var uri = new Uri(fullPath, UriKind.Absolute);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = uri;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze(); 

        LoopIconImage.ImageSource = bitmap;
    }
    
    private void UpdateLoopStatus()
    {
        if (IsLooping)
            OutputText.Text = $"Loop: ON | Interval: {LoopInterval}s | File: {Path.GetFileNameWithoutExtension(PythonFilePath)}";
    }

    private void StopLoop()
    {
        _loopTimer?.Stop();
        _loopTimer = null;
    }
    #endregion

    #region input things
    private void ToggleInput_Click(object sender, RoutedEventArgs e)
    {
        _isInputVisible = !_isInputVisible;
        UserInputBox.Visibility = _isInputVisible ? Visibility.Visible : Visibility.Collapsed;
        UpdateInputIcon(_isInputVisible);
    }
    
    private void UpdateInputIcon(bool value)
    {
        var path = value ? Path.Combine(_iconsPath + "./inputGreen.png") : Path.Combine(_iconsPath + "./input.png");
        InputIconImage.ImageSource = new BitmapImage(new Uri(path, UriKind.Relative));
    }

    private void UserInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _activeProcess == null || _activeProcess.HasExited) return;

        var input = UserInputBox.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        var utf8Bytes = Encoding.UTF8.GetBytes(input + "\n");
        _activeProcess.StandardInput.BaseStream.Write(utf8Bytes, 0, utf8Bytes.Length);
        _activeProcess.StandardInput.BaseStream.Flush();

        OutputText.Text += $"\n>>> {input}\n";
        UserInputBox.Clear();
    }
    #endregion

    public void SetFileNameText()
    {
        if (!string.IsNullOrEmpty(PythonFilePath))
            FileNameText.Text = Path.GetFileNameWithoutExtension(PythonFilePath);
    }

    public void RestoreLoopState()
    {
        UpdateLoopIcon(IsLooping);
        if (IsLooping)
        {
            if (!string.IsNullOrEmpty(PythonFilePath) && File.Exists(PythonFilePath))
            {
                _loopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(LoopInterval) };
                _loopTimer.Tick += (_, _) => RunPythonScript();
                _loopTimer.Start();

                UpdateLoopStatus();
            }
            else
            {
                IsLooping = false;
            }
        }
        else
        {
            UpdateLoopStatus();
        }
    }

    private void Rerun_Click(object sender, RoutedEventArgs e) => RunPythonScript();
}
