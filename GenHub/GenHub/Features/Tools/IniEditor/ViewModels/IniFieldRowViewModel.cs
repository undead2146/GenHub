using CommunityToolkit.Mvvm.ComponentModel;
using GenHub.Core.Models.Tools.IniEditor;
using System;

namespace GenHub.Features.Tools.IniEditor.ViewModels;

/// <summary>
/// Editable row wrapping a single INI field with write-through to the document block.
/// </summary>
public sealed partial class IniFieldRowViewModel : ObservableObject
{
    private readonly IniBlock _block;
    private readonly int _fieldIndex;
    private readonly Action _onChanged;
    private readonly Action<string, string> _onEditCommitted;
    private readonly string _editBase;

    /// <summary>
    /// Initializes a new instance of the <see cref="IniFieldRowViewModel"/> class.
    /// </summary>
    /// <param name="block">The owning block.</param>
    /// <param name="fieldIndex">Index of the field within the block.</param>
    /// <param name="description">Schema description, when known.</param>
    /// <param name="isKnown">Whether the field is covered by the schema.</param>
    /// <param name="onChanged">Callback invoked when the value changes.</param>
    /// <param name="onEditCommitted">Callback invoked with the pre-edit and current value for undo tracking.</param>
    public IniFieldRowViewModel(IniBlock block, int fieldIndex, string? description, bool isKnown, Action onChanged, Action<string, string> onEditCommitted)
    {
        _block = block;
        _fieldIndex = fieldIndex;
        Description = description;
        IsKnown = isKnown;
        _onChanged = onChanged;
        _onEditCommitted = onEditCommitted;
        _value = block.Fields[fieldIndex].Value;
        _editBase = _value;
    }

    /// <summary>
    /// Gets the field key.
    /// </summary>
    public string Key => _block.Fields[_fieldIndex].Key;

    /// <summary>
    /// Gets the schema description, when known.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets a value indicating whether the field is covered by the schema.
    /// </summary>
    public bool IsKnown { get; }

    /// <summary>
    /// Gets or sets the edited value, writing through to the document on change.
    /// </summary>
    [ObservableProperty]
    private string _value;

    partial void OnValueChanged(string value)
    {
        var current = _block.Fields[_fieldIndex];
        _block.Fields[_fieldIndex] = current with { Value = value };
        _onChanged();
        _onEditCommitted(_editBase, value);
    }
}
