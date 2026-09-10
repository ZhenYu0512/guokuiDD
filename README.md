# 锅盔肉夹馍点单系统

微信原生小程序 + .NET 8 Web API + EF Core + SQL Server 的小吃点单系统，支持小程序下单、微信支付 V3、到店自提取餐码，以及 PC 管理后台。

## 功能总览

**用户端小程序**
- 首页：门店信息、轮播广告、招牌推荐
- 点单：分类/商品联动、规格弹窗（辣度单选、加料多选上限 3）、售罄遮罩
- 确认订单：自提信息、优惠券、积分抵扣（金额后端重算）
- 支付：微信支付 V3（JSAPI），回调 + 主动对账双通道幂等确认
- 订单：状态 Tab（待支付/烤制中/待取餐/已完成/已取消）、4 位取餐码
- 我的：会员卡、积分明细、优惠券领取与管理

**PC 管理后台**
- 商品管理：分类 / 商品 / 规格 / 加料 / 售罄开关 / 库存调整
- 订单管理：按状态与日期筛选、出餐/完成/取消（自动退款）流转
- 库存预警：按阈值查询低库存并一键补货
- 打印机绑定：小票机 SN / AppKey 配置
- 优惠券：满减券创建与启停

## 快速开始

```powershell
# 1. 初始化数据库
sqlcmd -S localhost -U sa -P HuiYan1223 -i sql/01_schema.sql
sqlcmd -S localhost -U sa -P HuiYan1223 -i sql/02_seed.sql

# 2. 启动后端（默认模拟支付，开发免微信商户配置）
cd server/GuokuiDD.Api && dotnet run

# 3. 启动管理后台（admin / admin123）
npx serve admin -l 8080

# 4. 微信开发者工具导入 miniprogram/
```

详见 [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)。
