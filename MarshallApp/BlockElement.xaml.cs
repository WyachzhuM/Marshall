using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MarshallApp
{
    public partial class BlockElement : UserControl
    {
        public Action<BlockElement>? OnRemove;
        public string? pythonFilePath;
        public bool isLooping = false;
        public double LoopInterval { get; set; } = 5.0;
        private DispatcherTimer? loopTimer;

        public BlockElement(Action<BlockElement>? onRemove)
        {
            InitializeComponent();
            OnRemove = onRemove;
            UpdateLoopButton();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
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
            else
            {
                OutputText.Text = "No file selected!";
            }
        }

        public void SetFileNameText()
        {
            if (!string.IsNullOrEmpty(pythonFilePath))
            {
                FileNameText.Text = Path.GetFileNameWithoutExtension(pythonFilePath);
            }
        }

        public async void RunPythonScript()
        {
            if (string.IsNullOrEmpty(pythonFilePath) || !File.Exists(pythonFilePath))
            {
                OutputText.Text = "File not found or not selected!";
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{pythonFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                using (Process process = new Process { StartInfo = psi })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    if (error.Contains("ModuleNotFoundError"))
                    {
                        string? missing = ParseMissingModule(error);
                        if (!string.IsNullOrEmpty(missing))
                        {
                            OutputText.Text = $"Module not found: {missing}. We're trying to install it...";
                            bool installed = await InstallPythonPackage(missing);
                            if (installed)
                            {
                                OutputText.Text += $"\nThe {missing} module is installed. Restarting the script...\n";
                                RunPythonScript();
                                return;
                            }
                            else
                            {
                                OutputText.Text += $"\nCouldn't install {missing}. Install manually: pip install {missing}";
                            }
                        }
                    }
                    else
                    {
                        OutputText.Text = string.IsNullOrWhiteSpace(error) ? $"{output}" : $"[{Path.GetFileNameWithoutExtension(pythonFilePath)}]\n{output}\nError:\n{error}";
                    }
                }
            }
            catch (Exception ex)
            {
                OutputText.Text = $"Python startup error:\n{ex.Message}";
            }
        }

        private string? ParseMissingModule(string errorText)
        {
            int start = errorText.IndexOf("No module named '");
            if (start == -1)
                return null;
            start += "No module named '".Length;

            int end = errorText.IndexOf("'", start);
            if (end == -1)
                return null;
            return errorText.Substring(start, end - start);
        }

        private async Task<bool> InstallPythonPackage(string package)
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

        public void ToggleLoop_Click(object sender, RoutedEventArgs e)
        {
            isLooping = !isLooping;

            if (isLooping)
            {
                string input = Microsoft.VisualBasic.Interaction.InputBox(
                    "Interval in seconds:", "Loop Settings", LoopInterval.ToString());

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

            UpdateLoopButton();
            UpdateLoopStatus();
        }

        private void UpdateLoopButton()
        {
            LoopToggleButton.Content = $"Loop: {(isLooping ? "ON" : "OFF")}";
        }

        private void UpdateLoopStatus()
        {
            if (isLooping)
            {
                OutputText.Text = $"Loop: ON | Interval: {LoopInterval}s | File: {Path.GetFileNameWithoutExtension(pythonFilePath)}\n\nWaiting for next run...";
            }
            else
            {
                OutputText.Text = string.IsNullOrEmpty(pythonFilePath)
                    ? "(script output will appear here)"
                    : $"File: {Path.GetFileNameWithoutExtension(pythonFilePath)}\nReady to run.";
            }
        }

        private void StopLoop()
        {
            loopTimer?.Stop();
            loopTimer = null;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(OutputText.Text);
        }

        public void RestoreLoopState()
        {
            UpdateLoopButton();

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
                    UpdateLoopButton();
                }
            }
            else
            {
                UpdateLoopStatus();
            }
        }
    }
}
