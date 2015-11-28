using ARK_Server_Manager.Lib;
using ARK_Server_Manager.Lib.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace ARK_Server_Manager
{
	/// <summary>
	/// Interaction logic for UITests.xaml
	/// </summary>
	public partial class UITests : Window
	{
        Storyboard hideListSB;
        Storyboard hideDetailSB;

        Storyboard showListSB;
        Storyboard showDetailSB;

        public UITests()
		{
			this.InitializeComponent();
			
			// Insert code required on object creation below this point.
		}

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //
            // Kick off the initialization.
            //
            TaskUtils.RunOnUIThreadAsync(() =>
            {
                // We need to load the set of existing servers, or create a blank one if we don't have any...
                foreach (var profile in Directory.EnumerateFiles(Config.Default.ConfigDirectory, "*" + Config.Default.ProfileExtension))
                {
                    try
                    {
                        ServerManager.Instance.AddFromPath(profile);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(String.Format("The profile at {0} failed to load.  The error was: {1}\r\n{2}", profile, ex.Message, ex.StackTrace), "Profile failed to load", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                }
            }).DoNotWait();

            this.DataContext = this;

            showDetailSB = serverDetailCtrl.FindResource("show") as Storyboard;
            hideDetailSB = serverDetailCtrl.FindResource("hide") as Storyboard;
            hideDetailSB.Completed += new EventHandler(showListView);

            showListSB = this.FindResource("showServerList") as Storyboard;
            hideListSB = this.FindResource("hideServerList") as Storyboard;
            hideListSB.Completed += new EventHandler(showDetailView);

        }

        public ICommand OpenDetailView
        {
            get
            {
                return new RelayCommand<Server>(
                    execute: (svr) =>
                    {
                        serverDetailCtrl.DataContext = svr;
                        hideListView(null, null);
                    },
                    canExecute: (sort) => true
                );
            }
        }

        public void showDetailView(object sender, EventArgs e)
        {
            showDetailSB.Begin();
        }

        public void showListView(object sender, EventArgs e)
        {
            showListSB.Begin();
        }

        public void hideDetailView(object sender, EventArgs e)
        {
            hideDetailSB.Begin();
        }

        public void hideListView(object sender, EventArgs e)
        {
            hideListSB.Begin();
        }
    }
}