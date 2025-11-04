namespace MarshallApp.Models
{
    public class BlockConfig
    {
        public string? PythonFilePath { get; set; }
        public bool IsLooping { get; set; }
        public double LoopIntervalSeconds { get; set; } = 5.0;
    }
}
