# 素材与许可说明

本仓库是非营利的粉丝作品原型。Pokémon 及相关名称、角色、原版素材的权利属于 Nintendo / Creatures Inc. / GAME FREAK / The Pokémon Company。

2026-10-18 对照过这次要用的三个素材仓库。不能再分发的都没有提交，关卡和宝可梦用程序占位。设计里的用法写在 [game-design.md](game-design.md) 第 12 节。

## 这次核对过、没有提交的

| 来源 | 许可情况 | 决定 |
|---|---|---|
| [PMDO-Explorers-Maps](https://github.com/slothplaysnecro/PMDO-Explorers-Maps) `Content/Tile` | 根目录没有 LICENSE。README 只是粉丝说可以拿去用。内容是《探险队》地图原图 | 不提交任何 `.tile`。关卡只用其中的场景名字做主题，地面和墙是程序生成的 URP 材质 |
| [pokerogue](https://github.com/pagefaultgames/pokerogue) | 代码 **AGPL-3.0** | 不复制代码，避免把游戏和网络服务拉进 AGPL |
| [pokerogue-assets](https://github.com/pagefaultgames/pokerogue-assets) | REUSE。能授权的原创素材是 **CC BY-NC-SA 4.0**。官方图多为 `LicenseRef-FAIR-USE`，Logo 等是 `LicenseRef-NO-REUSE`，一部分战斗动画是 `LicenseRef-POKEMON-REBORN` | 不提交。FAIR-USE 和 NO-REUSE 都不能当成再分发许可 |
| [pokesurvivor](https://github.com/JPTER78/pokesurvivor) | 2026-10-14 提交 `b297089` 之后代码是 **PolyForm Noncommercial 1.0.0**。该提交及更早是 MIT。精灵来自 SpriteCollab，CC BY-NC 4.0。GitHub 识别为 `NOASSERTION` | 不复制代码，也不把它的精灵打进来 |

## 仓库里仍然有的

| 内容 | 来源 | 许可 | 注意 |
|---|---|---|---|
| `unity/Assets/Resources/Pokemon` 里 10 张旧图集 | 上一版从 poke-rouge 转来，源头是 [SpriteCollab](https://github.com/PMDCollab/SpriteCollab) | 声明为 **CC BY-NC 4.0**，署名在同目录 `CREDITS.md` | 基础帧标了 CHUNSOFT，也就是官方迷宫图。粉丝站的 CC 协议覆盖不了官方原画。搜打撤核心**不读取**这些图，运行时自己画占位。公开版本发出去之前应删掉带 CHUNSOFT 署名的帧 |
| 像素中文字体 | [Fusion Pixel Font](https://github.com/TakWolf/fusion-pixel-font) | SIL OFL 1.1 | 许可在 `unity/Assets/Resources/Fonts/OFL.txt` |
| 上一版玩法数值 | poke-rouge | MIT | 那套割草循环已经从脚本里拿掉，不再作为当前玩法 |

## 上一版参考过、这次仍然不要搬的

### PokéRogue

代码 AGPL-3.0。素材仓库按文件分成 CC BY-NC-SA、FAIR-USE、NO-REUSE、POKEMON-REBORN。只参考「有属性、有招式名」这件事，不复制实现。

### PokéSurvivor

2026-10-14 之前的提交是 MIT，之后是 PolyForm Noncommercial。玩法是幸存者，不是这次要做的搜打撤。
