using GenHub.Core.Constants;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GameProfile;
using GenHub.Core.Models.GameSettings;
using Microsoft.Extensions.Logging;

namespace GenHub.Core.Helpers;

/// <summary>
/// Helper class for mapping game profile settings to IniOptions.
/// </summary>
public static class GameSettingsMapper
{
    /// <summary>
    /// Applies profile settings to IniOptions with validation.
    /// </summary>
    /// <param name="profile">The game profile containing the settings.</param>
    /// <param name="options">The IniOptions object to apply settings to.</param>
    /// <param name="logger">Optional logger for validation warnings.</param>
    public static void ApplyToOptions(GameProfile profile, IniOptions options, ILogger? logger = null)
    {
        // Video settings with validation
        if (profile.VideoResolutionWidth.HasValue)
        {
            if (profile.VideoResolutionWidth.Value >= GameSettingsConstants.Resolution.MinWidth &&
                profile.VideoResolutionWidth.Value <= GameSettingsConstants.Resolution.MaxWidth)
            {
                options.Video.ResolutionWidth = profile.VideoResolutionWidth.Value;
            }
            else
            {
                logger?.LogWarning(
                    "Invalid VideoResolutionWidth {Width} for profile {ProfileId}, must be {Min}-{Max}",
                    profile.VideoResolutionWidth.Value,
                    profile.Id,
                    GameSettingsConstants.Resolution.MinWidth,
                    GameSettingsConstants.Resolution.MaxWidth);
            }
        }

        if (profile.VideoResolutionHeight.HasValue)
        {
            if (profile.VideoResolutionHeight.Value >= GameSettingsConstants.Resolution.MinHeight &&
                profile.VideoResolutionHeight.Value <= GameSettingsConstants.Resolution.MaxHeight)
            {
                options.Video.ResolutionHeight = profile.VideoResolutionHeight.Value;
            }
            else
            {
                logger?.LogWarning(
                    "Invalid VideoResolutionHeight {Height} for profile {ProfileId}, must be {Min}-{Max}",
                    profile.VideoResolutionHeight.Value,
                    profile.Id,
                    GameSettingsConstants.Resolution.MinHeight,
                    GameSettingsConstants.Resolution.MaxHeight);
            }
        }

        if (profile.VideoWindowed.HasValue)
        {
            options.Video.Windowed = profile.VideoWindowed.Value;
        }

        if (profile.VideoTextureQuality.HasValue)
        {
            if (profile.VideoTextureQuality.Value >= TextureQuality.Low &&
                profile.VideoTextureQuality.Value <= TextureQuality.High)
            {
                options.Video.TextureReduction = 2 - (int)profile.VideoTextureQuality.Value;
            }
            else
            {
                logger?.LogWarning(
                    "Invalid VideoTextureQuality {Quality} for profile {ProfileId}, must be 0-2",
                    profile.VideoTextureQuality.Value,
                    profile.Id);
            }
        }

        if (profile.EnableVideoShadows.HasValue)
        {
            options.Video.UseShadowVolumes = profile.EnableVideoShadows.Value;
            options.Video.UseShadowDecals = profile.EnableVideoShadows.Value;
        }

        if (profile.VideoExtraAnimations.HasValue)
        {
            options.Video.ExtraAnimations = profile.VideoExtraAnimations.Value;
        }

        if (profile.VideoGamma.HasValue)
        {
            if (profile.VideoGamma.Value >= GameSettingsConstants.Gamma.Min &&
                profile.VideoGamma.Value <= GameSettingsConstants.Gamma.Max)
            {
                options.Video.Gamma = profile.VideoGamma.Value;
            }
            else
            {
                logger?.LogWarning(
                    "Invalid VideoGamma {Gamma} for profile {ProfileId}, must be {Min}-{Max}",
                    profile.VideoGamma.Value,
                    profile.Id,
                    GameSettingsConstants.Gamma.Min,
                    GameSettingsConstants.Gamma.Max);
            }
        }

        // Audio settings with validation
        if (profile.AudioSoundVolume.HasValue)
        {
            if (profile.AudioSoundVolume.Value >= GameSettingsConstants.Volume.Min &&
                profile.AudioSoundVolume.Value <= GameSettingsConstants.Volume.Max)
            {
                options.Audio.SFXVolume = profile.AudioSoundVolume.Value;
            }
            else
            {
                logger?.LogWarning(
                    "Invalid AudioSoundVolume {Volume} for profile {ProfileId}, must be {Min}-{Max}",
                    profile.AudioSoundVolume.Value,
                    profile.Id,
                    GameSettingsConstants.Volume.Min,
                    GameSettingsConstants.Volume.Max);
            }
        }

        if (profile.AudioThreeDSoundVolume.HasValue)
        {
            if (profile.AudioThreeDSoundVolume.Value >= GameSettingsConstants.Volume.Min &&
                profile.AudioThreeDSoundVolume.Value <= GameSettingsConstants.Volume.Max)
            {
                options.Audio.SFX3DVolume = profile.AudioThreeDSoundVolume.Value;
            }
            else
            {
                logger?.LogWarning(
                    "Invalid AudioThreeDSoundVolume {Volume} for profile {ProfileId}, must be {Min}-{Max}",
                    profile.AudioThreeDSoundVolume.Value,
                    profile.Id,
                    GameSettingsConstants.Volume.Min,
                    GameSettingsConstants.Volume.Max);
            }
        }

        if (profile.AudioSpeechVolume.HasValue)
        {
            if (profile.AudioSpeechVolume.Value >= GameSettingsConstants.Volume.Min &&
                profile.AudioSpeechVolume.Value <= GameSettingsConstants.Volume.Max)
            {
                options.Audio.VoiceVolume = profile.AudioSpeechVolume.Value;
            }
            else
            {
                logger?.LogWarning(
                    "Invalid AudioSpeechVolume {Volume} for profile {ProfileId}, must be {Min}-{Max}",
                    profile.AudioSpeechVolume.Value,
                    profile.Id,
                    GameSettingsConstants.Volume.Min,
                    GameSettingsConstants.Volume.Max);
            }
        }

        if (profile.AudioMusicVolume.HasValue)
        {
            if (profile.AudioMusicVolume.Value >= GameSettingsConstants.Volume.Min &&
                profile.AudioMusicVolume.Value <= GameSettingsConstants.Volume.Max)
            {
                options.Audio.MusicVolume = profile.AudioMusicVolume.Value;
            }
            else
            {
                logger?.LogWarning(
                    "Invalid AudioMusicVolume {Volume} for profile {ProfileId}, must be {Min}-{Max}",
                    profile.AudioMusicVolume.Value,
                    profile.Id,
                    GameSettingsConstants.Volume.Min,
                    GameSettingsConstants.Volume.Max);
            }
        }

        if (profile.AudioEnabled.HasValue)
        {
            options.Audio.AudioEnabled = profile.AudioEnabled.Value;
        }

        if (profile.AudioNumSounds.HasValue)
        {
            if (profile.AudioNumSounds.Value >= GameSettingsConstants.Audio.MinNumSounds &&
                profile.AudioNumSounds.Value <= GameSettingsConstants.Audio.MaxNumSounds)
            {
                options.Audio.NumSounds = profile.AudioNumSounds.Value;
            }
            else
            {
                logger?.LogWarning(
                    "Invalid AudioNumSounds {NumSounds} for profile {ProfileId}, must be {Min}-{Max}",
                    profile.AudioNumSounds.Value,
                    profile.Id,
                    GameSettingsConstants.Audio.MinNumSounds,
                    GameSettingsConstants.Audio.MaxNumSounds);
            }
        }
    }
}
