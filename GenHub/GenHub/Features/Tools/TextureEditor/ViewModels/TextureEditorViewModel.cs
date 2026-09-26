using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Interfaces.Tools.TextureEditor;
using GenHub.Core.Models.Tools.TextureEditor;
using GenHub.Features.Tools.TextureEditor.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GenHub.Features.Tools.TextureEditor.ViewModels;

/// <summary>
/// ViewModel for the Texture Editor tool.
/// </summary>
[SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "TextureEditorViewModel coordinates atlas loading, slicing, registry search, packing, export, notifications, logging, and localization.")]
public sealed partial class TextureEditorViewModel : ObservableObject, IDisposable
{
    private readonly ISageMappedImageParser _parser;
    private readonly IMappedImageRegistry _registry;
    private readonly IAtlasPackingService _packingService;
    private readonly ITextureImageLoader _imageLoader;
    private readonly TextureBitmapService _bitmapService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<TextureEditorViewModel> _logger;
    private readonly ILocalizationService? _localizationService;
    private DecodedTexture? _atlasDecoded;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextureEditorViewModel"/> class.
    /// </summary>
    /// <param name="parser">The mapped image parser.</param>
    /// <param name="registry">The mapped image registry.</param>
    /// <param name="packingService">The atlas packing service.</param>
    /// <param name="imageLoader">The source image loader.</param>
    /// <param name="bitmapService">The bitmap bridge service.</param>
    /// <param name="notificationService">The notification service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="localizationService">The optional localization service.</param>
    public TextureEditorViewModel(
        ISageMappedImageParser parser,
        IMappedImageRegistry registry,
        IAtlasPackingService packingService,
        ITextureImageLoader imageLoader,
        TextureBitmapService bitmapService,
        INotificationService notificationService,
        ILogger<TextureEditorViewModel> logger,
        ILocalizationService? localizationService = null)
    {
        _parser = parser;
        _registry = registry;
        _packingService = packingService;
        _imageLoader = imageLoader;
        _bitmapService = bitmapService;
        _notificationService = notificationService;
        _logger = logger;
        _localizationService = localizationService;
    }

    [ObservableProperty]
    private Bitmap? _atlasBitmap;

    [ObservableProperty]
    private string _atlasPath = string.Empty;

    [ObservableProperty]
    private double _zoom = 1;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private TextureSliceViewModel? _selectedSlice;

