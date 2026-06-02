using UnityEngine;

public static class PawPalAudioResources
{
    public const string PawFriendsHome = "Audio/pawfriends_home";
    public const string NightAmbience = "Audio/night-ambience";
    public const string AmbientCar = "Audio/ambient_car";
    public const string AmbientBirds = "Audio/birds_chirping";
    public const string BarkLight = "Audio/bark_light";
    public const string BarkDark = "Audio/bark_dark";
    public const string DogWalk = "Audio/dog_walk";
    public const string BallBounce = "Audio/ball_bounce";
    public const string ToyPickup = "Audio/toy_pickup";
    public const string Sniffing = "Audio/sniffing";
    public const string ScratchAndShaking = "Audio/scratch-and-shaking";
    public const string ShakingX3 = "Audio/shakingx3";
    public const string DogPanting = "Audio/dog_panting";
    public const string DogAnnoyed = "Audio/dog_annoyed";
    public const string DogWhining = "Audio/dog_whining";
    public const string DogGnarl = "Audio/dog_gnarl";
    public const string DogEating = "Audio/dog_eating";
    public const string DogDrinking = "Audio/dog_drinking";
    public const string CatLick = "Audio/cat_lick";
    public const string CatMeow = "Audio/cat_meow";
    public const string CatPurr = "Audio/cat_purr";
    public const string CatScratch = "Audio/cat_scratch";

    public static AudioClip LoadClip(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }

        return Resources.Load<AudioClip>(resourcePath);
    }

    public static void AssignIfMissing(ref AudioClip clip, string resourcePath)
    {
        if (clip != null)
        {
            return;
        }

        clip = LoadClip(resourcePath);
    }
}
