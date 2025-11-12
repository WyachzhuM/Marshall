namespace MarshallApp.Models
{
    public class PanelState(double leftWidth, double rightWidth)
    {
        public double LeftWidth { get; set; } = leftWidth;
        public double RightWidth { get; set; } = rightWidth;
    }
}
