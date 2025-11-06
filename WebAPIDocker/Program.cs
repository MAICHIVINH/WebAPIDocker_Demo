using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Application.Infrastructure.Repositories;
using Application.Services;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using sepending.Application.Services;
using WebAPIDocker.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// setup nếu dùng RateLimit ở program.cs
//builder.Services.AddRateLimiter(options =>
//{
//    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
//        RateLimitPartition.GetFixedWindowLimiter(
//            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "global",
//            factory: _ => new FixedWindowRateLimiterOptions
//            {
//                PermitLimit = 5,                // số request cho phép
//                Window = TimeSpan.FromSeconds(10), // trong 10 giây
//                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
//                QueueLimit = 0
//            }));

//    // Khi bị giới hạn, chạy callback này
//    options.OnRejected = async (context, token) =>
//    {
//        var httpContext = context.HttpContext;

//        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
//        httpContext.Response.ContentType = "application/json";

//        // Có thể thêm Retry-After để client biết khi nào thử lại
//        httpContext.Response.Headers["Retry-After"] = "10";

//        var result = JsonSerializer.Serialize(new
//        {
//            status = 429,
//            error = "Too many requests",
//            message = "Bạn đã vượt quá giới hạn yêu cầu. Vui lòng thử lại sau."
//        });

//        await httpContext.Response.WriteAsync(result, token);
//    };
//});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()    // cho phép mọi domain
            .AllowAnyHeader()    // cho phép mọi header
            .AllowAnyMethod()    // cho phép mọi method (GET, POST, PUT, DELETE,…)
    );
});


builder.Services.AddDbContext<ExpenseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Sepending API",
        Version = "v1"
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddSingleton<WebSocketConnectionManager>();

builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IBudgetRepository, BudgetRepository>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.MapPost("/ws/clear", (WebSocketConnectionManager manager) =>
//{
//    manager.ClearAllConnections();
//    return Results.Ok("All WebSocket connections cleared.");
//});

//var wsHandler = app.Services.GetRequiredService<WebSocketHandler>();
//wsHandler.StartServer("ws://0.0.0.0:8181");

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseMiddleware<ExceptionMiddleware>();

// app.UseMiddleware<RateLimitMiddleware>(5, 10);
//app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();