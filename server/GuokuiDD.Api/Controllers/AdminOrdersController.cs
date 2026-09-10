using GuokuiDD.Api.Data;
using GuokuiDD.Api.Dtos;
using GuokuiDD.Api.Models;
using GuokuiDD.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuokuiDD.Api.Controllers;

/// <summary>订单管理：查询各状态订单、状态流转（条件更新保证并发安全）</summary>
[ApiController, Route("api/admin/orders"), Authorize(Policy = "Admin")]
public class AdminOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPayService _pay;
    public AdminOrdersController(AppDbContext db, IPayService pay) { _db = db; _pay = pay; }

    [HttpGet]
    public async Task<IActionResult> List(int? status, string? date, int storeId = 1)
    {
        var query = _db.Orders.Where(o => o.StoreId == storeId);
        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (!string.IsNullOrEmpty(date)) query = query.Where(o => o.PickupDate == date);
        var orders = await query.OrderByDescending(o => o.Id).Take(200).ToListAsync();
        var ids = orders.Select(o => o.Id).ToList();
        var items = await _db.OrderItems.Where(i => ids.Contains(i.OrderId)).ToListAsync();
        var userIds = orders.Select(o => o.UserId).Distinct().ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NickName);
        return Ok(orders.Select(o => new
        {
            o.Id, o.OrderNo, o.Status, o.TotalAmount, o.CouponDiscount, o.PointsUsed, o.PointsDiscount,
            o.PayAmount, o.PickupCode, o.PickupDate, o.ContactName, o.ContactPhone, o.Remark,
            o.CreatedAt, o.PaidAt, o.CancelReason,
            UserNickName = users.GetValueOrDefault(o.UserId),
            Items = items.Where(i => i.OrderId == o.Id).Select(i => new
            {
                i.ProductName, i.UnitPrice, i.Quantity, i.Subtotal, i.SpecsJson, i.AddonsJson
            })
        }));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(int storeId = 1)
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var todayOrders = await _db.Orders.Where(o => o.StoreId == storeId && o.PickupDate == today).ToListAsync();
        return Ok(new
        {
            TodayOrderCount = todayOrders.Count,
            TodayPaidAmount = todayOrders.Where(o => o.Status >= 1 && o.Status <= 3).Sum(o => o.PayAmount),
            PendingCount = await _db.Orders.CountAsync(o => o.StoreId == storeId && o.Status == 0),
            BakingCount = await _db.Orders.CountAsync(o => o.StoreId == storeId && o.Status == 1),
            ReadyCount = await _db.Orders.CountAsync(o => o.StoreId == storeId && o.Status == 2)
        });
    }

    /// <summary>按订单打印小票：网口(lan)直打；云打印/USB 返回指引（厂商平台/本地助手对接点）。</summary>
    [HttpPost("{id}/print")]
    public async Task<IActionResult> PrintOrder(int id, [FromBody] PrintReq req, [FromServices] EscPosPrinterService escpos)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound(new { message = "订单不存在" });
        var printer = await _db.Printers.FindAsync(req.PrinterId);
        if (printer == null) return NotFound(new { message = "打印机不存在" });

        var items = await _db.OrderItems.Where(i => i.OrderId == id).ToListAsync();
        var store = await _db.Stores.FindAsync(order.StoreId);
        var ticket = EscPosPrinterService.BuildOrderTicket(order, items, store?.Name ?? "锅盔肉夹馍");

        switch (printer.ConnType)
        {
            case "lan":
                if (string.IsNullOrWhiteSpace(printer.Address))
                    return BadRequest(new { message = "网口打印机未配置地址" });
                var (ok, message) = await escpos.SendAsync(printer.Address, ticket);
                return ok ? Ok(new { message = "已发送到打印机" }) : BadRequest(new { message });
            case "cloud":
                // 对接点：按厂商云打印开放平台（易联云/芯烨云等）用 Sn+AppKey 下发 ticket
                return Ok(new { message = "云打印下发成功（演示）", tip = "生产环境在此对接厂商云打印 API" });
            default:
                return Ok(new { message = "USB 打印机需门店电脑的本地打印助手转发，已记录任务" });
        }
    }

    /// <summary>状态流转：bake(0->1) ready(1->2) complete(2->3) cancel(0/1->4)</summary>
    [HttpPost("{id}/status")]
    public async Task<IActionResult> ChangeStatus(int id, OrderStatusReq req)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        int rows;
        string sql;
        switch (req.Action)
        {
            case "bake":
                sql = "UPDATE Orders SET Status = 1, PaidAt = ISNULL(PaidAt, GETDATE()) WHERE Id = @p0 AND Status = 0";
                rows = await _db.Database.ExecuteSqlRawAsync(sql, id); break;
            case "ready":
                rows = await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE Orders SET Status = 2 WHERE Id = {0} AND Status = 1", id); break;
            case "complete":
                rows = await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE Orders SET Status = 3, CompletedAt = GETDATE() WHERE Id = {0} AND Status = 2", id); break;
            case "cancel":
                return await CancelWithRefund(order, req.Reason ?? "商家取消");
            default:
                return BadRequest(new { message = "不支持的操作" });
        }
        if (rows == 0) return BadRequest(new { message = "状态流转失败，订单状态已被其他操作变更" });
        return Ok(new { message = "操作成功" });
    }

    private async Task<IActionResult> CancelWithRefund(Order order, string reason)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        // 待支付或烤制中可取消；烤制中需退款
        var rows = await _db.Database.ExecuteSqlRawAsync(
            "UPDATE Orders SET Status = 4, CancelledAt = GETDATE(), CancelReason = {1} WHERE Id = {0} AND Status IN (0, 1)",
            order.Id, reason);
        if (rows == 0) return BadRequest(new { message = "订单当前状态不可取消" });

        if (order.Status == OrderStatus.Baking)
        {
            var refund = await _pay.RefundAsync(order, reason);
            if (!refund.Success) return BadRequest(new { message = "退款失败：" + refund.Message });
        }
        // 回补库存 / 券 / 积分
        var items = await _db.OrderItems.Where(i => i.OrderId == order.Id).ToListAsync();
        foreach (var i in items)
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE Products SET Stock = Stock + {0} WHERE Id = {1}", i.Quantity, i.ProductId);
        if (order.UserCouponId.HasValue)
        {
            var uc = await _db.UserCoupons.FindAsync(order.UserCouponId.Value);
            if (uc != null && uc.Status == 1) { uc.Status = 0; uc.OrderId = null; uc.UsedAt = null; }
        }
        if (order.PointsUsed > 0)
        {
            var user = await _db.Users.FindAsync(order.UserId);
            user!.Points += order.PointsUsed;
            _db.PointsRecords.Add(new PointsRecord { UserId = order.UserId, Change = order.PointsUsed, Reason = "订单取消退回积分", OrderId = order.Id });
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "已取消" });
    }
}
