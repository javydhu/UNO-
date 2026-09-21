using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Uno.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            // Ocultar pantalla de inicio
            var pantallaInicio = this.FindControl<Grid>("StartScreen");
            if (pantallaInicio != null) pantallaInicio.IsVisible = false;

            // Mostrar mesa de juego
            var pantallaJuego = this.FindControl<Grid>("GameScreen");
            if (pantallaJuego != null) pantallaJuego.IsVisible = true;
            
            // InitializeDeck();
            // DealCards();
        }
    }
}