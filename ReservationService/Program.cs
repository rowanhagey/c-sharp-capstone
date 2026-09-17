using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReservationService.Data;
using ReservationService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Reservation Service API", Version = "v1" });
});

builder.Services.AddDbContext<ReservationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<ICatalogServiceClient, CatalogServiceClient>(client =>
{
    var baseUrl = builder.Configuration["Services:CatalogServiceBaseUrl"];
    client.BaseAddress = new Uri(baseUrl!);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    var baseUrl = builder.Configuration["Services:UserServiceBaseUrl"];
    client.BaseAddress = new Uri(baseUrl!);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<IWaitlistCascadeService, WaitlistCascadeService>();
builder.Services.AddHostedService<WaitlistExpiryJob>();

var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
