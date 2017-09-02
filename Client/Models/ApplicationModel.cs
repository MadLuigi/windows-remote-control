using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Net;

namespace Client_pds.Models
{
    public class ApplicationModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

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

        // Name of the application. 
        private string _applicationName;

        // Icon of the application.
        private BitmapImage _applicationIcon = null;

        // List of the IP Addresses of the servers that run the application. 
        private ObservableCollection<ServerModel> _appServerList = new ObservableCollection<ServerModel>();

        #endregion

        /*********************
         * PUBLIC PROPERTIES *
         *********************/

        #region Public Properties

        public ObservableCollection<ServerModel> AppServerList
        {
            get
            {
                return _appServerList;
            }
            set
            {
                _appServerList = value;
                OnPropertyChanged("AppServerList");
            }
        }

        public string ApplicationName
        {
            get
            {
                return _applicationName;
            }
            set
            {
                _applicationName = value;
                OnPropertyChanged("ApplicationName");
            }
        }

        public BitmapImage ApplicationIcon
        {
            get
            {
                return _applicationIcon;
            }
            set
            {
                _applicationIcon = value;
                OnPropertyChanged("ApplicationIcon");
            }
        }

        #endregion

        /***************
         * Constructor *
         ***************/

        public ApplicationModel(string appName, BitmapImage appIcon)
        {
            ApplicationName = appName;
            ApplicationIcon = appIcon;
        }
    
        /******************
         * PUBLIC METHODS *
         ******************/

        public void AddServerToList(ServerModel server)
        {
            AppServerList.Add(server);
            RaisePropertyChanged("AppServerList");
        }

    }
}
