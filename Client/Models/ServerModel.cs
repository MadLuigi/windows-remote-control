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
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.IO;
using Client_pds.Models;

namespace Client_pds
{
    public class ServerModel
    {
        /**********************
         * PRIVATE PROPERTIES *
         **********************/

        #region Private Properties

        private IAsyncResult _connectionResult;

        private bool _hasToStopTimer = false;

        // Timer of the server, segnaling event every tot to check connection aliveness. 
        private System.Timers.Timer _serverTimer;

        // Const deafault buffer length.
        private const int _bufferDimension = 8192;

        // The server address
        private IPAddress _serverIpAddress;

        // Timer to keep track of the connection time with the server
        private Stopwatch _connectionTimer;

        // The port number for the remote device.  
        private const int _port = 12058;

        // The socket the client connectes to server with. 
        Socket _clientSocket;

        // MainWindowViewModel that created the ServerModel
        private MainWindowViewModel _mainWinWMCaller;

        // Buffer for receiving data from the server. 
        private byte[] _receiveByteBuffer;

        // Buffer for storing data from the server. 
        private byte[] _storedByteBuffer;

        // Number of bytes to read for current message
        private int _bytesToRead;

        // Counter to keep track of the received bytes, and be sure to read _bytesToRead bytes per message. 
        private int _receiveBufferCounter = 0;

        // Flag to reset buffer and buffer counter for each message received
        private bool nextRead = true;

        // List of the processes of the server 
        private ObservableCollection<ProcessModel> _processList;

        // Focused process
        private ProcessModel _focusedProcess = null;

        #endregion

        /*********************
         * PUBLIC PROPERTIES *
         *********************/

        #region Public Properties

        public bool HasToStopTimer
        {
            get
            {
                return _hasToStopTimer;
            }
            set
            {
                _hasToStopTimer = value;
            }
        }

        public System.Timers.Timer ServerTimer
        {
            get
            {
                return _serverTimer;
            }
        }

        public IPAddress ServerIpAddress
        {
            get
            {
                return _serverIpAddress;
            }
        }

        public ObservableCollection<ProcessModel> TheProcesses
        {
            get
            {
                return _processList;
            }
        }

        public ProcessModel FocusedProcess
        {
            get
            {
                return _focusedProcess;
            }
        }

        #endregion

        /***************
         * Constructor *
         ***************/

        public ServerModel(IPAddress IpAddress, MainWindowViewModel MainWindowVMCaller)
        {
            _serverIpAddress = IpAddress;
            _connectionTimer = new Stopwatch();
            _mainWinWMCaller = MainWindowVMCaller;
            _processList = new ObservableCollection<ProcessModel>();
            _serverTimer = new System.Timers.Timer();

            // Instantiate and set the timer of the serverModel.
            _serverTimer.Elapsed += new ElapsedEventHandler(OnTimeEventTestConnection);
            // Set the interval (in milliseconds).
            _serverTimer.Interval = 2000;
            _serverTimer.AutoReset = true;
        }

        /******************
         * PUBLIC METHODS *
         ******************/

        public void StartClient()
        {
            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.  
                
                IPAddress ipAddress = _serverIpAddress;
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, _port);
            
