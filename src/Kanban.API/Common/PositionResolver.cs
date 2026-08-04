namespace Kanban.API.Common;

public static class PositionResolver
{
    public static int Resolve(int? requestedPosition, int existingCount)
    {
        var hasValidPosition = requestedPosition is int position
            && position >= 1
            && position <= existingCount + 1;

        return hasValidPosition ? requestedPosition!.Value : existingCount + 1;
    }
}
