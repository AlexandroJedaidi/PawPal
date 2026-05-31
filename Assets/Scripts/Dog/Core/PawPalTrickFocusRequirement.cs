public static class PawPalTrickFocusRequirement
{
    public static int GetRequiredFocus(PawPalTrickDefinition definition)
    {
        if (definition == null)
        {
            return 0;
        }

        if (definition.RequiredFocus >= 0)
        {
            return definition.RequiredFocus;
        }

        int difficulty = definition.Difficulty;
        if (difficulty <= 1)
        {
            return 0;
        }

        if (difficulty == 2)
        {
            return 3;
        }

        if (difficulty == 3)
        {
            return 6;
        }

        return 8;
    }

    public static bool MeetsFocusRequirement(PawPalDogState dog, PawPalTrickDefinition definition)
    {
        return dog != null && dog.Focus >= GetRequiredFocus(definition);
    }
}
