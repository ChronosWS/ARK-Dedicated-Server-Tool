using ARK_Server_Manager.Lib;
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

namespace ARK_Server_Manager
{
    /// <summary>
    /// Interaction logic for ServerDetailControl.xaml
    /// </summary>
    public partial class ServerDetailControl : UserControl
    {
        Server defaultSrv = Server.FromDefaults();

        public ServerDetailControl()
        {
            // Create a default server for the control to avoid binding errors on initialization

            this.DataContext = defaultSrv;
            InitializeComponent();  


        }

        private void listBox_Loaded(object sender, RoutedEventArgs e)
        {
            // If no item is selected, select the first item.
            if (listBox.SelectedIndex < 0)
            {
                listBox.SelectedIndex = 0;
            }
        }
    }
}
