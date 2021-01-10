using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BLE
{
    [AttributeUsage(AttributeTargets.All)]
    public class ManualUpdate : Attribute
    {
        public ManualUpdate() { }
        public ManualUpdate(bool updateIfForced)
        {
            UpdateIfForced = updateIfForced;
        }

        public bool UpdateIfForced { get; set; } = true;
    }

    public abstract class ViewModel : INotifyPropertyChanged
    {
        public static Action<Action> DispatchAction = a => a();

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            DispatchAction(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        protected virtual void SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            storage = value;
            RaisePropertyChanged(propertyName);
        }

        public void UpdateProperties(bool forceAll = false)
        {
            DispatchAction(() =>
            {
                foreach (var item in GetType().GetProperties())
                {
                    var manual = item.GetCustomAttributes(typeof(ManualUpdate), true).FirstOrDefault() as ManualUpdate;

                    if (manual != null && (!forceAll || !manual.UpdateIfForced))
                        continue;

                    RaisePropertyChanged(item.Name);
                }
            });
        }

        public void UpdateProperties(IEnumerable<string> names)
        {
            DispatchAction(() =>
            {
                foreach (var item in names)
                    RaisePropertyChanged(item);
            });
        }
    }
}
