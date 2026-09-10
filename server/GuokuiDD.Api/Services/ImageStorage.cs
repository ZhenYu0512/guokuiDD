using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace GuokuiDD.Api.Services;

/// <summary>
/// 图片存储抽象。
/// 现有占位实现 <see cref="PlaceholderImageStorage"/> 保留为兜底；
/// 新增 <see cref="TosImageStorage"/> 直传火山引擎 TOS（SigV4 签名，零第三方依赖）。
/// </summary>
public interface IImageStorage
{
    /// <summary>图片为空时给出占位图；非空原样返回（保持旧行为不变）。</summary>
    string ResolveImage(string? imageUrl, string seed);

    /// <summary>是否启用真实对象存储（未配置时走占位实现）。</summary>
    bool Enabled { get; }

    /// <summary>上传图片流，返回可访问 URL。</summary>
    Task<ImageUploadResult> UploadAsync(Stream content, string fileName, string? folder = null);

    /// <summary>按 URL 删除对象（替换旧图时清理）。</summary>
    Task<bool> DeleteAsync(string? imageUrl);
}

public class ImageUploadResult
{
    public bool Success { get; set; }
    public string? Url { get; set; }
    public string? Key { get; set; }
    public string? Error { get; set; }

    public static ImageUploadResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// 原占位实现：返回 picsum 占位图。
/// 对象存储未配置 / 上传失败时自动降级，保证业务不中断。
/// </summary>
public class PlaceholderImageStorage : IImageStorage
{
    private readonly string _base;
    public PlaceholderImageStorage(IConfiguration cfg)
    {
        _base = cfg["Image:PlaceholderBase"] ?? "https://picsum.photos/seed";
    }

    public bool Enabled => false;

    public string ResolveImage(string? imageUrl, string seed)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl)) return imageUrl;
        return $"{_base}/{Uri.EscapeDataString(seed)}/400/400";
    }

    public Task<ImageUploadResult> UploadAsync(Stream content, string fileName, string? folder = null)
        => Task.FromResult(ImageUploadResult.Fail("对象存储未配置，当前为占位实现"));

    public Task<bool> DeleteAsync(string? imageUrl) => Task.FromResult(false);
}

/// <summary>
/// 火山引擎 TOS 实现（AWS SigV4 直传，零第三方依赖）。
/// 配置节点 appsettings.json -> ObjectStorage。
///
/// ⚠️ TOS 有两套【互斥】的访问体系，算法 / service / scope 结尾必须成套使用，不能混搭：
///   ① 原生 TOS：endpoint=tos-cn-shanghai.volces.com      算法=TOS4-HMAC-SHA256  service=tos  scope 结尾=request       头=x-tos-*
///   ② S3 兼容：endpoint=tos-s3-cn-shanghai.volces.com    算法=AWS4-HMAC-SHA256  service=s3   scope 结尾=aws4_request  头=x-amz-*
/// 本实现走 ②（AWS SigV4 + S3 专属端点），因此 service 必须为 "s3"。
/// 若填成 "tos"，服务端会返回 AuthorizationHeaderMalformed: incorrect service。
///
/// 注：S3 协议下 TOS 仅支持 virtual-hosted-style（{bucket}.{endpoint}），不支持 path-style，本实现已符合。
/// </summary>
public class TosImageStorage : IImageStorage
{
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string ScopeTerminator = "aws4_request";
    private const string DefaultService = "s3";

    // 静态 HttpClient：避免频繁创建导致端口耗尽
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly string _ak;
    private readonly string _sk;
    private readonly string _region;
    private readonly string _bucket;
    private readonly string _endpoint;      // 签名/请求用的 S3 兼容端点：tos-s3-cn-shanghai.volces.com
    private readonly string _publicEndpoint;// 对外访问用的原生端点：tos-cn-shanghai.volces.com
    private readonly string _service;       // S3 协议下恒为 "s3"
    private readonly string _baseUrl;       // https://whgk.tos-cn-shanghai.volces.com
    private readonly string _placeholderBase;

