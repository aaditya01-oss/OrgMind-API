using OrgMind.API.Services;
using OrgMind.API.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient<IChatService, ChatService>();

var app = builder.Build();

app.UseHttpsRedirection();

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

app.Run();