using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TradeDesk.ViewModels;

public abstract class AbstractViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool SetValue<T>(ref T property, T value, [CallerMemberName] string propertyName = "")
    {
        if (Equals(value, property))
            return false;

        property = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public bool SetValue<T>(ref T property, T value, Action<T> callback, [CallerMemberName] string propertyName = "")
    {
        if (Equals(value, property))
            return false;

        property = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        callback?.Invoke(value);
        return true;
    }

    public bool SetValue<T>(ref T property, T value, SetValueCallback<T> callback, [CallerMemberName] string propertyName = "")
    {
        if (Equals(value, property))
            return false;

        var oldValue = property;
        property = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        callback?.Invoke(oldValue, value);
        return true;
    }

    public delegate void SetValueCallback<T>(T oldValue, T newValue);
}
