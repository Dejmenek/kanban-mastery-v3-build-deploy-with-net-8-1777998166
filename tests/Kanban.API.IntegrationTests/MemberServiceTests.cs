using Kanban.API.Common;
using Kanban.API.DTOs.Boards;
using Kanban.API.Errors;
using Kanban.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.IntegrationTests;

public class MemberServiceTests(IntegrationTestWebAppFactory<Program> factory)
    : IntegrationTestBase(factory), IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    [Fact]
    public async Task AddMemberAsync_WithValidEmail_AddsMemberAndReturnsSuccess()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var memberEmail = "member@example.com";
        var newMember = await CreateUserAsync(memberEmail, "Test123!");

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.AddMemberAsync(board.Id, new AddBoardMemberRequest(null, memberEmail), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(newMember.Id, result.Value.MemberId);
        Assert.Equal(newMember.UserName, result.Value.UserName);
        Assert.Equal(newMember.Email, result.Value.Email);
        Assert.Equal(nameof(Role.Member), result.Value.Role);

        var membership = await UseDbContextAsync(context => context.BoardsMemberships
            .FirstOrDefaultAsync(bm => bm.BoardId == board.Id && bm.MemberId == newMember.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(membership);
        Assert.Equal(Role.Member, membership.Role);
    }

    [Fact]
    public async Task AddMemberAsync_WithoutUserIdOrEmail_ReturnsValidationFailure()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.AddMemberAsync(board.Id, new AddBoardMemberRequest(null, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(BoardErrors.MissingMemberIdentifier, result.Error);
    }

    [Fact]
    public async Task AddMemberAsync_WithNonExistentBoard_ReturnsNotFoundFailure()
    {
        // Arrange
        const int nonExistentBoardId = 999;

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.AddMemberAsync(nonExistentBoardId, new AddBoardMemberRequest(null, "someone@example.com"), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(BoardErrors.NotFound(nonExistentBoardId), result.Error);
    }

    [Fact]
    public async Task AddMemberAsync_WithNonExistentUser_ReturnsNotFoundFailure()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        const string missingEmail = "doesnotexist@example.com";

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.AddMemberAsync(board.Id, new AddBoardMemberRequest(null, missingEmail), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(BoardErrors.UserNotFound(missingEmail), result.Error);
    }

    [Fact]
    public async Task AddMemberAsync_WhenUserIsAlreadyAMember_ReturnsConflictFailure()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var memberEmail = "member@example.com";
        var member = await CreateUserAsync(memberEmail, "Test123!");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.AddMemberAsync(board.Id, new AddBoardMemberRequest(null, memberEmail), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(BoardErrors.AlreadyMember, result.Error);
    }

    [Fact]
    public async Task GetAllAsync_WithNonExistentBoard_ReturnsNotFoundFailure()
    {
        // Arrange
        const int nonExistentBoardId = 999;

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.GetAllAsync(nonExistentBoardId, null, 20, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(BoardErrors.NotFound(nonExistentBoardId), result.Error);
    }

    [Fact]
    public async Task GetAllAsync_WhenAllMembersFitInPageSize_ReturnsSinglePageWithNullCursor()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var member = await CreateUserAsync("member@example.com", "Test123!");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.GetAllAsync(board.Id, null, 20, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Null(result.Value.NextCursor);
        Assert.Contains(result.Value.Items, m => m.MemberId == owner.Id);
        Assert.Contains(result.Value.Items, m => m.MemberId == member.Id);
    }

    [Fact]
    public async Task GetAllAsync_WithMoreMembersThanPageSize_ReturnsNextCursorAndRemainingOnNextPage()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        for (var i = 0; i < 4; i++)
        {
            var member = await CreateUserAsync($"member{i}@example.com", "Test123!");
            await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
                context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));
        }

        // Act
        var firstPage = await UseMemberServiceAsync(service =>
            service.GetAllAsync(board.Id, null, 2, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(firstPage.IsSuccess);
        Assert.Equal(2, firstPage.Value.Items.Count);
        Assert.NotNull(firstPage.Value.NextCursor);

        // Act - walk remaining pages
        var collectedIds = new List<string>(firstPage.Value.Items.Select(m => m.MemberId));
        var cursor = firstPage.Value.NextCursor;
        while (cursor is not null)
        {
            var page = await UseMemberServiceAsync(service =>
                service.GetAllAsync(board.Id, cursor, 2, TestContext.Current.CancellationToken));
            Assert.True(page.IsSuccess);
            collectedIds.AddRange(page.Value.Items.Select(m => m.MemberId));
            cursor = page.Value.NextCursor;
        }

        // Assert
        Assert.Equal(5, collectedIds.Count);
        Assert.Equal(collectedIds.Distinct().Count(), collectedIds.Count);
    }

    [Fact]
    public async Task GetAllAsync_WithInvalidCursor_ReturnsValidationFailure()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.GetAllAsync(board.Id, "not-valid-base64!!!", 20, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(MemberErrors.InvalidCursor, result.Error);
    }

    [Fact]
    public async Task GetAllAsync_WithPageSizeAboveMax_ClampsWithoutError()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.GetAllAsync(board.Id, null, 1000, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Null(result.Value.NextCursor);
    }

    [Fact]
    public async Task GetAllAsync_WithPageSizeBelowMin_ClampsToOne()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var member = await CreateUserAsync("member@example.com", "Test123!");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.GetAllAsync(board.Id, null, 0, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.NotNull(result.Value.NextCursor);
    }
}
