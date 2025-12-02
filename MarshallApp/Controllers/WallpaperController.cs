using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MarshallApp.Controllers;

public class WallpaperController
{
    private readonly ImageBrush _brush;
    private readonly string _workingDirectory;
    private readonly List<string> _imageSet;
    
    public WallpaperController(ImageBrush brush, string  workingDirectory)
    {
        _brush =  brush;
        _workingDirectory = workingDirectory;
        
        _imageSet = GetImages();
    }

    public void Update()
    {
        if (_imageSet.Count <= 0) return;
        var rnd = new Random();
        var current = rnd.Next(0, _imageSet.Count);
        _brush.ImageSource = new BitmapImage(new Uri(Path.Combine(_workingDirectory, _imageSet[current])));
    }

    private List<string> GetImages()
    {
        var imageSet = new List<string>();
        var files = Directory.GetFiles(_workingDirectory);

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (extension is not (".jpg" or ".jpeg" or ".png")) continue;
            imageSet.Add(file);
        }
        
        return imageSet;
    }
}