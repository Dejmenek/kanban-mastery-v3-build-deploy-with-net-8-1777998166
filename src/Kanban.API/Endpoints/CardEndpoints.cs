using Kanban.API.Common;
using Kanban.API.DTOs.Boards.Cards;
using Kanban.API.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Kanban.API.Endpoints;

public static class CardEndpoints
{
    public static void MapCardEndpoints(this IEndpointRouteBuilder boardsGroup)
    {
        var cards = boardsGroup.MapGroup("/{boardId:int}/cards")
            .RequireAuthorization();

        cards.MapPost("/", CreateCard)
            .Produces<CardResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        cards.MapDelete("/{cardId:int}", DeleteCard)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        cards.MapPut("/{cardId:int}", UpdateCard)
            .Produces<CardResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        cards.MapPut("/{cardId:int}/assign", AssignCard)
            .Produces<CardResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> AssignCard(
        int boardId, int cardId, AssignCardRequest request, ICardService cardService,
        ClaimsPrincipal user, IAuthorizationService authService, CancellationToken cancellationToken)
    {
        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardMember");
        if (!authResult.Succeeded)
        {
            return TypedResults.Forbid();
        }

        var result = await cardService.AssignCardToUserAsync(cardId, boardId, request, cancellationToken);
        if (result.IsFailure) return result.Error.ToTypedResult();

        return TypedResults.Ok(result.Value);
    }

    private static async Task<IResult> CreateCard(
        int boardId, CreateCardRequest request, ICardService cardService, ClaimsPrincipal user,
        IAuthorizationService authService, CancellationToken cancellationToken)
    {
        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardMember");
        if (!authResult.Succeeded)
        {
            return TypedResults.Forbid();
        }

        var result = await cardService.CreateAsync(boardId, request, cancellationToken);
        if (result.IsFailure) return result.Error.ToTypedResult();

        return TypedResults.Created($"/boards/{boardId}/cards/{result.Value.Id}", result.Value);
    }

    private static async Task<IResult> UpdateCard(
        int boardId, int cardId, UpdateCardRequest request, ICardService cardService, ClaimsPrincipal user,
        IAuthorizationService authService, CancellationToken cancellationToken)
    {
        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardMember");
        if (!authResult.Succeeded)
        {
            return TypedResults.Forbid();
        }

        var result = await cardService.UpdateAsync(boardId, cardId, request, cancellationToken);
        if (result.IsFailure) return result.Error.ToTypedResult();

        return TypedResults.Ok(result.Value);
    }

    private static async Task<IResult> DeleteCard(
        int boardId, int cardId, ICardService cardService, ClaimsPrincipal user,
        IAuthorizationService authService, CancellationToken cancellationToken)
    {
        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardMember");
        if (!authResult.Succeeded)
        {
            return TypedResults.Forbid();
        }

        var result = await cardService.DeleteAsync(boardId, cardId, cancellationToken);
        if (result.IsFailure) return result.Error.ToTypedResult();

        return TypedResults.NoContent();
    }
}
