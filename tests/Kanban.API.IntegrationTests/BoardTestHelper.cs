using Kanban.API.Data;
using Kanban.API.Models;

namespace Kanban.API.IntegrationTests;

public static class BoardTestHelper
{
    public static async Task<Board> SeedBoardAsync(ApplicationDbContext context, string ownerId, Board? board = null)
    {
        board ??= new Board { Name = "Test Board" };
        context.Boards.Add(board);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await SeedAsync(context, new BoardMember { BoardId = board.Id, MemberId = ownerId, Role = Role.Owner });

        return board;
    }

    public static Task<BoardMember> SeedBoardMemberAsync(ApplicationDbContext context, BoardMember member) =>
        SeedAsync(context, member);

    public static Task<Column> SeedColumnAsync(ApplicationDbContext context, Column column) =>
        SeedAsync(context, column);

    public static Task<Card> SeedCardAsync(ApplicationDbContext context, Card card) =>
        SeedAsync(context, card);

    private static async Task<TEntity> SeedAsync<TEntity>(ApplicationDbContext context, TEntity entity) where TEntity : class
    {
        context.Set<TEntity>().Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }
}
