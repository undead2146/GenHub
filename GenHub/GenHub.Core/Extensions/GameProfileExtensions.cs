using GenHub.Core.Models.GameProfile;

namespace GenHub.Core.Extensions;

/// <summary>
/// Extension methods for GameProfile.
/// </summary>
public static class GameProfileExtensions
{
    /// <summary>
    /// Checks if a profile has any custom game settings defined.
    /// </summary>
    /// <param name="profile">The game profile.</param>
    /// <returns>True if the profile has custom settings, false otherwise.</returns>
    public static bool HasCustomSettings(this GameProfile profile)
    {
        return profile.VideoResolutionWidth.HasValue ||
               profile.VideoResolutionHeight.HasValue ||
               profile.VideoWindowed.HasValue ||
               profile.VideoTextureQuality.HasValue ||
               profile.EnableVideoShadows.HasValue ||
               profile.VideoParticleEffects.HasValue ||
               profile.VideoExtraAnimations.HasValue ||
               profile.VideoBuildingAnimations.HasValue ||
               profile.VideoGamma.HasValue ||
               profile.AudioSoundVolume.HasValue ||
               profile.AudioThreeDSoundVolume.HasValue ||
               profile.AudioSpeechVolume.HasValue ||
               profile.AudioMusicVolume.HasValue ||
               profile.AudioEnabled.HasValue ||
               profile.AudioNumSounds.HasValue;
    }
}
