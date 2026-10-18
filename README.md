# Poketato

宝可梦搜打撤。进关卡搜索，实时打，把装备带出去。死了丢掉这一局捡到的东西。

不是 [poke-rouge](https://github.com/DietCokewithSugar/poke-rouge) 的移植。没有波次、没有局内三选一、没有糖果。装备参照暗黑破坏神 2（稀有度、随机词缀、宝可梦能穿）。关卡是做好的任务图，结构参照 Minecraft Dungeons。完整设计在 [docs/game-design.md](docs/game-design.md)。

这一版能跑的是无头核心：30 只初代、15 棵系别技能树、实时战斗、掉落、死亡丢包、撤离、关卡，以及 Boss / 工会副本 / 世界战的人数上限和输出统计。画面工程是 Unity 6.3 LTS + URP 前向管线，3D 房间加精灵面片，方向光软阴影、点光、反射探针。海滩关另外用平面反射。商城、战队、成就、排行榜的 id 在核心数据里，Supabase 和游戏服不在这次改。

## 无头验证

不需要 Unity。安装 .NET 8 后：

```bash
dotnet test unity/tests/Extraction.Tests/Extraction.Tests.csproj
```

测试覆盖 30 只和技能树门槛、伤害公式、黄装词缀、套装、死亡丢包、撤离、关卡连通、4/8/20 人上限、按技能拆开的输出，以及一场自动打完的训练关。

## Unity

- Unity **6000.3.26f1**（Unity 6.3 LTS），URP 17.3。项目在 `unity/`。
- 打开 `Assets/Scenes/Main.unity` 后按播放。默认雾林小径、喷火龙、20 级、自动战斗。
- `T` 关掉自动战斗。WASD 移动，鼠标瞄准，`J` 或左键普攻，`1`–`4` 技能，`E` 交互。
- 菜单 **Poketato → Setup Everything** 会保证前向管线和场景。无界面：

```bash
Unity -batchmode -nographics -projectPath unity \
  -executeMethod Poketato.EditorTools.PoketatoSetup.All -quit -logFile -
```

宝可梦是按属性色生成的占位图，不读取不能再分发的原画。关卡地面也是程序生成的，没有提交 PMDO 的 `.tile`。

`webgl/` 里仍是上一版割草原型的播放器。`render.yaml` 还在托管它。这次没有改部署文件，也没有把搜打撤重新打成 WebGL。

## 许可

见 [docs/LICENSES.md](docs/LICENSES.md)。PMDO 地图、PokéRogue、PokéSurvivor 的图和代码都没有搬进来。
