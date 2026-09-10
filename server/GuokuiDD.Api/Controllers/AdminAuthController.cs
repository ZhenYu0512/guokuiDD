using System.Security.Cryptography;
using System.Text;
using GuokuiDD.Api.Data;
using GuokuiDD.Api.Dtos;
using GuokuiDD.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuokuiDD.Api.Controllers;

[ApiController, Route("api/admin/auth")]
public class AdminAuthController : ControllerBase
{
    public const string PasswordSalt = "GuokuiDD@2026";

    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    public AdminAuthController(AppDbContext db, JwtService jwt) { _db = db; _jwt = jwt; }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(PasswordSalt + password));
        return Convert.ToHexString(bytes);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(AdminLoginReq req)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(a => a.Username == req.Username);
        if (admin == null || admin.PasswordHash != HashPassword(req.Password))
            return Unauthorized(new { message = "用户名或密码错误" });
        var token = _jwt.CreateAdminToken(admin.Id, admin.Username);
        return Ok(new { token, username = admin.Username, role = admin.Role });
    }
}
