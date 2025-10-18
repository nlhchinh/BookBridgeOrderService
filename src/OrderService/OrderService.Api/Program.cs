using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrderService.Application.Interface;
using OrderService.Application.MappingProfile;
using OrderService.Application.Services;
using OrderService.Application.Services.External;
using OrderService.Application.Services.Payment;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;
using OrderService.Infracstructure.Repositories;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;




// Add services to the container.
builder.Services.AddControllers();
//Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("OrderServiceConnection")));

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddScoped<IOrderServices, OrderServices>();

builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<OrderItemRepository>();

// 3. JWT
var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var jwtAudience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        NameClaimType = "nameid"
    };

    // options.Events = new JwtBearerEvents
    // {
    //     OnTokenValidated = async context =>
    //     {
    //         var jti = context.Principal.FindFirstValue(JwtRegisteredClaimNames.Jti);

    //         if (string.IsNullOrEmpty(jti))
    //         {
    //             context.Fail("JWT missing jti.");
    //             return;
    //         }

    //         var cacheService = context.HttpContext.RequestServices.GetRequiredService<ICacheService>();

    //         if (await cacheService.IsBlacklistedAsync(jti))
    //         {
    //             context.Fail("This token has been revoked.");
    //         }

    //         await Task.CompletedTask;
    //     }
    // };

});

// Cart client: base address = CartService hostname (render). Replace with actual url.
builder.Services.AddHttpClient<ICartClient, CartClient>(client =>
{
    client.BaseAddress = new Uri("https://bookbridgecartservice.onrender.com"); // <<--- Render URL
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Payment service: mock for now
builder.Services.AddScoped<IPaymentService, MockPaymentService>();

// Order service
builder.Services.AddScoped<IOrderServices, OrderServices>();

builder.Services.AddAutoMapper(typeof(OrderMappingProfile).Assembly);

var app = builder.Build();

// Tự động áp dụng migrations VÀ XỬ LÝ LỖI
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider; // <-- chỉ tồn tại trong scope này
    try
    {
        // Lấy DbContext đã đăng ký
        var context = services.GetRequiredService<OrderDbContext>();

        // Tự động áp dụng migration
        context.Database.Migrate();
        // -------------------------------
        Console.WriteLine("Database migration applied successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database migration.");
    }
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
