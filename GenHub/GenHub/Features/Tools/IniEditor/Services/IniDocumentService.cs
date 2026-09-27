using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Tools.IniEditor;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.Tools.IniEditor;
using GenHub.Core.Models.Validation;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Features.Tools.IniEditor.Services;

/// <summary>
/// Service for parsing, writing, formatting, and validating Generals and Zero Hour INI documents.
/// </summary>
public sealed class IniDocumentService(ILogger<IniDocumentService> logger) : IIniDocumentService
{
    /// <inheritdoc />
    public OperationResult<IniDocument> ParseText(string content, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();
        var document = new IniDocument { SourcePath = sourcePath };
        var stack = new Stack<IniBlock>();
        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var raw = lines[i];
            if (raw.Contains('\t'))
            {
                errors.Add($"Line {lineNumber}: Tab characters are not allowed in INI files.");
                continue;
            }

            var line = StripComment(raw).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (string.Equals(line, IniConstants.BlockTags.End, StringComparison.OrdinalIgnoreCase))
            {
                if (stack.Count == 0)
                {
                    errors.Add($"Line {lineNumber}: Unexpected 'End' without an open block.");
                    continue;
                }

                var closed = stack.Pop();
                if (stack.Count == 0)
                {
                    document.Blocks.Add(closed);
                }
                else
                {
                    stack.Peek().Children.Add(closed);
                }

                continue;
            }

            var separatorIndex = line.IndexOf(IniConstants.Syntax.KeyValueSeparator);
            if (separatorIndex > 0 && stack.Count > 0)
            {
                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();
                if (key.Length == 0)
                {
                    errors.Add($"Line {lineNumber}: Field is missing a key.");
                    continue;
                }

                stack.Peek().Fields.Add(new IniField(key, value));
                continue;
            }

            if (separatorIndex >= 0 && stack.Count == 0)
            {
                errors.Add($"Line {lineNumber}: Field '{line}' appears outside of a block.");
                continue;
            }

            var tokens = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                continue;
            }

