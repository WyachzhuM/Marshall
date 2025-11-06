using MarshallApp.Models;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace MarshallApp
{
    public partial class MainWindow : Window
    {
        private AppConfig appConfig;
        private readonly List<BlockElement> blocks = new();
        private Grid stack;

        public MainWindow()
        {
            InitializeComponent();
            stack = MStackPanel;

            ScriptBrowser.ScriptSelected += ScriptBrowser_ScriptSelected;
            ScriptBrowser.ScriptOpenInNewPanel += ScriptBrowser_OpenInNewPanel;

            appConfig = ConfigManager.Load();

            Width = appConfig.WindowWidth;
            Height = appConfig.WindowHeight;

            LoadPanelState();
            LoadAllConfigs();
            NewScript();
        }

        private void ScriptBrowser_OpenInNewPanel(string filePath)
        {
            var block = new BlockElement(RemoveBlockElement)
            {
                pythonFilePath = filePath
            };

            blocks.Add(block);
            stack.Children.Add(block);
            block.RunPythonScript();

            UpdateLayoutGrid();
            SaveAppConfig();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddButton.ContextMenu.PlacementTarget = AddButton;
            AddButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            AddButton.ContextMenu.IsOpen = true;
        }

        private void AddBlock_Click(object sender, RoutedEventArgs e)
        {
            AddBlockElement();
        }

        private void ScriptBrowser_ScriptSelected(string filePath)
        {
            CodeEditor.LoadScript(filePath);
            CodeEditor.Visibility = Visibility.Visible;
        }

        private void AddBlockElement()
        {
            var block = new BlockElement(RemoveBlockElement);
            blocks.Add(block);
            stack.Children.Add(block);
            UpdateLayoutGrid();
        }

        private void RemoveBlockElement(BlockElement element)
        {
            blocks.Remove(element);
            stack.Children.Remove(element);
            UpdateLayoutGrid();
            SaveAppConfig();
        }

        private void UpdateLayoutGrid()
        {
            int total = stack.Children.Count;
            if (total == 0) return;

            int columns = (int)Math.Ceiling(Math.Sqrt(total));
            int rows = (int)Math.Ceiling((double)total / columns);

            stack.RowDefinitions.Clear();
            stack.ColumnDefinitions.Clear();

            for (int i = 0; i < rows; i++)
                stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            for (int j = 0; j < columns; j++)
                stack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < total; i++)
            {
                var element = stack.Children[i];
                Grid.SetRow(element, i / columns);
                Grid.SetColumn(element, i % columns);
            }
        }

        private void NewScript()
        {
            CodeEditor.NewScript();
            CodeEditor.Visibility = Visibility.Visible;
        }

        private void SaveAppConfig()
        {
            // Обновляем состояние окна
            appConfig.WindowWidth = this.Width;
            appConfig.WindowHeight = this.Height;

            // Обновляем состояние панелей
            appConfig.PanelState = new PanelState(LeftCol.Width.Value, RightCol.Width.Value);

            // Обновляем блоки
            appConfig.Blocks.Clear();
            foreach (var block in blocks)
            {
                appConfig.Blocks.Add(new BlockConfig
                {
                    PythonFilePath = block.pythonFilePath,
                    IsLooping = block.isLooping,
                    LoopIntervalSeconds = block.LoopInterval
                });
            }

            ConfigManager.Save(appConfig);
        }

        private void LoadAllConfigs()
        {
            foreach (var cfg in appConfig.Blocks)
            {
                var block = new BlockElement(RemoveBlockElement)
                {
                    pythonFilePath = cfg.PythonFilePath,
                    isLooping = cfg.IsLooping,
                    LoopInterval = cfg.LoopIntervalSeconds
                };

                blocks.Add(block);
                stack.Children.Add(block);

                if (!string.IsNullOrEmpty(block.pythonFilePath) && File.Exists(block.pythonFilePath))
                {
                    block.SetFileNameText();
                    block.RunPythonScript();
                }
                block.RestoreLoopState();
            }

            UpdateLayoutGrid();
        }

        private void LoadPanelState()
        {
            var state = appConfig.PanelState;
            LeftCol.Width = new GridLength(state.LeftWidth);
            RightCol.Width = new GridLength(state.RightWidth);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            SaveAppConfig();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Настройки скоро будут здесь...");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}