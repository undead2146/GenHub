using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GenHub.Common.ViewModels;

/// <summary>
/// Base class for all view models in the application
/// </summary>
public class ViewModelBase : ObservableObject, INotifyPropertyChanged, IDisposable
{
    // ObservableObject already implements INotifyPropertyChanged,
    // but we're re-implementing it to ensure compatibility with all Avalonia binding scenarios
    public new event PropertyChangedEventHandler? PropertyChanged;
    
    // Flag to track if the object has been disposed
    private bool _disposed = false;
    
    protected internal virtual new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        base.OnPropertyChanged(propertyName);
    }
    
    protected new bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    /// <summary>
    /// Disposes of resources used by the view model
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Protected implementation of Dispose pattern
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        if (disposing)
        {
            // Free any managed objects here
            // No managed objects to dispose in the base class
        }
        
        // Free any unmanaged objects here
        
        _disposed = true;
    }
}
