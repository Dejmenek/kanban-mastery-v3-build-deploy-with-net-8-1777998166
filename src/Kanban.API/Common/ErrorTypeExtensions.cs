using Microsoft.AspNetCore.Http.HttpResults;

namespace Kanban.API.Common;

public static class ErrorTypeExtensions
{
    public static IResult ToTypedResult(this Error error) =>
        error.Type switch
        {
            ErrorType.NotFound => TypedResults.NotFound(error.Description),
            ErrorType.Validation => TypedResults.BadRequest(error.Description),
            ErrorType.Conflict => TypedResults.Conflict(error.Description),
            _ => TypedResults.Problem(
                detail: error.Description,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unhandled error type")
        };
}
