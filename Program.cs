using OrgMind.API.Services;
using OrgMind.API.Models;
using OrgMind.API.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient<IChatService, ChatService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddDbContext<OrgMindDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("OrgMind.API")
    ));

builder.Services.AddCors(options =>
{
    options.AddPolicy("OrgMindPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("OrgMindPolicy");

app.MapGet("/", () => "OrgMind API is running!");

app.MapGet("/health", () => new {
    status = "healthy",
    service = "OrgMind API",
    version = "1.0.0",
    timestamp = DateTime.UtcNow
});

app.MapPost("/chat", async (ChatRequest request, IChatService chatService) =>
{
    var response = await chatService.ChatAsync(
        request.UserMessage,
        request.UserId,
        request.OrgId,
        request.OrgName
    );

    return Results.Ok(new ChatResponse
    {
        Response = response,
        UserId = request.UserId,
        OrgId = request.OrgId
    });
});

app.MapPost("/auth/register", async (RegisterRequest request, IAuthService authService) =>
{
    var token = await authService.RegisterAsync(request.Email, request.Password, request.OrgId);
    return Results.Ok(new AuthResponse 
    { 
        Token = token,
        Email = request.Email,
        OrgId = request.OrgId,
        Role = "member"
    });
});

app.MapPost("/auth/login", async (LoginRequest request, IAuthService authService) =>
{
    var token = await authService.LoginAsync(request.Email, request.Password);
    if (token == null) return Results.Unauthorized();
    return Results.Ok(new { token });
});

app.Run();