            var block = new IniBlock
            {
                BlockType = tokens[0],
                Name = tokens.Length > 1 ? string.Join(' ', tokens[1..]) : string.Empty,
                LineNumber = lineNumber,
            };
            stack.Push(block);
        }

        while (stack.Count > 0)
        {
            var open = stack.Pop();
            errors.Add($"Line {open.LineNumber}: Block '{open.BlockType} {open.Name}' is missing 'End'.".Trim());
            if (stack.Count == 0)
            {
                document.Blocks.Add(open);
            }
            else
            {
                stack.Peek().Children.Add(open);
            }
        }

        if (errors.Count > 0)
        {
            logger.LogWarning("Failed to parse INI {Source}: {Error}", sourcePath ?? "(memory)", errors[0]);
            return OperationResult<IniDocument>.CreateFailure(errors, stopwatch.Elapsed);
        }

        logger.LogInformation("Parsed INI {Source} with {Count} blocks", sourcePath ?? "(memory)", document.Blocks.Count);
        return OperationResult<IniDocument>.CreateSuccess(document, stopwatch.Elapsed);
    }

    /// <inheritdoc />
    public async Task<OperationResult<IniDocument>> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(filePath))
        {
            return OperationResult<IniDocument>.CreateFailure($"INI file not found: {filePath}");
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var content = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            if (content.Contains('�'))
            {
                return OperationResult<IniDocument>.CreateFailure($"File contains replacement characters: {filePath}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return ParseText(content, filePath);
        }
        catch (DecoderFallbackException ex)
        {
            logger.LogWarning(ex, "Refusing to parse {Path}: non-UTF-8 or ANSI encoding detected", filePath);
            return OperationResult<IniDocument>.CreateFailure($"Cannot parse file with non-UTF-8 or unsupported ANSI encoding: {filePath}");
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Failed to read INI file {Path}", filePath);
            return OperationResult<IniDocument>.CreateFailure($"Failed to read INI file: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Access denied reading INI file {Path}", filePath);
            return OperationResult<IniDocument>.CreateFailure($"Access denied reading INI file: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public string WriteDocument(IniDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var builder = new StringBuilder();
        foreach (var block in document.Blocks)
        {
            WriteBlock(builder, block, 0);
            builder.Append(IniConstants.Syntax.NewLine);
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public async Task<OperationResult<bool>> FormatFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var stopwatch = Stopwatch.StartNew();

        if (!File.Exists(filePath))
        {
            return OperationResult<bool>.CreateFailure($"INI file not found: {filePath}", stopwatch.Elapsed);
        }

        var parseResult = await ParseFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (!parseResult.Success || parseResult.Data == null)
        {
            return OperationResult<bool>.CreateFailure(parseResult.Errors, stopwatch.Elapsed);
        }

        var canonical = WriteDocument(parseResult.Data);
        var directory = Path.GetDirectoryName(filePath);
        var tempPath = Path.Combine(directory ?? Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            await File.WriteAllTextAsync(tempPath, canonical, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
            logger.LogInformation("Formatted INI file {Path}", filePath);
            return OperationResult<bool>.CreateSuccess(true, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            DeleteTempFile(tempPath);
            throw;
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Failed to format INI file {Path}", filePath);
            DeleteTempFile(tempPath);
            return OperationResult<bool>.CreateFailure($"Failed to format INI file: {ex.Message}", stopwatch.Elapsed);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Access denied formatting INI file {Path}", filePath);
            DeleteTempFile(tempPath);
            return OperationResult<bool>.CreateFailure($"Access denied formatting INI file: {ex.Message}", stopwatch.Elapsed);
        }
    }

    /// <inheritdoc />
    public ValidationResult ValidateDocument(IniDocument document, string validatedTargetId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(validatedTargetId);
        var stopwatch = Stopwatch.StartNew();
        var issues = new List<ValidationIssue>();

        if (document.Blocks.Count == 0)
        {
            issues.Add(new ValidationIssue("Document contains no blocks.", ValidationSeverity.Warning, validatedTargetId));
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in document.Blocks)
        {
            ValidateBlock(block, validatedTargetId, issues, names);
        }

        return new ValidationResult(validatedTargetId, issues, stopwatch.Elapsed);
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ValidateFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var stopwatch = Stopwatch.StartNew();

        if (!File.Exists(filePath))
        {
            var missing = new ValidationIssue($"INI file not found: {filePath}", ValidationSeverity.Critical, filePath)
            {
                IssueType = ValidationIssueType.MissingFile,
            };
            return new ValidationResult(filePath, [missing], stopwatch.Elapsed);
        }

        var parseResult = await ParseFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (!parseResult.Success || parseResult.Data == null)
        {
            var issues = new List<ValidationIssue>();
            foreach (var error in parseResult.Errors)
            {
                issues.Add(new ValidationIssue(error, ValidationSeverity.Error, filePath)
                {
                    IssueType = ValidationIssueType.CorruptedFile,
                });
            }

            return new ValidationResult(filePath, issues, stopwatch.Elapsed);
        }

        return ValidateDocument(parseResult.Data, filePath);
    }

    private static void ValidateBlock(IniBlock block, string targetId, List<ValidationIssue> issues, HashSet<string> names)
    {
        if (block.BlockType.Length == 0)
        {
            issues.Add(new ValidationIssue(
                $"Block at line {block.LineNumber} is missing a block type.",
                ValidationSeverity.Error,
                targetId));
        }

        if (!string.IsNullOrEmpty(block.Name))
        {
            var key = $"{block.BlockType} {block.Name}";
            if (!names.Add(key))
            {
                issues.Add(new ValidationIssue(
                    $"Duplicate block '{key}'.",
                    ValidationSeverity.Warning,
                    targetId));
            }
        }

        if (block.Fields.Count == 0 && block.Children.Count == 0)
        {
            issues.Add(new ValidationIssue(
                $"Block '{block.BlockType} {block.Name}' is empty.".Trim(),
                ValidationSeverity.Warning,
                targetId));
        }

        foreach (var child in block.Children)
        {
            ValidateBlock(child, targetId, issues, names);
        }
    }

    private static void WriteBlock(StringBuilder builder, IniBlock block, int indent)
    {
        AppendLine(builder, indent, string.IsNullOrEmpty(block.Name) ? block.BlockType : $"{block.BlockType} {block.Name}");
        foreach (var field in block.Fields)
        {
            AppendLine(builder, indent + 1, $"{field.Key} = {field.Value}");
        }

        foreach (var child in block.Children)
        {
            WriteBlock(builder, child, indent + 1);
        }

        AppendLine(builder, indent, IniConstants.BlockTags.End);
    }

    private static void AppendLine(StringBuilder builder, int indent, string text)
    {
        builder.Append(' ', indent * 2);
        builder.Append(text);
        builder.Append(IniConstants.Syntax.NewLine);
    }

    private static string StripComment(string raw)
    {
        var inQuotes = false;
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '"')
            {
                inQuotes = !inQuotes;
            }

            if (!inQuotes && raw[i] == IniConstants.Syntax.Comment)
            {
                return raw[..i];
            }
        }

        return raw;
    }

    private static void DeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup of the temp file.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup of the temp file.
        }
    }
}
