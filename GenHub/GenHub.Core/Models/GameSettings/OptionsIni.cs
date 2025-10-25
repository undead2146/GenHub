namespace GenHub.Core.Models.GameSettings;

/// <summary>
/// Represents the structure of an Options.ini file for Command & Conquer Generals and Zero Hour.
/// </summary>
public class OptionsIni
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptionsIni"/> class with default values.
    /// </summary>
    public OptionsIni()
    {
        Audio = new AudioSettings();
        Video = new VideoSettings();
    }

    /// <summary>
    /// Gets or sets the audio settings.
    /// </summary>
    public AudioSettings Audio { get; set; }

    /// <summary>
    /// Gets or sets the video settings.
    /// </summary>
    public VideoSettings Video { get; set; }
}
