using Kanban.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.Data;

public static class DbSeeder
{
    public const string SeedUserPassword = "Passw0rd!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await context.Database.MigrateAsync();

        if (await context.Boards.AnyAsync())
        {
            return;
        }

        var alice = await GetOrCreateUserAsync(userManager, "alice@example.com");
        var bob = await GetOrCreateUserAsync(userManager, "bob@example.com");
        var carol = await GetOrCreateUserAsync(userManager, "carol@example.com");

        var productLaunch = new Board
        {
            Name = "Product Launch",
            Description = "Coordinating the launch of the new mobile app.",
            Members =
            [
                new BoardMember { Member = alice, Role = Role.Owner },
                new BoardMember { Member = bob, Role = Role.Member },
            ],
            Columns =
            [
                new Column
                {
                    Title = "Backlog",
                    Position = 1,
                    Cards =
                    [
                        new Card { Title = "Draft launch announcement", Description = "Write blog post and social copy.", Position = 1 },
                        new Card { Title = "Finalize pricing tiers", Position = 2, AssignedToUser = bob },
                    ]
                },
                new Column
                {
                    Title = "In Progress",
                    Position = 2,
                    Cards =
                    [
                        new Card { Title = "Build onboarding flow", Description = "Implement the first-run tutorial.", Position = 1, AssignedToUser = alice },
                    ]
                },
                new Column
                {
                    Title = "Review",
                    Position = 3,
                    Cards =
                    [
                        new Card { Title = "QA sign-off on payment flow", Position = 1, AssignedToUser = bob },
                    ]
                },
                new Column
                {
                    Title = "Done",
                    Position = 4,
                    Cards =
                    [
                        new Card { Title = "Set up analytics dashboard", Position = 1, AssignedToUser = alice },
                    ]
                },
            ]
        };

        var websiteRedesign = new Board
        {
            Name = "Website Redesign",
            Description = "Refreshing the marketing site ahead of Q3.",
            Members =
            [
                new BoardMember { Member = bob, Role = Role.Owner },
                new BoardMember { Member = alice, Role = Role.Member },
                new BoardMember { Member = carol, Role = Role.Member },
            ],
            Columns =
            [
                new Column
                {
                    Title = "To Do",
                    Position = 1,
                    Cards =
                    [
                        new Card { Title = "Audit current site accessibility", Position = 1 },
                        new Card { Title = "Collect stakeholder feedback", Position = 2, AssignedToUser = carol },
                    ]
                },
                new Column
                {
                    Title = "In Progress",
                    Position = 2,
                    Cards =
                    [
                        new Card { Title = "Design new homepage hero", Description = "Explore three layout directions.", Position = 1, AssignedToUser = bob },
                        new Card { Title = "Migrate blog to new CMS", Position = 2, AssignedToUser = carol },
                    ]
                },
                new Column
                {
                    Title = "Done",
                    Position = 3,
                    Cards =
                    [
                        new Card { Title = "Set up staging environment", Position = 1, AssignedToUser = bob },
                    ]
                },
            ]
        };

        var personalTasks = new Board
        {
            Name = "Personal Tasks",
            Description = "Carol's personal to-do list.",
            Members =
            [
                new BoardMember { Member = carol, Role = Role.Owner },
            ],
            Columns =
            [
                new Column
                {
                    Title = "To Do",
                    Position = 1,
                    Cards =
                    [
                        new Card { Title = "Book dentist appointment", Position = 1 },
                        new Card { Title = "Renew driver's license", Position = 2 },
                    ]
                },
                new Column
                {
                    Title = "Doing",
                    Position = 2,
                    Cards =
                    [
                        new Card { Title = "Plan weekend trip", Position = 1 },
                    ]
                },
                new Column
                {
                    Title = "Done",
                    Position = 3,
                    Cards =
                    [
                        new Card { Title = "Pay electricity bill", Position = 1 },
                    ]
                },
            ]
        };

        context.Boards.AddRange(productLaunch, websiteRedesign, personalTasks);

        await context.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> GetOrCreateUserAsync(UserManager<ApplicationUser> userManager, string email)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, SeedUserPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }
}
