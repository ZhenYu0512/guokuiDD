using GuokuiDD.Api.Data;
using GuokuiDD.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuokuiDD.Api.Controllers;

[ApiController, Route("api/users"), Authorize(Policy = "User")]
public class UsersController : BaseController
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    /// <summary>我的：用户信息 + 会员卡 + 积分 + 优惠券数量</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var uid = Uid();
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return NotFound();
        var card = await _db.MemberCards.FirstOrDefaultAsync(c => c.UserId == uid);
        var couponCount = await _db.UserCoupons.CountAsync(c => c.UserId == uid && c.Status == 0);
        return Ok(new
        {
            user.Id, user.NickName, user.AvatarUrl, user.Points,
            MemberCard = card == null ? null : new { card.CardNo, card.Level, card.IssuedAt },
            CouponCount = couponCount
        });
    }

    /// <summary>开通会员卡</summary>
    [HttpPost("me/member-card")]
    public async Task<IActionResult> OpenCard()
    {
        var uid = Uid();
        if (await _db.MemberCards.AnyAsync(c => c.UserId == uid))
            return BadRequest(new { message = "已开通会员卡" });
        var card = new MemberCard
        {
            UserId = uid,
            CardNo = "GK" + DateTime.Now.ToString("yyyyMMdd") + uid.ToString("D6")
        };
        _db.MemberCards.Add(card);
        await _db.SaveChangesAsync();
        return Ok(new { card.CardNo, card.Level, card.IssuedAt });
    }

    /// <summary>我的优惠券</summary>
    [HttpGet("me/coupons")]
    public async Task<IActionResult> MyCoupons(int status = 0)
    {
        var uid = Uid();
        var list = await (from uc in _db.UserCoupons
                          join c in _db.Coupons on uc.CouponId equals c.Id
                          where uc.UserId == uid && uc.Status == status
                          orderby uc.Id descending
                          select new
                          {
                              uc.Id, uc.Status, uc.ClaimedAt, uc.UsedAt,
                              c.Name, c.ThresholdAmount, c.DiscountAmount, c.ValidFrom, c.ValidTo
                          }).ToListAsync();
        return Ok(list);
    }

    /// <summary>下单可用优惠券（前端选择器用）</summary>
    [HttpGet("me/coupons/usable")]
    public async Task<IActionResult> UsableCoupons(int totalAmount)
    {
        var uid = Uid();
        var now = DateTime.Now;
        var list = await (from uc in _db.UserCoupons
                          join c in _db.Coupons on uc.CouponId equals c.Id
                          where uc.UserId == uid && uc.Status == 0
                                && c.IsActive && c.ValidFrom <= now && c.ValidTo >= now
                                && totalAmount >= c.ThresholdAmount
                          select new { uc.Id, c.Name, c.ThresholdAmount, c.DiscountAmount, c.ValidTo }).ToListAsync();
        return Ok(list);
    }

    /// <summary>可领取的优惠券</summary>
    [HttpGet("/api/coupons/claimable")]
    public async Task<IActionResult> Claimable()
    {
        var now = DateTime.Now;
        var list = await _db.Coupons
            .Where(c => c.IsActive && c.ValidFrom <= now && c.ValidTo >= now && c.IssuedCount < c.TotalCount)
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>领取优惠券（条件更新防超发）</summary>
    [HttpPost("/api/coupons/{id}/claim")]
    public async Task<IActionResult> Claim(int id)
    {
        var uid = Uid();
        if (await _db.UserCoupons.AnyAsync(x => x.UserId == uid && x.CouponId == id))
            return BadRequest(new { message = "已领取过该券" });
        var rows = await _db.Database.ExecuteSqlRawAsync(
            "UPDATE Coupons SET IssuedCount = IssuedCount + 1 WHERE Id = {0} AND IsActive = 1 AND IssuedCount < TotalCount", id);
        if (rows == 0) return BadRequest(new { message = "优惠券已被领完或不可用" });
        _db.UserCoupons.Add(new UserCoupon { UserId = uid, CouponId = id });
        await _db.SaveChangesAsync();
        return Ok(new { message = "领取成功" });
    }

    /// <summary>积分明细</summary>
    [HttpGet("me/points-records")]
    public async Task<IActionResult> PointsRecords()
    {
        var uid = Uid();
        var list = await _db.PointsRecords.Where(r => r.UserId == uid)
            .OrderByDescending(r => r.Id).Take(100).ToListAsync();
        return Ok(list);
    }
}
