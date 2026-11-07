using Avalonia.Controls;
using Avalonia.Input;

namespace Uno.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void EfectoHoverEnMano(object? sender, PointerWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                if (e.Delta.Y != 0)
                {
                    double scrollVel = 40; 
                    double newXOffset = scrollViewer.Offset.X - (e.Delta.Y * scrollVel);
                    scrollViewer.Offset = new Avalonia.Vector(newXOffset, scrollViewer.Offset.Y);
                    e.Handled = true;
                }
            }
        }
    }
}