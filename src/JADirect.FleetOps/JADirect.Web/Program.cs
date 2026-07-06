using System.Threading.RateLimiting;
using JADirect.Application.Interfaces;
using JADirect.Application.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using JADirect.Data.Repositories;
using JADirect.Web.Middleware;
using Microsoft.AspNetCore.RateLimiting;
using JADirect.Application.Services;
using QuestPDF.Infrastructure;


var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------
// 1. CONFIGURAÇÃO DE SERVIÇOS (CONTAINER)
// ---------------------------------------------------------


// QuestPDF — Community License
// DCODE Solutions se enquadra na licença gratuita (receita < USD 1M/ano).
// Esta declaração deve preceder qualquer geração de PDF em runtime.
QuestPDF.Settings.License = LicenseType.Community;


builder.Services.AddControllersWithViews();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 6 * 1024 * 1024;
});


builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(8);
    });

// ADICIONADO, Rate Limiting para proteger o endpoint de Login contra
//ataques de força bruta.
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.AddFixedWindowLimiter("login-policy", fixedWindowOptions =>
    {
        fixedWindowOptions.PermitLimit = 10;
        fixedWindowOptions.Window = TimeSpan.FromMinutes(1);
        fixedWindowOptions.QueueLimit = 0;
        fixedWindowOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    
    //Resposta padrão quando o limite é atingido.
    // HTTP 429 Too Many Requests é o código semântico correto para rate limiting.
    // Redireciona para o Login em vez de retornar JSON, compatível com a View.
    rateLimiterOptions.OnRejected = async (context, CancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Redirect("/Account/Login?blocked=true");
        await Task.CompletedTask;
    };
});

// ---------------------------------------------------------
// 2. INJEÇÃO DE DEPENDÊNCIA
// ---------------------------------------------------------

// Infraestrutura
builder.Services.AddScoped<JADirect.Data.Infrastructure.DbConnectionFactory>(sp =>
    new JADirect.Data.Infrastructure.DbConnectionFactory(builder.Configuration.GetConnectionString("DefaultConnection")
                                                         ?? throw new Exception("Connection String not found.")));

builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp =>
{
    string endpoint  = Environment.GetEnvironmentVariable("RAILWAY_BUCKET_ENDPOINT")
                       ?? throw new Exception("RAILWAY_BUCKET_ENDPOINT not configured.");
    string accessKey = Environment.GetEnvironmentVariable("RAILWAY_BUCKET_ACCESS_KEY")
                       ?? throw new Exception("RAILWAY_BUCKET_ACCESS_KEY not configured.");
    string secretKey = Environment.GetEnvironmentVariable("RAILWAY_BUCKET_SECRET_KEY")
                       ?? throw new Exception("RAILWAY_BUCKET_SECRET_KEY not configured.");

    var s3Config = new Amazon.S3.AmazonS3Config
    {
        ServiceURL = endpoint,
        ForcePathStyle = true,
    };
    return new Amazon.S3.AmazonS3Client(accessKey, secretKey, s3Config);
});
builder.Services.AddHttpClient();




// Repositórios (Camada de Dados - SQL Puro)
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<VehicleRepository>();
builder.Services.AddScoped<InspectionRepository>();
builder.Services.AddScoped<DailyLogRepository>();
builder.Services.AddScoped<ChecklistItemRepository>();
builder.Services.AddScoped<BlockingRuleRepository>();
builder.Services.AddScoped<PhotoRepository>();
builder.Services.AddScoped<AssignmentRepository>();
builder.Services.AddScoped<TenantRepository>();
builder.Services.AddScoped<AlertRepository>();
builder.Services.AddScoped<MessageLogRepository>();
builder.Services.AddScoped<AvailabilityRepository>();



// Serviços (Camada de Aplicação)
builder.Services.AddScoped<FleetService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WalkaroundService>();
builder.Services.AddScoped<ChecklistItemRepository>();
builder.Services.AddScoped<BlockingRuleRepository>();
builder.Services.AddScoped<IWalkaroundPdfService, WalkaroundPdfService>();
builder.Services.AddScoped<PhotoService>(sp =>
{
    string bucketName = Environment.GetEnvironmentVariable("RAILWAY_BUCKET_NAME")
                        ?? throw new Exception("RAILWAY_BUCKET_NAME not configured.");

    return new PhotoService(
        sp.GetRequiredService<PhotoRepository>(),
        sp.GetRequiredService<Amazon.S3.IAmazonS3>(),
        bucketName);
});
builder.Services.AddScoped<AssignmentService>();
builder.Services.AddScoped<DailyLogService>();
builder.Services.AddScoped<DailyLogComplianceService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<WhatsAppAlertService>();



// Serviço de background
builder.Services.AddHostedService<DraftCleanupService>();
builder.Services.AddHostedService<WhatsAppNotificationJob>();
builder.Services.AddHostedService<AvailabilityAutoReactivationJob>();



var app = builder.Build();

// ---------------------------------------------------------
// 3. MIDDLEWARES (PIPELINE DE EXECUÇÃO)
// ---------------------------------------------------------

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<UserStatusMiddleware>();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();