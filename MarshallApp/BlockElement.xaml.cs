// 悲しいという気持ち - Yuyoyuppe


using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private Process? activeProcess;
        private bool isInputVisible = false;

        public BlockElement(Action<BlockElement>? onRemove)
        {
            InitializeComponent();
            OnRemove = onRemove;
            UpdateLoopButton();
            UpdateInputButton();
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
        }

        public void SetFileNameText()
        {
            if (!string.IsNullOrEmpty(pythonFilePath))
                FileNameText.Text = Path.GetFileNameWithoutExtension(pythonFilePath);
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
                        Dispatcher.Invoke(() => OutputText.Text += "\n[Error] " + line);
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
                OutputText.Text = $"Python startup error:\n{ex.Message}";
            }
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

        private void ToggleInput_Click(object sender, RoutedEventArgs e)
        {
            isInputVisible = !isInputVisible;
            UserInputBox.Visibility = isInputVisible ? Visibility.Visible : Visibility.Collapsed;
            UpdateInputButton();
        }

        private void UpdateInputButton()
        {
            //InputToggleButton.Content = $"Input: {(isInputVisible ? "ON" : "OFF")}";
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

            UpdateLoopButton();
            UpdateLoopStatus();
        }

        private void UpdateLoopButton()
        {
            //LoopToggleButton.Content = $"Loop: {(isLooping ? "ON" : "OFF")}";
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

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(OutputText.Text);
        }

        public void RestoreLoopState()
        {
            UpdateLoopButton();
            UpdateInputButton();

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
