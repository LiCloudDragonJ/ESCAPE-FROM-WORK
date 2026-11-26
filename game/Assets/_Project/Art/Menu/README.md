# ESCAPE FROM WORK — 主菜单 / 加载界面 / 阵亡界面 / Logo

2D 主菜单美术，全部来自本机 ComfyUI（Flux.1-schnell fp8）+ Pillow 程序化后处理。
生成脚本：`tools/art/gen_menu.py`（幂等：seed 固定，缓存命中时重跑字节一致）。

- 只依赖 numpy + Pillow，没有 shapely / scipy / matplotlib。
- 本目录不提交 `.meta`，交给 Unity 首次导入生成（与 `Art/UI/` 的做法一致）；
  脚本本身不写 `.meta`，若目录里已出现，那是编辑器实时导入用**默认设置**生成的，
  请按下面第 3 节手动改（默认 mipmap 是开的，会让 UI 变软）。
- AI 底图缓存在 `tools/art/_cache/menu/`，**不进 Unity 工程**；删掉缓存并加 `--force` 才会重新采样。

## 1. 文件清单

| 文件 | 尺寸 | 格式 | 用途 |
| --- | --- | --- | --- |
| `M_MainMenu.png` | 1920×1080 | RGB PNG | 主菜单背景（无文字 / 无 UI） |
| `M_Loading.png` | 1920×1080 | RGB PNG | 加载界面背景（无文字 / 无 UI） |
| `M_Death.png` | 1920×1080 | RGB PNG | 阵亡 / 撤离失败结算背景 |
| `M_Logo.png` | 1600×600 | RGBA PNG | Logo 白色版（透明底） |
| `M_Logo_Amber.png` | 1600×600 | RGBA PNG | Logo 琥珀版 `#E0A03C`（透明底） |
| `_contact_sheet.png` | 1920×1578 | RGB PNG | 总览拼图（**只给美术/评审看，不要进构建**） |
| `README.md` | — | — | 本文件 |

## 2. 构图与 UI 安全区

三张背景都按「UI 会压在哪里」构图，并做了确定性压暗；压暗后仍保留该区域的材质层次（不是纯黑块）。

| 文件 | 尺寸 | 预留区 | 预留区平均亮度 | 全图平均亮度 | 相对全图 |
| --- | --- | --- | --- | --- | --- |
| `M_MainMenu.png` | 1920×1080 | 左侧 40 %（x < 768） | 58.2 | 86.4 | **33 % 更暗** |
| `M_Loading.png` | 1920×1080 | 中央 56 %×56 % | 27.6 | 40.1 | **31 % 更暗** |
| `M_Death.png` | 1920×1080 | 中央 56 %×56 % | 23.8 | 33.8 | **30 % 更暗** |

- 亮度口径：Rec.709 相对亮度（0–255 的 sRGB 编码值），预留区 = 上表窗口内所有像素的均值。
- 阈值要求「预留区比全图平均低 25 % 以上」，实测三张都在 30 % 以上，留了余量。
- 主菜单：左侧 40 % 是核心筒暗面 + 门洞琥珀应急灯，按钮放在 x≈120–700 px、垂直居中偏下最稳。
- 加载：中央 56 %×56 % 是机柜走道的尽端暗部，进度条压在轴线中央不会糊字。
- 阵亡：中央 56 %×56 % 是大厅的暗部，结算面板（`阵亡` / `撤离失败` / 统计）压正中。

## 3. Unity 导入设置

### 3.1 三张背景（`M_MainMenu` / `M_Loading` / `M_Death`）

