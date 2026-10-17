# 素材与许可说明

本仓库是非营利的粉丝作品原型。Pokémon 及相关名称、角色、原版素材的权利属于 Nintendo / Creatures Inc. / GAME FREAK / The Pokémon Company。
**不要商业化**（收费、内购、广告都不行）——下面几乎所有素材来源都带"非商业"条款，任天堂也会对粉丝游戏发下架通知。

## 本仓库实际用到的

| 内容 | 来源 | 许可 | 注意 |
|---|---|---|---|
| 10 只宝可梦的精灵图集（`unity/Assets/Art/Pokemon`） | poke-rouge `public/sprites/` ← [PMDCollab/SpriteCollab](https://github.com/PMDCollab/SpriteCollab) | **CC BY-NC 4.0** | 需署名（`CREDITS.md` 已列出画师）；部分基础帧标注为 CHUNSOFT，即源自官方《不可思议的迷宫》 |
| 像素中文字体（子集） | [Fusion Pixel Font](https://github.com/TakWolf/fusion-pixel-font) | SIL OFL 1.1 | 许可文件随附在 `Resources/Fonts/OFL.txt` |
| 玩法数值 | poke-rouge 源码 | MIT（Steven 的项目） | 移植时保留了数值，代码为 C# 重写 |
| 场景、贴图、特效、UI 边框 | 本仓库代码程序化生成 | — | 没有使用第三方 3D 素材 |

## 两个参考素材仓库

### [pagefaultgames/pokerogue](https://github.com/pagefaultgames/pokerogue) 与 [pokerogue-assets](https://github.com/pagefaultgames/pokerogue-assets)

- 游戏代码：**AGPL-3.0**。把它的代码搬进 Unity 项目，会让整个游戏（包括网络服务）都受 AGPL 约束，必须公开全部源码。**只参考思路，不要复制代码。**
- 素材仓库按 REUSE 标注，混合了多种许可：原创美术 **CC BY-NC-SA 4.0**（相同方式共享：改编后的素材也必须用同样许可发布）；
  源自官方作品的部分标为 `LicenseRef-FAIR-USE`（他们自称合理使用，不等于你也能用）；战斗动画里来自 Pokémon Reborn 的部分有单独的
  `LicenseRef-POKEMON-REBORN`；Logo 等标为 `LicenseRef-NO-REUSE`，不能用。
- poke-rouge 已经在用其中的训练家立绘和场地特效帧（CC BY-NC-SA 4.0）。迁移这些素材时要逐个文件核对 REUSE 标注，并把改编后的素材按 NC-SA 发布。

### [JPTER78/pokesurvivor](https://github.com/JPTER78/pokesurvivor)

- **许可在 2026-10-14 变了**：commit `b297089` 及之前的代码是 MIT；之后的代码改为 **PolyForm Noncommercial 1.0.0**（禁止任何商业用途）。
  poke-rouge 的 CREDITS 写的是 MIT，对应的是变更前的版本；以后再从这个仓库借东西，需要按新许可处理或固定在旧 commit。
- 它的宝可梦精灵同样来自 SpriteCollab（CC BY-NC 4.0）。
- 仓库没有 GitHub 能识别的许可标识（`NOASSERTION`），以仓库里的 `LICENSE` 文本为准。

## 其他

- poke-rouge 的真新镇图块、训练家行走图来自《火红/叶绿》原版素材（通过 pret/pokefirered 导入），版权属于任天堂，仅能作为非营利粉丝作品使用；迁移到 Unity 时同样如此。
- Unity Personal：年收入/融资 20 万美元以下免费；WebGL 版会强制显示 Made with Unity 启动画面。
