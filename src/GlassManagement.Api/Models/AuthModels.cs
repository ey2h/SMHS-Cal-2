namespace GlassManagement.Api.Models;

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class TokenLoginRequest
{
    public string ApiToken { get; set; } = "";
}

public class RegisterRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public AuthUserDto User { get; set; } = new();
}

public class AuthUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = "";
    public DateTime? LicenseExpiry { get; set; }
    public string? Company { get; set; }
    public string? Notes { get; set; }
    public int MaxProjects { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ApiToken { get; set; }
    public DateTime? LicenseStart { get; set; }
    public string? LastLoginIp { get; set; }
    public int TotalUsageMinutes { get; set; }
}

public class PulseRequest
{
    public string? LastPulseId { get; set; }
}

public class PulseResponse
{
    public bool Valid { get; set; }
    public int RemainingSeconds { get; set; }
    public string PulseId { get; set; } = "";
    public string? RefreshToken { get; set; }
    public DateTime? LicenseExpiry { get; set; }
    public int? LicenseRemainingDays { get; set; }
}

public class UsageStatDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
    public int TotalMinutes { get; set; }
    public int SessionCount { get; set; }
    public List<DailyUsageDto> DailyStats { get; set; } = new();
}

public class DailyUsageDto
{
    public string Date { get; set; } = "";
    public int Minutes { get; set; }
    public int SessionCount { get; set; }
}
