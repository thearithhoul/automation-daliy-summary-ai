using System.IdentityModel.Tokens.Jwt;
using System.Text;
using ChongReanProject.Config;
using ChongReanProject.Func;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Bind the "FireBaseSetting" section of appsettings.json into a typed object.
var firebaseSettings = builder.Configuration
    .GetSection("FireBaseSetting")
    .Get<FireBaseSetting>()
    ?? throw new InvalidOperationException("Missing 'FireBaseSetting' configuration section.");

// Also register it for injection via IOptions<FireBaseSetting> elsewhere (e.g. in services/controllers).
builder.Services.Configure<FireBaseSetting>(builder.Configuration.GetSection("FireBaseSetting"));

// FirestoreDb.Create resolves credentials the same way GoogleCredential.GetApplicationDefault()
// does (GOOGLE_APPLICATION_CREDENTIALS, or the attached identity on Google Cloud) - no
// FirebaseApp/FirebaseAdmin bootstrap needed, since this project only ever talks to
// Firestore via the service account, not through Firebase Auth.
builder.Services.AddSingleton(FirestoreDb.Create(firebaseSettings.ProjectId));
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<FirebaseStroing>();

builder.Services.Configure<GeminiSetting>(builder.Configuration.GetSection("Gemini"));
builder.Services.AddSingleton<GeminiAiService>();

builder.Services.Configure<ScrapingServiceSetting>(builder.Configuration.GetSection("ScrapingService"));
builder.Services.AddHttpClient<ArticleScrapingClient>();

var jwtSecret = builder.Configuration["Secret"]
    ?? throw new InvalidOperationException("Missing 'Secret' configuration value.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = AuthService.Issuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };

        // The standard TokenValidationParameters above only check signature/issuer/lifetime -
        // revocation (backed by Firestore, see AuthService) is checked separately here so
        // [Authorize] honors tokens revoked via AuthService.RevokeTokenAsync too.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.Claims
                    .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                var authService = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
                if (jti is null || await authService.IsRevokedAsync(jti))
                {
                    context.Fail("Token has been revoked.");
                }
            },
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ChongReanService v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

