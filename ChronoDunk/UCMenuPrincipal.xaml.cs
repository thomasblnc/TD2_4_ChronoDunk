using Projet_sae;
using System.Windows;
using System.Windows.Controls;

namespace ChronoDunk
{
    public partial class UCMenuPrincipal : UserControl
    {
        // Variable statique pour conserver le score entre les parties
        public static int meilleurScore = 0;

        public UCMenuPrincipal()
        {
            InitializeComponent();

            // Mise à jour du texte
            labMeilleurScore.Text = $"Meilleur score : {meilleurScore}";
        }

        private void buttonJeu_Click(object sender, RoutedEventArgs e)
        {
            // Navigation vers le choix de personnage (Mode 1 = Jeu Normal)
            Navigate(new UCChoixAdversaire(1));
        }

        private void buttonEntrainement_Click(object sender, RoutedEventArgs e)
        {
            // Navigation vers le choix de personnage (Mode 0 = Entrainement)
            Navigate(new UCChoixAdversaire(0));
        }

        private void buttonRegles_Click(object sender, RoutedEventArgs e)
        {
            Navigate(new UCReglesJeu());
        }

        private void buttonCopyright_Click(object sender, RoutedEventArgs e)
        {
            Navigate(new UCCredits());
        }

        private void buttonQuitter_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Méthode utilitaire pour éviter de répéter la logique de navigation
        private void Navigate(UserControl page)
        {
            var mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.Content = page;
        }
    }
}