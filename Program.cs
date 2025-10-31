using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AlphaPlusAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================
// 1️⃣  Add Services to Container
// ============================

// Add controller support
builder.Services.AddControllers();

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register custom services
builder.Services.AddScoped<DatabaseService>();  // Database helper
builder.Services.AddScoped<AuthService>();      // Authentication and user logic

// ============================
// 2️⃣  Configure CORS
// ============================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ============================
// 3️⃣  Configure JWT Authentication
// ============================

// Read JWT values from appsettings.json or Render env vars
var jwtKey = builder.Configuration["JwtSettings:SecretKey"]
             ?? "YourSuperSecretKeyForAlphaPlusApp2025!@#$%";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"]
                ?? "AlphaPlusAPI";
var jwtAudience = builder.Configuration["JwtSettings:Audience"]
                  ?? "AlphaPlusApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

// ============================
// 4️⃣  Build the App
// ============================
var app = builder.Build();

// ============================
// 5️⃣  Configure Middleware
// ============================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage(); // Detailed errors in dev
}
else
{
    // Optional: force HTTPS redirect in production
    app.UseHsts();
}

app.UseHttpsRedirection();

// Enable CORS for all requests
app.UseCors("AllowAll");

// Enable Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controller routes
app.MapControllers();

// ============================
// 6️⃣  Health Check Endpoint
// ============================
app.MapGet("/", () => new
{
    status = "Running ✅",
    environment = app.Environment.EnvironmentName,
    message = "AlphaPlus API is online and secure.",
    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
    version = "v1.0.0"
});

// ============================
// 7️⃣  Run the App
// ============================
app.Run();
