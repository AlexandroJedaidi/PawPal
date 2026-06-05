using UnityEngine;

public enum PawPalPetTurnClipKind
{
    None,
    Left,
    Right,
    Left180,
    Right180
}

public static class PawPalPetTurnAnimationUtility
{
    public const string TurnLeftStateName = "TurnLeft";
    public const string TurnRightStateName = "TurnRight";
    public const string TurnLeft180StateName = "TurnLeft180";
    public const string TurnRight180StateName = "TurnRight180";

    public static PawPalPetTurnClipKind ResolveTurnClipKind(float signedAngle, float minimumTurnAngle, float halfTurnAngle)
    {
        float absAngle = Mathf.Abs(signedAngle);
        if (absAngle <= Mathf.Max(0f, minimumTurnAngle))
        {
            return PawPalPetTurnClipKind.None;
        }

        bool useHalfTurn = absAngle >= Mathf.Max(minimumTurnAngle, halfTurnAngle);
        if (signedAngle >= 0f)
        {
            return useHalfTurn ? PawPalPetTurnClipKind.Right180 : PawPalPetTurnClipKind.Right;
        }

        return useHalfTurn ? PawPalPetTurnClipKind.Left180 : PawPalPetTurnClipKind.Left;
    }

    public static string GetStateName(PawPalPetTurnClipKind kind)
    {
        switch (kind)
        {
            case PawPalPetTurnClipKind.Left:
                return TurnLeftStateName;
            case PawPalPetTurnClipKind.Right:
                return TurnRightStateName;
            case PawPalPetTurnClipKind.Left180:
                return TurnLeft180StateName;
            case PawPalPetTurnClipKind.Right180:
                return TurnRight180StateName;
            default:
                return string.Empty;
        }
    }

    public static string[] GetTurnClipSuffixes(PawPalPetAnimationEntry entry, PawPalPetTurnClipKind kind)
    {
        return GetTurnClipSuffixes(entry != null && entry.Species == IntroPetSpecies.Cat, kind);
    }

    public static string[] GetTurnClipSuffixes(bool isCat, PawPalPetTurnClipKind kind)
    {
        switch (kind)
        {
            case PawPalPetTurnClipKind.Left:
                return new[] { "Turn_L_IP", "Turn_L_RM" };
            case PawPalPetTurnClipKind.Right:
                return new[] { "Turn_R_IP", "Turn_R_RM" };
            case PawPalPetTurnClipKind.Left180:
                return isCat
                    ? new[] { "Turn180_L_IP", "Turn180_L_RM" }
                    : new[] { "Turn_L180_IP", "Turn_L180_RM" };
            case PawPalPetTurnClipKind.Right180:
                return isCat
                    ? new[] { "Turn180_R_IP", "Turn180_R_RM" }
                    : new[] { "Turn_R180_IP", "Turn_R180_RM" };
            default:
                return new string[0];
        }
    }
}
