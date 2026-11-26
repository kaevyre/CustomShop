namespace Auth.Api.Services;

public class JwtSettings
{
    public string Issuer { get; set; } = "auth-service";
    public string Audience { get; set; } = "microservices-platform";
    public string SecretKey { get; set; } = default!;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
