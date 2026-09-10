/* ============================================================
   初始化数据脚本（金额单位：分）
   后台账号：admin / admin123
   ============================================================ */
USE GuokuiDD;
GO

-- 门店
IF NOT EXISTS (SELECT 1 FROM Stores)
INSERT INTO Stores (Name, Address, Phone, BusinessHours, Lat, Lng, Notice) VALUES
(N'锅盔肉夹馍（旗舰店）', N'学府路 88 号小吃街 12 号铺', '13800000000', N'09:00-21:00', 30.5728, 104.0668,
 N'现烤现卖，下单后约 10 分钟可取，请凭取餐码到店取餐。');
GO

-- 轮播
IF NOT EXISTS (SELECT 1 FROM Banners)
INSERT INTO Banners (StoreId, ImageUrl, LinkUrl, Sort) VALUES
(1, 'https://picsum.photos/seed/banner1/750/360', NULL, 1),
(1, 'https://picsum.photos/seed/banner2/750/360', NULL, 2),
(1, 'https://picsum.photos/seed/banner3/750/360', NULL, 3);
GO

-- 分类
IF NOT EXISTS (SELECT 1 FROM Categories)
INSERT INTO Categories (StoreId, Name, Sort) VALUES
(1, N'招牌锅盔', 1),
(1, N'经典肉夹馍', 2),
(1, N'特色小吃', 3),
(1, N'饮品', 4);
GO

-- 商品（价格单位：分）
IF NOT EXISTS (SELECT 1 FROM Products)
INSERT INTO Products (CategoryId, Name, ImageUrl, Description, BasePrice, Stock, IsSoldOut, IsFeatured, Sort) VALUES
(1, N'招牌牛肉锅盔', 'https://picsum.photos/seed/gk1/400/400', N'现烤酥脆锅盔夹卤牛肉，本店招牌', 1500, 100, 0, 1, 1),
(1, N'麻辣猪肉锅盔', 'https://picsum.photos/seed/gk2/400/400', N'麻辣鲜香，越嚼越香', 1200, 80, 0, 1, 2),
(1, N'梅干菜锅盔',   'https://picsum.photos/seed/gk3/400/400', N'咸香梅干菜，传统风味', 1000, 60, 0, 0, 3),
(2, N'腊汁肉夹馍',   'https://picsum.photos/seed/rjm1/400/400', N'老汤腊汁肉，肥瘦相间', 1300, 90, 0, 1, 1),
(2, N'纯瘦肉夹馍',   'https://picsum.photos/seed/rjm2/400/400', N'纯瘦不柴，肉量十足', 1400, 70, 0, 0, 2),
(2, N'孜然羊肉夹馍', 'https://picsum.photos/seed/rjm3/400/400', N'孜然爆炒羊肉，风味浓郁', 1600, 0, 1, 0, 3),
(3, N'凉皮',         'https://picsum.photos/seed/lp/400/400',  N'爽口凉皮，蒜香十足', 800, 50, 0, 0, 1),
(3, N'冰峰汽水搭档套餐', 'https://picsum.photos/seed/tc/400/400', N'肉夹馍+凉皮+汽水', 2200, 40, 0, 0, 2),
(4, N'冰峰汽水',     'https://picsum.photos/seed/bf/400/400',  N'西安经典橙味汽水', 300, 200, 0, 0, 1),
(4, N'酸梅汤',       'https://picsum.photos/seed/smt/400/400', N'古法熬制，解辣解腻', 500, 100, 0, 0, 2);
GO

-- 规格组：辣度（单选，必选）
IF NOT EXISTS (SELECT 1 FROM SpecGroups)
INSERT INTO SpecGroups (ProductId, Name, IsRequired, Sort)
SELECT Id, N'辣度', 1, 1 FROM Products WHERE Name IN (N'招牌牛肉锅盔', N'麻辣猪肉锅盔', N'腊汁肉夹馍', N'纯瘦肉夹馍', N'孜然羊肉夹馍', N'凉皮');
GO

IF NOT EXISTS (SELECT 1 FROM SpecOptions)
INSERT INTO SpecOptions (SpecGroupId, Name, PriceDelta, Sort)
SELECT g.Id, v.Name, v.PriceDelta, v.Sort
FROM SpecGroups g
CROSS JOIN (VALUES
    (N'不辣', 0, 1), (N'微辣', 0, 2), (N'中辣', 0, 3), (N'特辣', 0, 4)
) v(Name, PriceDelta, Sort);
GO

-- 加料（多选，上限 3 个，服务端强制）
IF NOT EXISTS (SELECT 1 FROM Addons)
INSERT INTO Addons (StoreId, Name, Price) VALUES
(1, N'加蛋', 150),
(1, N'加肉', 300),
(1, N'加芝士', 200),
(1, N'加辣条', 100),
(1, N'加青椒', 50);
GO

-- 后台管理员：admin / admin123（SHA256('GuokuiDD@2026' + 密码) 大写 HEX）
IF NOT EXISTS (SELECT 1 FROM AdminUsers)
INSERT INTO AdminUsers (Username, PasswordHash, Role)
VALUES ('admin', CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'GuokuiDD@2026' + 'admin123'), 2), 'admin');
GO

-- 优惠券
IF NOT EXISTS (SELECT 1 FROM Coupons)
INSERT INTO Coupons (Name, ThresholdAmount, DiscountAmount, ValidFrom, ValidTo, TotalCount, IsActive) VALUES
(N'满 20 减 5', 2000, 500, '2026-01-01', '2027-12-31', 1000, 1),
(N'新客立减 3 元', 0, 300, '2026-01-01', '2027-12-31', 500, 1);
GO

-- 打印机示例
IF NOT EXISTS (SELECT 1 FROM Printers)
INSERT INTO Printers (StoreId, Name, Sn, AppKey, Status) VALUES
(1, N'前台小票机', 'SN-DEMO-0001', NULL, 0);
GO
