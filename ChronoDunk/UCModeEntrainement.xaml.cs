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
    /// Logique d'interaction pour UCModeEntrainement.xaml
    /// </summary>
    public partial class UCModeEntrainement : UserControl
    {
        public UCModeEntrainement()
        {
            InitializeComponent();
        }

        private void buttonQuitter_Click(object sender, RoutedEventArgs e)
        {
            this.Content = new UCMenuPrincipal();
        }

        private void buttonReprendre_Click(object sender, RoutedEventArgs e)
        {
            canvasMenuPause.Visibility = Visibility.Collapsed;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            canvasMenuPause.Visibility = Visibility.Visible;
        }
    }
}
    
