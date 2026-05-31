public static class PawPalDogInteractionTuning
{
    public static float GetInactivityTimeoutSeconds(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Loyal:
                return 24f;
            case PawPalDogPersonality.Gentle:
                return 22f;
            case PawPalDogPersonality.Playful:
                return 20f;
            case PawPalDogPersonality.Social:
                return 18f;
            case PawPalDogPersonality.Relaxed:
                return 17f;
            case PawPalDogPersonality.Clever:
                return 15f;
            case PawPalDogPersonality.Curious:
                return 14f;
            case PawPalDogPersonality.Energetic:
                return 11f;
            case PawPalDogPersonality.Mischievous:
                return 9f;
            default:
                return 15f;
        }
    }
}
