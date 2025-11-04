using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarshallApp.Models
{
    public class PanelState
    {
        public PanelState(double leftWidth, double rightWidth)
        {
            LeftWidth = leftWidth;
            RightWidth = rightWidth;
        }

        public double LeftWidth { get; set; }
        public double RightWidth { get; set; }
    }
}
