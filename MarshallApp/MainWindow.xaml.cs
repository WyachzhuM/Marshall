using MarshallApp.Models;
using MarshallApp.Services;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MarshallApp.Controllers;

namespace MarshallApp;

public partial class MainWindow : INotifyPropertyChanged
{
    private bool _isRestoring;
    private readonly AppConfig _appConfig;
    private readonly List<BlockElement> _blocks = [];
    private WallpaperController? _wallControl;
    private readonly DispatcherTimer _wallpaperTimer = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        WallpaperControlInit();


        PrimWindow.MouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        };

        ScriptBrowser.ScriptSelected += ScriptBrowser_ScriptSelected;
        ScriptBrowser.ScriptOpenInNewPanel += ScriptBrowser_OpenInNewPanel;

        _appConfig = ConfigManager.Load();

        Width = _appConfig.WindowWidth;
        Height = _appConfig.WindowHeight;

        LoadPanelState();
        LoadAllConfigs();
        NewScript();
    }

    private void WallpaperControlInit()
    {
        var imagesSource = Path.Combine(Environment.CurrentDirectory + "/Resource/Background");
        _wallControl = new WallpaperController(RootImageBrush, imagesSource);
        _wallControl.Update();
        
        _wallpaperTimer.Interval = TimeSpan.FromSeconds(30);
        _wallpaperTimer.Tick += (_, _) =>
        {
            _wallControl.Update();
        };
        _wallpaperTimer.Start();
    }

    private void NewScript()
    {
        CodeEditor.NewScript();
        CodeEditor.Visibility = Visibility.Visible;
    }

    private void RemoveBlockElement(BlockElement element)
    {
        _blocks.Remove(element);
        MStackPanel.Children.Remove(element);
        UpdateLayoutGrid();
        SaveAppConfig();
    }

    private void UpdateLayoutGrid()
    {
        var total = MStackPanel.Children.Count;
        if(total == 0) return;

        var columns = (int)Math.Ceiling(Math.Sqrt(total));
        var rows = (int)Math.Ceiling((double)total / columns);

        MStackPanel.RowDefinitions.Clear();
        MStackPanel.ColumnDefinitions.Clear();

        for(var i = 0; i < rows; i++)
            MStackPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        for(var j = 0; j < columns; j++)
            MStackPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for(var i = 0; i < total; i++)
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
            PythonFilePath = filePath
        };

        _blocks.Add(block);
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
        if (AddButton.ContextMenu == null) return;
        AddButton.ContextMenu.PlacementTarget = AddButton;
        AddButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        AddButton.ContextMenu.IsOpen = true;
    }
    
    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        if (HelpButton.ContextMenu == null) return;
        HelpButton.ContextMenu.PlacementTarget = AddButton;
        HelpButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        HelpButton.ContextMenu.IsOpen = true;
    }

    private void WindowButton_Click(object sender, RoutedEventArgs e)
    {
        Console.WriteLine($"{LeftCol.Width}, {LeftPanelVisible}");
        
        if (WindowButton.ContextMenu == null) return;
        WindowButton.ContextMenu.PlacementTarget = WindowButton;
        WindowButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        WindowButton.ContextMenu.IsOpen = true;
    }

    private void AddBlock_Click(object sender, RoutedEventArgs e)
    {
        var block = new BlockElement(RemoveBlockElement);
        _blocks.Add(block);
        MStackPanel.Children.Add(block);
        UpdateLayoutGrid();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Настройки скоро будут здесь...");
    }

    private void ScriptBrowserHideChecker_Checked(object sender, RoutedEventArgs e)
    {
        if (_isRestoring) return;
        LeftCol.Width = new GridLength(LeftCol.MaxWidth);
    }

    private void ScriptBrowserHideChecker_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isRestoring) return;
        LeftCol.Width = new GridLength(0);
    }

    private void ScriptEditorHideChecker_Checked(object sender, RoutedEventArgs e)
    {
        if (_isRestoring) return;
        RightCol.Width = new GridLength(RightCol.MaxWidth);
    }

    private void ScriptEditorHideChecker_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isRestoring) return;
        RightCol.Width = new GridLength(0);
    }

    public bool LeftPanelVisible => LeftCol.Width != new GridLength(0);
    public bool RightPanelVisible => RightCol.Width != new GridLength(0);
    #endregion

    #region LoadSaving things

    private void SaveAppConfig()
    {
        _appConfig.WindowWidth = Width;
        _appConfig.WindowHeight = Height;

        _appConfig.PanelState = new PanelState(LeftCol.Width, RightCol.Width);

        _appConfig.Blocks.Clear();
        foreach(var block in _blocks)
        {
            _appConfig.Blocks.Add(new BlockConfig
            {
                PythonFilePath = block.PythonFilePath,
                IsLooping = block.IsLooping,
                LoopIntervalSeconds = block.LoopInterval
            });
        }

        ConfigManager.Save(_appConfig);
    }

    private void LoadAllConfigs()
    {
        foreach(var cfg in _appConfig.Blocks)
        {
            var block = new BlockElement(RemoveBlockElement)
            {
                PythonFilePath = cfg.PythonFilePath,
                IsLooping = cfg.IsLooping,
                LoopInterval = cfg.LoopIntervalSeconds
            };

            _blocks.Add(block);
            MStackPanel.Children.Add(block);

            if(!string.IsNullOrEmpty(block.PythonFilePath) && File.Exists(block.PythonFilePath))
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
        _isRestoring = true;

        LeftCol.Width = _appConfig.PanelState.Left;
        
        ScriptBrowser.Visibility = Visibility.Visible;
        ScriptBrowser.InvalidateMeasure();
        ScriptBrowser.UpdateLayout();
        
        RightCol.Width = _appConfig.PanelState.Right;

        ScriptBrowserHideChecker.IsChecked = LeftCol.Width.Value > 0;
        ScriptEditorHideChecker.IsChecked = RightCol.Width.Value > 0;

        _isRestoring = false;
    }

    #endregion

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        SaveAppConfig();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
        {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
            FullscreenEnter.Visibility = Visibility.Visible;
            FullscreenExit.Visibility = Visibility.Collapsed;
        }
        else
        {
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            FullscreenEnter.Visibility = Visibility.Collapsed;
            FullscreenExit.Visibility = Visibility.Visible;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        SaveAppConfig();
        Environment.Exit(0);
    }

    private void AboutMarshall_Click(object sender, RoutedEventArgs e)
    {
        if(About.Instance != null) return;
        
        var aboutWindow = new About();
        aboutWindow.Show();
    }
}