using ICSharpCode.AvalonEdit;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MarshallApp
{
    public partial class CodeEditorPanel : UserControl
    {
        private string? currentFilePath;
        private readonly string scriptsFolder = "Scripts";

        public CodeEditorPanel()
        {
            InitializeComponent();
            Directory.CreateDirectory(scriptsFolder);
        }

        public void LoadScript(string filePath)
        {
            currentFilePath = filePath;
            FileNameText.Text = Path.GetFileName(filePath);
            Editor.Text = File.ReadAllText(filePath);
        }

        public void NewScript()
        {
            currentFilePath = null;
            FileNameText.Text = "new_script.py";
            Editor.Clear();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveScript(false);
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveScript(true);
        }

        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmUnsavedChanges())
                NewScript();
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmUnsavedChanges())
                return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = Path.GetFullPath(scriptsFolder),
                Filter = "Python files (*.py)|*.py|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
                LoadScript(dlg.FileName);
        }

        private void SaveScript(bool saveAs)
        {
            string code = Editor.Text;

            if (currentFilePath == null || saveAs)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    InitialDirectory = Path.GetFullPath(scriptsFolder),
                    Filter = "Python files (*.py)|*.py",
                    FileName = FileNameText.Text
                };
                if (dlg.ShowDialog() == true)
                {
                    currentFilePath = dlg.FileName;
                    FileNameText.Text = Path.GetFileName(currentFilePath);
                }
                else return;
            }

            File.WriteAllText(currentFilePath, code);
            MessageBox.Show($"Saved: {Path.GetFileName(currentFilePath)}",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            (FindParent<MainWindow>()?.ScriptBrowser)?.RefreshScripts();
        }

        private bool ConfirmUnsavedChanges()
        {
            if (!string.IsNullOrWhiteSpace(Editor.Text))
            {
                var result = MessageBox.Show("Save changes before continuing?",
                    "Confirm", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel) return false;
                if (result == MessageBoxResult.Yes) SaveScript(false);
            }
            return true;
        }

        private T? FindParent<T>() where T : DependencyObject
        {
            DependencyObject parent = this;
            while (parent != null && !(parent is T))
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }
    }
}
