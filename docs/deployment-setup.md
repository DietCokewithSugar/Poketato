# 搜打撤后端要准备的密钥

Poketato 的联机后端分成三块：

- **Supabase**：账号、角色、宝可梦、装备、商城、战队、成就、排行榜、对局和每个技能的输出。
- **Render 游戏服** `poketato-server`：普通关 4 人，Boss 战和工会副本 8 人，世界战 20 人。伤害在服务器上算，结束时写入 Supabase。
- **Render 静态站** `poketato-webgl`：Unity 6.3 LTS 打出来的 WebGL 播放器。

货币是探险币 `explorer-coin` 和战队勋章 `guild-medal`。没有支付渠道，不需要 Stripe、微信支付或支付宝的密钥。

浏览器可以直连 Supabase，也可以只连游戏服，由游戏服转发 `/supabase`。推荐第二种：anon key 仍然会进到浏览器（这是公开密钥），但 **service role 只留在游戏服上**。

## 密钥放哪

| 密钥 | 从哪拿 | 放在哪 | 能不能进客户端或 Git |
|---|---|---|---|
| `SUPABASE_URL` | Supabase → Project Settings → API → Project URL | Render 游戏服环境变量；本机 `server/.env` | 可以告诉客户端「项目地址」，但不要把它和服务密钥写在一起提交 |
| `SUPABASE_ANON_KEY` | 同一页的 `anon` / `public`，或新版 `publishable` 密钥 | Render 游戏服；本机 `server/.env`。客户端只在直连时才需要它 | 公开密钥。可以出现在浏览器。不要当成密码 |
| `SUPABASE_SERVICE_ROLE_KEY` | 同一页的 `service_role`，或新版 `secret` 密钥 | **只**放 Render 游戏服和本机 `server/.env` | **禁止**放进 Unity、WebGL、GitHub Actions、仓库 |
| `UNITY_EMAIL` | Unity 账号邮箱 | GitHub → Settings → Secrets and variables → Actions | 禁止提交 |
| `UNITY_PASSWORD` | 这个 Unity 账号的密码 | 同上，Actions secret | 禁止提交。账号不要开 2FA，否则无头激活会失败 |
| `UNITY_LICENSE` | 本机 Unity Hub 生成的 `.ulf` 文件的**全部内容** | 同上，Actions secret | 禁止提交 |
| `WEBGL_ORIGIN` | 静态站地址，例如 `https://poketato-webgl.onrender.com` | Render 游戏服，可选 | 不是密钥。留空则公开接口允许任意网页来源 |
| `PUBLIC_URL` | 游戏服自己的 https 地址 | Render 游戏服，可选 | 不是密钥。留空则用请求里的 Host |

新版 Supabase 密钥也可以用这两个名字，游戏服会认：`SUPABASE_PUBLISHABLE_KEY`、`SUPABASE_SECRET_KEY`。

Unity 个人版许可证文件的位置：

- Linux：`~/.local/share/unity3d/Unity/Unity_lic.ulf`
- macOS：`/Library/Application Support/Unity/Unity_lic.ulf`
- Windows：`C:\ProgramData\Unity\Unity_lic.ulf`

把文件内容原文贴进 `UNITY_LICENSE`。还要同时有邮箱和密码。三个缺一个，Actions 会跳过 WebGL 构建，不会假装构建成功。

## 1. Supabase

