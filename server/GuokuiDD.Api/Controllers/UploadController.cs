using GuokuiDD.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuokuiDD.Api.Controllers;

/// <summary>
/// 图片上传：委托 IImageStorage（火山引擎 TOS，未配置时自动降级占位实现）。
/// 接口契约：POST multipart 字段名 file，查询参数 folder，返回 { url, key }。
/// </summary>
[ApiController, Route("api/admin/upload"), Authorize(Policy = "Admin")]
public class UploadController : ControllerBase
{
    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };
    private const long MaxSize = 5 * 1024 * 1024; // 5MB

    private readonly IImageStorage _storage;
    private readonly ILogger<UploadController> _logger;

    public UploadController(IImageStorage storage, ILogger<UploadController> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string? folder)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "未选择文件" });
        if (file.Length > MaxSize) return BadRequest(new { message = "图片不能超过 5MB" });
        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExt.Contains(ext)) return BadRequest(new { message = "仅支持 jpg/png/webp/gif/bmp" });

        await using var stream = file.OpenReadStream();
        var result = await _storage.UploadAsync(stream, file.FileName, folder);
        if (!result.Success)
        {
            _logger.LogWarning("图片上传失败：{Error}", result.Error);
            return BadRequest(new { message = "上传失败：" + result.Error });
        }
        return Ok(new { url = result.Url, key = result.Key });
    }

    /// <summary>对象存储连通性自检（分别用 s3/tos 两种 service 测试并给出诊断）。</summary>
    [HttpGet("selftest")]
    public async Task<IActionResult> SelfTest()
    {
        if (_storage is TosImageStorage tos)
        {
            var (ok, message) = await tos.SelfTestAsync();
            return Ok(new { ok, message });
        }
        return Ok(new { ok = false, message = "当前为占位实现，未配置对象存储" });
    }
}
