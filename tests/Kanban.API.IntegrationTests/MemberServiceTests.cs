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

    [Fact]
    public async Task SearchAsync_WithMatchingUserName_ReturnsMatchingMembers()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!", "alice-owner");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var member = await CreateUserAsync("bob@example.com", "Test123!", "alice-member");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        var other = await CreateUserAsync("carol@example.com", "Test123!", "carol-member");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = other.Id, Role = Role.Member }));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.SearchAsync(board.Id, "alice", 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, m => m.MemberId == owner.Id);
        Assert.Contains(result.Value, m => m.MemberId == member.Id);
        Assert.DoesNotContain(result.Value, m => m.MemberId == other.Id);
    }

    [Fact]
    public async Task SearchAsync_WithMatchingEmail_ReturnsMatchingMembers()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var member = await CreateUserAsync("findme@example.com", "Test123!", "randomname");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.SearchAsync(board.Id, "findme", 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Value);
        Assert.Equal(member.Id, match.MemberId);
    }

    [Fact]
    public async Task SearchAsync_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.SearchAsync(board.Id, "nonexistentquery", 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task SearchAsync_WithNonExistentBoard_ReturnsNotFoundFailure()
    {
        // Arrange
        const int nonExistentBoardId = 999;

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.SearchAsync(nonExistentBoardId, "query", 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(BoardErrors.NotFound(nonExistentBoardId), result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public async Task SearchAsync_WithInvalidQuery_ReturnsValidationFailure(string? query)
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.SearchAsync(board.Id, query, 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(MemberErrors.InvalidSearchQuery, result.Error);
    }

    [Fact]
    public async Task SearchAsync_RespectsLimit()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!", "search-user-owner");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        for (var i = 0; i < 5; i++)
        {
            var member = await CreateUserAsync($"search-user-{i}@example.com", "Test123!", $"search-user-{i}");
            await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
                context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));
        }

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.SearchAsync(board.Id, "search-user", 3, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task SearchAsync_WithLimitAboveMax_ClampsToMax()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!", "clamp-owner");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        for (var i = 0; i < 12; i++)
        {
            var member = await CreateUserAsync($"clamp-{i}@example.com", "Test123!", $"clamp-{i}");
            await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
                context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));
        }

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.SearchAsync(board.Id, "clamp", 1000, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.Count);
    }

    [Fact]
    public async Task SearchAsync_IsCaseInsensitive()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var member = await CreateUserAsync("member@example.com", "Test123!", "CaseSensitiveName");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.SearchAsync(board.Id, "casesensitive", 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value, m => m.MemberId == member.Id);
    }

    [Fact]
    public async Task SearchAsync_OnlyReturnsMembersOfGivenBoard()
    {
        // Arrange
        var owner1 = await CreateUserAsync("owner1@example.com", "Test123!");
        var board1 = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner1.Id));

        var owner2 = await CreateUserAsync("owner2@example.com", "Test123!", "shared-name-owner2");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner2.Id));

        var memberOnBoard1 = await CreateUserAsync("member1@example.com", "Test123!", "shared-name-member1");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board1.Id, MemberId = memberOnBoard1.Id, Role = Role.Member }));

        // Act
        var result = await UseMemberServiceAsync(service =>
            service.SearchAsync(board1.Id, "shared-name", 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Value);
        Assert.Equal(memberOnBoard1.Id, match.MemberId);
    }
}
