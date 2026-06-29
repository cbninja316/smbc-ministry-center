using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmbcStatusBoard.Api.Data;
using SmbcStatusBoard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<PraiseChartsService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("PraiseCharts", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(policy =>
    {
        var frontendUrl = builder.Configuration["App:FrontendUrl"]!;
        if (frontendUrl == "*")
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            // Support comma-separated list of allowed origins
            var origins = frontendUrl
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
    }));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await SeedDeveloperAsync(db, builder.Configuration);
    var users = await db.Users.ToListAsync();
    foreach (var u in users)
        Console.WriteLine($"[SEED CHECK] Id={u.Id} Username={u.Username} Role={u.Role} IsActive={u.IsActive} EmailVerified={u.EmailVerified}");
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task SeedDeveloperAsync(AppDbContext db, IConfiguration config)
{
    var username = config["Seed:DeveloperUsername"];
    var password = config["Seed:DeveloperPassword"];
    var email = config["Seed:DeveloperEmail"];

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
        return;

    var exists = await db.Users.AnyAsync(u => u.Username == username);
    if (exists) return;

    db.Users.Add(new SmbcStatusBoard.Api.Models.User
    {
        Username = username,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Role = SmbcStatusBoard.Api.Models.UserRole.Developer,
        IsActive = true,
        EmailVerified = true,
        AllowedItemTypes = ""
    });

    await db.SaveChangesAsync();
}
