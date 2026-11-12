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

    // Anonymous website visitors: tight limit per IP, because every question costs AI quota
    options.AddPolicy("public", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
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

    var role = user.FindFirst("role")?.Value ?? "member";
    var department = user.FindFirst("department")?.Value;
    if (string.IsNullOrWhiteSpace(department)) department = null;

    var result = await chatService.ChatAsync(request.UserMessage, email, orgId, request.OrgName, role, department);

    return Results.Ok(new
    {
        response = result.Response,
        userId = email,
        orgId,
        sources = result.Sources
    });
}).RequireAuthorization().RequireRateLimiting("user");


app.MapPost("/public/chat", async (PublicChatRequest request, IChatService chatService) =>
{
    var message = (request.Message ?? "").Trim();
    var orgId = (request.OrgId ?? "").Trim();
    if (message.Length == 0 || orgId.Length == 0)
        return Results.BadRequest(new { error = "A question and an organization are required." });
    if (message.Length > 500)
        return Results.BadRequest(new { error = "Please keep your question under 500 characters." });

    // The role is fixed here on the server. A visitor can never claim a higher one.
    var result = await chatService.ChatAsync(message, "visitor", orgId, "", "visitor", null);
    return Results.Ok(new { response = result.Response, sources = result.Sources });
}).AllowAnonymous().RequireRateLimiting("public");

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

app.MapPost("/auth/register-customer", async (RegisterCustomerRequest request, IAuthService authService, IAuditService audit, HttpContext http) =>
{
    var ip = http.Connection.RemoteIpAddress?.ToString();
    var emailInput = request.Email ?? "";
    var email = emailInput.Trim().ToLowerInvariant();
    var result = await authService.RegisterCustomerAsync(emailInput, request.Password ?? "", request.OrgId ?? "");

    if (!result.Success)
    {
        await audit.LogAsync("REGISTER_FAILED", email, null, new { reason = result.Error, method = "customer", ip }, "WARNING");
        return Results.BadRequest(new { error = result.Error });
    }

    await audit.LogAsync("CUSTOMER_JOINED", email, result.OrgId, new { role = result.Role, ip });
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
    // Departments are stored trimmed and lowercase so "HR" and "hr " are the same department.
    var department = request.Department?.Trim().ToLowerInvariant();
    if (string.IsNullOrEmpty(department)) department = null;
    if (department is not null && department.Length > 100)
        return Results.BadRequest(new { error = "Department name is too long." });
    var (code, expiresAt) = await authService.CreateInviteAsync(orgId, role, email, department);

    // Log that an invite was made, but never the code itself: it is a secret.
    await audit.LogAsync("INVITE_CREATED", email, orgId, new { role, department, expiresAt });
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

// Who sees what (role and department come from the signed token, never from the browser):
//   admin                      -> every ticket in the organization
//   employee with a department -> own tickets + tickets routed to their department
//   everyone else              -> own tickets only
app.MapGet("/tickets", async (ClaimsPrincipal user, OrgMindDbContext db) =>
{
    var orgId = user.FindFirst("orgId")?.Value;
    var email = user.FindFirst("email")?.Value;
    if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(email)) return Results.Unauthorized();

    var role = user.FindFirst("role")?.Value;
    var department = user.FindFirst("department")?.Value;
    if (string.IsNullOrWhiteSpace(department)) department = null;

    var isAdmin = role == "admin";
    var isDeptStaff = role == "member" && department != null;

    var query = db.Tickets.AsNoTracking().Where(t => t.OrgId == orgId);
    if (!isAdmin)
    {
        if (isDeptStaff) query = query.Where(t => t.CreatedBy == email || t.Department == department);
        else query = query.Where(t => t.CreatedBy == email);
    }

    var rows = await query.OrderByDescending(t => t.CreatedAt).Take(200).ToListAsync();
    return Results.Ok(rows.Select(t => new
    {
        id = t.Id,
        number = "TKT-" + t.Id.ToString("D4"),
        createdBy = t.CreatedBy,
        issue = t.Issue,
        priority = t.Priority,
        status = t.Status,
        department = t.Department,
        // Admins can change any ticket. A department's employees can change that department's tickets.
        canManage = isAdmin || (isDeptStaff && t.Department == department),
        createdAt = t.CreatedAt
    }));
}).RequireAuthorization().RequireRateLimiting("user");

app.MapPatch("/tickets/{id:int}/status", async (int id, TicketStatusRequest request, ClaimsPrincipal user, OrgMindDbContext db, IAuditService audit) =>
{
    var orgId = user.FindFirst("orgId")?.Value;
    var email = user.FindFirst("email")?.Value;
    if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(email)) return Results.Unauthorized();

    var role = user.FindFirst("role")?.Value;
    var department = user.FindFirst("department")?.Value;
    if (string.IsNullOrWhiteSpace(department)) department = null;

    var status = (request.Status ?? "").Trim().ToLowerInvariant();
    if (status is not ("open" or "in_progress" or "resolved"))
        return Results.BadRequest(new { error = "Status must be open, in_progress or resolved." });

    var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
    if (ticket is null) return Results.NotFound(new { error = "Ticket not found." });

    var allowed = role == "admin"
        || (role == "member" && department != null && ticket.Department == department);
    if (!allowed) return Results.StatusCode(403);

    var oldStatus = ticket.Status;
    ticket.Status = status;
    ticket.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    await audit.LogAsync("TICKET_STATUS_CHANGED", email, orgId,
        new { ticketId = id, from = oldStatus, to = status, department = ticket.Department });
    return Results.Ok(new { id, status });
}).RequireAuthorization().RequireRateLimiting("user");


