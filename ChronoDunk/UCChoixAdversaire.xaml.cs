using Projet_sae;
using System.Windows;
using System.Windows.Controls;

namespace ChronoDunk
{
    public partial class UCChoixAdversaire : UserControl
    {
        private int _gameMode;

        public UCChoixAdversaire(int mode)
        {
            InitializeComponent();
            _gameMode = mode;
        }

        // HOMME : Rapide (14), Saut Normal (-25)
        private void SelectLebron_Click(object sender, RoutedEventArgs e)
        {
            var stats = new CharacterStats("Homme", "/images/player.png", 14, -25, 1.0);
            StartGame(stats);
        }

        // FEMME : Lente (11), Saut Puissant (-28)
        private void SelectCurry_Click(object sender, RoutedEventArgs e)
        {
            var stats = new CharacterStats("Femme", "/images/player2.png", 11, -28, 1.5);
            StartGame(stats);
        }

        private void StartGame(CharacterStats selectedChar)
        {
            var mainWindow = (MainWindow)Application.Current.MainWindow;

            if (_gameMode == 0) // Mode Entraînement
            {
                mainWindow.Content = new UCModeEntrainement(selectedChar);
            }
            else // Mode Match (vs IA ou JcJ)
            {
                mainWindow.Content = new UCJeu(_gameMode, selectedChar);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.Content = new UCMenuPrincipal();
        }
    }
}