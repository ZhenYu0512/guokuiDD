/* ============================================================
   锅盔肉夹馍点单系统 - SQL Server 建表脚本
   说明：所有金额字段一律为「分」(int)
   注意：本文件为 UTF-8 编码，sqlcmd 执行时请带 -f 65001
   ============================================================ */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF DB_ID('GuokuiDD') IS NULL
    CREATE DATABASE GuokuiDD;
GO
USE GuokuiDD;
GO

IF OBJECT_ID('dbo.Stores', 'U') IS NULL
CREATE TABLE Stores (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    Name          NVARCHAR(100) NOT NULL,
    Address       NVARCHAR(200) NOT NULL DEFAULT '',
    Phone         NVARCHAR(30)  NOT NULL DEFAULT '',
    BusinessHours NVARCHAR(50)  NOT NULL DEFAULT '',
    Lat           FLOAT NOT NULL DEFAULT 0,
    Lng           FLOAT NOT NULL DEFAULT 0,
    Notice        NVARCHAR(500) NULL
);
GO

IF OBJECT_ID('dbo.Banners', 'U') IS NULL
CREATE TABLE Banners (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    StoreId  INT NOT NULL,
    ImageUrl NVARCHAR(300) NOT NULL,
    LinkUrl  NVARCHAR(300) NULL,
    Sort     INT NOT NULL DEFAULT 0
);
GO

IF OBJECT_ID('dbo.Categories', 'U') IS NULL
CREATE TABLE Categories (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    StoreId  INT NOT NULL,
    Name     NVARCHAR(50) NOT NULL,
    Sort     INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);
GO

IF OBJECT_ID('dbo.Products', 'U') IS NULL
CREATE TABLE Products (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId  INT NOT NULL,
    Name        NVARCHAR(100) NOT NULL,
    ImageUrl    NVARCHAR(300) NOT NULL DEFAULT '',
    Description NVARCHAR(500) NULL,
    BasePrice   INT NOT NULL,               -- 分
    Stock       INT NOT NULL DEFAULT 0,
    IsSoldOut   BIT NOT NULL DEFAULT 0,     -- 售罄（小程序显示遮罩）
    IsFeatured  BIT NOT NULL DEFAULT 0,     -- 招牌推荐
    Sort        INT NOT NULL DEFAULT 0,
    IsActive    BIT NOT NULL DEFAULT 1
);
CREATE INDEX IX_Products_CategoryId ON Products(CategoryId);
GO

IF OBJECT_ID('dbo.SpecGroups', 'U') IS NULL
CREATE TABLE SpecGroups (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    ProductId  INT NOT NULL,
    Name       NVARCHAR(50) NOT NULL,       -- 如「辣度」
    IsRequired BIT NOT NULL DEFAULT 1,
    Sort       INT NOT NULL DEFAULT 0
);
CREATE INDEX IX_SpecGroups_ProductId ON SpecGroups(ProductId);
GO

IF OBJECT_ID('dbo.SpecOptions', 'U') IS NULL
CREATE TABLE SpecOptions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    SpecGroupId INT NOT NULL,
    Name        NVARCHAR(50) NOT NULL,      -- 如「微辣」
    PriceDelta  INT NOT NULL DEFAULT 0,     -- 分
    Sort        INT NOT NULL DEFAULT 0
);
CREATE INDEX IX_SpecOptions_GroupId ON SpecOptions(SpecGroupId);
GO

IF OBJECT_ID('dbo.Addons', 'U') IS NULL
CREATE TABLE Addons (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    StoreId  INT NOT NULL,
    Name     NVARCHAR(50) NOT NULL,         -- 如「加蛋」
    Price    INT NOT NULL,                  -- 分
    IsActive BIT NOT NULL DEFAULT 1
);
GO

IF OBJECT_ID('dbo.Users', 'U') IS NULL
CREATE TABLE Users (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    OpenId    NVARCHAR(64) NOT NULL,
    NickName  NVARCHAR(50) NULL,
    AvatarUrl NVARCHAR(300) NULL,
    Phone     NVARCHAR(20) NULL,
    Points    INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);
CREATE UNIQUE INDEX UX_Users_OpenId ON Users(OpenId);
GO

IF OBJECT_ID('dbo.MemberCards', 'U') IS NULL
CREATE TABLE MemberCards (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    UserId   INT NOT NULL,
    CardNo   NVARCHAR(30) NOT NULL,
    Level    INT NOT NULL DEFAULT 1,
    IssuedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);
CREATE UNIQUE INDEX UX_MemberCards_UserId ON MemberCards(UserId);
GO

