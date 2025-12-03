using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MarshallApp;

public partial class BlockElement
{
    public bool IsRunning => _activeProcess is { HasExited: false };
    private readonly string _iconsPath;
    private CancellationTokenSource? _cts;
    private readonly Action<BlockElement>? _removeCallback;
    public string? PythonFilePath;
    public bool IsLooping;
    public double LoopInterval { get; set; } = 5.0;
    private DispatcherTimer? _loopTimer;
    private Process? _activeProcess;
    private bool _isInputVisible;
    private Point _dragStart;
    private bool _isDragging;
    private bool _pendingClear;

    public double OutputFontSize { get; set; } = 14.0;

    public BlockElement(Action<BlockElement>? removeCallback)
    {
        InitializeComponent();
        
        this.PreviewMouseMove += Block_PreviewMouseMove;
        this.PreviewMouseLeftButtonDown += Block_PreviewMouseLeftButtonDown;
        
        _removeCallback = removeCallback;
        _iconsPath = Path.Combine(Environment.CurrentDirectory + "/Resource/Icons/");
        UpdateLoopIcon(IsLooping);
        
        Application.Current.Exit += (_, _) => StopActiveProcess();
        this.Unloaded += (_, _) => StopActiveProcess();
    }
    
    public async Task RunPythonScript()
    {
        StopActiveProcess();

        if (string.IsNullOrEmpty(PythonFilePath) || !File.Exists(PythonFilePath))
        {
            OutputText.Text = "File not found or not selected!";
            return;
        }

        OpenPythonInstallerPage();
        SetFileNameText();
        _pendingClear = true;
        
        _cts = new CancellationTokenSource();

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
                    ["PYTHONUNBUFFERED"] = "1",
                    ["PYTHONUTF8"] = "1"
                }
            };

            CodeViewer.Text = await File.ReadAllTextAsync(PythonFilePath);

            _activeProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _activeProcess.Start();

            // Create JobObject if not created yet
            CreateJobObject();
            AssignProcessToJobObject(_jobHandle, _activeProcess.Handle);
            
            Task.Run(async () =>
            {
                try
                {
                    using var reader = _activeProcess.StandardOutput;
                    var buffer = new char[1024];
                    int charsRead;
                    
                    while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        var text = new string(buffer, 0, charsRead);
                        Dispatcher.Invoke(() =>
                        {
                            if (_pendingClear)
                            {
                                OutputText.Text = string.Empty;
                                _pendingClear = false;
                            }
                            
                            var cleaned = text.Replace("\r", "\n").Replace("\n\n", "\n");
                            OutputText.Text += cleaned;
                            Scroll.ScrollToEnd();
                        });
                    }
                }
                catch
                {
                    // ignored
                }
            }, _cts.Token);
            
            Task.Run(async () =>
            {
                try
                {
                    using var reader = _activeProcess.StandardError;
                    var buffer = new char[1024];
                    int charsRead;
            
                    while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        var text = new string(buffer, 0, charsRead);
            
                        if (text.Contains("No module named"))
                        {
                            var missingModule = ParseMissingModule(text);
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
            
                        Dispatcher.Invoke(() =>
                        {
                            var cleaned = text.Replace("\r", "\n");
                            OutputText.Text += cleaned;
                            Scroll.ScrollToEnd();
                        });
                    }
                }
                catch
                {
                    // ignored
                }
            }, _cts.Token);

            _activeProcess.Exited += (_, _) =>
            {
                Dispatcher.Invoke(() =>
                {
                    //if (!IsLooping)
                    //    OutputText.Text += "\n\n[Process exited]\n";
                });
            };
        }
        catch (Exception ex)
        {
            OutputText.Text = $"Python startup error:\n{ex.Message}";
        }
    }
    
    private static void OpenPythonInstallerPage()
    {
        if (!IsPythonInstalled())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.python.org/downloads/",
                UseShellExecute = true
            });
        }
    }

    private void CreateJobObject()
    {
        if (_jobHandle != IntPtr.Zero) return;
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

    #region Python things

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

            try { _activeProcess.StandardInput.BaseStream.Close(); }
            catch
            {
                // ignored
            }

            if (_activeProcess.HasExited) return;

            if (!forceKill) return;
            if (!_activeProcess.HasExited)
            {
                _activeProcess.Kill(); 
            }

            if (_jobHandle == IntPtr.Zero) return;
            CloseHandle(_jobHandle);
            _jobHandle = IntPtr.Zero;
        }
        catch { /* ignored */ }
        finally
        {
            _activeProcess?.Dispose();
            _activeProcess = null;
        }
        
        //LastHope();
    }

    /// <summary>
    /// Extremely dangerous method, don't use it
    /// </summary>
    private static void LastHope()
    {
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
                catch { /* ignored */ }
                finally { proc.Dispose(); }
            }

            foreach (var proc in Process.GetProcessesByName("pythonw"))
            {
                if (proc.SessionId != currentSessionId) continue;
                try { if (!proc.HasExited) proc.Kill(); }
                catch { /* ignored */ }
                finally { proc.Dispose(); }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    #endregion
    
    #region top menu buttons
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        StopLoop();
        _removeCallback?.Invoke(this);
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

    private void CopyButton_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(OutputText.Text);

    #endregion

    #region loop button behaviour

    private void ToggleLoop_Click(object sender, RoutedEventArgs e)
    {
        IsLooping = !IsLooping;
        
        if (IsLooping)
        {
            var input = new InputBoxWindow("Loop Settings", "Interval in seconds:", (sec) => LoopInterval = double.Parse(sec) > 0 ? double.Parse(sec) : 0, LoopInterval.ToString(CultureInfo.InvariantCulture))
                {
                    Owner = Application.Current.MainWindow
                };
            input.ShowDialog();

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
        
        UpdatePeriodTimeTextBlock();
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
            OutputText.Text = $"Waiting for the next call...\n\nLoop: ON | Interval: {LoopInterval}s | File: {Path.GetFileNameWithoutExtension(PythonFilePath)}";
    }

    private void StopLoop()
    {
        _loopTimer?.Stop();
        _loopTimer = null;
    }

    private void UpdatePeriodTimeTextBlock()
    {
        var value = IsLooping ? LoopInterval.ToString(CultureInfo.InvariantCulture) : string.Empty;
        
        PeriodTime.Text = value;
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
    
    private void OutputText_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl)) return;
        if (e.Delta > 0)
            OutputFontSize += 1;
        else
            OutputFontSize -= 1;

        if (OutputFontSize < 8) OutputFontSize = 8;
        if (OutputFontSize > 40) OutputFontSize = 40;

        OutputText.FontSize = OutputFontSize;

        e.Handled = true;
    }
    
    
    private void OnOutputLoaded(object sender, RoutedEventArgs e)
    {
        OutputText.FontSize = OutputFontSize;
    }
}