    public TosImageStorage(IConfiguration cfg)
    {
        var s = cfg.GetSection("ObjectStorage");
        _ak = s["AccessKeyId"] ?? "";
        _sk = s["AccessKeySecret"] ?? "";
        _region = s["Region"] ?? "";
        _bucket = s["BucketName"] ?? "";

        var rawEndpoint = (s["Endpoint"] ?? "").Trim();
        _publicEndpoint = StripScheme(rawEndpoint);
        // AWS SigV4 只能打 S3 兼容端点，这里强制归一化，避免配成原生端点导致 Unsupported Authorization Type
        _endpoint = NormalizeToS3Endpoint(rawEndpoint);

        _service = (s["Service"] ?? "").Trim().ToLowerInvariant() is { Length: > 0 } sv ? sv : DefaultService;

        _baseUrl = (s["BaseUrl"] ?? "").TrimEnd('/');
        _placeholderBase = cfg["Image:PlaceholderBase"] ?? "https://picsum.photos/seed";

        // 未显式配 BaseUrl 时，用原生端点拼公网访问地址（S3 端点仅用于 API 签名，不适合作为对外图片 URL）
        if (string.IsNullOrWhiteSpace(_baseUrl) && !string.IsNullOrWhiteSpace(_bucket) && !string.IsNullOrWhiteSpace(_publicEndpoint))
            _baseUrl = $"https://{_bucket}.{_publicEndpoint}";
    }

    public bool Enabled =>
        !string.IsNullOrWhiteSpace(_ak) && !string.IsNullOrWhiteSpace(_sk)
        && !string.IsNullOrWhiteSpace(_bucket) && !string.IsNullOrWhiteSpace(_endpoint);

    /// <summary>实际用于签名的 S3 端点（自检/排障时打印出来）。</summary>
    public string SigningEndpoint => _endpoint;

    /// <summary>签名用的 service（S3 协议下应为 s3）。</summary>
    public string SigningService => _service;

    // ================= 对外方法 =================

