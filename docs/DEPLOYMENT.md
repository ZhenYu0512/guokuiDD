# 锅盔肉夹馍点单系统 · 部署说明

## 一、项目结构

```
guokuiDD/
├── server/GuokuiDD.Api/    # .NET 8 Web API 后端
├── miniprogram/            # 用户端微信小程序（原生）
├── admin/                  # PC 管理后台（Vue3 + Element Plus，静态站点）
├── sql/                    # SQL Server 建表与初始化脚本
│   ├── 01_schema.sql
│   └── 02_seed.sql
└── docs/DEPLOYMENT.md
```

## 二、环境要求

| 组件 | 要求 |
|---|---|
| 后端 | .NET 8 SDK |
| 数据库 | SQL Server 2016+（账号 sa / HuiYan1223） |
| 小程序 | 微信开发者工具（稳定版即可） |
| 管理后台 | 任意静态 Web 服务器（IIS / Nginx / `npx serve`） |

## 三、数据库初始化

1. 使用 SSMS 或 sqlcmd 连接 SQL Server。
2. 依次执行：
   ```powershell
   sqlcmd -S localhost -U sa -P HuiYan1223 -i sql/01_schema.sql
   sqlcmd -S localhost -U sa -P HuiYan1223 -i sql/02_seed.sql
   ```
3. 脚本会创建 `GuokuiDD` 数据库、全部表结构（含取餐码唯一索引）及演示数据。
4. 预置后台账号：`admin / admin123`（生产环境请立即修改并更新哈希）。

## 四、后端部署

1. 修改 `server/GuokuiDD.Api/appsettings.json`：
   - `ConnectionStrings:Default`：数据库连接串。
   - `Jwt:Key`：更换为随机长字符串。
   - `WeChat:AppId / AppSecret`：小程序 AppID 与密钥；`DevMockLogin` 生产置 `false`。
   - `Pay`：微信支付 V3 配置（见第五节）。
2. 本地运行：
   ```powershell
   cd server/GuokuiDD.Api
   dotnet run
   # Swagger: http://localhost:5000/swagger
   ```
3. 生产部署（IIS 或 Linux 进程）：
   ```powershell
   dotnet publish -c Release -o publish
   ```
   将 `publish` 目录部署到服务器，建议使用 HTTPS 并配置反向代理。

## 五、微信支付 V3 配置

1. 在[微信支付商户平台](https://pay.weixin.qq.com)获取：
   - 商户号 `MchId`
   - 商户 API 证书：下载 `apiclient_key.pem`（私钥），放置于 `Certs/apiclient_key.pem`
   - 证书序列号 `MchSerialNo`（商户平台 → API 安全）
   - APIv3 密钥 `ApiV3Key`（32 字节，自行设置）
2. 填入 `appsettings.json` 的 `Pay` 节，并将 `Pay:UseMock` 置为 `false`。
3. `Pay:NotifyUrl` 必须是**公网可访问的 HTTPS 地址**（如 `https://your-domain.com/api/pay/notify`），
   支付成功后微信会回调该地址确认订单（服务端按状态机幂等处理，重复回调无副作用）。
4. 商户平台 → 产品中心 → JSAPI 支付：绑定小程序 AppID。
5. 本地开发回调不可达时：保持 `UseMock=true`（模拟支付直接成功），
   或真机支付后由小程序端自动调用 `/api/orders/{id}/sync-pay` 主动对账确认（已实现）。

## 六、管理后台部署

1. 修改 `admin/api.js` 中 `API_BASE` 为后端实际地址。
2. 任意静态服务器托管 `admin/` 目录即可，例如：
   ```powershell
   npx serve admin -l 8080
   ```
3. 浏览器访问 `http://localhost:8080`，使用 `admin / admin123` 登录。

## 七、小程序配置

1. 微信开发者工具 → 导入 `miniprogram/` 目录，填入小程序 AppID（测试可用测试号）。
2. 修改 `miniprogram/app.js` 中 `globalData.baseUrl` 为后端地址。
3. 开发阶段勾选「不校验合法域名」；上线前在小程序后台配置：
   - request 合法域名：后端 HTTPS 域名
4. 真机支付需后端 `Pay:UseMock=false` 且回调地址可达。

## 八、关键设计说明

| 约束 | 实现 |
|---|---|
| 金额单位 | 全链路「分」(int)，前端展示才转元；后端全量重算，前端金额不可信 |
| 防超卖 | `UPDATE Products SET Stock = Stock - @q WHERE Id=@id AND Stock >= @q`，影响行数 0 即失败回滚 |
| 支付幂等 | 状态机条件更新 `UPDATE Orders SET Status=1 WHERE Id=@id AND Status=0`，微信回调 / sync-pay / 重复请求均只生效一次 |
| 订单快照 | 下单时商品名、图、规格、加料、单价全量复制到 `OrderItems`（SpecsJson / AddonsJson） |
| 取餐码 | 4 位随机数字，`UNIQUE(StoreId, PickupDate, PickupCode)` 保证门店当日唯一 |
| 加料上限 | 服务端强制 ≤3 个，超出直接拒绝 |
| 积分 | 1 积分抵 0.01 元；实付 1 元返 1 积分；取消订单退回 |
| 优惠券 | 领取用条件 UPDATE 防超发；下单校验归属、有效期、门槛；取消订单返还 |

## 九、接口冒烟测试

```powershell
# 后台登录
curl -X POST http://localhost:5000/api/admin/auth/login -H "Content-Type: application/json" -d '{\"username\":\"admin\",\"password\":\"admin123\"}'

# 首页数据
curl http://localhost:5000/api/home?storeId=1

# 菜单
curl http://localhost:5000/api/menu?storeId=1
```
