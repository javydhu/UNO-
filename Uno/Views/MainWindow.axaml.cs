using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Uno.ViewModels;

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

        // Boton Copiar ID de la sala de espera (solo copia el texto al portapapeles)
        private async void CopiarId_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm || string.IsNullOrEmpty(vm.PartidaIdTexto)) return;

            var portapapeles = TopLevel.GetTopLevel(this)?.Clipboard;
            if (portapapeles == null) return;
            await portapapeles.SetTextAsync(vm.PartidaIdTexto);

            if (sender is Button boton)
            {
                boton.Content = "¡Copiado!";
                await Task.Delay(1500);
                boton.Content = "Copiar ID";
            }
        }
    }
}