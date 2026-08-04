using Kanban.API.Common;

namespace Kanban.API.Errors;

public static class CardErrors
{
    public static Error InvalidTitle => Error.Validation("InvalidTitle", "Card title cannot be null or empty.");

    public static Error NotFound(int cardId) => Error.NotFound("CardNotFound", $"Card with ID {cardId} was not found.");

    public static Error PositionConflict(int columnId) =>
        Error.Conflict("Card.PositionConflict", $"A card with the same position already exists in column '{columnId}'.");

    public static Error MoveConflict(int cardId) =>
        Error.Conflict("Card.MoveConflict", $"Card {cardId} was moved since you last saw it. Refresh and try again.");
}