    /// <summary>
    /// 保持与原占位实现完全一致的行为：有图原样返回，无图用 picsum 占位。
    /// 这里刻意不返回桶内占位图——桶里若没有该文件会 404，
    /// TOS 只负责"真实上传的图"，占位仍走公开 CDN，最稳。
    /// </summary>
    public string ResolveImage(string? imageUrl, string seed)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl)) return imageUrl;
        return $"{_placeholderBase}/{Uri.EscapeDataString(seed)}/400/400";
    }

    public async Task<ImageUploadResult> UploadAsync(Stream content, string fileName, string? folder = null)
    {
        if (!Enabled) return ImageUploadResult.Fail("对象存储未配置");

        try
        {
            var key = BuildKey(fileName, folder);
            var contentType = GuessContentType(fileName);

            // 读入内存：既要算 SHA256（签名需要），也便于失败重试
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms);
            var bytes = ms.ToArray();
            if (bytes.Length == 0) return ImageUploadResult.Fail("文件内容为空");

            // 首选 service
            var result = await PutOnceAsync(bytes, contentType, key, _service);
            if (result.Success) return result;

            // 兜底：若服务端抱怨 service 不对，用另一个值重试一次（自愈，避免再次改配置重启）
            if (IsServiceMismatch(result.Error))
            {
                var alt = _service == "s3" ? "tos" : "s3";
                var retry = await PutOnceAsync(bytes, contentType, key, alt);
                if (retry.Success) return retry;
                return ImageUploadResult.Fail(
                    $"主 service={_service} 失败：{result.Error}　｜　备用 service={alt} 失败：{retry.Error}");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ImageUploadResult.Fail("上传异常：" + ex.Message);
        }
    }

    public async Task<bool> DeleteAsync(string? imageUrl)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(imageUrl)) return false;
        var key = TryExtractKey(imageUrl);
        if (key == null) return false;

        try
        {
            return await DeleteOnceAsync(key, _service)
                   || await DeleteOnceAsync(key, _service == "s3" ? "tos" : "s3");
        }
        catch { return false; }
    }

    /// <summary>
    /// 连通性自检：列一下桶（ListObjects）。
    /// 会【分别用 s3 和 tos 两个 service 各测一次】，直接告诉你哪个可用，
    /// 用于一次性定位 AK/SK、Endpoint、Region、service 是否匹配。
    /// </summary>
    public async Task<(bool Ok, string Message)> SelfTestAsync()
    {
        if (!Enabled) return (false, "对象存储未配置");

        var host = $"{_bucket}.{_endpoint}";
        var tried = new List<string>();

        foreach (var svc in new[] { "s3", "tos" })
        {
            string msg;
            bool ok;
            try
            {
                (ok, msg) = await ListOnceAsync(svc);
            }
            catch (Exception ex)
            {
                ok = false; msg = "异常：" + ex.Message;
            }

            tried.Add($"service={svc} → {(ok ? "✅ 可用" : "❌ " + msg)}");
            if (ok)
            {
                return (true,
                    $"连接成功。可用 service={svc}，签名端点={host}，Region={_region}，Bucket={_bucket}。" +
                    $"请确保 appsettings.json 的 ObjectStorage:Service 设为 \"{svc}\"。");
            }
        }

        return (false,
            $"两种 service 均失败。签名端点={host}，Region={_region}，Bucket={_bucket}。" +
            $"明细：{string.Join("　｜　", tried)}");
    }

    // ================= 单次请求（可按 service 参数重试）=================

    private async Task<ImageUploadResult> PutOnceAsync(byte[] bytes, string contentType, string key, string service)
    {
        var payloadHash = Sha256Hex(bytes);
        var host = $"{_bucket}.{_endpoint}";
        var uri = new Uri($"https://{host}/{key}");
        var amzDate = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["content-type"] = contentType,
            ["host"] = host,
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = amzDate
        };

        var auth = BuildAuthorization("PUT", uri.AbsolutePath, headers, payloadHash, "", service);

        using var req = new HttpRequestMessage(HttpMethod.Put, uri);
        req.Content = new ByteArrayContent(bytes);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        req.Headers.Host = host;  // 用强类型属性，避免 TryAddWithoutValidation 产生重复 Host 头
        req.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        req.Headers.TryAddWithoutValidation("Authorization", auth);

        using var resp = await Http.SendAsync(req);
        if (resp.IsSuccessStatusCode)
            return new ImageUploadResult { Success = true, Url = $"{_baseUrl}/{key}", Key = key };

        var detail = await resp.Content.ReadAsStringAsync();
        return ImageUploadResult.Fail(Explain((int)resp.StatusCode, detail, service));
    }

    private async Task<bool> DeleteOnceAsync(string key, string service)
    {
        var host = $"{_bucket}.{_endpoint}";
        var uri = new Uri($"https://{host}/{key}");
        var emptyHash = Sha256Hex(Array.Empty<byte>());
        var amzDate = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = host,
            ["x-amz-content-sha256"] = emptyHash,
            ["x-amz-date"] = amzDate
        };

        var auth = BuildAuthorization("DELETE", uri.AbsolutePath, headers, emptyHash, "", service);

        using var req = new HttpRequestMessage(HttpMethod.Delete, uri);
        req.Headers.Host = host;  // 用强类型属性，避免 TryAddWithoutValidation 产生重复 Host 头
        req.Headers.TryAddWithoutValidation("x-amz-content-sha256", emptyHash);
        req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        req.Headers.TryAddWithoutValidation("Authorization", auth);

        using var resp = await Http.SendAsync(req);
        return resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.NotFound;
    }

    private async Task<(bool Ok, string Message)> ListOnceAsync(string service)
    {
        var host = $"{_bucket}.{_endpoint}";
        // virtual-hosted-style，ListObjects V2：GET /?list-type=2&max-keys=1
        var uri = new Uri($"https://{host}/?list-type=2&max-keys=1");
        var emptyHash = Sha256Hex(Array.Empty<byte>());
        var amzDate = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = host,
            ["x-amz-content-sha256"] = emptyHash,
            ["x-amz-date"] = amzDate
        };

        var auth = BuildAuthorization("GET", "/", headers, emptyHash, "list-type=2&max-keys=1", service);

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        req.Headers.Host = host;  // 用强类型属性，避免 TryAddWithoutValidation 产生重复 Host 头
        req.Headers.TryAddWithoutValidation("x-amz-content-sha256", emptyHash);
        req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        req.Headers.TryAddWithoutValidation("Authorization", auth);

        using var resp = await Http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (resp.IsSuccessStatusCode) return (true, $"HTTP {(int)resp.StatusCode}");
        return (false, Explain((int)resp.StatusCode, body, service));
    }

    // ================= SigV4 签名 =================

    private string BuildAuthorization(
        string method,
        string canonicalUri,
        SortedDictionary<string, string> headers,
        string payloadHash,
        string canonicalQueryString,
        string service)
    {
        var amzDate = headers["x-amz-date"];
        var dateStamp = amzDate.Substring(0, 8);

        var canonicalHeaders = new StringBuilder();
        var signedParts = new List<string>();
        foreach (var kv in headers) // SortedDictionary 已按 key 升序
        {
            canonicalHeaders.Append(kv.Key).Append(':').Append(kv.Value.Trim()).Append('\n');
            signedParts.Add(kv.Key);
        }
        var signedHeaders = string.Join(";", signedParts);

        var canonicalRequest =
            method + '\n' +
            canonicalUri + '\n' +
            canonicalQueryString + '\n' +
            canonicalHeaders +
            '\n' + signedHeaders + '\n' +
            payloadHash;

        var scope = $"{dateStamp}/{_region}/{service}/{ScopeTerminator}";
        var stringToSign =
            Algorithm + '\n' +
            amzDate + '\n' +
            scope + '\n' +
            Sha256Hex(Encoding.UTF8.GetBytes(canonicalRequest));

        var signingKey = DeriveSigningKey(dateStamp, service);
        var signature = HmacHex(signingKey, stringToSign);

        return $"{Algorithm} Credential={_ak}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}";
    }

    private byte[] DeriveSigningKey(string dateStamp, string service)
    {
        var k = Hmac(Encoding.UTF8.GetBytes("AWS4" + _sk), dateStamp);
        k = Hmac(k, _region);
        k = Hmac(k, service);
        k = Hmac(k, ScopeTerminator);
        return k;
    }

    private static byte[] Hmac(byte[] key, string data)
        => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static string HmacHex(byte[] key, string data)
        => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

    private static string Sha256Hex(byte[] data)
        => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    // ================= 端点处理 =================

    private static string StripScheme(string endpoint)
    {
        var e = (endpoint ?? "").Trim();
        if (e.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) e = e[8..];
        else if (e.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) e = e[7..];
        return e.TrimEnd('/');
    }

    /// <summary>
    /// 把任意形态的 TOS 端点归一化成 S3 兼容端点。
    /// tos-cn-shanghai.volces.com -> tos-s3-cn-shanghai.volces.com
    /// 已是 S3 端点则原样返回。
    /// </summary>
    private static string NormalizeToS3Endpoint(string endpoint)
    {
        var e = StripScheme(endpoint);
        if (string.IsNullOrWhiteSpace(e)) return "";
        if (e.Contains("-s3-", StringComparison.OrdinalIgnoreCase)) return e;

        var idx = e.IndexOf("tos-", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) e = e[..idx] + "tos-s3-" + e[(idx + 4)..];
        return e;
    }

    private static bool IsServiceMismatch(string? error)
        => error != null && error.Contains("AuthorizationHeaderMalformed", StringComparison.OrdinalIgnoreCase);

    /// <summary>把 TOS 的原始报错翻译成人话，并给出可执行的排查建议。</summary>
    private string Explain(int statusCode, string detail, string service)
    {
        var body = Trim(detail);
        var host = $"{_bucket}.{_endpoint}";
        var scope = $"{{date}}/{_region}/{service}/{ScopeTerminator}";

        if (body.Contains("Unsupported Authorization Type", StringComparison.OrdinalIgnoreCase)
            || body.Contains("0002-00000002", StringComparison.OrdinalIgnoreCase))
        {
            return $"签名类型不被接受：本实现用 AWS4-HMAC-SHA256，必须打 S3 兼容端点。"
                 + $"当前签名端点={host}；请确认 ObjectStorage:Endpoint 形如 tos-s3-{_region}.volces.com。原始响应：{body}";
        }

        if (body.Contains("AuthorizationHeaderMalformed", StringComparison.OrdinalIgnoreCase))
        {
            return $"Authorization 头格式/内容不被接受（当前 service={service}，scope={scope}）。"
                 + $"走 S3 协议时 service 必须为 s3、scope 结尾必须为 aws4_request；"
                 + $"若用了原生端点则应改用 TOS4-HMAC-SHA256 + service=tos + 结尾 request。"
                 + $"请核对 Endpoint={host}、Region={_region}、Service={service} 三者是否成套。原始响应：{body}";
        }

        if (statusCode == 403)
            return $"HTTP 403（签名已通过但被拒绝）：多为 AK/SK 无 tos:PutObject 权限、子账号未授权、或桶为私有。原始响应：{body}";

        if (statusCode == 404)
            return $"HTTP 404：桶名或地域不对。请确认 Region={_region}、Bucket={_bucket}、签名端点={host}。原始响应：{body}";

        return $"HTTP {statusCode}（service={service}，endpoint={host}）：{body}";
    }

    // ================= 工具 =================

    /// <summary>对象键：folder/yyyyMMdd/guid.ext，斜杠分段各自编码（保留 /）</summary>
    private static string BuildKey(string fileName, string? folder)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
        ext = ext.ToLowerInvariant();

        var dir = string.IsNullOrWhiteSpace(folder) ? "images" : folder.Trim('/');
        var name = Guid.NewGuid().ToString("N") + ext;
        var raw = $"{dir}/{DateTime.Now:yyyyMMdd}/{name}";

        return string.Join('/', raw.Split('/').Select(Uri.EscapeDataString));
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }

    /// <summary>从完整 URL 反解对象键，用于删除。非本桶地址返回 null。</summary>
    private string? TryExtractKey(string url)
    {
        if (!url.StartsWith(_baseUrl, StringComparison.OrdinalIgnoreCase)) return null;
        var path = url.Substring(_baseUrl.Length).TrimStart('/');
        var q = path.IndexOf('?');
        if (q >= 0) path = path[..q];
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static string Trim(string s)
    {
        if (string.IsNullOrEmpty(s)) return "(空响应)";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length > 300 ? s[..300] + "..." : s;
    }
}
