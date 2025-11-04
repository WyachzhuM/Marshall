using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MarshallApp
{
    public partial class ScriptBrowserPanel : UserControl
    {
        public event Action<string>? ScriptSelected;
        public event Action<string>? ScriptOpenInNewPanel;

        private string scriptsFolder = "Scripts";

        public ScriptBrowserPanel()
        {
            InitializeComponent();
            Directory.CreateDirectory(scriptsFolder);
            LoadScripts();
        }

        private void LoadScripts()
        {
            ScriptList.Items.Clear();
            var files = Directory.GetFiles(scriptsFolder, "*.py");

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                string fileNameNoExt = Path.GetFileNameWithoutExtension(file);

                var button = new Button
                {
                    Content = fileNameNoExt,
                    Tag = file,
                    Margin = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(6, 3, 6, 3),
                    Style = (Style)FindResource("FlatButtonStyle")
                };

                var contextMenu = new ContextMenu();

                var openPanelItem = new MenuItem { Header = "Run in new panel" };
                openPanelItem.Click += (s, e) => ScriptOpenInNewPanel?.Invoke(file);

                var openFolderItem = new MenuItem { Header = "Open file location" };
                openFolderItem.Click += (s, e) =>
                {
                    if (File.Exists(file))
                        Process.Start("explorer.exe", $"/select,\"{file}\"");
                };

                contextMenu.Items.Add(openPanelItem);
                contextMenu.Items.Add(new Separator());
                contextMenu.Items.Add(openFolderItem);

                button.ContextMenu = contextMenu;

                button.Click += (s, e) =>
                {
                    ScriptSelected?.Invoke(file);
                };

                ScriptList.Items.Add(button);
            }
        }

        private void ScriptList_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ScriptList.SelectedItem is string fileName) { string fullPath = Path.Combine(scriptsFolder, fileName); ScriptSelected?.Invoke(fullPath); } }

        public void RefreshScripts() => LoadScripts();
    }
}
