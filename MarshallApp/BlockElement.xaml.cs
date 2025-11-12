// 悲しいという気持ち - Yuyoyuppe

using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace MarshallApp;

public partial class BlockElement : UserControl
{
    public Action<BlockElement>? OnRemove;
    public string? pythonFilePath;
    public bool isLooping = false;
    public double LoopInterval { get; set; } = 5.0;
    private DispatcherTimer? loopTimer;
    private Process? activeProcess;
    private bool isInputVisible = false;

    public BlockElement(Action<BlockElement>? onRemove)
    {
        InitializeComponent();
        OnRemove = onRemove;
    }

    public void RunPythonScript()
    {
        if (string.IsNullOrEmpty(pythonFilePath) || !File.Exists(pythonFilePath))
        {
            OutputText.Text = "File not found or not selected!";
            return;
        }

        SetFileNameText();

        try
        {
            StopActiveProcess();

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{pythonFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            psi.EnvironmentVariables["PYTHONUTF8"] = "1";

            activeProcess = new Process { StartInfo = psi };
            activeProcess.Start();

            _ = Task.Run(async () =>
            {
                Dispatcher.Invoke(() =>
                {
                    OutputText.Text = string.Empty;
                });

                var buffer = new char[1];
                var reader = activeProcess.StandardOutput;
                while (!reader.EndOfStream)
                {
                    int count = await reader.ReadAsync(buffer, 0, 1);
                    if (count > 0)
                    {
                        Dispatcher.Invoke(() => OutputText.Text += buffer[0]);
                    }
                }
            });

            _ = Task.Run(async () =>
            {
                string? line;
                while ((line = await activeProcess.StandardError.ReadLineAsync()) != null)
                {
                    if (line.Contains("No module named"))
                    {
                        string? missingModule = ParseMissingModule(line);
                        if (!string.IsNullOrEmpty(missingModule))
                        {
                            Dispatcher.Invoke(() => OutputText.Text += $"\n[AutoFix] Installing missing module: {missingModule}...\n");
                            bool installed = await InstallPythonPackage(missingModule);
                            if (installed)
                            {
                                Dispatcher.Invoke(() => OutputText.Text += $"[AutoFix] Successfully installed {missingModule}. Restarting script...\n");
                                Dispatcher.Invoke(() => RunPythonScript());
                            }
                            else
                            {
                                Dispatcher.Invoke(() => OutputText.Text += $"[AutoFix] Failed to install {missingModule}.\n");
                            }
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(() => OutputText.Text += "\n[Error] " + line);
                    }
                }
            });

            activeProcess.Exited += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OutputText.Text = string.Empty;
                });
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
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"-m pip install {package}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using Process process = new Process
            {
                StartInfo = psi
            };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            bool success = !error.Contains("ERROR", StringComparison.OrdinalIgnoreCase);
            return success;
        }
        catch
        {
            return false;
        }
    }

    private static string? ParseMissingModule(string errorText)
    {
        int start = errorText.IndexOf("No module named '");
        if (start == -1)
            return null;
        start += "No module named '".Length;

        int end = errorText.IndexOf("'", start);
        if (end == -1)
            return null;
        return errorText[start..end];
    }

    private void StopActiveProcess()
    {
        try
        {
            if (activeProcess != null && !activeProcess.HasExited)
                activeProcess.Kill();
        }
        catch { }
    }

    #region top menu buttons
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        StopLoop();
        OnRemove?.Invoke(this);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        EditBlockButton.ContextMenu.PlacementTarget = EditBlockButton;
        EditBlockButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        EditBlockButton.ContextMenu.IsOpen = true;
    }

    private void SelectPythonFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Python files (*.py)|*.py|All files (*.*)|*.*" };
        if (dlg.ShowDialog() == true)
        {
            pythonFilePath = dlg.FileName;
            SetFileNameText();
            RunPythonScript();
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(OutputText.Text);
    }

    #endregion

    #region loop button behaviour
    public void ToggleLoop_Click(object sender, RoutedEventArgs e)
    {
        isLooping = !isLooping;

        if (isLooping)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox("Interval in seconds:", "Loop Settings", LoopInterval.ToString());
            if (double.TryParse(input, out double sec) && sec > 0)
                LoopInterval = sec;

            loopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(LoopInterval) };
            loopTimer.Tick += (s, _) => RunPythonScript();
            loopTimer.Start();
        }
        else
        {
            StopLoop();
        }

        UpdateLoopStatus();
    }

    private void UpdateLoopStatus()
    {
        if (isLooping)
            OutputText.Text = $"Loop: ON | Interval: {LoopInterval}s | File: {Path.GetFileNameWithoutExtension(pythonFilePath)}";
    }

    private void StopLoop()
    {
        loopTimer?.Stop();
        loopTimer = null;
    }
    #endregion

    #region input things
    private void ToggleInput_Click(object sender, RoutedEventArgs e)
    {
        isInputVisible = !isInputVisible;
        UserInputBox.Visibility = isInputVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UserInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && activeProcess != null && !activeProcess.HasExited)
        {
            string input = UserInputBox.Text.Trim();
            if (!string.IsNullOrEmpty(input))
            {
                activeProcess.StandardInput.WriteLine(input);
                OutputText.Text += $"\n>>> {input}\n";
                UserInputBox.Clear();
            }
        }
    }
    #endregion

    public void SetFileNameText()
    {
        if (!string.IsNullOrEmpty(pythonFilePath))
            FileNameText.Text = Path.GetFileNameWithoutExtension(pythonFilePath);
    }

    public void RestoreLoopState()
    {
        if (isLooping)
        {
            if (!string.IsNullOrEmpty(pythonFilePath) && File.Exists(pythonFilePath))
            {
                loopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(LoopInterval) };
                loopTimer.Tick += (s, _) => RunPythonScript();
                loopTimer.Start();

                UpdateLoopStatus();
            }
            else
            {
                isLooping = false;
            }
        }
        else
        {
            UpdateLoopStatus();
        }
    }
}
