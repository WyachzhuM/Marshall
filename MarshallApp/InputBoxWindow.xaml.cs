using System.Windows;
using System.Windows.Input;

namespace MarshallApp;

public partial class InputBoxWindow
{
    private readonly Action<string> _okCallback;

    public InputBoxWindow(string title, string desc, Action<string> okCallback, string defaultValue = "")
    {
        InitializeComponent();

        _okCallback = okCallback;
        Title.Text =  title;
        DescTextField.Text = desc;
        Input.Text = defaultValue;
    }
    
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _okCallback.Invoke(Input.Text);
        this.Close();
    }

    private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            this.DragMove();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Input.Focus();
        Input.SelectAll();
    }
}