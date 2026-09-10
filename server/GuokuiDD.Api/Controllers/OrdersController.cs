using System.Text.Json;
using GuokuiDD.Api.Data;
using GuokuiDD.Api.Dtos;
using GuokuiDD.Api.Models;
using GuokuiDD.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuokuiDD.Api.Controllers;

[ApiController, Route("api/orders"), Authorize(Policy = "User")]
public class OrdersController : BaseController
{
    private const int MaxAddons = 3; // 加料多选上限 3 个（服务端强制）

    private readonly AppDbContext _db;
    private readonly IPayService _pay;
    public OrdersController(AppDbContext db, IPayService pay) { _db = db; _pay = pay; }

    private static OrderDto ToDto(Order o, List<OrderItem> items) => new(
        o.Id, o.OrderNo, o.Status, o.TotalAmount, o.CouponDiscount, o.PointsUsed, o.PointsDiscount,
        o.PayAmount, o.PickupCode, o.ContactName, o.ContactPhone, o.Remark, o.CreatedAt,
        items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.ProductImage, i.BasePrice,
            JsonSerializer.Deserialize<object>(i.SpecsJson)!, JsonSerializer.Deserialize<object>(i.AddonsJson)!,
            i.UnitPrice, i.Quantity, i.Subtotal)).ToList());

    /// <summary>
    /// 创建订单。金额全部由后端重算，前端传入金额不可信。
    /// 防超卖：单条条件 UPDATE 扣减库存；订单明细全量快照。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderReq req)
    {
        var uid = Uid();
        if (req.Items.Count == 0) return BadRequest(new { message = "订单不能为空" });
        if (string.IsNullOrWhiteSpace(req.ContactName) || string.IsNullOrWhiteSpace(req.ContactPhone))
            return BadRequest(new { message = "请填写自提联系人信息" });

        var user = await _db.Users.FindAsync(uid);
        if (user == null) return Unauthorized();

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var orderItems = new List<OrderItem>();
            int totalAmount = 0;

            foreach (var item in req.Items)
            {
                if (item.Quantity <= 0 || item.Quantity > 99)
                    return BadRequest(new { message = "商品数量不合法" });

                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId && p.IsActive);
                if (product == null) return BadRequest(new { message = $"商品不存在或已下架" });
                if (product.IsSoldOut) return Conflict(new { message = $"「{product.Name}」已售罄" });

                // 防超卖：条件 UPDATE，库存不足时影响行数为 0
                var affected = await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE Products SET Stock = Stock - {0} WHERE Id = {1} AND Stock >= {0}",
                    item.Quantity, product.Id);
                if (affected == 0) return Conflict(new { message = $"「{product.Name}」库存不足" });

                // 规格校验：每个必选组恰好选 1 项（单选）
                var groups = await _db.SpecGroups.Where(g => g.ProductId == product.Id).ToListAsync();
                var groupIds = groups.Select(g => g.Id).ToList();
                var allOptions = await _db.SpecOptions.Where(o => groupIds.Contains(o.SpecGroupId)).ToListAsync();
                var chosenOptions = allOptions.Where(o => item.SpecOptionIds.Contains(o.Id)).ToList();
                var specSnapshot = new List<object>();
                int specDelta = 0;
                foreach (var g in groups)
                {
                    var chosen = chosenOptions.Where(o => o.SpecGroupId == g.Id).ToList();
                    if (g.IsRequired && chosen.Count != 1)
                        return BadRequest(new { message = $"「{product.Name}」请选择{g.Name}" });
                    if (chosen.Count > 1)
                        return BadRequest(new { message = $"「{product.Name}」{g.Name}只能单选" });
                    foreach (var o in chosen)
                    {
                        specDelta += o.PriceDelta;
                        specSnapshot.Add(new { group = g.Name, option = o.Name, priceDelta = o.PriceDelta });
                    }
                }

                // 加料校验：上限 3 个
                var addonIds = item.AddonIds.Distinct().ToList();
                if (addonIds.Count > MaxAddons)
                    return BadRequest(new { message = $"「{product.Name}」加料最多选择 {MaxAddons} 个" });
                var addons = await _db.Addons.Where(a => addonIds.Contains(a.Id) && a.IsActive).ToListAsync();
                if (addons.Count != addonIds.Count)
                    return BadRequest(new { message = "存在无效的加料项" });
                int addonPrice = addons.Sum(a => a.Price);
                var addonSnapshot = addons.Select(a => new { a.Name, a.Price }).Cast<object>().ToList();

                int unitPrice = product.BasePrice + specDelta + addonPrice;
                int subtotal = unitPrice * item.Quantity;
                totalAmount += subtotal;

                // 全量快照
                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductImage = product.ImageUrl,
                    BasePrice = product.BasePrice,
                    SpecsJson = JsonSerializer.Serialize(specSnapshot),
                    AddonsJson = JsonSerializer.Serialize(addonSnapshot),
                    UnitPrice = unitPrice,
                    Quantity = item.Quantity,
                    Subtotal = subtotal
                });
            }

            // 优惠券（后端校验门槛与归属）
            int couponDiscount = 0;
            if (req.UserCouponId.HasValue)
            {
                var uc = await _db.UserCoupons.FirstOrDefaultAsync(x => x.Id == req.UserCouponId && x.UserId == uid && x.Status == 0);
                if (uc == null) return BadRequest(new { message = "优惠券不可用" });
                var coupon = await _db.Coupons.FindAsync(uc.CouponId);
                var now = DateTime.Now;
                if (coupon == null || !coupon.IsActive || coupon.ValidFrom > now || coupon.ValidTo < now)
                    return BadRequest(new { message = "优惠券已失效" });
                if (totalAmount < coupon.ThresholdAmount)
                    return BadRequest(new { message = "未达到优惠券使用门槛" });
                couponDiscount = coupon.DiscountAmount;
            }

            // 积分抵扣：1 积分 = 1 分
            int afterCoupon = totalAmount - couponDiscount;
            int pointsUsed = Math.Max(0, Math.Min(req.PointsUsed, Math.Min(user.Points, afterCoupon)));
            int pointsDiscount = pointsUsed;
            int payAmount = Math.Max(0, afterCoupon - pointsDiscount);

            // 取餐码：4位数字，门店当日唯一
            var today = DateTime.Now.ToString("yyyyMMdd");
            var usedCodes = (await _db.Orders
                .Where(o => o.StoreId == req.StoreId && o.PickupDate == today && o.PickupCode != null)
                .Select(o => o.PickupCode!).ToListAsync()).ToHashSet();
            if (usedCodes.Count >= 10000) return Conflict(new { message = "今日取餐码已发完" });
            var rnd = new Random();
            string pickupCode;
            do { pickupCode = rnd.Next(0, 10000).ToString("D4"); } while (usedCodes.Contains(pickupCode));

            var order = new Order
            {
                OrderNo = "GK" + DateTime.Now.ToString("yyyyMMddHHmmss") + rnd.Next(1000, 9999),
                UserId = uid,
                StoreId = req.StoreId,
                Status = OrderStatus.Pending,
                TotalAmount = totalAmount,
                CouponDiscount = couponDiscount,
                UserCouponId = req.UserCouponId,
                PointsUsed = pointsUsed,
                PointsDiscount = pointsDiscount,
                PayAmount = payAmount,
                PickupCode = pickupCode,
                PickupDate = today,
                ContactName = req.ContactName,
                ContactPhone = req.ContactPhone,
                Remark = req.Remark
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            foreach (var oi in orderItems) oi.OrderId = order.Id;
            _db.OrderItems.AddRange(orderItems);

            // 核销优惠券
            if (req.UserCouponId.HasValue)
            {
                var uc = await _db.UserCoupons.FindAsync(req.UserCouponId.Value);
                uc!.Status = 1; uc.OrderId = order.Id; uc.UsedAt = DateTime.Now;
            }
            // 扣积分 + 流水
            if (pointsUsed > 0)
            {
                user.Points -= pointsUsed;
                _db.PointsRecords.Add(new PointsRecord { UserId = uid, Change = -pointsUsed, Reason = "下单抵扣", OrderId = order.Id });
            }
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { order.Id, order.OrderNo, order.PayAmount, order.PickupCode });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>订单列表（按状态 tab）</summary>
    [HttpGet]
    public async Task<IActionResult> List(int? status)
    {
        var uid = Uid();
        var query = _db.Orders.Where(o => o.UserId == uid);
        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        var orders = await query.OrderByDescending(o => o.Id).Take(50).ToListAsync();
        var ids = orders.Select(o => o.Id).ToList();
        var items = await _db.OrderItems.Where(i => ids.Contains(i.OrderId)).ToListAsync();
        return Ok(orders.Select(o => ToDto(o, items.Where(i => i.OrderId == o.Id).ToList())));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(int id)
    {
        var uid = Uid();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        if (order == null) return NotFound();
        var items = await _db.OrderItems.Where(i => i.OrderId == id).ToListAsync();
        return Ok(ToDto(order, items));
    }

    /// <summary>
    /// 发起支付（幂等）。
    /// - Mock 模式：直接按状态机确认支付（开发用）。
    /// - 微信 V3：JSAPI 下单，返回 wx.requestPayment 参数；订单状态由支付回调 / sync-pay 确认，
    ///   确认动作是条件 UPDATE（待支付(0)->烤制中(1)），重复请求/重复回调不会重复生效。
    /// </summary>
    [HttpPost("{id}/pay")]
    public async Task<IActionResult> Pay(int id)
    {
        var uid = Uid();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        if (order == null) return NotFound();

        if (order.Status >= OrderStatus.Baking && order.Status != OrderStatus.Cancelled)
            return Ok(new { mode = "wechat", paid = true, already = true, order.PickupCode }); // 幂等：已支付
        if (order.Status != OrderStatus.Pending)
            return BadRequest(new { message = "订单状态不允许支付" });

        var user = await _db.Users.FindAsync(uid);
        PrepayResult prepay;
        try
        {
            prepay = await _pay.CreatePrepayAsync(order, user!.OpenId);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "微信支付下单失败：" + ex.Message });
        }

        if (prepay.Mode == "mock")
        {
            await OrderPaymentConfirmer.Confirm(_db, order);
            return Ok(new { mode = "mock", paid = true, already = false, order.PickupCode });
        }
        return Ok(new { mode = "wechat", paid = false, payParams = prepay.PayParams });
    }

    /// <summary>
    /// 主动对账确认（回调不可达时的兜底，如本地开发）：向微信查询订单支付状态，
    /// 已支付则按状态机确认（幂等）。
    /// </summary>
    [HttpPost("{id}/sync-pay")]
    public async Task<IActionResult> SyncPay(int id)
    {
        var uid = Uid();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        if (order == null) return NotFound();
        if (order.Status >= OrderStatus.Baking && order.Status != OrderStatus.Cancelled)
            return Ok(new { paid = true, already = true, order.PickupCode });
        if (order.Status != OrderStatus.Pending)
            return BadRequest(new { message = "订单状态不允许支付确认" });

        var result = await _pay.QueryOrderAsync(order.OrderNo);
        if (!result.Paid) return Ok(new { paid = false });

        var confirmed = await OrderPaymentConfirmer.Confirm(_db, order);
        return Ok(new { paid = true, already = !confirmed, order.PickupCode });
    }

    /// <summary>取消订单（仅待支付）。条件更新保证只取消一次，并回补库存/券/积分。</summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] OrderStatusReq? req)
    {
        var uid = Uid();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);
        if (order == null) return NotFound();

        await using var tx = await _db.Database.BeginTransactionAsync();
        var rows = await _db.Database.ExecuteSqlRawAsync(
            "UPDATE Orders SET Status = 4, CancelledAt = GETDATE(), CancelReason = {1} WHERE Id = {0} AND Status = 0",
            id, req?.Reason ?? "用户取消");
        if (rows == 0)
            return BadRequest(new { message = "订单当前状态不可取消" });

        await RollbackResources(order, uid);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "已取消" });
    }

    internal async Task RollbackResources(Order order, int uid)
    {
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
            var user = await _db.Users.FindAsync(uid);
            user!.Points += order.PointsUsed;
            _db.PointsRecords.Add(new PointsRecord { UserId = uid, Change = order.PointsUsed, Reason = "取消订单退回积分", OrderId = order.Id });
        }
    }
}
