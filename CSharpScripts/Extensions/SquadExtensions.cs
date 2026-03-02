public static class SquadExtensions
{
    public static bool IsTransport(this Squad? squad)
    {
        return squad?.SquadType?.Contains("Transport") == true;
    }

    public static bool IsEmbarked(this Squad? squad)
    {
        return squad?.TransportedBy != null;
    }

    public static bool IsEmbarkEligiblePassenger(this Squad? squad)
    {
        if (squad == null || squad.TransportedBy != null)
        {
            return false;
        }

        return squad.SquadType?.Contains("Infantry") == true || squad.SquadType?.Contains("Character") == true;
    }
}
