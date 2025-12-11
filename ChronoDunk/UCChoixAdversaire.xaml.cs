using System.Windows;
using System.Windows.Controls;

namespace ChronoDunk
{
    public partial class UCChoixAdversaire : UserControl
    {
        int gameMode;

        public UCChoixAdversaire(int mode)
        {
            InitializeComponent();
            this.gameMode = mode;
        }

        private void SelectLebron_Click(object sender, RoutedEventArgs e)
        {
            // Lebron : Moins rapide (11), Saute haut (-28)
            var stats = new CharacterStats("Homme", "/images/player.png", 11, -28, 1.5);
            StartGame(stats);
        }

        private void SelectCurry_Click(object sender, RoutedEventArgs e)
        {
            // Curry : Rapide (14), Saute moins haut (-25)
            // Assure-toi d'avoir une image pour lui, sinon remet "/Assets/player.png"
            var stats = new CharacterStats("Femme", "/images/player2.png", 14, -25, 1.0);
            StartGame(stats);
        }

        private void StartGame(CharacterStats selectedChar)
        {
            // Lance le jeu avec les stats !
            this.Content = (new UCJeu(gameMode, selectedChar));
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            UCMenuPrincipal interfaceJeu = new UCMenuPrincipal();

            this.Content = interfaceJeu;
        }
    }
}