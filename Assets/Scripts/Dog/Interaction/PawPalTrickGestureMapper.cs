public static class PawPalTrickGestureMapper
{
    public static PawPalTrickId MapGestureToTrick(PawPalGestureSnapshot snapshot, PawPalTrickId selectedTrick, bool dogIsSitting)
    {
        if (snapshot == null)
        {
            return selectedTrick;
        }

        switch (snapshot.Type)
        {
            case PawPalGestureType.SwipeDown:
                return selectedTrick == PawPalTrickId.Lie || dogIsSitting ? PawPalTrickId.Lie : PawPalTrickId.Sit;
            case PawPalGestureType.DragFromBodyPart:
                return PawPalTrickId.Shake;
            case PawPalGestureType.Tap:
                return snapshot.StartZone == PawPalDogBodyZone.AirAboveDog ? PawPalTrickId.Jump : selectedTrick;
            case PawPalGestureType.SwipeUp:
                return PawPalTrickId.Jump;
            case PawPalGestureType.CircularSwipe:
            case PawPalGestureType.HorizontalSwipe:
                return PawPalTrickId.Spin;
            default:
                return snapshot.CandidateTrick;
        }
    }
}
