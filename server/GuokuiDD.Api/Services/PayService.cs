using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GuokuiDD.Api.Models;

namespace GuokuiDD.Api.Services;

/// <summary>预支付结果。Mode=mock 表示开发环境直接支付成功；Mode=wechat 时 PayParams 供小程序 wx.requestPayment 调起支付。</summary>
public record PrepayResult(string Mode, Dictionary<string, string>? PayParams);
public record PayResult(bool Success, string Message);
public record QueryPayResult(bool Paid, string? TransactionId);

public interface IPayService
{
    Task<PrepayResult> CreatePrepayAsync(Order order, string openId);
    Task<PayResult> RefundAsync(Order order, string reason);
    Task<QueryPayResult> QueryOrderAsync(string outTradeNo);
    /// <summary>AEAD_AES_256_GCM 解密支付回调 resource，返回明文 JSON</summary>
    string DecryptNotifyResource(string nonce, string ciphertext, string associatedData);
}

/// <summary>开发环境模拟支付：不走微信，直接视为支付成功。</summary>
public class MockPayService : IPayService
{
    public Task<PrepayResult> CreatePrepayAsync(Order order, string openId) =>
        Task.FromResult(new PrepayResult("mock", null));
    public Task<PayResult> RefundAsync(Order order, string reason) =>
        Task.FromResult(new PayResult(true, "MOCK_REFUND_SUCCESS"));
    public Task<QueryPayResult> QueryOrderAsync(string outTradeNo) =>
        Task.FromResult(new QueryPayResult(false, null));
    public string DecryptNotifyResource(string nonce, string ciphertext, string associatedData) =>
        throw new NotSupportedException("Mock 模式不支持回调解密");
}

/// <summary>
/// 微信支付 V3（小程序 JSAPI）。
/// 文档：https://pay.weixin.qq.com/wiki/doc/apiv3/apis/chapter3_5_1.shtml
/// </summary>
public class WeChatPayV3Service : IPayService
{
    private const string BaseUrl = "https://api.mch.weixin.qq.com";

    private readonly IConfiguration _cfg;
    private readonly HttpClient _http = new();
    private readonly RSA _rsa;
    private readonly byte[] _apiV3Key;

    public WeChatPayV3Service(IConfiguration cfg)
    {
        _cfg = cfg;
        var pemPath = cfg["Pay:PrivateKeyPath"]!;
        if (!Path.IsPathRooted(pemPath)) pemPath = Path.Combine(AppContext.BaseDirectory, pemPath);
        _rsa = RSA.Create();
        _rsa.ImportFromPem(File.ReadAllText(pemPath));
        _apiV3Key = Encoding.UTF8.GetBytes(cfg["Pay:ApiV3Key"]!);
    }

    private string MchId => _cfg["Pay:MchId"]!;
    private string SerialNo => _cfg["Pay:MchSerialNo"]!;
    private string AppId => _cfg["WeChat:AppId"]!;

    /// <summary>V3 请求签名：HTTP方法\nURL\n时间戳\n随机串\n请求体\n，SHA256-RSA 后 Base64</summary>
    private string BuildAuthorization(string method, string urlPath, string body)
    {
        var nonce = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var message = $"{method}\n{urlPath}\n{timestamp}\n{nonce}\n{body}\n";
        var sign = Convert.ToBase64String(_rsa.SignData(Encoding.UTF8.GetBytes(message),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        return $"WECHATPAY2-SHA256-RSA2048 mchid=\"{MchId}\",nonce_str=\"{nonce}\","
             + $"signature=\"{sign}\",timestamp=\"{timestamp}\",serial_no=\"{SerialNo}\"";
    }

    private async Task<JsonDocument> SendAsync(string method, string urlPath, object? body)
    {
        var json = body == null ? "" : JsonSerializer.Serialize(body);
        var req = new HttpRequestMessage(new HttpMethod(method), BaseUrl + urlPath);
        req.Headers.Add("Authorization", BuildAuthorization(method, urlPath, json));
        req.Headers.Add("Accept", "application/json");
        if (body != null)
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await _http.SendAsync(req);
        var respText = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"微信支付接口错误 {(int)resp.StatusCode}: {respText}");
        return JsonDocument.Parse(respText);
    }

    /// <summary>JSAPI 下单，返回小程序调起支付所需参数</summary>
    public async Task<PrepayResult> CreatePrepayAsync(Order order, string openId)
    {
        using var doc = await SendAsync("POST", "/v3/pay/transactions/jsapi", new
        {
            appid = AppId,
            mchid = MchId,
            description = "锅盔肉夹馍-" + order.OrderNo,
            out_trade_no = order.OrderNo,
            notify_url = _cfg["Pay:NotifyUrl"],
            amount = new { total = order.PayAmount, currency = "CNY" }, // 单位：分
            payer = new { openid = openId }
        });
        var prepayId = doc.RootElement.GetProperty("prepay_id").GetString()!;

        // 二次签名生成小程序支付参数
        var timeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonceStr = Guid.NewGuid().ToString("N");
        var package = "prepay_id=" + prepayId;
        var message = $"{AppId}\n{timeStamp}\n{nonceStr}\n{package}\n";
        var paySign = Convert.ToBase64String(_rsa.SignData(Encoding.UTF8.GetBytes(message),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        return new PrepayResult("wechat", new Dictionary<string, string>
        {
            ["timeStamp"] = timeStamp,
            ["nonceStr"] = nonceStr,
            ["package"] = package,
            ["signType"] = "RSA",
            ["paySign"] = paySign
        });
    }

    /// <summary>退款</summary>
    public async Task<PayResult> RefundAsync(Order order, string reason)
    {
        try
        {
            using var doc = await SendAsync("POST", "/v3/refund/domestic/refunds", new
            {
                out_trade_no = order.OrderNo,
                out_refund_no = "R" + order.OrderNo,
                reason,
                amount = new { refund = order.PayAmount, total = order.PayAmount, currency = "CNY" }
            });
            var status = doc.RootElement.GetProperty("status").GetString();
            return new PayResult(status is "SUCCESS" or "PROCESSING", status ?? "UNKNOWN");
        }
        catch (Exception ex)
        {
            return new PayResult(false, ex.Message);
        }
    }

    /// <summary>按商户单号查询订单（用于回调不可达时的主动对账/补单）</summary>
    public async Task<QueryPayResult> QueryOrderAsync(string outTradeNo)
    {
        try
        {
            using var doc = await SendAsync("GET",
                $"/v3/pay/transactions/out-trade-no/{outTradeNo}?mchid={MchId}", null);
            var state = doc.RootElement.GetProperty("trade_state").GetString();
            var txnId = doc.RootElement.TryGetProperty("transaction_id", out var t) ? t.GetString() : null;
            return new QueryPayResult(state == "SUCCESS", txnId);
        }
        catch
        {
            return new QueryPayResult(false, null);
        }
    }

    /// <summary>解密回调 resource（AEAD_AES_256_GCM，密钥为 APIv3 密钥）</summary>
    public string DecryptNotifyResource(string nonce, string ciphertext, string associatedData)
    {
        var cipherBytes = Convert.FromBase64String(ciphertext);
        var tag = cipherBytes[^16..];
        var data = cipherBytes[..^16];
        var plain = new byte[data.Length];
        using var aes = new AesGcm(_apiV3Key, 16);
        aes.Decrypt(Encoding.UTF8.GetBytes(nonce), data, tag, plain, Encoding.UTF8.GetBytes(associatedData));
        return Encoding.UTF8.GetString(plain);
    }
}
