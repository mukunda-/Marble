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

namespace Marble
{
    /// <summary>
    /// Interaction logic for Page1.xaml
    /// </summary>
    public partial class StartSprintPage : Page
    {
        public StartSprintPage()
        {
            InitializeComponent();
        }

        public (bool, double, double) GetSprintSettings() {

            double sprint, rest;
            if (!double.TryParse(sprintText.Text, out sprint)
                || !double.TryParse(restText.Text, out rest))
            {
                MessageBox.Show("Invalid input.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return (false, 0.0, 0.0);
            }
            if (sprint <= 0)
            {
                MessageBox.Show("Invalid sprint range. 😦", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return (false, 0.0, 0.0);
            }
            if (rest < 0)
            {
                MessageBox.Show("Invalid rest range. 🧙", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return (false, 0.0, 0.0);
            }

            return (true, sprint, rest);
        }

    }
}