                // Create a TCP/IP socket.  
                _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                _connectionResult = _clientSocket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), null);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception : {0}", e.ToString());

                System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    // Open the Error Window 
                    Views.ErrorWindowView errView = new Views.ErrorWindowView();
                    string errormessage = "La connessione con il server è fallita";
                    ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel(errormessage);
                    errWindViewModel.ClosingRequest += errView.Close;
                    errView.DataContext = errWindViewModel;
                    errView.Show();
                });

                // Remove the server Ip Address from the structure that contains it as a key
                if (_mainWinWMCaller.ListOfConnectionThreads.ContainsKey(this.ServerIpAddress))
                    _mainWinWMCaller.ListOfConnectionThreads.Remove(this.ServerIpAddress);

                // Close the Socket 
                CloseClient();
            }
        }

        // Closes and releases the Socekt 
        public void CloseClient()
        {
            // Release the socket.
            if (_clientSocket.Connected) {
                
                // Shutdown the socket. 
                _clientSocket.Shutdown(SocketShutdown.Both);
            }

            // Stop the Stopwatch. 
            _connectionTimer.Stop();

            // Close the connection
            _clientSocket.Close();

            // Remove the Server from the MainWindowViewModel
            _mainWinWMCaller.RemoveServer(this, _serverIpAddress);
        }

        public void SendCommand(List<string> keyCommands, int Handle, int Pid)
        {
            int numKeys = keyCommands.Count();

            // Generate the string that has to be sent.
            string commandToSend = "command " + Handle + " " + Pid + " " + numKeys;
            foreach (string key in keyCommands)
            {
                commandToSend += " " + key;
            }

            // Create the buffer byte that has to be sent. 
            byte[] commandByteBuffer = Encoding.Default.GetBytes(commandToSend); // Command to send have variable length

            // Send the dimension of the send byte buffer together with it
            // Ushort is on 2Bytes 
            ushort sBBLength = (ushort)commandByteBuffer.Length;
            // Convert the dimension directly into bytes encoding 
            byte[] lengthIn2Bytes = BitConverter.GetBytes(sBBLength);

            // Message length is encoded in network byte order (Big Endian), so I need to check if the architecture
            // of the host is Little Endian, in that case reverse the order of the bytes before decoding
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthIn2Bytes);

            byte[] sendByteBuffer = new byte[lengthIn2Bytes.Length + commandByteBuffer.Length];
            Buffer.BlockCopy(lengthIn2Bytes, 0, sendByteBuffer, 0, lengthIn2Bytes.Length);
            Buffer.BlockCopy(commandByteBuffer, 0, sendByteBuffer, lengthIn2Bytes.Length, commandByteBuffer.Length);

            try
            {
                // Call the call back for the sending. 
                _clientSocket.BeginSend(sendByteBuffer, 0, sendByteBuffer.Length, SocketFlags.None, new AsyncCallback(SendCallback), null);
            }
            catch (Exception e)
            {
                _mainWinWMCaller.CancelStringCommand();
                Console.WriteLine(e.ToString());
                // Close the connection with the server. The disconnect function calls the CloseClient, the Remove server, and the removeProcessesFromApplication. 
                _mainWinWMCaller.DisconnectFromServer(this.ServerIpAddress);

                System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    // Open the Error Window 
                    Views.ErrorWindowView errView = new Views.ErrorWindowView();
                    ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Invio non riuscito, server disconnesso.");
                    errWindViewModel.ClosingRequest += errView.Close;
                    errView.DataContext = errWindViewModel;
                    errView.Show();
                });
            }
        }

        public TimeSpan GetElapsedTime()
        {
            return _connectionTimer.Elapsed;
        }

        public void RemoveProcessFromApplications(ProcessModel process)
        {
            bool appHasToBeRemoved = false;
            string exeName = Path.GetFileName(process.ExePath); // returns File.exe
            ApplicationModel applic = null;

            // Search for the application. 
            foreach (ApplicationModel app in _mainWinWMCaller.TheApplicationsList)
            {
                if (app.ApplicationName.Equals(exeName))
                {
                    // Remove this server from its list. 
                    app.AppServerList.Remove(this);

                    if (app.AppServerList.Count() == 0)
                    {
                        appHasToBeRemoved = true;
                        applic = app;
                    }
                    break;
                }
            }

            _mainWinWMCaller.RaisePropertyChanged("ServerOfCurrentApplicationList");
            _mainWinWMCaller.RaisePropertyChanged("Applications");

            // Check if this application has to be removed. 
            if (appHasToBeRemoved)
            {
                // Check that the application that has to be removed is not the selected one
                if (applic.Equals(_mainWinWMCaller.CurrentSelectedApplication))
                {
                    // Deselect the application
                    _mainWinWMCaller.CurrentSelectedApplication = null;
                    _mainWinWMCaller.ServerOfCurrentApplicationList.Clear();
                    _mainWinWMCaller.SelectedServersForApplication.Clear();
                }

                System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    _mainWinWMCaller.TheApplicationsList.Remove(applic);
                });

                // Update the GUI
                _mainWinWMCaller.RaisePropertyChanged("Applications");
                _mainWinWMCaller.RaisePropertyChanged("CurrentSelectedApplication");
                _mainWinWMCaller.RaisePropertyChanged("ServerOfCurrentApplicationList");
                _mainWinWMCaller.RaisePropertyChanged("SelectedServersForApplication");
            }
        }

        /*******************
         * PRIVATE METHODS *
         *******************/

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                _clientSocket.EndConnect(ar);

                if (_clientSocket.Connected) // There is connection
                {
                    // Notify the GUI the connetions has been performerd
                    _mainWinWMCaller.NotificationMessage = "Client connected to server " + _serverIpAddress;

                    // Start the timer to keep track of the connection time. 
                    _connectionTimer.Start();

                    // Start the timer to check the connection aliveness.
                    _serverTimer.Start();

                    // Add the server to the data structures.
                    System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate {
                        _mainWinWMCaller.AddServer(this, _serverIpAddress);
                    });

                    // Set this newly created Server as the Currently selected one. 
                    _mainWinWMCaller.CurrentSelectedServer = this;

                    // Update the comboBox. 
                    _mainWinWMCaller.CurrentServerChanged(_serverIpAddress);

                    // Start receiving data from the server. 
                    Receive(2); 
                }
            }
            catch (Exception e)
            {
                // _connectionResult.AsyncWaitHandle.WaitOne(5000, true);

                System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    // Open the Error Window 
                    Views.ErrorWindowView errView = new Views.ErrorWindowView();
                    ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Connessione con " + this.ServerIpAddress.ToString() + " non riuscita.");
                    errWindViewModel.ClosingRequest += errView.Close;
                    errView.DataContext = errWindViewModel;
                    errView.Show();
                });

                // Remove the server Ip Address from the structure that contains it as a key
                if (_mainWinWMCaller.ListOfConnectionThreads.ContainsKey(this.ServerIpAddress))
                    _mainWinWMCaller.ListOfConnectionThreads.Remove(this.ServerIpAddress);

                Console.WriteLine(e.ToString());
                CloseClient();
            }
        }

        private void Receive(int bytesToRead)
        {
            try
            {
                // Begin receiving the data from the remote device.
                if (nextRead)
                {
                    _bytesToRead = bytesToRead;
                    _receiveBufferCounter = 0;
                    _receiveByteBuffer = new byte[bytesToRead];
                    _storedByteBuffer = new byte[bytesToRead];
                    nextRead = false;
                }
                _clientSocket.BeginReceive(_receiveByteBuffer, 0, bytesToRead, SocketFlags.None, new AsyncCallback(ReceiveCallback), null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Read data from the remote device.  
                int bytesRead = _clientSocket.EndReceive(ar);

                // There is something to read 
                if (bytesRead > 0)
                {
                    // Update the counter of reeived bytes. 
                    _receiveBufferCounter += bytesRead;

                    if (_receiveBufferCounter > 0 && _receiveBufferCounter < _bytesToRead)
                    {
                        // I still need to receive data for the current message. 

                        // Save what I received up to now, and listen again the socket.
                        Array.Copy(_receiveByteBuffer, 0, _storedByteBuffer, _receiveBufferCounter - bytesRead, bytesRead);

                        // Call the Receive again, to reed more bytes. 
                        Receive(_bytesToRead - _receiveBufferCounter);
                    }

                    else if (_receiveBufferCounter == _bytesToRead)
                    {
                        // The counter is equal to _bytesToRead, the read is now complete. 

                        // Still need to copy in the buffer what I have just received. 
                        Array.Copy(_receiveByteBuffer, 0, _storedByteBuffer, _receiveBufferCounter - bytesRead, bytesRead);

                        // I read the first 2 bytes of a message containing its length
                        if (_receiveBufferCounter == 2)
                        {
                            // Message length is encoded in network byte order (Big Endian), so I need to check if the architecture
                            // of the host is Little Endian, in that case reverse the order of the bytes before decoding
                            if (BitConverter.IsLittleEndian)
                                Array.Reverse(_storedByteBuffer);

                            ushort messageLength = BitConverter.ToUInt16(_storedByteBuffer, 0);

                            nextRead = true;
                            Receive(messageLength);
                        }

                        else
                        {

                            // Then I can start processing the data.

                            // Create a string and get the received content of the buffer.
                            string receivedString = Encoding.Default.GetString(_storedByteBuffer);

                            string[] lines = Regex.Split(receivedString, "\n");

                            if (lines[0].Equals("newwindow")) // newwindows\n<path_exe>\n<nome_finestra>\n<handle>\n<pid>\n<dim_icona>\n<byteicon>
                            {
                                // I have a new window to listen about, and then add the to the ServerModel
                                string exePath = lines[1];
                                // Create the path and extract the file (program) name. 
                                string windowName = lines[2];
                                int processHandle = Int32.Parse(lines[3]);
                                int processID = Int32.Parse(lines[4]);
                                // Set anyway the image as null. 
                                BitmapImage icon = null;

                                if (lines.Length > 6)
                                {
                                    // Means that there also information about the image, otherwise there is not
                                    int iconDimension = Int32.Parse(lines[5]);

                                    // If the iconDimension is < 0, that means that there were some errors: don't even read the rest of the message. 
                                    if (iconDimension > 0)
                                    {
                                        // Means that the Icon has been properly sent 
                                        icon = new BitmapImage();

                                        byte[] imageByte = new byte[iconDimension];


                                        // Compute the index of the start of the byte of the icon, so the already processed bytes. 
                                        int iconOffset = 0;
                                        for (int i = 0; i < 6; i++)
                                        {
                                            iconOffset += lines[i].Length + 1;
                                        }

                                        // Take the image byte from the receivedByte, not from the receivedString, because it could have some \n characters. 
                                        Array.Copy(_storedByteBuffer.Skip(iconOffset).ToArray(), imageByte, iconDimension);

                                        // Save the byte as a BitMapImage
                                        using (var stream = new MemoryStream(imageByte))
                                        {
                                            stream.Position = 0;
                                            icon.BeginInit();
                                            icon.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                            icon.CacheOption = BitmapCacheOption.OnLoad;
                                            icon.StreamSource = stream;
                                            icon.EndInit();
                                            icon.Freeze();
                                        }
                                    }
                                }

                                // Create the processModel with its constructor 
                                // Icon can be null
                                ProcessModel newProcessM = new ProcessModel(windowName, exePath, processHandle, processID, icon);

                                // Set the notification message for the GUI. 
                                _mainWinWMCaller.NotificationMessage = "Server: " + _serverIpAddress.ToString() + ".\nNew window " + newProcessM.ExePath + " has been created.";

                                // The process must be added in the ListofProcesses of the ServerModel 
                                System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                                {
                                    _processList.Add(newProcessM);
                                });

                                // Update the process list the of the mainWindowModel. 
                                _mainWinWMCaller.RaisePropertyChanged("TheProcessesList");

                                // Add this process to the applications of the same type
                                AddProcessToApplication(newProcessM);
                            }

                            if (lines[0].Equals("closed")) // closed\n<handle>\n<pid>
                            {
                                // I have received a notification from the server: one window have been closed. 

                                // Extract Handle and PID
                                string[] handleAndPID = Regex.Split(lines[1], " ");
                                int handle = Int32.Parse(handleAndPID[0]);
                                int pid = Int32.Parse(handleAndPID[1]);

                                // Remove the Windows from the process lists. Find the process.
                                foreach (ProcessModel proc in _processList)
                                {
                                    // Check if the window has the focus (different from null and they have the same handle)
                                    if (_focusedProcess != null && _focusedProcess.ProcessWindowHandle.Equals(proc.ProcessWindowHandle))
                                    {

                                        // Check if the window has the focus (different from null and they have the same handle)
                                        if (_focusedProcess != null && _focusedProcess.ProcessWindowHandle.Equals(proc.ProcessWindowHandle))
                                        {
                                            _focusedProcess.PauseWatch();
                                            _focusedProcess = null;
                                        }

                                        System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                                        {
                                        // Remove the process from the process list. 
                                        _processList.Remove(proc);

                                        // Remove the process from the list of applications.
                                        RemoveProcessFromApplications(proc);
                                        });

                                        // Set the notification message for the GUI. 
                                        _mainWinWMCaller.NotificationMessage = "Server: " + _serverIpAddress.ToString() + ".\nProcess " + proc.ExePath + " has been closed.";

                                        // Exit the for loop.
                                        break;
                                    }
                                }
                            }

                            if (lines[0].Equals("focus"))
                            {
                                // I have received a notification, the window with focus has changed.

                                if (lines[1].Equals("nullwindow"))
                                {
                                    // There is no focused window. Stop the focus: interrupt a timer.
                                    // But only if the focused process already exists
                                    if (_focusedProcess != null)
                                    {
                                        _focusedProcess.PauseWatch();
                                        _focusedProcess.ProcessStatus = "";
                                        _focusedProcess = null;
                                    }
                                    _mainWinWMCaller.NotificationMessage = "Server: " + _serverIpAddress.ToString() + ".\nFocus set on: null.";
                                }
                                if (lines[1].Equals("windowsoperatingsystem"))
                                {
                                    // The focus is hold by the operating system. Stop the focus: interrupt a timer
                                    // But only if the focused process already exists
                                    if (_focusedProcess != null)
                                    {
                                        _focusedProcess.PauseWatch();
                                        _focusedProcess.ProcessStatus = "";
                                        _focusedProcess = null;
                                    }
                                    _mainWinWMCaller.NotificationMessage = "Server: " + _serverIpAddress.ToString() + ".\nFocus set on: Operating System.";
                                }
                                if (lines[1].All(char.IsDigit)) // Check that the string is a number.
                                {
                                    // A window holds the focus. 
                                    int handle = Int32.Parse(lines[1]);
                                    int pid = Int32.Parse(lines[2]);

                                    // Change the focus: interrupt a timer (if there was one running) and start another.
                                    // But only if the focused process already exists
                                    if (_focusedProcess != null)
                                    {
                                        _focusedProcess.PauseWatch();
                                        _focusedProcess.ProcessStatus = "";
                                    }

                                    // Find the focused process.
                                    foreach (ProcessModel proc in _processList)
                                    {
                                        if (proc.ProcessWindowHandle == handle)
                                        {
                                            // Set this process as the focused one.
                                            _focusedProcess = proc;
                                            // Exit the for loop.
                                            break;
                                        }
                                    }
                                    _focusedProcess.RestartWatch();
                                    _focusedProcess.ProcessStatus = "On Focus";
                                    _mainWinWMCaller.NotificationMessage = "Server: " + _serverIpAddress.ToString() + ".\nFocus set on: " + _focusedProcess.ExePath + ".";
                                }
                            }

                            // Set the flag to read next block and go back to listen on the socket.
                            nextRead = true;
                            Receive(2);
                        }
                    }
                }

                else if (bytesRead == 0)
                {
                    // In am in the case of a graceful disconnection, shutdown of the server from the serverside. 

                    // Stop the Check Connection Timer
                    ServerTimer.Stop();

                    // Close the connection with the server. The disconnect function calls the CloseClient, the Remove server, and the removeProcessesFromApplication. 
                    _mainWinWMCaller.DisconnectFromServer(this.ServerIpAddress);

                    // Close the connection with the server. The disconnect function calls the CloseClient, the Remove server, and the removeProcessesFromApplication. 
                    System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                    // Open the Error Window 
                    Views.ErrorWindowView errView = new Views.ErrorWindowView();
                        ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Connessione interrotta dal server " + this.ServerIpAddress.ToString() + ".");
                        errWindViewModel.ClosingRequest += errView.Close;
                        errView.DataContext = errWindViewModel;
                        errView.Show();
                    });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void SendConnectionAlivenessVerificationCallback(IAsyncResult ar)
        {
            SocketError sockErr = new SocketError();
            try
            {
                // End the sending. 
                int bytesSent = _clientSocket.EndSend(ar, out sockErr);
                if (!sockErr.Equals(SocketError.Success) && !sockErr.Equals(SocketError.WouldBlock))
                {
                    _mainWinWMCaller.CancelStringCommand();
                    // Close the connection with the server. The disconnect function calls the CloseClient, the Remove server, and the removeProcessesFromApplication. 
                    _mainWinWMCaller.DisconnectFromServer(this.ServerIpAddress);

                    System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        // Open the Error Window 
                        Views.ErrorWindowView errView = new Views.ErrorWindowView();
                        ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Server " + this.ServerIpAddress.ToString() + " disconnesso.");
                        errWindViewModel.ClosingRequest += errView.Close;
                        errView.DataContext = errWindViewModel;
                        errView.Show();
                    });
                }
            }
            catch (Exception e)
            {
                // Check if the socket error is not WouldBlock (that is not a proper error):
                // in this case return without taking action
                if (sockErr.Equals(SocketError.WouldBlock))
                {
                    Console.WriteLine(e.ToString());
                }
                else
                {
                    _mainWinWMCaller.CancelStringCommand();
                    Console.WriteLine(e.ToString());
                    // Close the connection with the server. The disconnect function calls the CloseClient, the Remove server, and the removeProcessesFromApplication. 
                    _mainWinWMCaller.DisconnectFromServer(this.ServerIpAddress);

                    System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        // Open the Error Window 
                        Views.ErrorWindowView errView = new Views.ErrorWindowView();
                        ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Server " + this.ServerIpAddress.ToString() + " disconnesso.");
                        errWindViewModel.ClosingRequest += errView.Close;
                        errView.DataContext = errWindViewModel;
                        errView.Show();
                    });
                }
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            SocketError sockErr = new SocketError();
            try
            {
                // End the sending. 
                int bytesSent = _clientSocket.EndSend(ar, out sockErr);
                // _mainWinWMCaller.CancelStringCommand();
            }
            catch (Exception e)
            {
                // Check if the socket error is not WouldBlock (that is not a proper error):
                // in this case return without taking action
                if (sockErr.Equals(SocketError.WouldBlock))
                {
                    // _mainWinWMCaller.CancelStringCommand();
                    Console.WriteLine(e.ToString());
                }
                else
                {
                    _mainWinWMCaller.CancelStringCommand();
                    Console.WriteLine(e.ToString());
                    // Close the connection with the server. The disconnect function calls the CloseClient, the Remove server, and the removeProcessesFromApplication. 
                    _mainWinWMCaller.DisconnectFromServer(this.ServerIpAddress);

                    System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        // Open the Error Window 
                        Views.ErrorWindowView errView = new Views.ErrorWindowView();
                        ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Server " + this.ServerIpAddress.ToString() + " disconnesso.");
                        errWindViewModel.ClosingRequest += errView.Close;
                        errView.DataContext = errWindViewModel;
                        errView.Show();
                    });
                }
            }
        }

        private void AddProcessToApplication(ProcessModel process)
        {
            bool appFound = false;
            string exeName = Path.GetFileName(process.ExePath); // returns File.exe

            // Search if the application is already present. 
            foreach (ApplicationModel app in _mainWinWMCaller.TheApplicationsList)
            {
                if (app.ApplicationName.Equals(exeName))
                {
                    appFound = true;
                    // The application with this name already exists. 

                    // Check if it has an Icon, and otherwise add it from this process.
                    if (app.ApplicationIcon == null)
                    {
                        app.ApplicationIcon = process.ProcessIcon;
                    }

                    // If the app is not already executed by this server
                    if (!app.AppServerList.Contains(this))
                    {
                        // Add this server to its list. 
                        app.AddServerToList(this);
                        // Update the GUI. 
                        _mainWinWMCaller.RaisePropertyChanged("Applications");
                        _mainWinWMCaller.RaisePropertyChanged("ServerOfCurrentApplicationList");
                    }

                    // Otherwise it means it is already in the list, I won't do anything
                    break;
                }
            }

            // Check if the application has been found or not. In this case create a new application. 
            if (!appFound)
            {
                // I have to create a new application, using the constructor with its properties. 
                ApplicationModel app = new ApplicationModel(exeName, process.ProcessIcon);

                // Add this server to its list. 
                app.AppServerList.Add(this);

                // Add the application to the Application list of the mainView
                // I need a delegate to Modify an ObservableCollection instantiated ny another thread. 
                System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    _mainWinWMCaller.TheApplicationsList.Add(app);
                });

                // Update the GUI. 
                _mainWinWMCaller.RaisePropertyChanged("Applications");
                _mainWinWMCaller.RaisePropertyChanged("ServerOfCurrentApplicationList");
            }

        }

        private void OnTimeEventTestConnection(object source, ElapsedEventArgs e)
        {
            // Send 0 byte buffer, to check if the connection is still alive. 
            byte[] zeroByte = new byte[1];

            try
            {
                // Call the call back for the sending. 
                _clientSocket.BeginSend(zeroByte, 0, 0, SocketFlags.None, new AsyncCallback(SendConnectionAlivenessVerificationCallback), null);
            }
            catch (Exception ex)
            {
                _serverTimer.Stop();
                _mainWinWMCaller.CancelStringCommand();
                Console.WriteLine(ex.ToString());

                // Close the connection with the server. The disconnect function calls the CloseClient, the Remove server, and the removeProcessesFromApplication. 
                System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    // Open the Error Window 
                    Views.ErrorWindowView errView = new Views.ErrorWindowView();
                    ErrorWindowViewModel errWindViewModel = new ErrorWindowViewModel("Connessione interrotta dal server " + this.ServerIpAddress.ToString() + ".");
                    errWindViewModel.ClosingRequest += errView.Close;
                    errView.DataContext = errWindViewModel;
                    errView.Show();
                });

                _mainWinWMCaller.DisconnectFromServer(_serverIpAddress);
            }
        }
    }
}
