using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Text;
using System.Windows.Controls;

namespace Client_pds
{
    class ErrorWindowViewModel : ObservableObject
    {
        private ICommand _closeCommand;
        private string _errString;

        public string ErrString
        {
            get
            {
                return _errString;
            }
            set
            {
                _errString = value;
            }
        }

        public ErrorWindowViewModel(string errorString)
        {
            ErrString = errorString;
        }

        public ICommand CloseCommand
        {
            get
            {
                if (_closeCommand == null)
                {
                    _closeCommand = new RelayCommand(
                        param => Close(),
                        param => CanClose());
                }
                return _closeCommand;
            }
        }

        public event Action ClosingRequest;

        public virtual void Close()
        {
            if (ClosingRequest != null)
            {
                ClosingRequest();
            }
        }

        public virtual bool CanClose()
        {
            return true;
        }
    }
}
