using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace MarshallApp;

public partial class About
{
    public static About? Instance;
    public About()
    {
        Instance = this;
        
        InitializeComponent();
    }
    
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        
        Instance = null;
    }

    private void OpenGitHub(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/LPLP-ghacc/Marshall#",
            UseShellExecute = true
        });
    }
}