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

// ✅ Register custom services in correct order (dependencies first)
builder.Services.AddScoped<DatabaseService>();      // Must be first - other services depend on it
builder.Services.AddScoped<SyncService>();          // Depends on DatabaseService
builder.Services.AddScoped<AuthService>();          // Authentication and user logic

// Add logging (already included by default, but explicit configuration)
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

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

// Read JWT values from appsettings.json or environment variables (Render)
var jwtKey = builder.Configuration["JwtSettings:SecretKey"]
             ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
             ?? "YourSuperSecretKeyForAlphaPlusApp2025!@#$%";

var jwtIssuer = builder.Configuration["JwtSettings:Issuer"]
                ?? Environment.GetEnvironmentVariable("JWT_ISSUER")
                ?? "AlphaPlusAPI";

var jwtAudience = builder.Configuration["JwtSettings:Audience"]
                  ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE")
                  ?? "AlphaPlusApp";

Console.WriteLine($"🔐 JWT Configuration:");
Console.WriteLine($"   Issuer: {jwtIssuer}");
Console.WriteLine($"   Audience: {jwtAudience}");
Console.WriteLine($"   Key Length: {jwtKey.Length} chars");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // For development/testing
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero // Remove default 5 minute clock skew
    };

    // Log authentication events for debugging
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"❌ Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"✅ Token validated for user: {context.Principal?.Identity?.Name}");
            return Task.CompletedTask;
        }
    };
});

// ============================
// 4️⃣  Build the App
// ============================
var app = builder.Build();

// ============================
// 5️⃣  Configure Middleware Pipeline
// ============================

// Always enable Swagger (useful for production debugging)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AlphaPlus API v1");
    c.RoutePrefix = "swagger"; // Access at: /swagger
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Detailed errors in dev
}
else
{
    app.UseHsts(); // HTTPS strict transport security
}

// ⚠️ IMPORTANT: Order matters!
app.UseHttpsRedirection();  // 1. Redirect HTTP to HTTPS
app.UseCors("AllowAll");    // 2. Enable CORS
app.UseAuthentication();    // 3. Enable authentication
app.UseAuthorization();     // 4. Enable authorization

// Map controller routes
app.MapControllers();

// ============================
// 6️⃣  Health Check Endpoints
// ============================

// Root endpoint
app.MapGet("/", () => new
{
    status = "Running ✅",
    environment = app.Environment.EnvironmentName,
    message = "AlphaPlus API is online and secure.",
    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
    version = "v1.0.0",
    endpoints = new[]
    {
        "/api/auth/login",
        "/api/products",
        "/api/dashboard",
        "/api/invoices",
        "/api/customers",
        "/swagger"
    }
});

// Detailed health check
app.MapGet("/api/health", () => new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName,
    services = new
    {
        database = "configured",
        authentication = "enabled",
        cors = "enabled",
        swagger = "enabled"
    },
    configuration = new
    {
        jwtIssuer,
        jwtAudience,
        hasConnectionString = !string.IsNullOrEmpty(
            builder.Configuration.GetConnectionString("DefaultConnection") 
            ?? Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
        )
    }
});

// Test endpoint (no auth required)
app.MapGet("/api/test", () => new
{
    success = true,
    message = "API is working!",
    timestamp = DateTime.UtcNow
});

// ============================
// 7️⃣  Database Connection Test on Startup
// ============================
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        Console.WriteLine("🔍 Testing database connection...");
        
        using (var conn = dbService.GetConnection())
        {
            await conn.OpenAsync();
            Console.WriteLine("✅ Database connection successful!");
            await conn.CloseAsync();
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Database connection failed: {ex.Message}");
    Console.WriteLine("App will continue, but database operations will fail.");
}

// ============================
// 8️⃣  Run the App
// ============================
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"\n{'=',-50}");
Console.WriteLine($"🚀 AlphaPlus API Starting");
Console.WriteLine($"{'=',-50}");
Console.WriteLine($"📍 Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"🌐 Port: {port}");
Console.WriteLine($"📚 Swagger UI: /swagger");
Console.WriteLine($"💚 Health Check: /api/health");
Console.WriteLine($"{'=',-50}\n");

app.Run();