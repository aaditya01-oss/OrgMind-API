var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "OrgMind API is running!");

app.MapGet("/health", () => new {
    status = "healthy",
    service = "OrgMind API",
    version = "1.0.0",
    timestamp = DateTime.UtcNow
});

app.Run();