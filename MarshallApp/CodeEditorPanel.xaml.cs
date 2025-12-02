using System.IO;
using System.Windows;
using System.Windows.Media;

namespace MarshallApp;

public partial class CodeEditorPanel
{
    private string? _currentFilePath;
    private const string ScriptsFolder = "Scripts";

    public CodeEditorPanel()
    {
        InitializeComponent();
        Directory.CreateDirectory(ScriptsFolder);
    }

    public void LoadScript(string filePath)
    {
        _currentFilePath = filePath;
        FileNameText.Text = Path.GetFileName(filePath);
        Editor.Text = File.ReadAllText(filePath);
    }

    public void NewScript()
    {
        _currentFilePath = null;
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
        if(ConfirmUnsavedChanges())
            NewScript();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if(!ConfirmUnsavedChanges())
            return;

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            InitialDirectory = Path.GetFullPath(ScriptsFolder),
            Filter = "Python files (*.py)|*.py|All files (*.*)|*.*"
        };

        if(dlg.ShowDialog() == true)
            LoadScript(dlg.FileName);
    }

    private void SaveScript(bool saveAs)
    {
        var code = Editor.Text;

        if(_currentFilePath == null || saveAs)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = Path.GetFullPath(ScriptsFolder),
                Filter = "Python files (*.py)|*.py",
                FileName = FileNameText.Text
            };
            if(dlg.ShowDialog() == true)
            {
                _currentFilePath = dlg.FileName;
                FileNameText.Text = Path.GetFileName(_currentFilePath);
            }
            else return;
        }

        File.WriteAllText(_currentFilePath, code);
        MessageBox.Show($"Saved: {Path.GetFileName(_currentFilePath)}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);

        (FindParent<MainWindow>()?.ScriptBrowser)?.RefreshScripts();
    }

    private bool ConfirmUnsavedChanges()
    {
        if (string.IsNullOrWhiteSpace(Editor.Text)) return true;
        var result = MessageBox.Show("Save changes before continuing?",
            "Confirm", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        switch (result)
        {
            case MessageBoxResult.Cancel:
                return false;
            case MessageBoxResult.Yes:
                SaveScript(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        return true;
    }

    private T? FindParent<T>() where T : DependencyObject
    {
        DependencyObject? parent = this;
        while(parent != null && parent is not T)
            parent = VisualTreeHelper.GetParent(parent);
        return parent as T;
    }
}
