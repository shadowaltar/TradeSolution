using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TradeDesk.Utils;

public class DelegateCommand<T> : ICommand
{
    private readonly Func<T, bool>? _canExecute;
    private readonly Action<T> _execute;
    private readonly Task<Action<T>> _executeAsync;

    public event EventHandler? CanExecuteChanged;

    public DelegateCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public DelegateCommand(Task<Action<T>> executeAsync, Func<T, bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        if (_canExecute == null)
        {
            return true;
        }
        if (parameter is T param)
        {
            return _canExecute(param);
        }
        return false;
    }

    public void Execute(object? parameter)
    {
        if (parameter is T p)
        {
            if (_execute != null)
                _execute(p);
            if (_executeAsync != null)
                ExecuteAsync(_executeAsync);
        }
    }

    private async void ExecuteAsync(Task<Action<T>> executeAsync)
    {
        await executeAsync;
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public class DelegateCommand : ICommand
{
    private readonly Func<bool>? _canExecute;
    private readonly Action _execute;
    private readonly Task<Action> _executeAsync;

    public event EventHandler? CanExecuteChanged;

    public DelegateCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public DelegateCommand(Task<Action> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute();
    }

    public void Execute(object? parameter)
    {
        if (_execute != null)
            _execute();
        if (_executeAsync != null)
            ExecuteAsync(_executeAsync);
    }

    private async void ExecuteAsync(Task<Action> executeAsync)
    {
        await executeAsync;
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
