// Leia - Yuyoyuppe(ゆよゆっぺ)

using MarshallApp.Models;
using MarshallApp.Services;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace MarshallApp;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly AppConfig appConfig;
    private readonly List<BlockElement> blocks = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        ScriptBrowser.ScriptSelected += ScriptBrowser_ScriptSelected;
        ScriptBrowser.ScriptOpenInNewPanel += ScriptBrowser_OpenInNewPanel;

        appConfig = ConfigManager.Load();

        Width = appConfig.WindowWidth;
        Height = appConfig.WindowHeight;

        LoadPanelState();
        LoadAllConfigs();
        NewScript();
    }

    private void NewScript()
    {
        CodeEditor.NewScript();
        CodeEditor.Visibility = Visibility.Visible;
    }

    private void RemoveBlockElement(BlockElement element)
    {
        blocks.Remove(element);
        MStackPanel.Children.Remove(element);
        UpdateLayoutGrid();
        SaveAppConfig();
    }

    private void UpdateLayoutGrid()
    {
        int total = MStackPanel.Children.Count;
        if(total == 0) return;

        int columns = (int)Math.Ceiling(Math.Sqrt(total));
        int rows = (int)Math.Ceiling((double)total / columns);

        MStackPanel.RowDefinitions.Clear();
        MStackPanel.ColumnDefinitions.Clear();

        for(int i = 0; i < rows; i++)
            MStackPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        for(int j = 0; j < columns; j++)
            MStackPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for(int i = 0; i < total; i++)
        {
            var element = MStackPanel.Children[i];
            Grid.SetRow(element, i / columns);
            Grid.SetColumn(element, i % columns);
        }
    }

    #region Script Browser
    private void ScriptBrowser_OpenInNewPanel(string filePath)
    {
        var block = new BlockElement(RemoveBlockElement)
        {
            pythonFilePath = filePath
        };

        blocks.Add(block);
        MStackPanel.Children.Add(block);
        block.RunPythonScript();

        UpdateLayoutGrid();
        SaveAppConfig();
    }

    private void ScriptBrowser_ScriptSelected(string filePath)
    {
        CodeEditor.LoadScript(filePath);
        CodeEditor.Visibility = Visibility.Visible;
    }
    #endregion

    #region Top Panel Menu
    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        AddButton.ContextMenu.PlacementTarget = AddButton;
        AddButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        AddButton.ContextMenu.IsOpen = true;
    }

    private void WindowButton_Click(object sender, RoutedEventArgs e)
    {
        Console.WriteLine($"{LeftCol.Width}, {LeftPanelVisible}");
        WindowButton.ContextMenu.PlacementTarget = WindowButton;
        WindowButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        WindowButton.ContextMenu.IsOpen = true;
    }

    private void AddBlock_Click(object sender, RoutedEventArgs e)
    {
        var block = new BlockElement(RemoveBlockElement);
        blocks.Add(block);
        MStackPanel.Children.Add(block);
        UpdateLayoutGrid();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Настройки скоро будут здесь...");
    }

    private void ScriptBrowserHideChecker_Checked(object sender, RoutedEventArgs e)
    {
        LeftCol.Width = new GridLength(LeftCol.MaxWidth);
    }

    private void ScriptBrowserHideChecker_Unchecked(object sender, RoutedEventArgs e)
    {
        LeftCol.Width = new GridLength(0);
    }

    private void ScriptEditorHideChecker_Checked(object sender, RoutedEventArgs e)
    {
        RightCol.Width = new GridLength(RightCol.MaxWidth);
    }

    private void ScriptEditorHideChecker_Unchecked(object sender, RoutedEventArgs e)
    {
        RightCol.Width = new GridLength(0);
    }

    public bool LeftPanelVisible => LeftCol.Width != new GridLength(0);
    public bool RightPanelVisible => RightCol.Width != new GridLength(0);
    #endregion

    #region LoadSaving things

    private void SaveAppConfig()
    {
        appConfig.WindowWidth = Width;
        appConfig.WindowHeight = Height;
        appConfig.PanelState = new PanelState(LeftCol.Width.Value, RightCol.Width.Value);

        appConfig.Blocks.Clear();
        foreach(var block in blocks)
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
        foreach(var cfg in appConfig.Blocks)
        {
            var block = new BlockElement(RemoveBlockElement)
            {
                pythonFilePath = cfg.PythonFilePath,
                isLooping = cfg.IsLooping,
                LoopInterval = cfg.LoopIntervalSeconds
            };

            blocks.Add(block);
            MStackPanel.Children.Add(block);

            if(!string.IsNullOrEmpty(block.pythonFilePath) && File.Exists(block.pythonFilePath))
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

    #endregion

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        SaveAppConfig();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}