using System;

namespace GenHub.Features.Tools.IniEditor.ViewModels;

/// <summary>
/// A single undoable edit in the INI editor.
/// </summary>
/// <param name="Title">The display title of the edit.</param>
/// <param name="Redo">Applies the edit.</param>
/// <param name="Undo">Reverts the edit.</param>
public sealed record IniEditAction(string Title, Action Redo, Action Undo);
