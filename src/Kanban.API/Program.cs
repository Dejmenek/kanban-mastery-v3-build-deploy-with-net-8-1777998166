using Kanban.API.Authorization;
using Kanban.API.Data;
using Kanban.API.Endpoints;
using Kanban.API.Models;
using Kanban.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null
            );
        });
});
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddScoped<IAuthorizationHandler, IsBoardOwnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, IsBoardMemberHandler>();
builder.Services.AddScoped<IRetryExecutor, DbUpdateRetryExecutor>();
builder.Services.AddScoped<IBoardService, BoardService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IColumnService, ColumnService>();
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsBoardOwner", policy =>
        policy.Requirements.Add(new IsBoardOwnerRequirement()));
    options.AddPolicy("IsBoardMember", policy =>
        policy.Requirements.Add(new IsBoardMemberRequirement()));
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAllOrigins",
            policyBuilder => policyBuilder.AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin()
                .SetIsOriginAllowed(_ => true)
        );
    });
}
else
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ProdPolicy",
            policyBuilder => policyBuilder.WithOrigins("https://red-bay-0ce75de03.7.azurestaticapps.net")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10))
        );
    });
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    await DbSeeder.SeedAsync(app.Services);
}

app.UseHttpsRedirection();


if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAllOrigins");
}
else
{
    app.UseCors("ProdPolicy");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<ApplicationUser>();

app.MapBoardEndpoints();
app.MapUserEndpoints();

app.Run();

public partial class Program { }