1. 在 [supabase.com](https://supabase.com) 新建项目。数据库密码自己保管，游戏服用不到它。
2. 打开 SQL Editor，把 `supabase/migrations/20261018120000_extraction_backend.sql` 整段执行一次。重复执行不会删掉已有数据，也不会覆盖你改过的商品价格。
3. Authentication → Providers：打开 Email。先做联机时可以关掉 “Confirm email”，这样注册接口会直接返回登录令牌。
4. Authentication → URL Configuration：Site URL 填 WebGL 站点地址，Redirect URLs 里加上同一个地址。
5. 从 Project Settings → API 复制 URL、anon key、service role key。

这套表是搜打撤用的，不是肉鸽存档。玩家不能直接改钱包、流水、对局或技能输出。购买、穿装备、建队走数据库函数；对局结算只有 service role 能调用 `record_match_result`。

新号的探险币是 0，硬币来自撤离。主撤离拿到该关的通关奖金再加上本局金币；提前撤离奖金减半。死亡、超时没撤离、掉线都不入库本局掉落，身上原有装备耐久减 15。雾林奖金 80，时空之塔 400，公会地下 300 外加 1 枚战队勋章。世界战没有固定奖金，只把本局金币换成探险币。

商城只卖伤药、解毒药、修理锤、天赋重置、仓库扩页和占位围巾，价格都是探险币。装备从关卡掉落，不在商城里卖。

普通关最多 4 人，第 5 人进不了同一间。Boss 战和工会副本 8 人，世界战 20 人。Boss、工会副本和世界战会按角色、按技能（`fire-0` 这种「系-格子」）写入输出；同一场里各技能伤害加起来等于该角色的总伤害。

## 2. Render

1. Render → New → Blueprint，连接 `DietCokewithSugar/Poketato`。
2. Branch 选要部署的分支。合并进 `main` 之后改用 `main`。
3. 它会创建两个服务：
   - `poketato-webgl`：静态站，发布目录 `webgl/`。
   - `poketato-server`：Docker 游戏服，地区 `singapore`，套餐 `starter`。WebSocket 不能用会休眠的免费实例，否则房间会在休眠时断掉。静态站可以保持免费。
4. 按提示填写游戏服的环境变量。`sync: false` 的值不会写回仓库。

| 变量 | 填什么 |
|---|---|
| `SUPABASE_URL` | 项目 URL，不要末尾斜杠 |
| `SUPABASE_ANON_KEY` | anon / publishable |
| `SUPABASE_SERVICE_ROLE_KEY` | service role / secret |
| `WEBGL_ORIGIN` | 静态站的 https 源，或 `*` |
| `PUBLIC_URL` | 游戏服的 https 根地址，例如 `https://poketato-server.onrender.com` |

地区可以改 `render.yaml` 里的 `region`。可选值包括 `singapore`、`oregon`、`frankfurt`。两个服务放同一地区。

游戏服起来后：

- `GET /health` 返回 `{"ok":true,"supabase":true}`。`supabase` 为 false 说明三个密钥没配齐。
- `GET /api/public-config` 返回给浏览器的地址、anon key 和 `wsUrl`。响应里不会有 service role。
- 普通关满 4 人、Boss 和工会副本满 8 人、世界战满 20 人就开；没满则最多等 `QUEUE_WAIT_MS`（默认 20 秒）。一局默认 180 秒，用 `MATCH_DURATION_MS` 改。掉线后默认留 60 秒（`DISCONNECT_GRACE_MS`），超时按死亡。
- `REQUIRE_AUTH=1` 时，没登录或没配 Supabase 就不能排队。

## 3. 网页怎么连上游戏服

播放器按这个顺序找配置：

1. 页面上的 `window.POKETATO_BACKEND`
2. 地址栏 `?api=https://poketato-server.onrender.com`
3. Unity 的 `Resources/backend.json`（只有 anon key，可选）

推荐在托管页面里、Unity 加载脚本之前加上：

```html
<script>
window.POKETATO_BACKEND = {
  gameServer: "wss://poketato-server.onrender.com/ws"
};
</script>
```

客户端会向游戏服要 `/api/public-config`，再用返回的 `/supabase` 代理登录和读写。这样浏览器不必直连 `*.supabase.co`。

直连时才把 URL 和 anon key 放进这个对象的 `url`、`anonKey`。客户端会拒绝 `service_role` 和 `sb_secret_` 开头的密钥。

`Resources/backend.json` 被 git 忽略。本机可以用菜单 **Poketato → Backend → Write Config From Environment**，它只读 `POKETATO_SUPABASE_URL` 和 `POKETATO_SUPABASE_ANON_KEY`。

当前仓库里的 `webgl/` 还是单机原型，里面没有这份客户端。要等下面的 Actions 用 Unity 6 LTS 重新打一次包，联机代码才会进播放器。

## 4. GitHub Actions

- `.github/workflows/server.yml`：不需要密钥。它会在 PostgreSQL 16 上跑迁移和权限测试，并构建游戏服镜像。
- `.github/workflows/webgl.yml`：使用 **Unity 6000.3.26f1**。这是 2026-10-08 的 Unity 6.3 LTS 补丁，变更集 `de8167e7440c`，和 `unity/ProjectSettings/ProjectVersion.txt`、设计文档一致。6000.0.85f1 是更早的 6.0 线，CI 不用它。只有三个 Unity secret 都在时才构建，产物上传为名为 `webgl` 的 artifact。构建入口是现有的 `Poketato.EditorTools.PoketatoBuild.WebGL`，输出到仓库的 `webgl/`。

Actions 不会把构建结果自动提交回分支。要更新 Render 上的播放器，把构建出的 `webgl/` 提交到部署分支。

## 5. 本机游戏服

```bash
cd server
cp .env.example .env
# 编辑 .env，填入三个 Supabase 值。REQUIRE_AUTH 本机可以设为 0。
npm ci
npm test
npm start
```

`npm test` 要本机 PostgreSQL，并且 `DATABASE_URL` 指向本机名为 `poketato` 或 `poketato_rls` 的库。没设 `DATABASE_URL` 时会跳过库测试，房间和结算公式的测试仍然会跑。

游戏服默认听 `8787`。健康检查是 `http://127.0.0.1:8787/health`。

## 还没配密钥时会怎样

- 没有 Supabase 三个密钥：游戏服能启动，`/health` 是正常的，但 `REQUIRE_AUTH=1` 时排队会返回「服务器还没有配置 Supabase」。不会有账号和结算。
- 没有 Unity 三个 secret：WebGL workflow 显示跳过，现有的 `webgl/` 原型继续可以被静态站托管。
- 没有支付密钥：这是预期的。商城只扣探险币。
