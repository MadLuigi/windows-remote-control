using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Client_pds.Models;
using Client_pds.Views;
using System.Windows;
using System.IO;

namespace Client_pds
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        /**********************
         * PRIVATE PROPERTIES *
         **********************/

        #region Private Properties

        // Flag for the disconnectButton;
        public bool IsDisconnectButtonEnabled = true;

        // Timer of the application, segnaling event every tot. 
        private System.Timers.Timer _clientTimer = new System.Timers.Timer();

        // IpAddress that is related to the GUI.
        private string _ipAddress;

        // String of commands written by the user. 
        private string _stringCommand;

        // List of the keyString.
        private List<string> _keyStrings = new List<string>();

        // Currently selected server from the GUI. 
        private ServerModel _currentSelectedServer = null;

        // Currently selected process from the GUI.
        private ProcessModel _currentSelectedProcess = null;

        // Current selectedServer IPaddress
        private IPAddress _currentSelectedIpAddress = null;

        private ApplicationModel _currentSelectedApplication = null;

        // The processes list that is displayed in the GUI. 
        ObservableCollection<ProcessModel> _theProcessesList = new ObservableCollection<ProcessModel>();

        // Reflects the visibility of the ApplicationView.
        private bool _shouldShowApplication = false;

        // Reflects the visibility of the ServerView.
        private bool _shouldShowServer = true;

        // List of the selected servers, for the currently selected application. 
        private List<ServerModel> _selectedServersForApplication = new List<ServerModel>();

        // List of the server that run the application
        private ObservableCollection<ServerModel> _serverOfCurrentApplicationsList = new ObservableCollection<ServerModel>(); /* with lock */

        // Collection of all the Servers connected. 
        private ObservableCollection<ServerModel> _serverConnections = new ObservableCollection<ServerModel>(); /* with lock */

        // Collection of all the IPs connected to the client.
        private ObservableCollection<IPAddress> _ipConnections = new ObservableCollection<IPAddress>(); /* with lock */

        // Collection of all the applications from different servers. 
        private ObservableCollection<ApplicationModel> _theApplicationsList = new ObservableCollection<ApplicationModel>(); /* with lock */

        // Notification message for the GUI.
        string _notificationMessage = ""; /* with lock */

        // Dictionary, to collect the IpAddresses of the server and the relative thread. 
        private Dictionary<IPAddress, Thread> _listOfConnectionThreads = new Dictionary<IPAddress, Thread>(); /* with lock */

        // Lock objects. 
        private static object syncTheApplicationsList = new object();
        private static object syncNotificationMessage = new object(); 
        private static object syncIpConnections = new object(); 
        private static object syncServerConnections = new object();
        private static object syncListOfConnectionThreads = new object();

        #endregion

        /*********************
         * PUBLIC PROPERTIES 
         *********************/

        #region Public Properties

        public Dictionary<IPAddress, Thread> ListOfConnectionThreads
        {
            get
            {
                lock (syncListOfConnectionThreads)
                {
                    return _listOfConnectionThreads;
                }
            }
            set
            {
                lock (syncListOfConnectionThreads)
                {
                    _listOfConnectionThreads = value;
                    OnPropertyChanged("ListOfConnectionThreads");
                }
            }
        }

        public ObservableCollection<ServerModel> ServerConnections
        {
            get
            {
                lock (syncServerConnections)
                {
                    return _serverConnections;
                }
            }
            set
            {
                lock (syncServerConnections)
                {
                    _serverConnections = value;
                    OnPropertyChanged("ServerConnections");
                }
            }
        }

        public List<ServerModel> SelectedServersForApplication
        {
            get
            {
                return _selectedServersForApplication;
            }
            set
            {
                _selectedServersForApplication = value;
                OnPropertyChanged("SelectedServersForApplication");
            }
        }

        public ObservableCollection<ServerModel> ServerOfCurrentApplicationList
        {
            get
            {
                return _serverOfCurrentApplicationsList;
            }
            set
            {
                _serverOfCurrentApplicationsList = value;
                OnPropertyChanged("ServerOfCurrentApplicationList");
            }
        }

        public Visibility ApplicationViewVisibility
        {
            get
            {
                return _shouldShowApplication ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility ServerViewVisibility
        {
            get
            {
                return _shouldShowServer ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public ApplicationModel CurrentSelectedApplication
        {
            get
            {
                return _currentSelectedApplication;
            }
            set
            {
                _currentSelectedApplication = value;
                OnPropertyChanged("CurrentSelectedApplication");
            }
        }

        public ObservableCollection<ApplicationModel> TheApplicationsList
        {
            get
            {
                lock (syncTheApplicationsList)
                {
                    return _theApplicationsList;
                }
            }
            set
            {
                lock (syncTheApplicationsList)
                {
                    _theApplicationsList = value;
                                    OnPropertyChanged("Applications");
                }
            }
        }


        public ObservableCollection<ProcessModel> TheProcessesList
        {
            get
            {
                return _theProcessesList;
            }

            set
            {
                _theProcessesList = value;
                OnPropertyChanged("TheProcessList");
            }
        }

        public string IpAddress
        {
            get
            {
                return _ipAddress;
            }

            set
            {
                _ipAddress = value;
                OnPropertyChanged("IpAddress");
            }
        }

        public ObservableCollection<IPAddress> IpConnections
        {
            get
            {
                lock (syncIpConnections)
                {
                    return _ipConnections;
                }
            }
        }

        
        public ServerModel CurrentSelectedServer
        {
            get
            {
                return _currentSelectedServer;
            }
            set
            {
                _currentSelectedServer = value;
                OnPropertyChanged("CurrentSelectedServer");
            }
        }

        public ProcessModel CurrentSelectedProcess
        {
            get
            {
                return _currentSelectedProcess;
            }
            set
            {
                _currentSelectedProcess = value;
                OnPropertyChanged("CurrentSelectedProcess");
            }
        }

        public IPAddress CurrentSelectedIpAddress
        {
            get
            {
                return _currentSelectedIpAddress;
            }
            set
            {
                _currentSelectedIpAddress = value;
                OnPropertyChanged("CurrentSelectedIpAddress");
            }
        }

        public string StringCommand
        {
            get
            {
                return _stringCommand;
            }
            set
            {
                if (_stringCommand != value)
                {
                    _stringCommand = value;
                }
                    OnPropertyChanged("StringCommand");
            }
        }

        public string NotificationMessage
        {
            get
            {
                lock (syncNotificationMessage)
                {
                    return _notificationMessage;
                }
            }
            set
            {
                lock (syncNotificationMessage)
                {
                    _notificationMessage = value;
                    OnPropertyChanged("NotificationMessage");
                }
            }
        }

        #endregion

        /******************
         * PUBLIC METHODS *
         ******************/
       
        // Connect the Client with the Server. 
        public void ConnectToServer()
        {

            // Check that the Ip Address is in the right and expected form. 
            if (IpAddress != null)
            {
                if (Regex.IsMatch(IpAddress, @"^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$"))
                {
                    // Set a flag to check that the address in within the boundaries. 
                    bool wrongIpAddress = false;
                    // Split the address in the numbers. 
                    string[] ipNumbers = IpAddress.Split('.');
                    foreach (string num in ipNumbers)
                    {
                        if (Int32.Parse(num) > 255)
                        {
                            // If a number is too big (can not be smaller than zero, checked in the regex) change the flag value.
                            wrongIpAddress = true;
                        }
                    }

                    // If the address is correct 
                    if (!wrongIpAddress)
                    {
                        IPAddress serverIpAddress = IPAddress.Parse(IpAddress);

                        if (!ListOfConnectionThreads.ContainsKey(serverIpAddress))
                        {
                            // Clean the Input string
                            IpAddress = "";

                            // Create the thread passing a parameter, the serverIpAddress I want to connect with.
                            Thread myNewThread = new Thread(() => ServerThreadRoutine(serverIpAddress));

                            // Add the thread to the data structure.
                            ListOfConnectionThreads.Add(serverIpAddress, myNewThread);

                            // Start the thread.
                            myNewThread.Start();
                        }
                        else // The address is already inserted in the IpConnections
                        {
                            // Open the Error Window 
                            Views.ErrorWindowView errView = new Views.ErrorWindowView();
                            ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("La connessione con questo server già è presente.");
                            errWindViewModel.ClosingRequest += errView.Close;
                            errView.DataContext = errWindViewModel;
                            errView.Show();
                        }
                    }
                    else // The address is not in the proper form, show the ErrorWindow. 
                    {
                        // Open the Error Window 
                        Views.ErrorWindowView errView = new Views.ErrorWindowView();
                        ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Formato indirizzo Ip sbagliato.");
                        errWindViewModel.ClosingRequest += errView.Close;
                        errView.DataContext = errWindViewModel;
                        errView.Show();
                    }
                }
                else // The address is not in the proper form, show the ErrorWindow. 
                {
                    // Open the Error Window 
                    Views.ErrorWindowView errView = new Views.ErrorWindowView();
                    ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Formato indirizzo Ip sbagliato.");
                    errWindViewModel.ClosingRequest += errView.Close;
                    errView.DataContext = errWindViewModel;
                    errView.Show();
                }
            }
            else // The IpAddress is null, show the Error Window. 
            {
                // Open the Error Window 
                Views.ErrorWindowView errView = new Views.ErrorWindowView();
                ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Indirizzo Ip assente.");
                errWindViewModel.ClosingRequest += errView.Close;
                errView.DataContext = errWindViewModel;
                errView.Show();
            }
        }

        // Remove a Server from the data structures.
        public void RemoveServer(ServerModel ServerToRemove, IPAddress IpAddressToRemove)
        {
            // Iterate for each process in the server and remove it from the applications listed in the applicationList of the viewModel. 
            foreach (ProcessModel proc in ServerToRemove.TheProcesses)
            {
                ServerToRemove.RemoveProcessFromApplications(proc);
                // The function already iterates for all the application in the list and checks if the application has still at least a server running it. 
                // It also takes care of raising the propertyChanged. 
            }

            // Remove it from the IpConnections.
            if (IpConnections.Contains(IpAddressToRemove))
            {
                System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    IpConnections.Remove(IpAddressToRemove);
                    RaisePropertyChanged("IpConnections");
                });
            }
            // Remove it from the ServerConnectons. 
            if (ServerConnections.Contains(ServerToRemove))
                ServerConnections.Remove(ServerToRemove);

            // Check that there is the entry correspondent witht the serverIpAddress.
            lock (syncListOfConnectionThreads)
            {
                if (_listOfConnectionThreads != null && _currentSelectedServer != null)
                {
                    if (_listOfConnectionThreads.ContainsKey(_currentSelectedServer.ServerIpAddress))
                    {
                        // Abort the thread running the connection. 
                        _listOfConnectionThreads[_currentSelectedServer.ServerIpAddress].Abort();
                        // Remove the thread from the dictionary. 
                        _listOfConnectionThreads.Remove(_currentSelectedServer.ServerIpAddress);
                    }
                }
            }

            // If there are other servers, select the first of them
            if (ServerConnections.Count() > 0)
            {
                // Select the first server.
                CurrentSelectedServer = ServerConnections.First();
                CurrentSelectedIpAddress = CurrentSelectedServer.ServerIpAddress;
                CurrentSelectedProcess = null;
                TheProcessesList = CurrentSelectedServer.TheProcesses;
                // Update the GUI
                RaisePropertyChanged("TheProcessesList");
                RaisePropertyChanged("CurrentSelectedIpAddress");
                RaisePropertyChanged("CurrentSelectedServer");
                RaisePropertyChanged("CurrentSelectedProcess");
            }
            else
            {
                // There are no more connected servers.
                CurrentSelectedServer = null;
                CurrentSelectedProcess = null;
                CurrentSelectedIpAddress = null;
                TheProcessesList = null;
                RaisePropertyChanged("TheProcessesList");
            }
        }

        public void AddServer(ServerModel ServerToAdd, IPAddress IpAddressToAdd)
        {
            // Add the server to the servers list. 
            // IpConnections is also bound to the ServersComboBox
            IpConnections.Add(IpAddressToAdd);

            // Add the server Ip Address to the IpConnection list.
            ServerConnections.Add(ServerToAdd);
        }

        // Disconnect the client from the currently selected Server 
        public void DisconnectFromServer(IPAddress ipServerToDisconnect)
        {
            if (ipServerToDisconnect != null)
            {
                ServerModel serverToDisconnect = null;

                // From the Ip look-up the server.
                foreach (ServerModel server in ServerConnections)
                {
                    if (server.ServerIpAddress.Equals(ipServerToDisconnect))
                    {
                        serverToDisconnect = server;
                        // Stop the serverTimer if the disconnect is called from the GUI
                        if (serverToDisconnect.HasToStopTimer)
                        {
                            serverToDisconnect.ServerTimer.Stop();
                        }
                        break;
                    }
                }

                if (serverToDisconnect != null)
                {
                    // I found the server I want to disconnect 

                    if (serverToDisconnect.Equals(CurrentSelectedServer))
                    {
                        // I want to disconnect the currentSelectedServer
                        serverToDisconnect.CloseClient();
                        // The function calls already the RemoveServer function

                        // Remove the server information form the GUI
                        NotificationMessage = "Server " + ipServerToDisconnect.ToString() + " disconnesso.";

                        // If there are other servers, select the first of them
                        if (ServerConnections.Count() > 0)
                        {
                            // Select the first server.
                            CurrentSelectedServer = ServerConnections.First();
                            CurrentSelectedIpAddress = CurrentSelectedServer.ServerIpAddress;
                            CurrentSelectedProcess = null;
                            TheProcessesList = CurrentSelectedServer.TheProcesses;
                            // Update the GUI
                            RaisePropertyChanged("TheProcessesList");
                            RaisePropertyChanged("CurrentSelectedIpAddress");
                            RaisePropertyChanged("CurrentSelectedServer");
                            RaisePropertyChanged("CurrentSelectedProcess");
                            RaisePropertyChanged("IpConnections");
                        }
                        else // There are no more connected servers.
                        {
                            CurrentSelectedServer = null;
                            CurrentSelectedProcess = null;
                            CurrentSelectedIpAddress = null;
                            TheProcessesList = null;
                            // Update the GUI
                            RaisePropertyChanged("TheProcessesList");
                            RaisePropertyChanged("CurrentSelectedIpAddress");
                            RaisePropertyChanged("CurrentSelectedServer");
                            RaisePropertyChanged("CurrentSelectedProcess");
                            RaisePropertyChanged("IpConnections");
                        }
                    }
                    else
                    {
                        // The server I want to disconnect is just one among the others.
                        // Close takes care of calling the removeProcessfromApplications and the remove server.  
                        serverToDisconnect.CloseClient();
                        // Update the GUI
                        RaisePropertyChanged("TheProcessesList");
                        RaisePropertyChanged("CurrentSelectedIpAddress");
                        RaisePropertyChanged("CurrentSelectedServer");
                        RaisePropertyChanged("CurrentSelectedProcess");
                        RaisePropertyChanged("IpConnections");
                    }
                }
                else // I haven't found the server I am looking for 
                {
                    System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        // Open the Error Window 
                        Views.ErrorWindowView errView = new Views.ErrorWindowView();
                        ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Non è stato trovato il server da disconnettere.");
                        errWindViewModel.ClosingRequest += errView.Close;
                        errView.DataContext = errWindViewModel;
                        errView.Show();
                    });
                }
            }
            else // No server is selected 
            {
                // Open the Error Window 
                Views.ErrorWindowView errView = new Views.ErrorWindowView();
                ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Non è selezionato nessun server.");
                errWindViewModel.ClosingRequest += errView.Close;
                errView.DataContext = errWindViewModel;
                errView.Show();
            }
        }

        public void CurrentServerChanged(IPAddress newlySelectedIpAddress)
        {
            // I don't have to do nothing with the timers, since the servers are connected anyway. 
            // I have to show the processlist in the ListView. 
            if (newlySelectedIpAddress != null && CurrentSelectedServer != null)
            {
                // I have to look up the server corresponding to the newly selected ip Address. 
                foreach (ServerModel server in ServerConnections)
                {
                    if (server.ServerIpAddress.Equals(newlySelectedIpAddress))
                    {
                        CurrentSelectedServer = server;
                        break;
                    }
                }

                // Update everything
                TheProcessesList = CurrentSelectedServer.TheProcesses;
                CurrentSelectedIpAddress = CurrentSelectedServer.ServerIpAddress;
                CurrentSelectedProcess = null;
                // Update the GUI
                RaisePropertyChanged("CurrentSelectedIpAddress");
                RaisePropertyChanged("CurrentSelectedProcess");
                RaisePropertyChanged("CurrentSelectedServer");
                RaisePropertyChanged("TheProcessesList");
            }
        }

        // The user has focused a certain process.
        public void CurrentProcessChanged()
        {
            // I don't have to do nothing with the timers, since the servers are connected anyway. 
        }

        public void CurrentApplicationChanged()
        {
            // Update the list of servers to show. 
            if (CurrentSelectedApplication != null)
            {
                // Assign the proper list of server to visualize. 
                ServerOfCurrentApplicationList = CurrentSelectedApplication.AppServerList;

                // Clear the list of the current selected server for the application selected, since it has just been selected. 
                SelectedServersForApplication.Clear();

                RaisePropertyChanged("ServerOfCurrentApplicationList");
                RaisePropertyChanged("SelectedServersForApplication");
            }
        }

        public void getCommandKey(KeyEventArgs e)
        {
            // Get the key and trasform it into string. 
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            string keyString = key.ToString();

            // Update the stringCommand in the view. 
            if (_keyStrings.Count() == 0)
            {
                // First key inserted. 
                _stringCommand = keyString;
                RaisePropertyChanged("StringCommand");
            }
            else
            {
                // There are already other keys. 
                _stringCommand += " + " + keyString;
                RaisePropertyChanged("StringCommand");
            }

            // Extract the virtual key from the key, add it to the list of keys. 
            var virtualKey = KeyInterop.VirtualKeyFromKey(key);

            string hexString = virtualKey.ToString("X");

            _keyStrings.Add(hexString);
        }

        public void CancelStringCommand()
        {
            // Clean the string 
            StringCommand = "";

            // Empty the list of commands already inserted.
            _keyStrings.Clear();
        }

        public void SendCommand()
        {
            // Divide the cases: the one with the ServerView and the one with the ApplicationView

            if (_shouldShowServer)
            {
                // The selected view is the one of the servers.

                // If there is at lest a key in the command
                if (_keyStrings.Count() != 0)
                {
                    // If there is a CurrentSelectedServer
                    if (CurrentSelectedServer != null)
                    {
                        // If there is a process selected. 
                        if (CurrentSelectedProcess != null)
                        {
                            CurrentSelectedServer.SendCommand(_keyStrings, _currentSelectedProcess.ProcessWindowHandle, _currentSelectedProcess.ProcessId);
                        }
                        else // The CurrentSelectedProcess is null, show the Error Window. 
                        {
                            // Open the Error Window 
                            Views.ErrorWindowView errView = new Views.ErrorWindowView();
                            ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Nessuna finestra selezionata.");
                            errWindViewModel.ClosingRequest += errView.Close;
                            errView.DataContext = errWindViewModel;
                            errView.Show();
                        }  
                    }
                    else // The CurrentSelectedServer is null, show the Error Window. 
                    {
                        // Open the Error Window 
                        Views.ErrorWindowView errView = new Views.ErrorWindowView();
                        ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Non è selezionato nessun server.");
                        errWindViewModel.ClosingRequest += errView.Close;
                        errView.DataContext = errWindViewModel;
                        errView.Show();
                    }
                }
                else // There is no command to send. 
                {
                    // Open the Error Window 
                    Views.ErrorWindowView errView = new Views.ErrorWindowView();
                    ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Nessun comando inserito.");
                    errWindViewModel.ClosingRequest += errView.Close;
                    errView.DataContext = errWindViewModel;
                    errView.Show();
                }

                // Clean the textBox in the GUI and clean the list of keys.
                CancelStringCommand();
            }
            else if (_shouldShowApplication)
            {
                // The selected view is the one of the applications. 

                // If there is at lest a key in the command
                if (_keyStrings.Count() != 0)
                {
                    // If there is a selected application. 
                    if (CurrentSelectedApplication != null)
                    {
                        // If the are selected severs.
                        if (SelectedServersForApplication != null)
                        {
                            // If there is at least a server selected for the selected application. 
                            if (SelectedServersForApplication.Count() > 0)
                            {
                                // For each server selected, send the signal to all the applications it runs, that have the same application name of the one selected. 
                                foreach (ServerModel server in SelectedServersForApplication)
                                {
                                    foreach (ProcessModel process in server.TheProcesses)
                                    {
                                        // I have to check if the exeName of the process is equal to the selected application.
                                        string processExeName = Path.GetFileName(process.ExePath); // returns File.exe

                                        if (processExeName.Equals(_currentSelectedApplication.ApplicationName))
                                        {
                                            // I have to send the command, since they are the same application
                                            server.SendCommand(_keyStrings, process.ProcessWindowHandle, process.ProcessId);
                                        }
                                    }
                                }
                                CancelStringCommand();
                            }
                            else // There is no selected server. 
                            {
                                // Open the Error Window 
                                Views.ErrorWindowView errView = new Views.ErrorWindowView();
                                ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Non è selezionato nessun server.");
                                errWindViewModel.ClosingRequest += errView.Close;
                                errView.DataContext = errWindViewModel;
                                errView.Show();
                            }
                        }
                        else // There is no selected server. 
                        {
                            // Open the Error Window 
                            Views.ErrorWindowView errView = new Views.ErrorWindowView();
                            ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Non è selezionato nessun server.");
                            errWindViewModel.ClosingRequest += errView.Close;
                            errView.DataContext = errWindViewModel;
                            errView.Show();
                        }   
                    }
                    else // There is no selected application. 
                    {
                        // Open the Error Window 
                        Views.ErrorWindowView errView = new Views.ErrorWindowView();
                        ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Non è selezionata nessuna applicazione.");
                        errWindViewModel.ClosingRequest += errView.Close;
                        errView.DataContext = errWindViewModel;
                        errView.Show();
                    }
                }
                else // There is no command to send. 
                {
                    // Open the Error Window 
                    Views.ErrorWindowView errView = new Views.ErrorWindowView();
                    ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Nessun comando inserito.");
                    errWindViewModel.ClosingRequest += errView.Close;
                    errView.DataContext = errWindViewModel;
                    errView.Show();
                }
            }
            
        }

        public void ViewApplicationCommand()
        {
            IsDisconnectButtonEnabled = false;

            // Make ServerView collapsed.
            _shouldShowServer = false;
            RaisePropertyChanged("ServerViewVisibility");

            // Make ApplicationView visible.
            _shouldShowApplication = true;
            RaisePropertyChanged("ApplicationViewVisibility");
        }

        public void ViewServerCommand()
        {
            IsDisconnectButtonEnabled = true;

            // Make ApplicationView collapsed.
            _shouldShowApplication = false;
            RaisePropertyChanged("ApplicationViewVisibility");

            // Make ServerView visible.
            _shouldShowServer = true;
            RaisePropertyChanged("ServerViewVisibility");
        }

        // Start the timer for the first time. 
        public void StartClientTimer()
        {
            // Set the eventHandler. 
            _clientTimer.Elapsed += new ElapsedEventHandler(OnTimeEventUpdateGUI);

            // Set the interval (in milliseconds)
            _clientTimer.Interval = 250;
            _clientTimer.AutoReset = true;
            _clientTimer.Start();
        }

        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            // Handle the closing logic. 
            for (int i = 0; i < ServerConnections.Count; i++)
            {
                // Do the shutdown of the socket, in order to terminate the TCP connection. 
                if (ServerConnections[i] != null)
                {
                    DisconnectFromServer(ServerConnections[i].ServerIpAddress);
                }
            }
        }

        /*******************
         * PRIVATE METHODS *
         *******************/

        private void OnTimeEventUpdateGUI(object source, ElapsedEventArgs e)
        {
            // Method invoked every _clientTimer milliseconds. 
            // Has to invoke a method to update the timePercentage of the displayed processes. 

            // Update only if the process list is not null 
            if (CurrentSelectedServer != null && TheProcessesList != null)
            {
                // For each process in the list, update the timePercentage.
                foreach (ProcessModel proc in TheProcessesList)
                {
                    if (proc != null)
                    {
                        // Compute and update the time%
                        proc.TimePercentage = (int)System.Math.Ceiling(100 * (TimeSpanToMilliSeconds(proc.GetElapsedTime()) / TimeSpanToMilliSeconds(CurrentSelectedServer.GetElapsedTime())));
                        RaisePropertyChanged("TheProcessesList");
                    }
                }
            }
        }

        private float TimeSpanToMilliSeconds(TimeSpan span)
        {
            // Computes the time elasped in milliseconds. 
            float seconds = (((span.Days * 24) * 3600 * 1000) + (span.Hours * 3600 * 1000) + (span.Minutes * 60 * 1000) + (span.Seconds * 1000) + (span.Milliseconds));
            return seconds;
        }

        private void ServerThreadRoutine(IPAddress serverIpAddress)
        {
            // Create the server model. 
            ServerModel newServerConnection = new ServerModel(serverIpAddress, this);

            // Actually starts the server routine, connecting the Server and receiving data. 
            newServerConnection.StartClient();
        }

    }
}