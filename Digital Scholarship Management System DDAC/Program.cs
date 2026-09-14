using Digital_Scholarship_Management_System_DDAC.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Digital_Scholarship_Management_System_DDAC.Services;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ApplicationDbContextConnection")
    ?? throw new InvalidOperationException("Connection string 'ApplicationDbContextConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Register S3 Upload Service
builder.Services.AddHttpClient<IS3Service, S3LambdaClientService>(client =>
{
    var documentServiceUrl = builder.Configuration["DocumentService:BaseUrl"]
        ?? throw new InvalidOperationException("DocumentService:BaseUrl not configured");

    if (!documentServiceUrl.EndsWith("/")) documentServiceUrl += "/";

    client.BaseAddress = new Uri(documentServiceUrl);
});

// Register Notification Service
builder.Services.AddHttpClient<INotificationService, NotificationServiceClient>(client =>
{
    var notificationServiceUrl = builder.Configuration["NotificationService:BaseUrl"]
        ?? throw new InvalidOperationException("NotificationService:BaseUrl not configured");

    if (!notificationServiceUrl.EndsWith("/")) notificationServiceUrl += "/";

    client.BaseAddress = new Uri(notificationServiceUrl);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedRolesAndDemoUsersAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
