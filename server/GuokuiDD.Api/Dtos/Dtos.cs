namespace GuokuiDD.Api.Dtos;

// ---------- 认证 ----------
public record WeChatLoginReq(string Code, string? NickName, string? AvatarUrl);
public record AdminLoginReq(string Username, string Password);

// ---------- 下单 ----------
public record OrderItemReq(int ProductId, List<int> SpecOptionIds, List<int> AddonIds, int Quantity);
public record CreateOrderReq(int StoreId, List<OrderItemReq> Items, int? UserCouponId, int PointsUsed,
    string ContactName, string ContactPhone, string? Remark);

public record OrderItemDto(int ProductId, string ProductName, string ProductImage, int BasePrice,
    object Specs, object Addons, int UnitPrice, int Quantity, int Subtotal);

public record OrderDto(int Id, string OrderNo, int Status, int TotalAmount, int CouponDiscount,
    int PointsUsed, int PointsDiscount, int PayAmount, string? PickupCode,
    string ContactName, string ContactPhone, string? Remark, DateTime CreatedAt,
    List<OrderItemDto> Items);

// ---------- 后台 ----------
public record CategoryReq(int StoreId, string Name, int Sort, bool IsActive);
public record ProductReq(int CategoryId, string Name, string ImageUrl, string? Description,
    int BasePrice, int Stock, bool IsSoldOut, bool IsFeatured, int Sort, bool IsActive);
public record SpecGroupReq(int ProductId, string Name, bool IsRequired, int Sort);
public record SpecOptionReq(int SpecGroupId, string Name, int PriceDelta, int Sort);
public record AddonReq(int StoreId, string Name, int Price, bool IsActive);
public record SoldOutReq(bool IsSoldOut);
public record StockReq(int Stock);
public record OrderStatusReq(string Action, string? Reason); // bake/ready/complete/cancel
public record PrinterReq(int StoreId, string Name, string ConnType, string Sn, string? AppKey, string? Address, int Status);
public record PrintReq(int PrinterId);
public record CouponReq(string Name, int ThresholdAmount, int DiscountAmount,
    DateTime ValidFrom, DateTime ValidTo, int TotalCount, bool IsActive);
