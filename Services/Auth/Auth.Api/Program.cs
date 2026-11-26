using Platform.ServiceDefaults.Security;
using System.Text;
using Auth.Api.Data;
using Auth.Api.Models;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// ── БД "IdentityDb" (SQL Server з AppHost; рядок підключення приходить через WithReference)
builder.AddSqlServerDbContext<ApplicationDbContext>("IdentityDb");

// ── ASP.NET Identity: вимоги до пароля, lockout, унікальний email
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Токени підтвердження email / скидання пароля — одноразові (змінюється SecurityStamp) і з терміном дії
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));

// ── JWT
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
    throw new InvalidOperationException("Jwt:SecretKey має містити щонайменше 32 символи.");

// AddIdentity реєструє cookie-схеми й виставляє їх як Default{Authenticate,Challenge,Forbid,SignIn}Scheme — вони мають
// пріоритет над DefaultScheme, тож [Authorize] перенаправляв би на /Account/Login (404) замість перевірки JWT.
// Явно повертаємо JwtBearer як схему за замовчуванням для всіх операцій.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;                // claims лишаються з іменами "sub", "role", ...
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            RoleClaimType = "role",
            NameClaimType = "unique_name",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddSingleton<IEmailService, MockEmailService>(); // у production — реальний SMTP/SendGrid
builder.Services.AddControllers();

var app = builder.Build();

// Ролі User і Admin створюються під час старту (+ тестовий admin у Development)
await IdentitySeeder.InitializeAsync(app.Services, app.Configuration, app.Environment.IsDevelopment());

app.UseGatewayOnly();                  // Лаб. 09: прямий доступ повз Gateway блокується (якщо задано Gateway:Secret)
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