// extension part
public partial class BlockElement
{
    #region --- JOB OBJECT KILL TREE ---

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
#pragma warning disable SYSLIB1054
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);
#pragma warning restore SYSLIB1054

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
#pragma warning disable SYSLIB1054
    // ReSharper disable once InconsistentNaming
    private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo, int cbJobObjectInfoLength);
#pragma warning restore SYSLIB1054

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
#pragma warning disable SYSLIB1054
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
#pragma warning restore SYSLIB1054

    private IntPtr _jobHandle;

    // ReSharper disable once ArrangeTypeMemberModifiers
    // ReSharper disable once InconsistentNaming
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

    // ReSharper disable once ArrangeTypeMemberModifiers
    // ReSharper disable once InconsistentNaming
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

    // ReSharper disable once ArrangeTypeMemberModifiers
    // ReSharper disable once InconsistentNaming
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

    // ReSharper disable once InconsistentNaming
    private const int JOB_OBJECT_EXTENDED_LIMIT_INFORMATION = 9;
    // ReSharper disable once InconsistentNaming
    private const int JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
#pragma warning disable SYSLIB1054
    private static extern bool CloseHandle(IntPtr hObject);
#pragma warning restore SYSLIB1054

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
                UpdatePeriodTimeTextBlock();
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

    private void Rerun_Click(object sender, RoutedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            OutputText.Text = string.Empty;
        });
        RunPythonScript();
    } 
    
    private void Block_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = false;
    }

    private void Block_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var pos = e.GetPosition(this);
        var diff = pos - _dragStart;

        if (_isDragging || (!(Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance) &&
                            !(Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))) return;
        _isDragging = true;

        DragDrop.DoDragDrop(this, this, DragDropEffects.Move);
    }

    private void CallLogViewer_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.ShowLogViewer(this);
    }
}