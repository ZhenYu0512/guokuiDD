using GuokuiDD.Api.Data;
using GuokuiDD.Api.Dtos;
using GuokuiDD.Api.Models;
using GuokuiDD.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuokuiDD.Api.Controllers;

/// <summary>库存预警 / 打印机绑定 / 优惠券管理 / 门店与轮播</summary>
[ApiController, Route("api/admin"), Authorize(Policy = "Admin")]
public class AdminMiscController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminMiscController(AppDbContext db) => _db = db;

    // ---------- 库存预警 ----------
    [HttpGet("inventory/alerts")]
    public async Task<IActionResult> StockAlerts(int threshold = 10)
    {
        var list = await _db.Products
            .Where(p => p.IsActive && !p.IsSoldOut && p.Stock <= threshold)
            .OrderBy(p => p.Stock)
            .Select(p => new { p.Id, p.Name, p.Stock, p.CategoryId })
            .ToListAsync();
        return Ok(list);
    }

    // ---------- 打印机 ----------
    private static readonly string[] ConnTypes = { "cloud", "usb", "lan" };

    [HttpGet("printers")]
    public async Task<IActionResult> Printers(int storeId = 1) =>
        Ok(await _db.Printers.Where(p => p.StoreId == storeId).ToListAsync());

    [HttpPost("printers")]
    public async Task<IActionResult> CreatePrinter(PrinterReq req)
    {
        if (!ConnTypes.Contains(req.ConnType))
            return BadRequest(new { message = "连接方式须为 cloud/usb/lan" });
        if (req.ConnType == "cloud" && string.IsNullOrWhiteSpace(req.Sn))
            return BadRequest(new { message = "云打印机须填写终端号 SN" });
        if (req.ConnType != "cloud" && string.IsNullOrWhiteSpace(req.Address))
            return BadRequest(new { message = "有线打印机须填写地址（USB 填 COM 口，网口填 IP:端口）" });
        if (req.ConnType == "cloud" && await _db.Printers.AnyAsync(p => p.Sn == req.Sn))
            return BadRequest(new { message = "该终端号已绑定" });
        var p = new Printer
        {
            StoreId = req.StoreId, Name = req.Name, ConnType = req.ConnType,
            Sn = req.Sn ?? "", AppKey = req.AppKey, Address = req.Address, Status = req.Status
        };
        _db.Printers.Add(p); await _db.SaveChangesAsync(); return Ok(p);
    }

    [HttpPut("printers/{id}")]
    public async Task<IActionResult> UpdatePrinter(int id, PrinterReq req)
    {
        var p = await _db.Printers.FindAsync(id); if (p == null) return NotFound();
        p.Name = req.Name; p.ConnType = req.ConnType; p.Sn = req.Sn ?? "";
        p.AppKey = req.AppKey; p.Address = req.Address; p.Status = req.Status;
        await _db.SaveChangesAsync(); return Ok(p);
    }

    /// <summary>测试打印：网口(lan)直接发送 ESC/POS 测试小票；usb/cloud 返回指引。</summary>
    [HttpPost("printers/{id}/test-print")]
    public async Task<IActionResult> TestPrint(int id, [FromServices] EscPosPrinterService escpos)
    {
        var p = await _db.Printers.FindAsync(id); if (p == null) return NotFound();
        switch (p.ConnType)
        {
            case "lan":
                if (string.IsNullOrWhiteSpace(p.Address))
                    return BadRequest(new { message = "请先填写网口地址（IP:端口）" });
                var (ok, message) = await escpos.TestPrintAsync(p.Address, EscPosPrinterService.BuildTestTicket(p.Name));
                return ok ? Ok(new { message }) : BadRequest(new { message });
            case "usb":
                return Ok(new { message = "USB 有线打印机需在门店电脑部署本地打印助手，由助手监听后转发打印（配置已保存）" });
            default:
                return Ok(new { message = "云打印机请通过厂商云打印平台下发测试页" });
        }
    }

    [HttpDelete("printers/{id}")]
    public async Task<IActionResult> DeletePrinter(int id)
    {
        var p = await _db.Printers.FindAsync(id); if (p == null) return NotFound();
        _db.Printers.Remove(p); await _db.SaveChangesAsync(); return Ok();
    }

    // ---------- 优惠券 ----------
    [HttpGet("coupons")]
    public async Task<IActionResult> Coupons() =>
        Ok(await _db.Coupons.OrderByDescending(c => c.Id).ToListAsync());

    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon(CouponReq req)
    {
        var c = new Coupon
        {
            Name = req.Name, ThresholdAmount = req.ThresholdAmount, DiscountAmount = req.DiscountAmount,
            ValidFrom = req.ValidFrom, ValidTo = req.ValidTo, TotalCount = req.TotalCount, IsActive = req.IsActive
        };
        _db.Coupons.Add(c); await _db.SaveChangesAsync(); return Ok(c);
    }

    [HttpPatch("coupons/{id}/toggle")]
    public async Task<IActionResult> ToggleCoupon(int id)
    {
        var c = await _db.Coupons.FindAsync(id); if (c == null) return NotFound();
        c.IsActive = !c.IsActive; await _db.SaveChangesAsync();
        return Ok(new { c.Id, c.IsActive });
    }

    // ---------- 门店与轮播 ----------
    [HttpPut("stores/{id}")]
    public async Task<IActionResult> UpdateStore(int id, Store req)
    {
        var s = await _db.Stores.FindAsync(id); if (s == null) return NotFound();
        s.Name = req.Name; s.Address = req.Address; s.Phone = req.Phone;
        s.BusinessHours = req.BusinessHours; s.Lat = req.Lat; s.Lng = req.Lng; s.Notice = req.Notice;
        await _db.SaveChangesAsync(); return Ok(s);
    }

    [HttpPost("banners")]
    public async Task<IActionResult> CreateBanner(Banner req)
    {
        _db.Banners.Add(req); await _db.SaveChangesAsync(); return Ok(req);
    }

    [HttpDelete("banners/{id}")]
    public async Task<IActionResult> DeleteBanner(int id)
    {
        var b = await _db.Banners.FindAsync(id); if (b == null) return NotFound();
        _db.Banners.Remove(b); await _db.SaveChangesAsync(); return Ok();
    }
}
