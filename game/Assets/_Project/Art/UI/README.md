# ESCAPE FROM WORK — HUD / UI 图标集

白色单色、透明背景、可被代码染色的矢量风格图标。全部由 `generate_ui_icons.py`
（纯 Pillow 程序化绘制，4× 超采样后 LANCZOS 缩小）生成，可重复执行，幂等。

- 生成：`python generate_ui_icons.py`
- 预览：`_contact_sheet.png`
- 画布：准星 512×512，HUD/地图 256×256，罗盘字母 128×128；内容四周留 ≥16px 安全边
- 线条：@256 主线 8px、内细节 6–7px、圆角端点/圆角连接；准星 512 画布用 14px 细线
- 颜色：纯白 `#FFFFFF`，Alpha 通道即图形；Unity 中用 `Image.color` / 顶点色染色
- 无文字、无阴影、无描边特效；所有图标统一中心 pivot
- 注：`.meta` 由 Unity 首次导入时生成，本目录未提交 `.meta`

## 建议 Unity 导入设置（全部图标一致）

| 设置项 | 值 |
| --- | --- |
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Pixels Per Unit | 100 |
| Mesh Type | Full Rect |
| Pivot | Center |
| Alpha Is Transparency | ✅ On |
| sRGB (Color Texture) | ✅ On |
| Wrap Mode | Clamp |
| Filter Mode | Bilinear |
| Generate Mip Maps | ❌ Off（HUD 不随距离缩放） |
| Compression | None（或 High Quality，避免细线糊边） |
| Max Size | ≥ 512（准星）/ 256（其余） |

## 文件清单


### 准星 / 命中反馈（Crosshair/，5 个）

512×512，细线（14px），适合直接叠在屏幕中心

| 文件 | 尺寸 | 用途 |
| --- | --- | --- |
| `Crosshair/I_Cross_Dot.png` | 512×512 | 默认 FPS 准星：中心点 + 四短刻线 |
| `Crosshair/I_Cross_Cross.png` | 512×512 | 四向开口十字准星（无中心点） |
| `Crosshair/I_Cross_Circle.png` | 512×512 | 圆环 + 中心点准星（霰弹/蓄力） |
| `Crosshair/I_Cross_Chevron.png` | 512×512 | 四向小三角准星（精确瞄准） |
| `Crosshair/I_Hitmarker.png` | 512×512 | 命中反馈：四斜线 + 中心点，可放大闪一下 |

### HUD 图标（Icons/，20 个）

256×256，主线宽 8px，细节 6–7px；均为白色单色

| 文件 | 尺寸 | 用途 |
| --- | --- | --- |
| `Icons/I_Health.png` | 256×256 | 生命值（心形） |
| `Icons/I_Armor.png` | 256×256 | 护甲值（盾牌） |
| `Icons/I_Stamina.png` | 256×256 | 耐力/体力（闪电） |
| `Icons/I_Weight.png` | 256×256 | 负重/背包容量 |
| `Icons/I_Ammo.png` | 256×256 | 弹药（子弹） |
| `Icons/I_Weapon.png` | 256×256 | 当前武器（步枪剪影） |
| `Icons/I_Damage.png` | 256×256 | 护甲破损/受伤（裂盾） |
| `Icons/I_Loot.png` | 256×256 | 可搜刮容器/物资箱 |
| `Icons/I_Key.png` | 256×256 | 钥匙/门禁卡（钥匙） |
| `Icons/I_Value.png` | 256×256 | 战利品价值（钞票） |
| `Icons/I_Document.png` | 256×256 | 情报文件/合同 |
| `Icons/I_HardDrive.png` | 256×256 | 硬盘/数据载体 |
| `Icons/I_Medkit.png` | 256×256 | 医疗包/治疗 |
| `Icons/I_Extract.png` | 256×256 | 撤离点（门 + 向外箭头） |
| `Icons/I_Elevator.png` | 256×256 | 电梯 |
| `Icons/I_Stairs.png` | 256×256 | 楼梯/楼层通行 |
| `Icons/I_Interact.png` | 256×256 | 交互提示（E 键方格） |
| `Icons/I_Lock.png` | 256×256 | 上锁/需要钥匙 |
| `Icons/I_Check.png` | 256×256 | 完成/已确认 |
| `Icons/I_Alert.png` | 256×256 | 警告/危险 |

### 地图 / 罗盘（Map/，10 个）

256×256；罗盘字母 128×128

| 文件 | 尺寸 | 用途 |
| --- | --- | --- |
| `Map/I_Map_Player.png` | 256×256 | 地图玩家标记，向上 = 0° |
| `Map/I_Map_Extract.png` | 256×256 | 地图撤离点标记 |
| `Map/I_Map_Spawn.png` | 256×256 | 地图出生点标记 |
| `Map/I_Map_Unknown.png` | 256×256 | 地图未知区域/未探索 |
| `Map/I_Map_StairsUp.png` | 256×256 | 地图上行楼梯 |
| `Map/I_Map_StairsDown.png` | 256×256 | 地图下行楼梯 |
| `Map/I_Compass_N.png` | 128×128 | 罗盘北 |
| `Map/I_Compass_E.png` | 128×128 | 罗盘东 |
| `Map/I_Compass_S.png` | 128×128 | 罗盘南 |
| `Map/I_Compass_W.png` | 128×128 | 罗盘西 |

## 建议显示尺寸（UI 里的 Image / SpriteRenderer）

| 用途 | 贴图 | 建议显示尺寸 | 说明 |
| --- | --- | --- | --- |
| HUD 图标（生命/护甲/弹药等） | 256 | 32–48px | 24px 仍可辨认，低于 24px 细线会发灰 |
| 背包/交互大图标 | 256 | 64–96px | 线宽随尺寸线性放大，不会糊 |
| 准星 | 512 | 128–256px | 图形本体约 220px，线宽约 3.5–7px；命中反馈建议 96→160 缩放闪 |
| 地图标记 | 256 | 24–40px | 玩家箭头向上 = 0°，代码旋转即可 |
| 罗盘字母 | 128 | 24–40px | 小尺寸下 12px 笔画仍清晰 |

## 已知说明

- `I_Hunger.png`（饥饿）未生成：256px 下难以与其它线条图标拉开辨识度，按任务要求跳过；后续如需可用餐具/苹果图形补做。
- 罗盘字母（N/E/S/W）是图形化笔画，不是字体渲染；笔画 12px 比其它图标略粗，用于保证小尺寸可读。
- 所有图形均在 16px 安全边内；准星刻意留出大面积透明，方便叠层与动画缩放。
- 准星图形本体 220×220（512 画布），所以贴图尺寸大但内容不占满；按上表 128–256px 显示即可，线宽随尺寸线性变化。
