# Render 与 Supabase 计划

> 历史文档里的静态站步骤仍然描述 `webgl/` 里的旧原型。搜打撤的游戏服和存档不在这份旧计划里改 `render.yaml`。玩法见 [game-design.md](game-design.md)。

## 现在：Render 静态站点（已可部署）

- `render.yaml` 定义一个静态站点 `poketato-webgl`，发布目录 `webgl/`，构建命令是空操作（播放器已预构建并提交）。
- 构建使用 gzip + Unity 解压回退：不依赖 `Content-Encoding` 响应头；`Build/` 文件名带哈希，设置了一年的 `immutable` 缓存，`index.html` 不缓存。
- 具体点击步骤和设置见 README 的"部署到 Render"。

## 下一步：更小的首包

Brotli 构建比 gzip 小约 23%（12.1 MB 对 15.6 MB），但需要主机为预压缩文件返回正确的头。Render 静态站点支持自定义头，可以在 `render.yaml` 里加：

```yaml
    headers:
      - path: /Build/*.br
        name: Content-Encoding
        value: br
      - path: /Build/*.wasm.br
        name: Content-Type
        value: application/wasm
      - path: /Build/*.js.br
        name: Content-Type
        value: application/javascript
```

并用 `POKETATO_WEBGL_COMPRESSION=brotli` 构建。上线前要在 Render 的预览环境确认 CDN 不会二次压缩或改写这些头；不确定时保留 gzip 回退版本。

## CI：不用每次手动提交构建

1. GitHub Actions（Linux runner）安装 Unity 6000.3.26f1 + WebGL 模块（或用 GameCI 的 `unityci/editor` 镜像）。
2. 用 README 里的 Licensing Client 命令无头激活 Personal（`UNITY_EMAIL` / `UNITY_PASSWORD` 放在 Actions secrets），或者用 GameCI 的 `UNITY_LICENSE`（.ulf）方式。
3. 运行 `PoketatoSetup.All` 和 `PoketatoBuild.WebGL`，把 `webgl/` 推到一个专门的部署分支（例如 `deploy/webgl`）。
4. Render 静态站点改为监听部署分支，主分支就不再需要提交 15 MB 的构建产物。

构建缓存（`unity/Library`）能把 7 分钟的全量构建降到 2~4 分钟。

## 联机上线后：静态站点 + 游戏服务器

poke-rouge 现在是一个 Node 进程同时提供静态文件、WebSocket 房间和 Supabase 代理（README 里有 Render + Dockerfile 的部署说明）。Unity 版建议：

| 服务 | Render 类型 | 内容 |
|---|---|---|
| `poketato-webgl` | Static Site | Unity WebGL 播放器（CDN 缓存） |
| `poketato-server` | Web Service（Docker） | WebSocket 房间、权威模拟、对战匹配、Supabase 代理。可以先沿用 poke-rouge 的 Node 服务器做账号与存档，联机模拟再决定是 C#（.NET 容器，复用 `Sim/` 代码）还是保留 TS |

Unity WebGL 只能用浏览器的 WebSocket（通过 jslib 或 NativeWebSocket 一类的插件），不能用 UDP。跨境延迟的结论和 poke-rouge 一样：问题在线路而不是服务器性能，需要时把服务器放到离玩家更近的地区。

## Supabase：沿用现有结构

poke-rouge 已有迁移脚本 `supabase/migrations/20261015150000_cloud_save.sql`：

- `saves`：每个账号一行 `data jsonb` + `rev`（乐观锁，过期 rev 的保存会被拒绝）+ RLS 只允许本人读写；
- `pvp_players` / `pvp_ghosts`：积分、战绩、排行榜，只允许服务器（service role）读写。

Unity 版的做法：

1. **继续让游戏服务器代理 Supabase**（浏览器只连游戏服务器）。这是 poke-rouge 为了中国大陆玩家做的设计，Unity 客户端不需要也不应该拿到 service role key。
2. Unity 客户端通过 HTTPS（`UnityWebRequest`）调用服务器现有的账号/存档接口，WebSocket 仍用于房间。
3. 存档 `data` 里加 `schema`/`client` 字段（例如 `"client": "unity", "schema": 1`），让网页版和 Unity 版在过渡期能共存、互相读档；
   Unity 端写一个 C# 的存档模型和迁移函数。
4. 账号：邮箱 + 密码走服务器转发到 Supabase Auth；Google 登录在 WebGL 里需要弹出窗口回调，放到后面做。
5. 本 MVP 没有接 Supabase，也没有创建任何云端资源。
