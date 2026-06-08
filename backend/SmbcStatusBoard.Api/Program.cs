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
    await SeedSuperAdminAsync(db, builder.Configuration);
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task SeedSuperAdminAsync(AppDbContext db, IConfiguration config)
{
    if (await db.Users.AnyAsync()) return;

    var username = config["Seed:SuperAdminUsername"] ?? "admin";
    var password = config["Seed:SuperAdminPassword"] ?? "ChangeMe123!";
    var email = config["Seed:SuperAdminEmail"] ?? "admin@church.org";

    db.Users.Add(new SmbcStatusBoard.Api.Models.User
    {
        Username = username,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Role = SmbcStatusBoard.Api.Models.UserRole.SuperAdmin,
        IsActive = true,
        AllowedItemTypes = "ChurchEvent,FacilityUse,Benevolence,Maintenance,Receipt"
    });

    await db.SaveChangesAsync();
}
