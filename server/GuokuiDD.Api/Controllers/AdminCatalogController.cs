using GuokuiDD.Api.Data;
using GuokuiDD.Api.Dtos;
using GuokuiDD.Api.Models;
using GuokuiDD.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuokuiDD.Api.Controllers;

/// <summary>商品管理：分类 / 商品 / 规格 / 加料 / 售罄状态</summary>
[ApiController, Route("api/admin/catalog"), Authorize(Policy = "Admin")]
public class AdminCatalogController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminCatalogController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(int storeId = 1)
    {
        var categories = await _db.Categories.Where(c => c.StoreId == storeId).OrderBy(c => c.Sort).ToListAsync();
        var products = await _db.Products.ToListAsync();
        var groups = await _db.SpecGroups.ToListAsync();
        var options = await _db.SpecOptions.ToListAsync();
        var addons = await _db.Addons.Where(a => a.StoreId == storeId).ToListAsync();
        return Ok(new { categories, products, specGroups = groups, specOptions = options, addons });
    }

    // ---------- 分类 ----------
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(CategoryReq req)
    {
        var c = new Category { StoreId = req.StoreId, Name = req.Name, Sort = req.Sort, IsActive = req.IsActive };
        _db.Categories.Add(c); await _db.SaveChangesAsync(); return Ok(c);
    }

    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(int id, CategoryReq req)
    {
        var c = await _db.Categories.FindAsync(id); if (c == null) return NotFound();
        c.Name = req.Name; c.Sort = req.Sort; c.IsActive = req.IsActive;
        await _db.SaveChangesAsync(); return Ok(c);
    }

    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        if (await _db.Products.AnyAsync(p => p.CategoryId == id))
            return BadRequest(new { message = "该分类下存在商品，无法删除" });
        var c = await _db.Categories.FindAsync(id); if (c == null) return NotFound();
        _db.Categories.Remove(c); await _db.SaveChangesAsync(); return Ok();
    }

    // ---------- 商品 ----------
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct(ProductReq req, [FromServices] IImageStorage images)
    {
        var p = new Product
        {
            CategoryId = req.CategoryId, Name = req.Name,
            ImageUrl = images.ResolveImage(req.ImageUrl, req.Name), // 无图时用占位图，后续可换对象存储
            Description = req.Description, BasePrice = req.BasePrice, Stock = req.Stock,
            IsSoldOut = req.IsSoldOut, IsFeatured = req.IsFeatured, Sort = req.Sort, IsActive = req.IsActive
        };
        _db.Products.Add(p); await _db.SaveChangesAsync(); return Ok(p);
    }

    [HttpPut("products/{id}")]
    public async Task<IActionResult> UpdateProduct(int id, ProductReq req)
    {
        var p = await _db.Products.FindAsync(id); if (p == null) return NotFound();
        p.CategoryId = req.CategoryId; p.Name = req.Name;
        if (!string.IsNullOrWhiteSpace(req.ImageUrl)) p.ImageUrl = req.ImageUrl; // 未传图则保留原图
        p.Description = req.Description; p.BasePrice = req.BasePrice; p.Stock = req.Stock;
        p.IsSoldOut = req.IsSoldOut; p.IsFeatured = req.IsFeatured; p.Sort = req.Sort; p.IsActive = req.IsActive;
        await _db.SaveChangesAsync(); return Ok(p);
    }

    [HttpDelete("products/{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var p = await _db.Products.FindAsync(id); if (p == null) return NotFound();
        p.IsActive = false; // 软删除，保留订单快照关联
        await _db.SaveChangesAsync(); return Ok();
    }

    /// <summary>售罄开关（小程序端显示遮罩）</summary>
    [HttpPatch("products/{id}/soldout")]
    public async Task<IActionResult> SetSoldOut(int id, SoldOutReq req)
    {
        var p = await _db.Products.FindAsync(id); if (p == null) return NotFound();
        p.IsSoldOut = req.IsSoldOut; await _db.SaveChangesAsync();
        return Ok(new { p.Id, p.IsSoldOut });
    }

    [HttpPatch("products/{id}/stock")]
    public async Task<IActionResult> SetStock(int id, StockReq req)
    {
        var p = await _db.Products.FindAsync(id); if (p == null) return NotFound();
        p.Stock = req.Stock; await _db.SaveChangesAsync();
        return Ok(new { p.Id, p.Stock });
    }

    // ---------- 规格 ----------
    [HttpPost("spec-groups")]
    public async Task<IActionResult> CreateSpecGroup(SpecGroupReq req)
    {
        var g = new SpecGroup { ProductId = req.ProductId, Name = req.Name, IsRequired = req.IsRequired, Sort = req.Sort };
        _db.SpecGroups.Add(g); await _db.SaveChangesAsync(); return Ok(g);
    }

    [HttpDelete("spec-groups/{id}")]
    public async Task<IActionResult> DeleteSpecGroup(int id)
    {
        var g = await _db.SpecGroups.FindAsync(id); if (g == null) return NotFound();
        _db.SpecOptions.RemoveRange(_db.SpecOptions.Where(o => o.SpecGroupId == id));
        _db.SpecGroups.Remove(g); await _db.SaveChangesAsync(); return Ok();
    }

    [HttpPost("spec-options")]
    public async Task<IActionResult> CreateSpecOption(SpecOptionReq req)
    {
        var o = new SpecOption { SpecGroupId = req.SpecGroupId, Name = req.Name, PriceDelta = req.PriceDelta, Sort = req.Sort };
        _db.SpecOptions.Add(o); await _db.SaveChangesAsync(); return Ok(o);
    }

    [HttpDelete("spec-options/{id}")]
    public async Task<IActionResult> DeleteSpecOption(int id)
    {
        var o = await _db.SpecOptions.FindAsync(id); if (o == null) return NotFound();
        _db.SpecOptions.Remove(o); await _db.SaveChangesAsync(); return Ok();
    }

    // ---------- 加料 ----------
    [HttpPost("addons")]
    public async Task<IActionResult> CreateAddon(AddonReq req)
    {
        var a = new Addon { StoreId = req.StoreId, Name = req.Name, Price = req.Price, IsActive = req.IsActive };
        _db.Addons.Add(a); await _db.SaveChangesAsync(); return Ok(a);
    }

    [HttpPut("addons/{id}")]
    public async Task<IActionResult> UpdateAddon(int id, AddonReq req)
    {
        var a = await _db.Addons.FindAsync(id); if (a == null) return NotFound();
        a.Name = req.Name; a.Price = req.Price; a.IsActive = req.IsActive;
        await _db.SaveChangesAsync(); return Ok(a);
    }

    [HttpDelete("addons/{id}")]
    public async Task<IActionResult> DeleteAddon(int id)
    {
        var a = await _db.Addons.FindAsync(id); if (a == null) return NotFound();
        _db.Addons.Remove(a); await _db.SaveChangesAsync(); return Ok();
    }
}
