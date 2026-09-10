using System.Net.Sockets;
using System.Text;

namespace GuokuiDD.Api.Services;

/// <summary>
/// 网口（LAN）小票机直打：通过 TCP 9100 发送 ESC/POS 指令。
/// USB 有线打印机无法由 Web 后端直接驱动，需在门店电脑部署本地打印助手后转发。
/// </summary>
public class EscPosPrinterService
{
    static EscPosPrinterService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // GBK 支持
    }

    /// <summary>向网口打印机发送任意 ESC/POS 文本（GBK）。</summary>
    public Task<(bool ok, string message)> SendAsync(string address, string content) => TestPrintAsync(address, content);

    public async Task<(bool ok, string message)> TestPrintAsync(string address, string content)
    {
        var parts = address.Split(':');
        var host = parts[0];
        var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 9100;

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                return (false, $"连接 {host}:{port} 超时");

            var bytes = new List<byte> { 0x1B, 0x40 }; // 初始化
            bytes.AddRange(Encoding.GetEncoding("GBK").GetBytes(content));
            bytes.AddRange(new byte[] { 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x00 }); // 走纸 + 切纸

            await client.GetStream().WriteAsync(bytes.ToArray());
            return (true, "测试小票已发送");
        }
        catch (Exception ex)
        {
            return (false, "打印失败：" + ex.Message);
        }
    }

    /// <summary>按订单构建小票文本（GBK 打印，网口/云打印通用）。</summary>
    public static string BuildOrderTicket(Models.Order o, IEnumerable<Models.OrderItem> items, string storeName)
    {
        static string Yuan(int fen) => (fen / 100m).ToString("0.00");
        var sb = new StringBuilder();
        sb.AppendLine("================================");
        sb.AppendLine($"   {storeName}");
        sb.AppendLine("================================");
        sb.AppendLine($"取餐码: ** {o.PickupCode} **");
        sb.AppendLine($"单号: {o.OrderNo}");
        sb.AppendLine($"时间: {o.CreatedAt:MM-dd HH:mm}");
        sb.AppendLine("--------------------------------");
        foreach (var i in items)
        {
            sb.AppendLine($"{i.ProductName} x{i.Quantity}  {Yuan(i.Subtotal)}");
            try
            {
                var specs = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(i.SpecsJson);
                var addons = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(i.AddonsJson);
                var specText = string.Join("/", (specs ?? new()).Select(s => s["option"].GetString()));
                var addonText = string.Join("+", (addons ?? new()).Select(a => a["name"].GetString()));
                var line = string.Join(" ", new[] { specText, addonText }.Where(x => !string.IsNullOrEmpty(x)));
                if (!string.IsNullOrEmpty(line)) sb.AppendLine($"  ({line})");
            }
            catch { }
        }
        sb.AppendLine("--------------------------------");
        sb.AppendLine($"商品金额:              {Yuan(o.TotalAmount)}");
        if (o.CouponDiscount > 0) sb.AppendLine($"优惠券:               -{Yuan(o.CouponDiscount)}");
        if (o.PointsDiscount > 0) sb.AppendLine($"积分抵扣:             -{Yuan(o.PointsDiscount)}");
        sb.AppendLine($"实付:                  {Yuan(o.PayAmount)}");
        sb.AppendLine("--------------------------------");
        sb.AppendLine($"取餐人: {o.ContactName} {o.ContactPhone}");
        if (!string.IsNullOrWhiteSpace(o.Remark)) sb.AppendLine($"备注: {o.Remark}");
        sb.AppendLine("================================");
        sb.AppendLine("        请凭取餐码取餐");
        return sb.ToString();
    }

    public static string BuildTestTicket(string printerName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================");
        sb.AppendLine("        锅盔肉夹馍 测试小票");
        sb.AppendLine("================================");
        sb.AppendLine($"打印机: {printerName}");
        sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("--------------------------------");
        sb.AppendLine("招牌牛肉锅盔(微辣)  x1   15.00");
        sb.AppendLine("  + 加蛋  加肉");
        sb.AppendLine("--------------------------------");
        sb.AppendLine("合计:                    15.00");
        sb.AppendLine("取餐码: 8888");
        sb.AppendLine("================================");
        sb.AppendLine("      打印正常则绑定成功");
        return sb.ToString();
    }
}
