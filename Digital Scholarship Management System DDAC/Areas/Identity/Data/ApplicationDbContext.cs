using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Digital_Scholarship_Management_System_DDAC.Models;

namespace Digital_Scholarship_Management_System_DDAC.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<StudentProfile> StudentProfiles { get; set; } = null!;
    public DbSet<InstitutionProfile> InstitutionProfiles { get; set; } = null!;
    public DbSet<Scholarship> Scholarships { get; set; } = null!;
    public DbSet<Application> Applications { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Customize the ASP.NET Identity model and override the defaults if needed.
        // For example, you can rename the ASP.NET Identity table names and more.
        // Add your customizations after calling base.OnModelCreating(builder);
    }
}
