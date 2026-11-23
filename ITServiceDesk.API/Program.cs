
using System.Text;
using System.Security.Claims;
using ITServiceDesk.API.Data;
using ITServiceDesk.API.Services;
using ITServiceDesk.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        "Logs/ITServiceDesk-.log",
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 1. Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// 3. JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key is missing.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// 4. Authorization
builder.Services.AddAuthorization();

// 5. Controllers and CORS
builder.Services.AddControllersWithViews();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 6. Application Services
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IPriorityService, PriorityService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAssignmentService, AssignmentService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// 7. Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer",
        new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Enter your JWT token."
        });

    options.AddSecurityRequirement(document =>
        new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.OpenApiSecuritySchemeReference(
                    "Bearer", document),
                new List<string>()
            }
        });
});

var app = builder.Build();

// 8. Create roles and seed accounts
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;

    var roleManager = serviceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    var userManager = serviceProvider
        .GetRequiredService<UserManager<IdentityUser>>();

    // Create all required roles
    string[] roles = { "SuperAdmin", "Admin", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(
                new IdentityRole(role));

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    // Seed account helper
    async Task SeedAccountAsync(
        string? email,
        string? password,
        string role,
        string fullName)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            Log.Warning("{Role} credentials are not configured in User Secrets.", role);
            return;
        }

        var user = await userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }

            var claimResult = await userManager.AddClaimAsync(
                user,
                new Claim("FullName", fullName));

            if (!claimResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to configure {role} account claims: " +
                    string.Join("; ", claimResult.Errors.Select(e => e.Description)));
            }
        }
        else if (!await userManager.IsInRoleAsync(user, role))
        {
            throw new InvalidOperationException(
                $"A configured {role} account already exists without its intended role. " +
                "Manual account verification and provisioning are required before startup can continue.");
        }

        // Ensure the seeded account has its intended role
        if (!await userManager.IsInRoleAsync(user, role))
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }
        }

        // Keep seeded accounts in their intended role only
        var otherRoles = (await userManager.GetRolesAsync(user))
            .Where(existingRole => existingRole != role)
            .ToList();

        if (otherRoles.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, otherRoles);

            if (!removeResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to configure {role} account roles: " +
                    string.Join("; ", removeResult.Errors.Select(e => e.Description)));
            }
        }

        Log.Information("{Role} account configured: {Email}", role, email);
    }

    // Existing Admin account
    await SeedAccountAsync(
        builder.Configuration["AdminSetup:Email"],
        builder.Configuration["AdminSetup:Password"],
        "Admin",
        "System Administrator");

    // New SuperAdmin account
    await SeedAccountAsync(
        builder.Configuration["SuperAdminSetup:Email"],
        builder.Configuration["SuperAdminSetup:Password"],
        "SuperAdmin",
        "Company Super Administrator");
}

// 9. HTTP Pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("AngularClient");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 10. Test endpoints
app.MapGet("/", () => "IT Service Desk API is running!");

app.MapGet("/api/test", () => new
{
    Message = "Backend is working successfully!",
    Project = "IT Service Desk"
});

app.Run();
