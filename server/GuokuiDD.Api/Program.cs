using System.Text;
using GuokuiDD.Api.Data;
using GuokuiDD.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// 禁用 JWT 出入站 Claim 类型映射，保证 "role"/"typ"/"uid" 等短名声明原样传递
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// 确保 wwwroot/uploads 存在（WebRootPath 在目录缺失时为 null）
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false; // .NET 8 默认用 JsonWebTokenHandler 映射入站声明，必须关闭
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("Admin", p => p.RequireClaim("role", "admin"));
    opt.AddPolicy("User", p => p.RequireClaim("typ", "user"));
});
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddScoped<JwtService>();
builder.Services.AddSingleton<IImageStorage, TosImageStorage>();
builder.Services.AddSingleton<EscPosPrinterService>();
// Pay:UseMock=true 时使用模拟支付（本地开发）；false 时接入微信支付 V3
if (builder.Configuration.GetValue<bool>("Pay:UseMock"))
    builder.Services.AddScoped<IPayService, MockPayService>();
else
    builder.Services.AddScoped<IPayService, WeChatPayV3Service>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
// 提供静态文件访问（/uploads/**）
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
