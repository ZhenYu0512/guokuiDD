using System.Text.Json;
using GuokuiDD.Api.Data;
using GuokuiDD.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuokuiDD.Api.Controllers;

/// <summary>
/// 微信支付 V3 回调。解密 resource 后按状态机条件更新订单（幂等：重复通知只生效一次）。
/// 注：生产环境建议同时使用平台证书校验 Wechatpay-Signature 头，
/// 见 https://pay.weixin.qq.com/wiki/doc/apiv3/wechatpay/wechatpay4_2.shtml
/// </summary>
[ApiController, Route("api/pay")]
public class PayNotifyController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPayService _pay;
    public PayNotifyController(AppDbContext db, IPayService pay) { _db = db; _pay = pay; }

    [HttpPost("notify")]
    public async Task<IActionResult> Notify()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch { return BadRequest(new { code = "FAIL", message = "报文解析失败" }); }

        var root = doc.RootElement;
        var eventType = root.GetProperty("event_type").GetString();
        if (eventType != "TRANSACTION.SUCCESS")
            return Ok(new { code = "SUCCESS", message = "忽略的事件类型" });

        var resource = root.GetProperty("resource");
        string plain;
        try
        {
            plain = _pay.DecryptNotifyResource(
                resource.GetProperty("nonce").GetString()!,
                resource.GetProperty("ciphertext").GetString()!,
                resource.GetProperty("associated_data").GetString()!);
        }
        catch
        {
            return BadRequest(new { code = "FAIL", message = "回调解密失败" });
        }

        using var plainDoc = JsonDocument.Parse(plain);
        var outTradeNo = plainDoc.RootElement.GetProperty("out_trade_no").GetString()!;
        var tradeState = plainDoc.RootElement.GetProperty("trade_state").GetString();
        if (tradeState != "SUCCESS")
            return Ok(new { code = "SUCCESS", message = "非支付成功状态" });

        await OrderPaymentConfirmer.ConfirmByOrderNo(_db, outTradeNo);
        return Ok(new { code = "SUCCESS", message = "成功" });
    }
}

/// <summary>支付确认的公共逻辑：状态机条件更新 + 返积分，保证幂等。</summary>
public static class OrderPaymentConfirmer
{
    public static async Task<bool> ConfirmByOrderNo(AppDbContext db, string orderNo)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderNo == orderNo);
        if (order == null) return false;
        return await Confirm(db, order);
    }

    public static async Task<bool> Confirm(AppDbContext db, Models.Order order)
    {
        // 状态机：仅允许 待支付(0) -> 烤制中(1)，重复回调/重复请求影响行数为 0
        var rows = await db.Database.ExecuteSqlRawAsync(
            "UPDATE Orders SET Status = 1, PaidAt = GETDATE() WHERE Id = {0} AND Status = 0", order.Id);
        if (rows == 0) return false; // 已支付或已取消，幂等返回

        var earn = order.PayAmount / 100; // 实付 1 元 = 1 积分
        if (earn > 0)
        {
            var user = await db.Users.FindAsync(order.UserId);
            user!.Points += earn;
            db.PointsRecords.Add(new Models.PointsRecord
            { UserId = order.UserId, Change = earn, Reason = "消费返积分", OrderId = order.Id });
            await db.SaveChangesAsync();
        }
        return true;
    }
}
