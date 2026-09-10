namespace GuokuiDD.Api.Models;

// 金额一律以「分」存储（int）
public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string BusinessHours { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Notice { get; set; }
}

public class Banner
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public string ImageUrl { get; set; } = "";
    public string? LinkUrl { get; set; }
    public int Sort { get; set; }
}

public class Category
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public string Name { get; set; } = "";
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string? Description { get; set; }
    public int BasePrice { get; set; }          // 分
    public int Stock { get; set; }
    public bool IsSoldOut { get; set; }         // 手动售罄（遮罩）
    public bool IsFeatured { get; set; }        // 招牌推荐
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SpecGroup
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = "";      // 如「辣度」
    public bool IsRequired { get; set; } = true;
    public int Sort { get; set; }
}

public class SpecOption
{
    public int Id { get; set; }
    public int SpecGroupId { get; set; }
    public string Name { get; set; } = "";      // 如「微辣」
    public int PriceDelta { get; set; }         // 分，可为 0
    public int Sort { get; set; }
}

public class Addon
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public string Name { get; set; } = "";      // 如「加蛋」
    public int Price { get; set; }              // 分
    public bool IsActive { get; set; } = true;
}

public class User
{
    public int Id { get; set; }
    public string OpenId { get; set; } = "";
    public string? NickName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }
    public int Points { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class MemberCard
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string CardNo { get; set; } = "";
    public int Level { get; set; } = 1;
    public DateTime IssuedAt { get; set; } = DateTime.Now;
}

public class Coupon
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int ThresholdAmount { get; set; }    // 满 X 分可用
    public int DiscountAmount { get; set; }     // 减 Y 分
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public int TotalCount { get; set; }
    public int IssuedCount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserCoupon
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CouponId { get; set; }
    public int Status { get; set; }             // 0未使用 1已使用 2已过期
    public int? OrderId { get; set; }
    public DateTime ClaimedAt { get; set; } = DateTime.Now;
    public DateTime? UsedAt { get; set; }
}

public class PointsRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Change { get; set; }             // 正为获得，负为消耗
    public string Reason { get; set; } = "";
    public int? OrderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public static class OrderStatus
{
    public const int Pending = 0;    // 待支付
    public const int Baking = 1;     // 烤制中（已支付）
    public const int Ready = 2;      // 待取餐
    public const int Completed = 3;  // 已完成
    public const int Cancelled = 4;  // 已取消
}

public class Order
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = "";
    public int UserId { get; set; }
    public int StoreId { get; set; }
    public int Status { get; set; } = OrderStatus.Pending;
    public int TotalAmount { get; set; }        // 商品总额（分）
    public int CouponDiscount { get; set; }
    public int? UserCouponId { get; set; }
    public int PointsUsed { get; set; }
    public int PointsDiscount { get; set; }
    public int PayAmount { get; set; }          // 实付（分）
    public string? PickupCode { get; set; }     // 4位取餐码
    public string PickupDate { get; set; } = ""; // yyyyMMdd，取餐码按门店+当日唯一
    public string ContactName { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
}

// 订单明细全量快照：下单时复制商品/规格/加料/价格
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductImage { get; set; } = "";
    public int BasePrice { get; set; }
    public string SpecsJson { get; set; } = "[]";   // [{group,option,priceDelta}]
    public string AddonsJson { get; set; } = "[]";  // [{name,price}]
    public int UnitPrice { get; set; }              // 含规格/加料的单价（分）
    public int Quantity { get; set; }
    public int Subtotal { get; set; }
}

public class Printer
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public string Name { get; set; } = "";
    public string ConnType { get; set; } = "cloud"; // cloud 云打印 / usb 有线USB / lan 网口
    public string Sn { get; set; } = "";        // 云打印终端号（cloud 时使用）
    public string? AppKey { get; set; }
    public string? Address { get; set; }        // usb: COM3；lan: 192.168.1.100:9100
    public int Status { get; set; }             // 0离线 1在线
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "admin";
}