| 设置项 | 值 | 理由 |
| --- | --- | --- |
| Texture Type | **Default** | 只当 `RawImage.texture` 用，不需要 Sprite 切片与 PPU |
| sRGB (Color Texture) | 勾选 On | 是给玩家看的颜色图，不是线性数据 |
| Alpha Is Transparency | 取消 Off | 没有 alpha 通道 |
| Non-Power of 2 | None | 1920×1080 不是 2 的幂，**不要让 Unity 拉伸成 2048** |
| Read/Write Enabled | 取消 Off | 运行时不需要 CPU 读像素 |
| Generate Mip Maps | 取消 Off | 屏幕空间 1:1 使用，mipmap 只会让 UI 变软 |
| Wrap Mode | Clamp | 避免边缘采样溢出 |
| Filter Mode | Bilinear | UI 缩放平滑 |
| Max Size | 2048 | 1920 需要 ≥ 2048 才不降采样 |
| Compression | **BC7 / High Quality**（PC）；移动端 ASTC 6×6 | 大面积平滑渐变，BC1/ETC2 会在暗部出明显色带 |
| 目标平台建议 | PC 用 BC7，主机同；移动端若包体紧张再降到 ASTC 8×8 | |

接入方式建议：`Canvas` 下放一层全屏 `RawImage`（`texture = M_MainMenu`），
菜单按钮与 Logo 叠在其上；不要用 `Image` + `Sprite`，省掉一次 sprite 生成，
也避免 `Preserve Aspect` 带来的黑边。

### 3.2 Logo（`M_Logo` / `M_Logo_Amber`）

| 设置项 | 值 |
| --- | --- |
| Texture Type | **Sprite (2D and UI)** |
| Sprite Mode | Single |
| Pixels Per Unit | 100 |
| Mesh Type | Full Rect |
| Alpha Is Transparency | 勾选 On |
| sRGB | 勾选 On |
| Generate Mip Maps | 取消 Off |
| Wrap Mode | Clamp |
| Filter Mode | Bilinear |
| Compression | **None**（或 High Quality）——描边只有 1.33 px，压缩会吃掉 |
| Max Size | 2048 |

- 显示尺寸建议：宽度 620–760 px（原图 1600，约 0.4–0.47 倍），放在主菜单左上或中上。
- 两张 Logo 内容完全一致，只有字色不同：亮背景用 `M_Logo_Amber`，暗背景用 `M_Logo`；
  也可以只留一张白色版，用 `Image.color` 染成 `#E0A03C`（描边也会被一起染，见「已知不足」）。

## 4. 生成命令

```powershell
# 全部重出（缺缓存会自动连 ComfyUI；有缓存则只重跑后处理，字节一致）
C:\Users\17613\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe tools\art\gen_menu.py

# 只重出一张
& $py tools\art\gen_menu.py --only main_menu

# 强制重新采样 AI 底图（换构图时才用）
& $py tools\art\gen_menu.py --force

# 试几种 seed salt 找构图（输出到 tools/art/_cache/menu/sweep/）
& $py tools\art\gen_menu.py --sweep death a b c d
```

ComfyUI 没起来时脚本会直接报错并给出启动命令：

```powershell
F:\ComfyUI\venv\Scripts\python.exe F:\ComfyUI\main.py --listen 127.0.0.1 --port 8188 --disable-auto-launch
```

### 处理管线

1. **Flux.1-schnell fp8**，1920×1088，6 steps / euler / simple / guidance 3.5 / cfg 1.0
   （schnell 是蒸馏模型，cfg 必须 1.0，负向提示基本不生效，所以「无人 / 无文字」写在正向里）。
2. 裁掉上下各 4 px → 1920×1080。
3. Pillow + numpy 后处理（顺序固定）：降饱和 → 冷白平衡 → 阴影蓝灰染色 → 对比/黑位 →
   暖像素拉向琥珀 → （主菜单）门洞琥珀实用光 → （阵亡）局部修补灯牌 → Bloom →
   UI 安全区压暗（迭代求解到目标比值）→ 暗角 → 胶片颗粒
   （同时充掉 8 bit 渐变的抖动层，防止暗部色带）。
4. Logo 用 `msyhbd.ttc` 逐字排版（字距逐字累加），3× 超采样后 LANCZOS 缩小，
   描边 4 px@3× = 1.33 px。

## 5. Prompt 原文

### M_MainMenu.png

```text
photorealistic architectural photography of a vast completely deserted corporate office atrium, nobody present, no human figures anywhere, abandoned empty hall, monumental raw concrete core wall with vertical fins in deep shadow on the left, multi-storey gallery balconies with glass railings and a giant glass curtain wall on the right, cold white fluorescent ceiling strips, desaturated cool grey blue palette, cold white fluorescent light, no saturated colours, volumetric haze, low angle wide establishing shot, 35mm lens, polished floor reflections, no people, no text, no signage, no letters, cinematic game key art
```

