using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace Client_pds
{
    public class ProcessModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        /*********************
         * PUBLIC PROPERTIES *
         *********************/

        #region Private Properties
        
        // Name of hte process. 
        private string _windowName;

        // Name (path) of the window. 
        private string _exePath;

        // Id of the process, on the server side. 
        private int _processId;

        // Status of the process, can be "On Focus" or "" 
        private string _processStatus = "";

        // Keep count of the time the process has been focused. 
        private Stopwatch _processWatch;

        // Percentage of focused time, since client connected to the server. 
        private int _timePercentage;

        // Icon of the process. 
        private BitmapImage _processIcon;

        // Handle of the process, on the server side. 
        private int _processWindowHandle;

        #endregion

        /*********************
         * PUBLIC PROPERTIES *
         *********************/

        #region Public Properties

        public string WindowName
        {
            get
            {
                return _windowName;
            }
            set
            {
                _windowName = value;
                OnPropertyChanged("ExeName");
            }
        }

        public string ExePath
        {
            get
            {
                return _exePath;
            }

            set
            {
                _exePath = value;
                OnPropertyChanged("ProcessName");
            }
        }

        public int ProcessId
        {
            get
            {
                return _processId;
            }

            set
            {
                _processId = value;
                OnPropertyChanged("ProcessId");
            }
        }

        public int TimePercentage
        {
            get
            {
                return _timePercentage;
            }
            set
            {
                _timePercentage = value;
                OnPropertyChanged("TimePercentage");
            }
        }

        public BitmapImage ProcessIcon
        {
            get
            {
                return _processIcon;
            }
            set
            {
                _processIcon = value;
                OnPropertyChanged("ProcessIcon");
            }
        }

        public int ProcessWindowHandle
        {
            get
            {
                return _processWindowHandle;
            }
        }

        public string ProcessStatus
        {
            get
            {
                return _processStatus;
            }
            set
            {
                _processStatus = value;
                OnPropertyChanged("ProcessStatus");
            }
        }

        #endregion

        /***************
         * Constructor *
         ***************/

        public ProcessModel(string pWindowName, string pPath, int pWindowHandle, int pID, BitmapImage pIcon)
        {
            // As soon as the ProcessModel is created, start its timer. 
            _processWatch = new Stopwatch();

            // Create the ProcessModel, with all its parameters. 
            _timePercentage = 0;
            _windowName = pWindowName;
            _exePath = pPath;
            _processId = pID;
            _processIcon = new BitmapImage();
            _processIcon = pIcon;
            _processWindowHandle = pWindowHandle;
        }

        /******************
         * PUBLIC METHODS *
         ******************/
  
        public void PauseWatch()
        {
            if (_processWatch.IsRunning)
            {
                _processWatch.Stop();
            }
        }

        public void RestartWatch()
        {
            if (!_processWatch.IsRunning)
            {
                _processWatch.Start();
            }
        }

        public TimeSpan GetElapsedTime()
        {
            // return _processWatch.ElapsedMilliseconds;
            return _processWatch.Elapsed;
        }

    }
}
