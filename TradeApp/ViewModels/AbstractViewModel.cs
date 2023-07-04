using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TradeApp.ViewModels;
public abstract class AbstractViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetValue<T>(ref T property, T value, [CallerMemberName] string propertyName = "")
    {
        if (Equals(value, property))
            return;

        property = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void SetValue<T>(ref T property, T value, Action<T> callback, [CallerMemberName] string propertyName = "")
    {
        if (Equals(value, property))
            return;

        property = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        callback?.Invoke(value);
    }

    public void SetValue<T>(ref T property, T value, SetValueCallback<T> callback, [CallerMemberName] string propertyName = "")
    {
        if (Equals(value, property))
            return;

        var oldValue = property;
        property = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        callback?.Invoke(oldValue, value);
    }

    public delegate void SetValueCallback<T>(T oldValue, T newValue);
}