- seed：`401`（固定在 `PLATES` 里的字面量，缓存命中即字节一致）
- 6 steps / euler / simple / guidance 3.5 / cfg 1.0 / 1920×1088

### M_Loading.png

```text
photorealistic wide shot down a narrow aisle between two rows of tall dark server cabinets in an empty corporate data centre, perforated steel rack doors, structured cabling overhead, cold white fluorescent strips lit in the foreground only, desaturated cold grey blue palette, no saturated colours, the aisle turns into unlit blackness beyond the last light, heavy shadow in the centre of the frame, symmetrical one point perspective, low camera position, no people, no text, no glowing coloured indicators, no neon, cinematic game key art
```

- seed：`3104`（固定在 `PLATES` 里的字面量，缓存命中即字节一致）
- 6 steps / euler / simple / guidance 3.5 / cfg 1.0 / 1920×1088

### M_Death.png

```text
photorealistic architectural photography of a vast empty corporate office atrium at night with every main light turned off, almost black interior, a row of dim deep red emergency lamps glowing along the mezzanine balconies, a thin drift of smoke and dust in the still air, the far end of the hall is unlit darkness, dark concrete columns and steel railings in silhouette, desaturated cool grey blue palette, cold white fluorescent light, no saturated colours, no magenta, no purple, oppressive silent mood, low camera angle, wide shot, no people, no text, no signage, cinematic game key art
```

- seed：`3101`（固定在 `PLATES` 里的字面量，缓存命中即字节一致）
- 6 steps / euler / simple / guidance 3.5 / cfg 1.0 / 1920×1088


## 6. 已知不足

- **文字与招牌**：Flux schnell 会随机在墙上生成假英文（`DAIN ET`、`TARET` 之类）。
  当前三张都挑了没有可读文字/招牌的 seed，但主菜单左墙的小门牌、加载图机柜上的
  小标签仍有 2–3 px 的模糊色块，凑近看不到字母，远景无感。
- **阵亡图的三处壁灯**是 Flux 自己画成霓虹字母（`TUV`、`匚LCP`）的伪文字。后处理用
  `retouch_patches()` 把它们换成了柔和的红色应急光斑——形状变成没有灯具的「光团」，
  凑近看会觉得光源不明，但比可读的伪文字干净，也不违反「不要霓虹」。
- **人物**：Flux 喜欢在大空间里放一个「比例人」。当前三张逐张放大目检过，均无人物。
- **冷白偏青**：模型默认把「冷白荧光」理解成青蓝，已用降饱和 + 白平衡压回灰蓝，
  加载图的机柜仍带一点青，是这套素材里最偏的一张。
- **琥珀点缀是合成的**：主菜单门洞那盏琥珀应急灯由 Pillow 屏幕叠加生成，
  不是模型画出来的灯具，所以没有可见的灯罩/灯丝——只有光；20 px 内看会觉得光斑「平」。
- **Logo 描边会被 Unity 染色**：白色版描边是深灰 `#0B0E12`，若用 `Image.color`
  染成琥珀色，描边会一起被染（变成暗琥珀）。要严格单色化请直接用两张现成 PNG。
- **Logo 字体**：`msyhbd.ttc`（微软雅黑 Bold）的拉丁字形偏「系统 UI 感」，
  不是定制字体；任务要求必须用这个字体排版，所以字形没有做二次改造。
- **分辨率**：AI 底图 1920×1088，即 1:1 出 1080p，没有做 2× 超分；4K 屏上会被
  Unity 拉伸到 2×，细节（天花灯带边缘）会变软。若要 4K 版需另跑一次 3840×2160 的
  超分管线（本脚本未包含）。
- **加载图中央仍是最亮处之一**：压暗后中央亮度低于全图 30 % 以上，但镜头正对的尽端
  仍是全图最亮处之一；进度条用浅色字（`#E6EAEE`）没问题，用纯白粗体会略微「发光」。
