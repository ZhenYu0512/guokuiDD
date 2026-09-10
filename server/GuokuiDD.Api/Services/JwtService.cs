using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace GuokuiDD.Api.Services;

public class JwtService
{
    private readonly IConfiguration _cfg;
    public JwtService(IConfiguration cfg) => _cfg = cfg;

    public string CreateToken(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(double.Parse(_cfg["Jwt:ExpireHours"] ?? "72")),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateUserToken(int userId) =>
        CreateToken(new[] { new Claim("typ", "user"), new Claim("uid", userId.ToString()) });

    public string CreateAdminToken(int adminId, string username) =>
        CreateToken(new[] { new Claim("role", "admin"), new Claim("aid", adminId.ToString()), new Claim("name", username) });
}
