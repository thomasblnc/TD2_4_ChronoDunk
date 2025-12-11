using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChronoDunk
{
    /// <summary>
    /// Logique d'interaction pour UCMenuPrincipal.xaml
    /// </summary>
    public partial class UCMenuPrincipal : UserControl
    {
        public UCMenuPrincipal()
        {
            InitializeComponent();
        }

        private void buttonJeu_Click(object sender, RoutedEventArgs e)
        {
            // 1. On passe '1' (ou un autre chiffre) pour définir le mode de jeu.
            // 2. On change le type de la variable de 'UCJeu' à 'UCChoixAdversaire' (ou var).
            UCChoixAdversaire interfaceChoix = new UCChoixAdversaire(1);

            this.Content = interfaceChoix;
        }

        private void buttonQuitter_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void buttonEntrainement_Click(object sender, RoutedEventArgs e)
        {
            UCModeEntrainement modeEntrainement = new UCModeEntrainement();

            this.Content = modeEntrainement;
        }

        private void buttonRegles_Click(object sender, RoutedEventArgs e)
        {
            UCReglesJeu interfaceRegles = new UCReglesJeu();

            this.Content = interfaceRegles;
        }
    }
}