// Admin screens: people in this organization. Only admins, and only their own organization.
// The password hash is never sent out.
app.MapGet("/admin/users", async (ClaimsPrincipal user, OrgMindDbContext db) =>
{
    var orgId = user.FindFirst("orgId")?.Value;
    if (string.IsNullOrEmpty(orgId)) return Results.Unauthorized();

    var users = await db.Users.AsNoTracking()
        .Where(u => u.OrgId == orgId)
        .OrderBy(u => u.Id)
        .Select(u => new { id = u.Id, email = u.Email, role = u.Role, department = u.Department, createdAt = u.CreatedAt })
        .ToListAsync();
    return Results.Ok(users);
}).RequireAuthorization(p => p.RequireRole("admin")).RequireRateLimiting("user");

app.MapPatch("/admin/users/{id:int}", async (int id, UpdateUserRequest request, ClaimsPrincipal user, OrgMindDbContext db, IAuditService audit) =>
{
    var orgId = user.FindFirst("orgId")?.Value;
    var adminEmail = user.FindFirst("email")?.Value;
    if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(adminEmail)) return Results.Unauthorized();

    var target = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.OrgId == orgId);
    if (target is null) return Results.NotFound(new { error = "User not found." });

    var oldRole = target.Role;
    var oldDepartment = target.Department;

    if (request.Role is not null)
    {
        var role = request.Role.Trim().ToLowerInvariant();
        if (role is not ("admin" or "member" or "customer"))
            return Results.BadRequest(new { error = "Role must be admin, member or customer." });

        // An organization must always keep at least one admin.
        if (target.Role == "admin" && role != "admin")
        {
            var otherAdmins = await db.Users.CountAsync(u => u.OrgId == orgId && u.Role == "admin" && u.Id != id);
            if (otherAdmins == 0)
                return Results.BadRequest(new { error = "The organization must keep at least one admin." });
        }
        target.Role = role;
    }

    if (request.Department is not null)
    {
        var dept = request.Department.Trim().ToLowerInvariant();
        if (dept.Length > 100) return Results.BadRequest(new { error = "Department name is too long." });
        target.Department = dept.Length == 0 ? null : dept;
    }

    // Customers are outsiders and never belong to a department.
    if (target.Role == "customer") target.Department = null;

    await db.SaveChangesAsync();
    await audit.LogAsync("USER_UPDATED", adminEmail, orgId,
        new { userId = id, roleFrom = oldRole, roleTo = target.Role, departmentFrom = oldDepartment, departmentTo = target.Department });

    return Results.Ok(new { id, role = target.Role, department = target.Department });
}).RequireAuthorization(p => p.RequireRole("admin")).RequireRateLimiting("user");

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

    // Who may see this document. Only the train route is limited to admins (see below),
    // so only an admin's choice ever reaches the AI engine.
    var visibility = form["visibility"].ToString().Trim().ToLowerInvariant();
    if (visibility.Length == 0) visibility = "internal";
    var allowedLabels = new[] { "public", "registered", "internal", "department", "admin_only" };
    if (!allowedLabels.Contains(visibility))
        return Results.BadRequest(new { error = "Invalid visibility choice." });
    var department = form["department"].ToString().Trim().ToLowerInvariant();
    if (visibility == "department" && department.Length == 0)
        return Results.BadRequest(new { error = "Choose a department for a department-only document." });
    if (department.Length > 100)
        return Results.BadRequest(new { error = "Department name is too long." });
    if (visibility != "department") department = "";

    var (status, body) = await docs.ForwardAsync(route, file, orgId, email, docName, visibility, department);

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
    .RequireAuthorization(p => p.RequireRole("admin")).RequireRateLimiting("user");

app.MapPost("/documents/ocr", (HttpRequest http, ClaimsPrincipal user, IDocumentService docs) =>
    HandleUpload(http, user, docs, "/documents/ocr", new[] { ".pdf", ".png", ".jpg", ".jpeg" }))
    .RequireAuthorization().RequireRateLimiting("user");

app.Run();

public record RegisterCustomerRequest(string? Email, string? Password, string? OrgId);
public record PublicChatRequest(string? Message, string? OrgId);

public record TicketStatusRequest(string? Status);

public record UpdateUserRequest(string? Role, string? Department);