    partial void OnAtlasBitmapChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasAtlas));
        OnPropertyChanged(nameof(DisplayWidth));
        OnPropertyChanged(nameof(DisplayHeight));
        OnPropertyChanged(nameof(AtlasDimensions));
    }

    partial void OnAtlasPathChanged(string value) => OnPropertyChanged(nameof(AtlasFileName));

    partial void OnZoomChanged(double value)
    {
        foreach (var slice in Slices)
        {
            slice.UpdateZoom(value);
        }

        OnPropertyChanged(nameof(DisplayWidth));
        OnPropertyChanged(nameof(DisplayHeight));
    }

    partial void OnSelectedSliceChanged(TextureSliceViewModel? oldValue, TextureSliceViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }
    }

    /// <summary>
    /// Gets the slices of the open atlas.
    /// </summary>
    public ObservableCollection<TextureSliceViewModel> Slices { get; } = [];

    /// <summary>
    /// Gets the registry entries shown in the shared picker.
    /// </summary>
    public ObservableCollection<MappedImageDefinition> RegistryImages { get; } = [];

    /// <summary>
    /// Gets a value indicating whether an atlas is open.
    /// </summary>
    public bool HasAtlas => AtlasBitmap is not null;

    /// <summary>
    /// Gets the atlas file name for display.
    /// </summary>
    public string AtlasFileName => AtlasPath.Length == 0 ? string.Empty : Path.GetFileName(AtlasPath);

    /// <summary>
    /// Gets the display width of the atlas canvas.
    /// </summary>
    public double DisplayWidth => AtlasBitmap is null ? 0 : AtlasBitmap.PixelSize.Width * Zoom;

    /// <summary>
    /// Gets the display height of the atlas canvas.
    /// </summary>
    public double DisplayHeight => AtlasBitmap is null ? 0 : AtlasBitmap.PixelSize.Height * Zoom;

    /// <summary>
    /// Gets the atlas dimensions display text.
    /// </summary>
    public string AtlasDimensions => AtlasBitmap is null
        ? string.Empty
        : $"{AtlasBitmap.PixelSize.Width} x {AtlasBitmap.PixelSize.Height} px";

    /// <summary>
    /// Gets the thumbnail provider for the shared mapped image picker.
    /// </summary>
    public Func<MappedImageDefinition, IImage?> PickerThumbnailProvider => CreatePickerThumbnail;

    /// <summary>
    /// Loads a registry entry as a new slice on the open atlas.
    /// </summary>
    /// <param name="definition">The mapped image definition.</param>
    public void LoadRegistryEntry(MappedImageDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (AtlasBitmap is null)
        {
            _notificationService.ShowWarning(
                Localize("TextureEditor.Notify.NoAtlas.Title", "No atlas open"),
                Localize("TextureEditor.Notify.NoAtlas.Message", "Open a texture atlas before adding slices."),
                NotificationDurations.Medium);
            return;
        }

        var slice = new TextureSliceViewModel(definition);
        slice.UpdateTexture(AtlasFileName, AtlasBitmap.PixelSize.Width, AtlasBitmap.PixelSize.Height);
        slice.UpdateZoom(Zoom);
        AddSlice(slice);
        SelectedSlice = slice;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var slice in Slices)
        {
            slice.PropertyChanged -= OnSlicePropertyChanged;
            DisposeThumbnail(slice.Thumbnail);
        }

        AtlasBitmap?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static TopLevel? GetTopLevel()
    {
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return TopLevel.GetTopLevel(lifetime?.MainWindow);
    }

    private static void DisposeThumbnail(IImage? image)
    {
        if (image is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(8, Math.Round(Zoom + 0.25, 2));

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(0.25, Math.Round(Zoom - 0.25, 2));

    [RelayCommand]
    private void ResetZoom() => Zoom = 1;

    [RelayCommand(CanExecute = nameof(HasAtlas))]
    private void AddSlice()
    {
        if (AtlasBitmap is null)
        {
            return;
        }

        int width = AtlasBitmap.PixelSize.Width;
        int height = AtlasBitmap.PixelSize.Height;
        int size = Math.Min(TextureEditorConstants.CameoLargeWidth, Math.Min(width, height));
        int left = Math.Max(0, (width - size) / 2);
        int top = Math.Max(0, (height - size) / 2);
        var definition = new MappedImageDefinition(
            $"Slice{Slices.Count + 1}",
            AtlasFileName,
            width,
            height,
            left,
            top,
            left + size - 1,
            top + size - 1);
        var slice = new TextureSliceViewModel(definition);
        slice.UpdateZoom(Zoom);
        AddSlice(slice);
        SelectedSlice = slice;
    }

    [RelayCommand]
    private void DeleteSelectedSlice()
    {
        if (SelectedSlice is null)
        {
            return;
        }

        SelectedSlice.PropertyChanged -= OnSlicePropertyChanged;
        DisposeThumbnail(SelectedSlice.Thumbnail);
        Slices.Remove(SelectedSlice);
        SelectedSlice = Slices.FirstOrDefault();
    }

    [RelayCommand]
    private void ApplyPreset(string preset)
    {
        if (SelectedSlice is null)
        {
            return;
        }

        (int width, int height) = preset switch
        {
            "60x48" => (TextureEditorConstants.CameoSmallWidth, TextureEditorConstants.CameoSmallHeight),
            "32x32" => (TextureEditorConstants.HudButtonWidth, TextureEditorConstants.HudButtonHeight),
            _ => (TextureEditorConstants.CameoLargeWidth, TextureEditorConstants.CameoLargeHeight),
        };
        SelectedSlice.Right = SelectedSlice.Left + width - 1;
        SelectedSlice.Bottom = SelectedSlice.Top + height - 1;
    }

    [RelayCommand]
    private async Task OpenAtlasAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Localize("TextureEditor.Dialog.OpenAtlas", "Open texture atlas"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Localize("TextureEditor.Dialog.TextureFiles", "Texture files"))
                {
                    Patterns = ["*.tga", "*.dds", "*.png"],
                },
            ],
        }).ConfigureAwait(true);

        if (files.Count == 0)
        {
            return;
        }

        await LoadAtlasAsync(files[0].Path.LocalPath).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ScanRegistryAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Localize("TextureEditor.Dialog.ScanFolder", "Select MappedImages folder"),
            AllowMultiple = false,
        }).ConfigureAwait(true);

        if (folders.Count == 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            _registry.Clear();
            var result = await _registry.ScanDirectoryAsync(folders[0].Path.LocalPath).ConfigureAwait(true);
            RegistryImages.Clear();
            foreach (var image in _registry.All)
            {
                RegistryImages.Add(image);
            }

            if (result.Success && result.Data is not null)
            {
                _notificationService.ShowSuccess(
                    Localize("TextureEditor.Notify.ScanComplete.Title", "Scan complete"),
                    Localize("TextureEditor.Notify.ScanComplete.Message", "Indexed {0} mapped images from {1} files.", result.Data.ImagesIndexed, result.Data.FilesScanned),
                    NotificationDurations.Medium);
                LoadSlicesForAtlas();
            }
            else
            {
                _notificationService.ShowError(
                    Localize("TextureEditor.Notify.ScanFailed.Title", "Scan failed"),
                    result.FirstError ?? Localize("TextureEditor.Notify.ScanFailed.Message", "Failed to scan MappedImages folder."),
                    NotificationDurations.Long);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registry scan failed");
            _notificationService.ShowError(
                Localize("TextureEditor.Notify.ScanFailed.Title", "Scan failed"),
                ex.Message,
                NotificationDurations.Long);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasAtlas))]
    private async Task ExportIniAsync()
    {
        if (AtlasBitmap is null)
        {
            return;
        }

        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Localize("TextureEditor.Dialog.ExportIni", "Export MappedImages INI"),
            SuggestedFileName = Path.GetFileNameWithoutExtension(AtlasPath) + ".ini",
            FileTypeChoices =
            [
                new FilePickerFileType(Localize("TextureEditor.Dialog.IniFiles", "INI files"))
                {
                    Patterns = ["*.ini"],
                },
            ],
        }).ConfigureAwait(true);

        if (file is null)
        {
            return;
        }

        await ExportIniToPathAsync(file.Path.LocalPath).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasAtlas))]
    private async Task ExportSheetAsync()
    {
        if (AtlasBitmap is null || _atlasDecoded is null)
        {
            return;
        }

        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Localize("TextureEditor.Dialog.ExportSheet", "Export texture sheet"),
            SuggestedFileName = Path.GetFileNameWithoutExtension(AtlasPath),
            FileTypeChoices =
            [
                new FilePickerFileType("TGA")
                {
                    Patterns = ["*.tga"],
                },
                new FilePickerFileType("PNG")
                {
                    Patterns = ["*.png"],
                },
            ],
        }).ConfigureAwait(true);

        if (file is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            string path = file.Path.LocalPath;
            var saved = Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? await _bitmapService.SavePngAsync(AtlasBitmap, path).ConfigureAwait(true)
                : await _bitmapService.SaveTgaAsync(_atlasDecoded, path).ConfigureAwait(true);

            if (saved.Success)
            {
                _notificationService.ShowSuccess(
                    Localize("TextureEditor.Notify.ExportComplete.Title", "Export complete"),
                    Localize("TextureEditor.Notify.ExportComplete.Message", "Saved {0}.", Path.GetFileName(path)),
                    NotificationDurations.Medium);
            }
            else
            {
                _notificationService.ShowError(
                    Localize("TextureEditor.Notify.ExportFailed.Title", "Export failed"),
                    saved.FirstError ?? string.Empty,
                    NotificationDurations.Long);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sheet export failed");
            _notificationService.ShowError(
                Localize("TextureEditor.Notify.ExportFailed.Title", "Export failed"),
                ex.Message,
                NotificationDurations.Long);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AutoPackAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Localize("TextureEditor.Dialog.PackFolder", "Select folder with loose images"),
            AllowMultiple = false,
        }).ConfigureAwait(true);

        if (folders.Count == 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            string sourceDir = folders[0].Path.LocalPath;
            var request = new TextureAtlasBuildRequest(
                sourceDir,
                Path.Combine(sourceDir, "PackedAtlas.tga"),
                Path.Combine(sourceDir, "PackedAtlas.ini"));
            var built = await _packingService.BuildAtlasAsync(request, _imageLoader).ConfigureAwait(true);
            if (built.Failed || built.Data is null)
            {
                _notificationService.ShowError(
                    Localize("TextureEditor.Notify.PackFailed.Title", "Auto-pack failed"),
                    built.FirstError ?? string.Empty,
                    NotificationDurations.Long);
                return;
            }

            OpenDecodedAtlas(built.Data.Sheet, request.TargetTexture);
            ReplaceSlices(built.Data.MappedImages);
            _notificationService.ShowSuccess(
                Localize("TextureEditor.Notify.PackComplete.Title", "Atlas packed"),
                Localize("TextureEditor.Notify.PackComplete.Message", "Packed {0} sprites into a {1}x{2} sheet.", built.Data.MappedImages.Count, built.Data.Sheet.Width, built.Data.Sheet.Height),
                NotificationDurations.Medium);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-pack failed");
            _notificationService.ShowError(
                Localize("TextureEditor.Notify.PackFailed.Title", "Auto-pack failed"),
                ex.Message,
                NotificationDurations.Long);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string Localize(string key, string fallback, params object?[] args)
    {
        string template = _localizationService?.GetString(key) ?? fallback;
        return args.Length == 0 ? template : string.Format(template, args);
    }

    private async Task LoadAtlasAsync(string path)
    {
        IsBusy = true;
        try
        {
            var decoded = await _bitmapService.LoadDecodedAsync(path).ConfigureAwait(true);
            if (decoded.Failed || decoded.Data is null)
            {
                _notificationService.ShowError(
                    Localize("TextureEditor.Notify.OpenFailed.Title", "Failed to open atlas"),
                    decoded.FirstError ?? string.Empty,
                    NotificationDurations.Long);
                return;
            }

            OpenDecodedAtlas(decoded.Data, path);
            LoadSlicesForAtlas();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open atlas {Path}", path);
            _notificationService.ShowError(
                Localize("TextureEditor.Notify.OpenFailed.Title", "Failed to open atlas"),
                ex.Message,
                NotificationDurations.Long);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenDecodedAtlas(DecodedTexture decoded, string path)
    {
        var bitmap = _bitmapService.ToBitmap(decoded);
        if (bitmap.Failed || bitmap.Data is null)
        {
            _notificationService.ShowError(
                Localize("TextureEditor.Notify.OpenFailed.Title", "Failed to open atlas"),
                bitmap.FirstError ?? string.Empty,
                NotificationDurations.Long);
            return;
        }

        ClearAtlas();
        _atlasDecoded = decoded;
        AtlasPath = path;
        AtlasBitmap = bitmap.Data;
        AddSliceCommand.NotifyCanExecuteChanged();
        ExportIniCommand.NotifyCanExecuteChanged();
        ExportSheetCommand.NotifyCanExecuteChanged();
    }

    private void LoadSlicesForAtlas()
    {
        if (AtlasBitmap is null)
        {
            return;
        }

        var matches = _registry.GetByTexture(AtlasFileName);
        if (matches.Count == 0)
        {
            return;
        }

        ReplaceSlices(matches);
    }

    private void ReplaceSlices(IEnumerable<MappedImageDefinition> definitions)
    {
        foreach (var slice in Slices)
        {
            slice.PropertyChanged -= OnSlicePropertyChanged;
            DisposeThumbnail(slice.Thumbnail);
        }

        Slices.Clear();
        foreach (var definition in definitions)
        {
            var slice = new TextureSliceViewModel(definition);
            slice.UpdateZoom(Zoom);
            AddSlice(slice);
        }

        SelectedSlice = Slices.FirstOrDefault();
    }

    private void AddSlice(TextureSliceViewModel slice)
    {
        slice.PropertyChanged += OnSlicePropertyChanged;
        slice.Thumbnail = CreateThumbnail(slice);
        Slices.Add(slice);
    }

    private void ClearAtlas()
    {
        foreach (var slice in Slices)
        {
            slice.PropertyChanged -= OnSlicePropertyChanged;
            DisposeThumbnail(slice.Thumbnail);
        }

        Slices.Clear();
        SelectedSlice = null;
        AtlasBitmap?.Dispose();
        AtlasBitmap = null;
        _atlasDecoded = null;
        AtlasPath = string.Empty;
    }

    private async Task ExportIniToPathAsync(string path)
    {
        IsBusy = true;
        try
        {
            var definitions = Slices.Select(slice => slice.ToDefinition()).ToList();
            string content = _parser.Serialize(definitions, $"Generated by GenHub {TextureEditorConstants.ToolName} from {AtlasFileName}");
            await File.WriteAllTextAsync(path, content).ConfigureAwait(true);
            _notificationService.ShowSuccess(
                Localize("TextureEditor.Notify.ExportComplete.Title", "Export complete"),
                Localize("TextureEditor.Notify.ExportComplete.Message", "Saved {0}.", Path.GetFileName(path)),
                NotificationDurations.Medium);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "INI export failed");
            _notificationService.ShowError(
                Localize("TextureEditor.Notify.ExportFailed.Title", "Export failed"),
                ex.Message,
                NotificationDurations.Long);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSlicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TextureSliceViewModel slice)
        {
            return;
        }

        if (e.PropertyName is nameof(TextureSliceViewModel.Left) or nameof(TextureSliceViewModel.Top) or nameof(TextureSliceViewModel.Right) or nameof(TextureSliceViewModel.Bottom))
        {
            DisposeThumbnail(slice.Thumbnail);
            slice.Thumbnail = CreateThumbnail(slice);
        }
    }

    private IImage? CreateThumbnail(TextureSliceViewModel slice)
    {
        if (AtlasBitmap is null)
        {
            return null;
        }

        return TextureBitmapService.Crop(AtlasBitmap, slice.Left, slice.Top, slice.Width, slice.Height);
    }

    private IImage? CreatePickerThumbnail(MappedImageDefinition definition)
    {
        if (AtlasBitmap is null || !string.Equals(definition.TextureFileName, AtlasFileName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return TextureBitmapService.Crop(AtlasBitmap, definition.Left, definition.Top, definition.Width, definition.Height);
    }
}
