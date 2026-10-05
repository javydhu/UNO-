using Avalonia.Controls;
using Avalonia.Input; // <-- Necesario para PointerWheelEventArgs

namespace Uno.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        //Convierte el scroll
        private void OnHandPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // Si detectamos un movimiento vertical (trackpad hacia arriba/abajo o rueda del mouse)
                if (e.Delta.Y != 0)
                {
                    double scrollVel = 40; 
                    double newXOffset = scrollViewer.Offset.X - (e.Delta.Y * scrollVel);

                    // Aplicamos el nuevo offset al ScrollViewer (Avalonia se encarga de que no se salga de los bordes)
                    scrollViewer.Offset = new Avalonia.Vector(newXOffset, scrollViewer.Offset.Y);

                    // Marcamos el evento como "manejado" para que no interfiera con otros controles de la ventana
                    e.Handled = true;
                }
            }
        }
    }
}