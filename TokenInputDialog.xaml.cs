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

namespace Aaron.PluginRacenetReceiver
{
    /// <summary>
    /// Interaction logic for TokenInputDialog.xaml
    /// </summary>
    public partial class TokenInputDialog : Window
    {
        public string Token { get; private set; }

        public TokenInputDialog()
        {
            InitializeComponent();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            Token = inputTokenTextBox.Text;
            DialogResult = true;
        }
    }
}
