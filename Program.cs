using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrgMind.API.Data;
using OrgMind.API.Models;
using OrgMind.API.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = builder.Configuration["JwtSecret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new InvalidOperationException("JwtSecret must be set and at least 32 characters long.");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient<IChatService, ChatService>();
builder.Services.AddHttpClient<IDocumentService, DocumentService>();
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 25 * 1024 * 1024);
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IAuditService, AuditService>();

builder.Services.AddDbContext<OrgMindDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("OrgMind.API")
    ));


builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep claim names exactly as issued: orgId, email, role
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "email",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("OrgMindPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login and registration: limited per client IP address
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Chat and uploads: limited per signed-in user
    options.AddPolicy("user", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst("email")?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("OrgMindPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/", () => "OrgMind API is running!");

app.MapGet("/health", () => new {
    status = "healthy",
    service = "OrgMind API",
    version = "1.0.0",
    timestamp = DateTime.UtcNow
});

app.MapPost("/chat", async (ChatRequest request, ClaimsPrincipal user, IChatService chatService) =>
{
    // Identity comes from the verified token, never from the request body.
    var orgId = user.FindFirst("orgId")?.Value;
    var email = user.FindFirst("email")?.Value;

    if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(email))
        return Results.Unauthorized();

        var result = await chatService.ChatAsync(request.UserMessage, email, orgId, request.OrgName);

    return Results.Ok(new
    {
        response = result.Response,
        userId = email,
        orgId,
        sources = result.Sources
    });
}).RequireAuthorization().RequireRateLimiting("user");

app.MapPost("/auth/register", async (RegisterRequest request, IAuthService authService, IAuditService audit, HttpContext http) =>
{
    var ip = http.Connection.RemoteIpAddress?.ToString();
        var emailInput = request.Email ?? "";
    var email = emailInput.Trim().ToLowerInvariant();
    var result = await authService.RegisterWithInviteAsync(emailInput, request.Password ?? "", request.InviteCode ?? "");

    if (!result.Success)
    {
        await audit.LogAsync("REGISTER_FAILED", email, null, new { reason = result.Error, method = "invite", ip }, "WARNING");
        return Results.BadRequest(new { error = result.Error });
    }

    await audit.LogAsync("USER_JOINED", email, result.OrgId, new { role = result.Role, ip });
    return Results.Ok(new AuthResponse { Token = result.Token!, Email = email, OrgId = result.OrgId!, Role = result.Role! });
}).RequireRateLimiting("auth");

app.MapPost("/auth/register-org", async (CreateOrgRequest request, IAuthService authService, IAuditService audit, HttpContext http) =>
{
    var ip = http.Connection.RemoteIpAddress?.ToString();
    var emailInput = request.Email ?? "";
    var email = emailInput.Trim().ToLowerInvariant();
    var result = await authService.CreateOrganizationAsync(emailInput, request.Password ?? "", request.OrgName ?? "");

    if (!result.Success)
    {
        await audit.LogAsync("REGISTER_FAILED", email, null, new { reason = result.Error, method = "create_org", ip }, "WARNING");
        return Results.BadRequest(new { error = result.Error });
    }

    await audit.LogAsync("ORG_CREATED", email, result.OrgId, new { orgName = request.OrgName, ip });
    return Results.Ok(new AuthResponse { Token = result.Token!, Email = email, OrgId = result.OrgId!, Role = result.Role! });
}).RequireRateLimiting("auth");

app.MapPost("/orgs/invites", async (CreateInviteRequest request, ClaimsPrincipal user, IAuthService authService, IAuditService audit) =>
{
    var orgId = user.FindFirst("orgId")?.Value;
    var email = user.FindFirst("email")?.Value;
    if (orgId is null || email is null) return Results.Unauthorized();

    var role = request.Role == "admin" ? "admin" : "member";
    var (code, expiresAt) = await authService.CreateInviteAsync(orgId, role, email);

    // Log that an invite was made, but never the code itself: it is a secret.
    await audit.LogAsync("INVITE_CREATED", email, orgId, new { role, expiresAt });
    return Results.Ok(new { code, expiresAt });
}).RequireAuthorization(p => p.RequireRole("admin")).RequireRateLimiting("user");

app.MapPost("/auth/login", async (LoginRequest request, IAuthService authService, IAuditService audit, HttpContext http) =>
{
    var ip = http.Connection.RemoteIpAddress?.ToString();
        var emailInput = request.Email ?? "";
    var email = emailInput.Trim().ToLowerInvariant();
    var token = await authService.LoginAsync(emailInput, request.Password ?? "");

    if (token == null)
    {
        await audit.LogAsync("LOGIN_FAILED", email, null, new { ip }, "WARNING");
        return Results.Unauthorized();
    }

    var orgId = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.FirstOrDefault(c => c.Type == "orgId")?.Value;
    await audit.LogAsync("LOGIN_SUCCESS", email, orgId, new { ip });
    return Results.Ok(new { token });
}).RequireRateLimiting("auth");
const long MaxUploadBytes = 20 * 1024 * 1024;

async Task<IResult> HandleUpload(HttpRequest http, ClaimsPrincipal user, IDocumentService docs, string route, string[] allowed)
{
        // Identity comes from the verified token, never from the browser.
    var orgId = user.FindFirst("orgId")?.Value;
    var email = user.FindFirst("email")?.Value;
    if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(email)) return Results.Unauthorized();

    if (!http.HasFormContentType)
        return Results.BadRequest(new { error = "Expected multipart/form-data." });

    var form = await http.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0) return Results.BadRequest(new { error = "No file uploaded." });
    if (file.Length > MaxUploadBytes) return Results.StatusCode(413);

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowed.Contains(ext))
        return Results.BadRequest(new { error = $"Allowed types: {string.Join(", ", allowed)}" });

    var docName = Path.GetFileName(file.FileName);
    var (status, body) = await docs.ForwardAsync(route, file, orgId, email, docName);

        // 422 = the file itself is the problem (e.g. a scan with no text). That message was
    // written to be shown to users, so we pass it on.
    if (status == 422)
    {
        var message = "This file could not be processed.";
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String)
                message = d.GetString() ?? message;
        }
        catch (System.Text.Json.JsonException) { }
        return Results.Json(new { error = message }, statusCode: 422);
    }

    if (status < 200 || status >= 300)
        return Results.Problem("The document service could not process this file.", statusCode: 502);

    return Results.Content(body, "application/json");
}

app.MapPost("/documents/train", (HttpRequest http, ClaimsPrincipal user, IDocumentService docs) =>
    HandleUpload(http, user, docs, "/documents/train", new[] { ".pdf", ".docx", ".txt" }))
    .RequireAuthorization().RequireRateLimiting("user");

app.MapPost("/documents/ocr", (HttpRequest http, ClaimsPrincipal user, IDocumentService docs) =>
    HandleUpload(http, user, docs, "/documents/ocr", new[] { ".pdf", ".png", ".jpg", ".jpeg" }))
    .RequireAuthorization().RequireRateLimiting("user");

app.Run();