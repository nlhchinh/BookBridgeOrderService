using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interface;
using OrderService.Application.Services;
using OrderService.Infracstructure.DBContext;
using OrderService.Infracstructure.Repositories;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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

var app = builder.Build();

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
