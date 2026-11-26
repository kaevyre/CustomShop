using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Platform.IdentityServer.Models;

namespace Platform.IdentityServer.Data;

/// <summary>Група таблиць «ASP.NET Identity»: AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims...</summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options);
