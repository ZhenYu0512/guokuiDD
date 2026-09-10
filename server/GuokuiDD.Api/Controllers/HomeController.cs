using GuokuiDD.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuokuiDD.Api.Controllers;

[ApiController, Route("api")]
public class HomeController : ControllerBase
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    /// <summary>首页：门店信息 + 轮播 + 招牌推荐</summary>
    [HttpGet("home")]
    public async Task<IActionResult> Home(int storeId = 1)
    {
        var store = await _db.Stores.FindAsync(storeId);
        if (store == null) return NotFound(new { message = "门店不存在" });
        var banners = await _db.Banners.Where(b => b.StoreId == storeId).OrderBy(b => b.Sort).ToListAsync();
        var featured = await _db.Products
            .Where(p => p.IsFeatured && p.IsActive)
            .OrderBy(p => p.Sort)
            .Select(p => new { p.Id, p.Name, p.ImageUrl, p.BasePrice, p.IsSoldOut })
            .ToListAsync();
        return Ok(new { store, banners, featured });
    }

    /// <summary>点单页：分类 + 商品 + 规格 + 加料</summary>
    [HttpGet("menu")]
    public async Task<IActionResult> Menu(int storeId = 1)
    {
        var categories = await _db.Categories
            .Where(c => c.StoreId == storeId && c.IsActive).OrderBy(c => c.Sort).ToListAsync();
        var catIds = categories.Select(c => c.Id).ToList();
        var products = await _db.Products
            .Where(p => catIds.Contains(p.CategoryId) && p.IsActive).OrderBy(p => p.Sort).ToListAsync();
        var prodIds = products.Select(p => p.Id).ToList();
        var groups = await _db.SpecGroups.Where(g => prodIds.Contains(g.ProductId)).OrderBy(g => g.Sort).ToListAsync();
        var groupIds = groups.Select(g => g.Id).ToList();
        var options = await _db.SpecOptions.Where(o => groupIds.Contains(o.SpecGroupId)).OrderBy(o => o.Sort).ToListAsync();
        var addons = await _db.Addons.Where(a => a.StoreId == storeId && a.IsActive).ToListAsync();

        var result = categories.Select(c => new
        {
            c.Id, c.Name,
            Products = products.Where(p => p.CategoryId == c.Id).Select(p => new
            {
                p.Id, p.Name, p.ImageUrl, p.Description, p.BasePrice, p.Stock, p.IsSoldOut, p.IsFeatured,
                SpecGroups = groups.Where(g => g.ProductId == p.Id).Select(g => new
                {
                    g.Id, g.Name, g.IsRequired,
                    Options = options.Where(o => o.SpecGroupId == g.Id)
                        .Select(o => new { o.Id, o.Name, o.PriceDelta }).ToList()
                }).ToList()
            }).ToList()
        });
        return Ok(new { categories = result, addons });
    }
}