IF OBJECT_ID('dbo.Coupons', 'U') IS NULL
CREATE TABLE Coupons (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(100) NOT NULL,
    ThresholdAmount INT NOT NULL,           -- 满 X 分可用
    DiscountAmount  INT NOT NULL,           -- 减 Y 分
    ValidFrom       DATETIME2 NOT NULL,
    ValidTo         DATETIME2 NOT NULL,
    TotalCount      INT NOT NULL,
    IssuedCount     INT NOT NULL DEFAULT 0,
    IsActive        BIT NOT NULL DEFAULT 1
);
GO

IF OBJECT_ID('dbo.UserCoupons', 'U') IS NULL
CREATE TABLE UserCoupons (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    UserId    INT NOT NULL,
    CouponId  INT NOT NULL,
    Status    INT NOT NULL DEFAULT 0,       -- 0未使用 1已使用 2已过期
    OrderId   INT NULL,
    ClaimedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UsedAt    DATETIME2 NULL
);
CREATE INDEX IX_UserCoupons_UserId ON UserCoupons(UserId, Status);
GO

IF OBJECT_ID('dbo.PointsRecords', 'U') IS NULL
CREATE TABLE PointsRecords (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    UserId    INT NOT NULL,
    Change    INT NOT NULL,                 -- 正为获得，负为消耗
    Reason    NVARCHAR(100) NOT NULL,
    OrderId   INT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);
CREATE INDEX IX_PointsRecords_UserId ON PointsRecords(UserId);
GO

IF OBJECT_ID('dbo.Orders', 'U') IS NULL
CREATE TABLE Orders (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    OrderNo        NVARCHAR(40) NOT NULL,
    UserId         INT NOT NULL,
    StoreId        INT NOT NULL,
    Status         INT NOT NULL DEFAULT 0,  -- 0待支付 1烤制中 2待取餐 3已完成 4已取消
    TotalAmount    INT NOT NULL,            -- 分
    CouponDiscount INT NOT NULL DEFAULT 0,
    UserCouponId   INT NULL,
    PointsUsed     INT NOT NULL DEFAULT 0,
    PointsDiscount INT NOT NULL DEFAULT 0,
    PayAmount      INT NOT NULL,            -- 实付（分）
    PickupCode     NVARCHAR(4) NULL,        -- 4位取餐码
    PickupDate     NVARCHAR(8) NOT NULL,    -- yyyyMMdd
    ContactName    NVARCHAR(30) NOT NULL,
    ContactPhone   NVARCHAR(20) NOT NULL,
    Remark         NVARCHAR(200) NULL,
    CreatedAt      DATETIME2 NOT NULL DEFAULT GETDATE(),
    PaidAt         DATETIME2 NULL,
    CompletedAt    DATETIME2 NULL,
    CancelledAt    DATETIME2 NULL,
    CancelReason   NVARCHAR(200) NULL
);
CREATE UNIQUE INDEX UX_Orders_OrderNo ON Orders(OrderNo);
-- 取餐码：门店当日唯一
CREATE UNIQUE INDEX UX_Orders_PickupCode ON Orders(StoreId, PickupDate, PickupCode)
    WHERE PickupCode IS NOT NULL;
CREATE INDEX IX_Orders_UserId ON Orders(UserId, Status);
GO

-- 订单明细：下单时全量快照商品/规格/加料/价格
IF OBJECT_ID('dbo.OrderItems', 'U') IS NULL
CREATE TABLE OrderItems (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    OrderId      INT NOT NULL,
    ProductId    INT NOT NULL,
    ProductName  NVARCHAR(100) NOT NULL,
    ProductImage NVARCHAR(300) NOT NULL DEFAULT '',
    BasePrice    INT NOT NULL,
    SpecsJson    NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    AddonsJson   NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    UnitPrice    INT NOT NULL,
    Quantity     INT NOT NULL,
    Subtotal     INT NOT NULL
);
CREATE INDEX IX_OrderItems_OrderId ON OrderItems(OrderId);
GO

IF OBJECT_ID('dbo.Printers', 'U') IS NULL
CREATE TABLE Printers (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    StoreId   INT NOT NULL,
    Name      NVARCHAR(50) NOT NULL,
    Sn        NVARCHAR(64) NOT NULL,        -- 打印机终端号
    AppKey    NVARCHAR(64) NULL,
    Status    INT NOT NULL DEFAULT 0,       -- 0离线 1在线
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);
GO

IF OBJECT_ID('dbo.AdminUsers', 'U') IS NULL
CREATE TABLE AdminUsers (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(50) NOT NULL,
    PasswordHash NVARCHAR(128) NOT NULL,
    Role         NVARCHAR(20) NOT NULL DEFAULT 'admin'
);
CREATE UNIQUE INDEX UX_AdminUsers_Username ON AdminUsers(Username);
GO
