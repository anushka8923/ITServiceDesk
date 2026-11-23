
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;

namespace ITServiceDesk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public AuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _context = context;
    }

    // PUBLIC REGISTRATION - Only User role
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var email = dto.Email.Trim();
        var reservedDemoEmails = new[]
        {
            "admin@itservicedesk.com",
            "superadmin@itservicedesk.com",
            _configuration["AdminSetup:Email"],
            _configuration["SuperAdminSetup:Email"]
        };

        if (reservedDemoEmails.Any(reservedEmail =>
                !string.IsNullOrWhiteSpace(reservedEmail) &&
                string.Equals(email, reservedEmail.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new
            {
                Message = "This is a demo Admin/SuperAdmin account. Please use the demo credentials available on the Login page. Registration is not allowed for this account."
            });
        }

        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                Message = "Email is already registered."
            });
        }

        if (!await _roleManager.RoleExistsAsync("User"))
        {
            var createRoleResult = await _roleManager.CreateAsync(
                new IdentityRole("User"));

            if (!createRoleResult.Succeeded)
            {
                return StatusCode(500, new
                {
                    Message = "Unable to configure User role."
                });
            }
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                Errors = result.Errors.Select(e => e.Description)
            });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "User");

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            return StatusCode(500, new
            {
                Message = "Unable to assign User role."
            });
        }

        var claimResult = await _userManager.AddClaimAsync(
            user,
            new Claim("FullName", dto.FullName));

        if (!claimResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            return StatusCode(500, new
            {
                Message = "Unable to save user details."
            });
        }

        return Ok(new
        {
            Message = "Registration successful. Please login."
        });
    }

    // PUBLIC LOGIN - SuperAdmin, Admin and User
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user == null)
        {
            return Unauthorized(new
            {
                Message = "Invalid email or password."
            });
        }

        var passwordResult = await _signInManager.CheckPasswordSignInAsync(
            user,
            dto.Password,
            lockoutOnFailure: true);

        if (!passwordResult.Succeeded)
        {
            return Unauthorized(new
            {
                Message = "Invalid email or password."
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userClaims = await _userManager.GetClaimsAsync(user);

        var fullName = userClaims
            .FirstOrDefault(c => c.Type == "FullName")?.Value ?? "";

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, fullName)
        };

        claims.AddRange(roles.Select(role =>
            new Claim(ClaimTypes.Role, role)));

        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key is missing.");

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var expiryMinutes = Convert.ToDouble(
            _configuration["Jwt:ExpiryInMinutes"]);

        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenString =
            new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new AuthResponseDto
        {
            Token = tokenString,
            Email = user.Email ?? "",
            FullName = fullName,
            Roles = roles,
            ExpiresAt = expiresAt
        });
    }

    // SUPERADMIN + ADMIN - GET USERS
    [Authorize(Roles = "SuperAdmin,Admin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var allUsers = await _userManager.Users.ToListAsync();

        var result = new List<object>();

        var isSuperAdmin = User.IsInRole("SuperAdmin");

        foreach (var user in allUsers)
        {
            var roles = await _userManager.GetRolesAsync(user);

            // Admin can only see normal Users.
            // SuperAdmin can see all accounts.
            if (!isSuperAdmin && !roles.Contains("User"))
            {
                continue;
            }

            var claims = await _userManager.GetClaimsAsync(user);

            var fullName = claims
                .FirstOrDefault(c => c.Type == "FullName")?.Value ?? "";

            result.Add(new
            {
                UserId = user.Id,
                Email = user.Email,
                FullName = fullName,
                Roles = roles,
                EmailConfirmed = user.EmailConfirmed
            });
        }

        return Ok(result);
    }

    // SUPERADMIN + ADMIN - CHANGE USER ROLE
    [Authorize(Roles = "SuperAdmin,Admin")]
    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> UpdateUserRole(
        string userId,
        [FromBody] UpdateUserRoleRequest request)
    {
        var requestedRole = request.Role?.Trim();

        var isSuperAdmin = User.IsInRole("SuperAdmin");

        // SuperAdmin role can never be assigned through this endpoint.
        if (requestedRole != "Admin" && requestedRole != "User")
        {
            return BadRequest(new
            {
                Message = "Only Admin and User roles can be assigned."
            });
        }

        // Admin is only allowed to assign the User role.
        if (!isSuperAdmin && requestedRole != "User")
        {
            return Forbid();
        }

        if (!await _roleManager.RoleExistsAsync(requestedRole))
        {
            return BadRequest(new
            {
                Message = "Requested role does not exist."
            });
        }

        // All role changes use the same transaction-owned lock so a request cannot
        // make its Admin-count decision against another request's in-flight change.
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var connection = _context.Database.GetDbConnection();
        await using var lockCommand = connection.CreateCommand();
        lockCommand.Transaction = _context.Database.CurrentTransaction!.GetDbTransaction();
        lockCommand.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = @Resource,
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Transaction',
                    @LockTimeout = @LockTimeout;
                SELECT @result;
                """;
        var resourceParameter = lockCommand.CreateParameter();
        resourceParameter.ParameterName = "@Resource";
        resourceParameter.Value = "ITServiceDesk:Auth:RoleChanges";
        lockCommand.Parameters.Add(resourceParameter);
        var timeoutParameter = lockCommand.CreateParameter();
        timeoutParameter.ParameterName = "@LockTimeout";
        timeoutParameter.Value = 15000;
        lockCommand.Parameters.Add(timeoutParameter);

        var lockResult = Convert.ToInt32(await lockCommand.ExecuteScalarAsync());

        if (lockResult < 0)
        {
            await transaction.RollbackAsync();
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = "Role updates are temporarily busy. Please try again."
            });
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            await transaction.RollbackAsync();
            return NotFound(new { Message = "User not found." });
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        // Never allow changing a SuperAdmin account through this endpoint.
        if (currentRoles.Contains("SuperAdmin"))
        {
            await transaction.RollbackAsync();
            return Forbid();
        }

        // Admin can only manage existing normal User accounts.
        if (!isSuperAdmin && !currentRoles.Contains("User"))
        {
            await transaction.RollbackAsync();
            return Forbid();
        }

        // Prevent removal of the last Admin while holding the shared role-change lock.
        if (currentRoles.Contains("Admin") && requestedRole == "User")
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");

            if (admins.Count <= 1)
            {
                await transaction.RollbackAsync();
                return BadRequest(new
                {
                    Message = "Cannot remove the last Admin account."
                });
            }
        }

        // Add the new role first, retaining the existing role-update ordering.
        if (!currentRoles.Contains(requestedRole))
        {
            var addResult = await _userManager.AddToRoleAsync(user, requestedRole);

            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return BadRequest(new
                {
                    Errors = addResult.Errors.Select(e => e.Description)
                });
            }
        }

        var rolesToRemove = currentRoles
            .Where(role => (role == "Admin" || role == "User") && role != requestedRole)
            .ToList();

        if (rolesToRemove.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Message = "Role update could not be completed.",
                    Errors = removeResult.Errors.Select(e => e.Description)
                });
            }
        }

        await transaction.CommitAsync();

        return Ok(new
        {
            Message = "User role updated successfully.",
            UserId = user.Id,
            Email = user.Email,
            Role = requestedRole
        });
    }
}

// Request model for role update
public class UpdateUserRoleRequest
{
    public string Role { get; set; } = string.Empty;
}
