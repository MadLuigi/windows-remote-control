using System.ComponentModel;
using System.Collections;

namespace Client_pds
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public virtual void VerifyPropertyName(string propertyName)
        {
            //verify that the property name is real
            /*if (TypeDescriptor.GetProperties(this)[propertyName] == null)
            {
                string msg = "Invalid Property Name: " + propertyName;
                if (this.ThrowInvalidpropertyName)
                {
                    throw new Exception(msg);
                }
                else
                {
                    Debug.Fail(msg);
                }
            }*/

        }

        //returns wheater an exception is thrown 
        protected virtual bool ThrowInvalidpropertyName { get; private set; }


        //raises the PropertyChanged event for the property specified 
        public virtual void RaisePropertyChanged(string propertyName)
        {
            //this.VerifyPropertyName(propertyName);
            OnPropertyChanged(propertyName);
        }

        //raised when a property on this object has a new value
        public event PropertyChangedEventHandler PropertyChanged;

        //raises the object's PropertyChanged event
        protected virtual void OnPropertyChanged(string propertyName)
        {
            //this.VerifyPropertyName(propertyName);
            /*PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }*/
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }

        }
    }
}