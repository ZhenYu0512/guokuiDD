using GuokuiDD.Api.Data;
using GuokuiDD.Api.Dtos;
using GuokuiDD.Api.Models;
using GuokuiDD.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuokuiDD.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly IConfiguration _cfg;

    public AuthController(AppDbContext db, JwtService jwt, IConfiguration cfg)
    {
        _db = db; _jwt = jwt; _cfg = cfg;
    }

    /// <summary>小程序登录。开发模式下 code 直接作为 openId；生产环境调用 code2session 换取 openId。</summary>
    [HttpPost("wechat-login")]
    public async Task<IActionResult> WeChatLogin(WeChatLoginReq req)
    {
        if (string.IsNullOrWhiteSpace(req.Code)) return BadRequest(new { message = "code 不能为空" });

        string openId;
        if (_cfg.GetValue<bool>("WeChat:DevMockLogin"))
        {
            openId = "dev_" + req.Code;
        }
        else
        {
            using var http = new HttpClient();
            var url = $"https://api.weixin.qq.com/sns/jscode2session?appid={_cfg["WeChat:AppId"]}&secret={_cfg["WeChat:AppSecret"]}&js_code={req.Code}&grant_type=authorization_code";
            var json = await http.GetFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>(url);
            if (json == null || !json.TryGetValue("openid", out var oid))
                return BadRequest(new { message = "微信登录失败" });
            openId = oid.GetString()!;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.OpenId == openId);
        if (user == null)
        {
            user = new User { OpenId = openId, NickName = req.NickName ?? "微信用户", AvatarUrl = req.AvatarUrl };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        var token = _jwt.CreateUserToken(user.Id);
        return Ok(new { token, user = new { user.Id, user.NickName, user.AvatarUrl, user.Points } });
    }
}
