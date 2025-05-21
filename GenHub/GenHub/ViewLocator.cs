using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GenHub
{
    /// <summary>
    /// Maps view models to views in the application
    /// </summary>
    public class ViewLocator : IDataTemplate
    {
        private readonly ILogger<ViewLocator>? _logger;

        public ViewLocator(ILogger<ViewLocator>? logger = null)
        {
            _logger = logger;
        }
        public ViewLocator()
        {
        }

        public Control Build(object? data)
        {
            if (data == null)
                return new TextBlock { Text = "No data context provided" };

            try
            {
                var name = data.GetType().FullName!.Replace("ViewModel", "View");
                var type = Type.GetType(name);

                if (type != null)
                {
                    var instance = Activator.CreateInstance(type) as Control;
                    return instance ?? new TextBlock { Text = $"Could not create instance of {name}" };
                }
                else
                {
                    _logger?.LogWarning("Could not find view for {ViewModelType}", data.GetType().FullName);
                    return new TextBlock { Text = $"Not Found: {name}" };
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error locating view for {ViewModelType}", data.GetType().FullName);
                Console.WriteLine($"ViewLocator error: {ex.Message}");
                return new TextBlock { Text = $"Error: {ex.Message}" };
            }
        }

        public bool Match(object? data)
        {
            return data is not null && data.GetType().Name.EndsWith("ViewModel");
        }
    }
}
