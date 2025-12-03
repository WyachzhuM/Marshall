using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MarshallApp;

public partial class ScriptBrowserPanel
{
    public event Action<string>? ScriptSelected;
    public event Action<string>? ScriptOpenInNewPanel;

    private const string ScriptsFolder = "Scripts";

    public ScriptBrowserPanel()
    {
        InitializeComponent();
        Directory.CreateDirectory(ScriptsFolder);
        LoadScripts();
    }

    private void LoadScripts()
    {
        ScriptList.Items.Clear();
        var files = Directory.GetFiles(ScriptsFolder, "*.py");

        foreach (var file in files)
        {
            var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
            
            var button = new Button
            {
                Content = fileNameNoExt,
                Tag = file,
                Margin = new Thickness(3),
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 3, 6, 3),
                Style = (Style?)Application.Current.FindResource("FlatButtonStyle")
            };

            var contextMenu = new ContextMenu
            {
                Style = (Style?)Application.Current.FindResource("DarkContextMenuStyle")
            };

            var openPanelItem = new MenuItem
            {
                Header = "Run in new panel",
                Style = (Style?)Application.Current.FindResource("DarkMenuItemStyle")
            };
            openPanelItem.Click += (_, _) => ScriptOpenInNewPanel?.Invoke(file);

            var openFolderItem = new MenuItem
            {
                Header = "Open file location",
                Style = (Style?)Application.Current.FindResource("DarkMenuItemStyle")
            };
            
            openFolderItem.Click += (_, _) =>
            {
                if (File.Exists(file))
                    Process.Start("explorer.exe", $"/select,\"{file}\"");
            };

            contextMenu.Items.Add(openPanelItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(openFolderItem);

            button.ContextMenu = contextMenu;

            button.Click += (_, _) =>
            {
                ScriptSelected?.Invoke(file);

                try
                {
                    if (MainWindow.Instance != null)
                        MainWindow.Instance.RightCol.Width = new GridLength(MainWindow.Instance.RightCol.MaxWidth);
                }
                catch
                {
                    if (MainWindow.Instance != null)
                        MainWindow.Instance.RightCol.Width = new GridLength(500);
                }
            };

            ScriptList.Items.Add(button);
        }
    }

    public void ScriptList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScriptList.SelectedItem is not string fileName) return;
        var fullPath = Path.Combine(ScriptsFolder, fileName); 
        ScriptSelected?.Invoke(fullPath);
    }

    public void RefreshScripts() => LoadScripts();

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshScripts();
    }
}
