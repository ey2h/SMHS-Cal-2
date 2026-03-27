namespace GlassManagement.Api.Models;

public class AdminCreateUserRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Company { get; set; }
    public string Role { get; set; } = "user";
    public DateTime? LicenseExpiry { get; set; }
    public DateTime? LicenseStart { get; set; }
    public int MaxProjects { get; set; } = 10;
}

public class AdminUpdateUserRequest
{
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Company { get; set; }
    public string? Notes { get; set; }
    public int MaxProjects { get; set; } = 10;
}

public class SetLicenseRequest
{
    public DateTime? LicenseExpiry { get; set; }
}

public class UserListResponse
{
    public List<AuthUserDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class RegenerateTokenResponse
{
    public string NewToken { get; set; } = "";
}

public class UsageSummaryResponse
{
    public List<UsageStatDto> Users { get; set; } = new();
}
