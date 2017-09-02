using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;

namespace Client_pds
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowViewModel _mainWindowViewModel = new MainWindowViewModel();
        

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = _mainWindowViewModel;

            // Start the timer of the application.
            _mainWindowViewModel.StartClientTimer();

            // What to do on closing command .
            Closing += _mainWindowViewModel.OnWindowClosing;
        }

        // Change the selection of the process. 
        private void ListViewProcesses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.CurrentProcessChanged();
            }
        }

        // Change the selection of the application. 
        private void ListViewApplications_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.CurrentApplicationChanged();
            }
        }

        // Update the multiple selection of the server for the current selected application. 
        private void ListViewOfServerApplications_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_mainWindowViewModel != null)
            {
                // Set the SelectedServers in the MainwindowViewModel with the one selected in the view. 

                _mainWindowViewModel.SelectedServersForApplication.Clear();
                for (int i = 0; i < ListViewOfServerApplications.SelectedItems.Count; i++)
                {
                    _mainWindowViewModel.SelectedServersForApplication.Add((ServerModel)ListViewOfServerApplications.SelectedItems[i]);
                }
            }
        }

        // Change the selection of the server. 
        private void ServersComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.CurrentServerChanged((IPAddress)ServersComboBox.SelectedItem);
            }
        }

        // Intercept the key when typing in the command textbox. 
        private void textBoxComandi_KeyDown(object sender, KeyEventArgs e)
        {
            if (_mainWindowViewModel != null)
            {
                _mainWindowViewModel.getCommandKey(e);
            }
        }

        // Intercept the Enter key, to enter the IpAddress. 
        private void textBoxIpAddress_KeyDown(object sender, KeyEventArgs e)
        {
            if (_mainWindowViewModel != null)
            {
                if (e.Key == Key.Enter)
                {
                    _mainWindowViewModel.ConnectToServer();
                } 
            }
        }

        // Intercept the disconnet_button_click, to stop if in the ApplicationView. 
        private void DisconnectFromServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindowViewModel != null)
            {
                if (_mainWindowViewModel.IsDisconnectButtonEnabled)
                {
                    // Check that there is a currentSelectedServer!
                    if (_mainWindowViewModel.CurrentSelectedServer != null)
                    {
                        _mainWindowViewModel.CurrentSelectedServer.HasToStopTimer = true;
                        _mainWindowViewModel.DisconnectFromServer(_mainWindowViewModel.CurrentSelectedServer.ServerIpAddress);
                    } 
                }
                else
                {
                    // Open the Error Window 
                    Views.ErrorWindowView errView = new Views.ErrorWindowView();
                    ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Per disconnettere un server, andare sulla vista Server.");
                    errWindViewModel.ClosingRequest += errView.Close;
                    errView.DataContext = errWindViewModel;
                    errView.Show();
                }
            }
        }
    }
}
