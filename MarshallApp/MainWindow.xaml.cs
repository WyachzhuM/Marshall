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
        private readonly List<BlockElement> blocks = new();
        private Grid stack;

        public MainWindow()
        {
            InitializeComponent();
            stack = MStackPanel;

            ScriptBrowser.ScriptSelected += ScriptBrowser_ScriptSelected;
            ScriptBrowser.ScriptOpenInNewPanel += ScriptBrowser_OpenInNewPanel;

            NewScript();
            LoadPanelState();
            LoadAllConfigs();
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
            SaveAllConfigs();
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
            SaveAllConfigs();
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

        #region Config & Panel State

        private void SavePanelState()
        {
            var state = new PanelState(LeftCol.Width.Value, RightCol.Width.Value);

            File.WriteAllText("panel_state.json", JsonSerializer.Serialize(state));
        }

        private void LoadPanelState()
        {
            if (File.Exists("panel_state.json"))
            {
                var json = File.ReadAllText("panel_state.json");
                var state = JsonSerializer.Deserialize<PanelState>(json);
                if (state != null)
                {
                    LeftCol.Width = new GridLength(state.LeftWidth);
                    RightCol.Width = new GridLength(state.RightWidth);
                }
            }
            else
            {

            }
        }

        #endregion

        private void SaveAllConfigs()
        {
            var configs = new List<BlockConfig>();

            foreach (var block in blocks)
            {
                configs.Add(new BlockConfig
                {
                    PythonFilePath = block.pythonFilePath,
                    IsLooping = block.isLooping,
                    LoopIntervalSeconds = block.LoopInterval
                });
            }

            ConfigManager.SaveAll(configs);
            SavePanelState();
        }

        private void LoadAllConfigs()
        {
            var configs = ConfigManager.LoadAll();
            foreach (var cfg in configs)
            {
                var block = new BlockElement(RemoveBlockElement);
                block.pythonFilePath = cfg.PythonFilePath;
                block.isLooping = cfg.IsLooping;
                block.LoopInterval = cfg.LoopIntervalSeconds;

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

        private void NewScript()
        {
            CodeEditor.NewScript();
            CodeEditor.Visibility = Visibility.Visible;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            SaveAllConfigs();
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