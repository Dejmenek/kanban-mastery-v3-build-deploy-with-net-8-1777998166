using Kanban.API.Common;

namespace Kanban.API.Errors;

public static class CardErrors
{
    public static Error InvalidTitle => Error.Validation("InvalidTitle", "Card title cannot be null or empty.");

    public static Error NotFound(int cardId) => Error.NotFound("CardNotFound", $"Card with ID {cardId} was not found.");
}
