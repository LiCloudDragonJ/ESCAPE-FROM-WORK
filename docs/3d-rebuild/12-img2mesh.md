# 12 · 本地 image-to-3D 可行性验证（TripoSR）

> 目标：在本机（Windows + RTX 4070 Ti SUPER 16GB，无 Docker）跑通"一张图 → 3D 模型 → FBX"，
> 判断它值不值得接进现在的 Blender 参数化资产管线。
> 结论先行：**TripoSR 已在本机完整跑通**（含贴图烘焙、减面、FBX 导出），单件 <10s、峰值显存 2.3GB；
> **建议"局部接入"**——只用于 AI 概念图/实拍图里的**不规则杂物**（纸箱堆、破家具、管线簇、植物），
> 不替代 `10-props-blender.md` 的参数化建模。
>
> 实测日期：2026-11-27 · 机器：RTX 4070 Ti SUPER 16GB / 30.9GB RAM / Windows
> 相关：`10-props-blender.md`（现有参数化管线）、`ai-3d-generation-reference.md`（在线服务/引擎内置方案）

---

## 0. TL;DR

| 问题 | 答案 |
|------|------|
| 本机能跑开源 image-to-3D 吗 | **能**，TripoSR 已跑通，全程本地、无账号、无 Docker |
| 权重拿得到吗 | 能。`huggingface.co` 被墙，`hf-mirror.com` 实测 **8.5MB/s**，1.68GB 权重约 3 分钟 |
| 出一件游戏资产要多久 | **GPU ≈ 6.4s + Blender ≈ 1s**（减面 + 烘焙 + 导出）；加 4 视角 QA 图共 <10s |
| 显存够吗 | 256 网格 **2.32GB**、384 **4.11GB**、512 **7.57GB**（16GB 卡余量很大） |
| 质量能进游戏吗 | 中远景道具可以（1500 tris 减面后轮廓保持）；近景主角级不行（腿会缺、细节粘连） |
| 建议 | **局部接入**：AI 概念图/实拍 → 杂物高模参考 → 减面 → FBX；参数化家具/设备继续用 Blender |

---

## 1. 候选方案对比（16GB 显存 / Windows / 无 Docker）

权重体积取自 `hf-mirror.com` 的模型文件列表实测；显存与平台要求取自各自官方 README（链接见备注）。

| 方案 | 权重体积 | 官方显存 | Windows 可行性（本机亲测/文档） | 关键依赖坑 | 结论 |
|------|---------|---------|------------------------------|-----------|------|
| **TripoSR** (VAST-AI-Research) | **1.56 GB**（model.ckpt） | 官方 README：单图约 **6GB**；本机实测 **2.3GB**（mc256） | **✅ 已跑通**（本次验证） | `torchmcubes` 需 nvcc 编译 CUDA 扩展 → 已用 scikit-image 替代；transformers 5.x 键名不兼容 → 已加映射 | **推荐，唯一跑通** |
| **InstantMesh** (TencentARC) | 6.77 GB（含 instant_mesh_base/large、instant_nerf_base/large、diffusion 1.61GB） | 官方未标，社区 10–16GB | ❌ 官方只给 Linux/CUDA 12.1 流程；Windows 需预编译 Triton 轮子（仅 cp310） | `xformers==0.0.22.post7`、`triton`、`torch>=2.1`+`CUDA>=12.1`；本机是 cp312 | **不做**（依赖风险最高，且 6.77GB 超下载预算） |
| **StableFast3D** (Stability AI) | **3.75 GB**（model.safetensors 单文件） | README：约 6GB（官方未硬标，社区 6–8GB） | ⚠️ 官方"Windows support **experimental**"，需 **Visual Studio 2022** 编译 CUDA 扩展 | uv 展开/烘焙用自定义 CUDA kernel，必须 MSVC + nvcc；≥8GB 显存 | **备选**（要装 VS2022 才能编译） |
| **Hunyuan3D-2(mini)** (腾讯) | 完整 2.0 十余 GB；mini 子集亦数 GB | README：**shaping 6GB**，shaping+texture 合计 **16GB**；有 `--low_vram_mode` | ⚠️ 官方只支持 Linux 脚本；Windows 靠第三方 **WinPortable**(YanWenKun) | 自定义光栅化器需编译；WinPortable 要求 VS Build Tools 2022；下载量远超 3GB 预算 | **暂缓**（质量更好，但本次预算/工期不试） |
| **TRELLIS** (Microsoft) | 数 GB（+ 子模块） | README：**至少 16GB** | ❌ "code is currently tested only on **Linux**"；Windows 参考 issue #3，"not fully tested" | `nvdiffrast`、`spconv`、`flash-attn`/`xformers`、`kaolin`、`diffoctreerast` 全要编译 | **不推荐**（本机 16GB 正好卡在最低线） |

补充：以上除 TripoSR 外均**未在本机安装**——按"总下载 ≤3GB"的约束，1.56GB 的 TripoSR 是唯一在预算内能真正跑通的，
其余按"依赖多 = 编译失败风险高"排序后放弃，没有空口判断，依赖坑均引官方 README 原文。

---

## 2. 本机实测环境

- GPU：RTX 4070 Ti SUPER，16376 MiB，驱动 621.40，CUDA UMD 13.5
- RAM：30.9 GB；F: 盘剩余 326 GB
- Python：`F:\ComfyUI\venv\Scripts\python.exe`（3.12.10，torch 2.11.0+cu128，cuda 12.8，可直接用）
  - 未污染系统 Python `F:\Tools\Python312\python.exe`
- Blender：`F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe`（headless）
- 网络：`huggingface.co` **超时**；`hf-mirror.com` 200/643ms、下载 8.5MB/s；GitHub API 快、`codeload` 慢（~44KB/s）→ 代码走 API 逐文件取

---

## 3. 安装步骤（全部实测，可按序复现）

### 3.1 依赖（装进 ComfyUI 的 venv，复用其 torch）

```powershell
F:\ComfyUI\venv\Scripts\python.exe -m pip install trimesh omegaconf xatlas moderngl glcontext fast-simplification
```

全部是 cp312 wheel，**无需** MSVC / nvcc。版本实测：trimesh 5.2.2、omegaconf 2.4.0、xatlas 0.0.11、
moderngl 5.13.0、glcontext 3.1.0、fast-simplification 0.2.0。

### 3.2 权重（约 1.68GB，3 分钟）

```powershell
curl.exe -L -o "tools\img2mesh\weights\TripoSR\model.ckpt" `
  "https://hf-mirror.com/stabilityai/TripoSR/resolve/main/model.ckpt"
```

- 实测 `1,677,246,742` 字节，sha256 `429E2C6B...970EE`（与 HF 一致）
- 另取 `config.yaml`（把 `pretrained_model_name_or_path` 改成本地 DINO 配置目录）
- 取 `https://hf-mirror.com/facebook/dino-vitb16/resolve/main/config.json` 到 `weights/dino-vitb16/config.json`
  （**只要 config.json**，ViT 权重在 model.ckpt 里）
- 源码：`tools/img2mesh/src/TripoSR/`，20 个文件经 GitHub contents API 获取（不用慢速 zip）

### 3.3 四个必须打的补丁（源码在 `tools/img2mesh/src/TripoSR/`）

| # | 文件 | 问题 | 处理 |
|---|------|------|------|
| 1 | `tsr/models/isosurface.py:7` | `from torchmcubes import marching_cubes`，Windows 要 nvcc+MSVC 编译 CUDA 扩展 | 换成 `skimage.measure.marching_cubes`（纯 wheel）；**同时**去掉上游 `v_pos[..., [2,1,0]]` 轴交换（skimage 已是 xyz 序）；因 skimage 缠向相反（体带有符号体积为负）再 `faces[:, ::-1]` 翻一次 → 修正后体积 +0.0644、watertight=True |
| 2 | `tsr/system.py:26` | transformers 5.23 与 4.x checkpoint 键名不匹配（200 个键全对不上，形状相同） | 加 `_remap_vit_keys()`：`encoder.layer.N.attention.attention.query → layers.N.attention.q_proj`（同理 k/v/o、mlp.fc1/fc2），加载前映射。选映射而非降级 transformers 4.35，因为 4.35 锁 `tokenizers 0.14`（cp312 无轮子） |
| 3 | `tsr/models/tokenizers/image.py:21` | `configure()` 只会 `hf_hub_download`，离线/本地权重不可用 | 支持本地目录：`os.path.isdir(...)` 时用 `ViTModel.config_class.from_pretrained(dir/config.json)` |
| 4 | `tsr/bake_texture.py:138` | 烘焙时 `positions` 在 CPU、triplane 在 CUDA → `grid is on cpu, different from other tensors on cuda:0` | `torch.tensor(..., device=scene_code.device)`，回读加 `.cpu()` |

### 3.4 自写脚本

| 文件 | 作用 |
|------|------|
| `tools/img2mesh/run_tsr.py` | 主入口：抠图/归一化 → TripoSR → 网格 → 可选减面 → 可选 xatlas 贴图烘焙 → GLB/OBJ + `metrics.json`（分阶段耗时、显存峰值、面数） |
| `tools/img2mesh/blender_mesh.py` | `render`（4 视角 QA 图）/ `tofbx`（减面+FBX）/ `texture_fbx`（烘焙图→材质→FBX，含 QA 渲染） |
| `tools/img2mesh/blender_input.py` | 把仓库里现成 props FBX 渲成白底输入图（用于反向验证） |

---

## 4. 实测性能（RTX 4070 Ti SUPER，`--chunk-size 8192`）

### 4.1 网格分辨率 vs 耗时/显存/面数（输入 512×512，单图）

| `--mc-resolution` | 体素提取 | **峰值显存** | 三角面 | GLB 体积 | 总耗时（python） |
|---|---|---|---|---|---|
| 256（默认） | 1.16–1.54 s | **2.32 GB** | 83,728 | 1.68 MB | **6.37 s** |
| 384 | 3.69 s | 4.11 GB | 190,224 | — | ~9.3 s |
| 512 | 9.06 s | 7.57 GB | 340,188 | 6.80 MB | 14.02 s |

其中模型加载 4.46–5.26 s（419.3M 参数）、前向推理（image → triplane）**0.31–0.45 s**、前向后显存 1.82 GB。
即"单图生成"本身 0.3s 级，真正耗时是网格提取。

### 4.2 后处理

| 步骤 | 耗时 | 备注 |
|---|---|---|
| 减面 83,728 → **1,500 tris**（fast-simplification 二次误差） | **0.05 s** | 保 UV/颜色属性；Blender `DECIMATE` 修 UV 缝会卡在 3,513 面，故减面放 Python 里做 |
| xatlas 展开 + 烘焙 1024² | **0.37 s**（首次 16.68 s，CUDA kernel 首次编译） | 2048² 也只要 0.92 s |
| Blender `tofbx`（导入+减面+导出 FBX） | **0.9 s** | 含 4 视角渲染的 `texture_fbx` 2.4 s（16 采样） |

### 4.3 一件成品资产的总账

```
图 → TripoSR(mc256) 6.4s → 减面到 1500 tris + 烘焙 1024 贴图 → Blender 转 FBX(1s) ≈ 8s/件
```
（复用已加载的模型时，后续每件只要 ~1.7s：forward 0.3 + extract 1.2 + 减面/烘焙 0.4）

---

## 5. 样例产物

两个样例，一个"干净单物体"、一个"仓库现成参数化道具反渲"：

### 5.1 椅子（TripoSR 官方示例图）

| 文件 | 说明 |
|---|---|
| `tools/img2mesh/out/chair/mesh.glb` | 原始高模：41,862 verts / **83,728 tris**，1.68 MB，带顶点色 |
| `tools/img2mesh/out/chair/chair_1500.fbx` | **1,500 tris**，带顶点色（`Color` 层），pivot 在脚底 `min_z=0` |
| `tools/img2mesh/out/chair/render_front.png` 等 | 4 视角 QA（Cycles，顶点色材质） |
| `tools/img2mesh/out/chair/render_1500_q34.png` | 减面到 1500 面后的同一视角 |
| `tools/img2mesh/out/chair_lo/chair_lo_final.fbx` | **1,500 tris + UVMap + 烘焙 1024 贴图**（贴图在 `chair_lo_final.fbm/`） |
| `tools/img2mesh/out/chair_lo/texture.png` | xatlas 烘焙贴图 |

### 5.2 盆栽（用仓库 `P_plant.fbx` 反渲成输入图）

| 文件 | 说明 |
|---|---|
| `tools/img2mesh/inputs/P_plant.png` | 用 `blender_input.py` 从 `game/Assets/_Project/Art/Props/P_plant.fbx` 渲的输入图 |
| `tools/img2mesh/out/plant/mesh.glb` | 45,499 verts / **90,998 tris** |
| `tools/img2mesh/out/plant/plant_1500.fbx` | **1,500 tris**，顶点色，pivot 在盆底 |

### 5.3 QA 图（相对本文档）

| | |
|---|---|
| 输入 → | ![chair input](../../tools/img2mesh/out/chair/input.png) |
| 高模 4 视角 | ![chair side](../../tools/img2mesh/out/chair/render_side.png) |
| 1500 面 + 烘焙贴图 | ![chair lo](../../tools/img2mesh/out/chair_lo/chair_lo_final_render_front.png) |
| 盆栽高模（反渲输入） | ![plant](../../tools/img2mesh/out/plant/render_side.png) |

---

## 6. 质量评价（别只看渲染图漂亮）

**做得好的**
- 整体比例、剪影、主要体块（座面/靠背/坐垫）准确，颜色/材质分布对（木框深、坐垫浅）
- 布艺的褶皱、拉扣、木框线脚这类"高频细节"能被顶点色/烘焙贴图带出来，远看很占便宜
- 1500 面减面后轮廓几乎不变，作为**中远景道具**完全够用（`render_1500_q34.png` 对比高模差异很小）

**做得差的（结构性，不是调参能救的）**
- **细长件丢失/粘连**：椅子的 4 条腿只出来 3 条，后腿拐成弧形；细杆、把手、线缆一律变粗变软
- **硬边变圆**：方管、纸箱棱角会被"磨圆"，因为底层是 triplane 神经场 + marching cubes，本质是等值面
- **不保证水密可用**：虽本次 watertight=True、单一连通块，但薄片/镂空结构常出破洞
- **没有 UV/材质语义**：只有顶点色，进引擎必须再烘焙；PBR 拆分（金属度/粗糙度）完全没有
- **分辨率提高 ≠ 拓扑变好**：512 网格把面数堆到 34 万，缺腿问题依旧，只是表面更顺
- **朝向/坐标轴要手工修**：TripoSR 输出 Z-up、正对 +X；本仓库脚本已自动转成 Y-up + 面向 -Y（→ Unity +Z）

**逐件判断**

| 用例 | 结论 |
|---|---|
| 纸箱堆、垃圾袋、破家具、管线簇、植物、杂物 | ✅ 适合：本来就没有干净拓扑，能省大量建模时间 |
| 椅子/桌子/柜子/货架/机器设备（现有 39 个参数化道具） | ❌ 不适合：参数化建模更快、更干净、可复用，AI 反而要修 |
| 主角/近景重点道具 | ❌ 不适合：面数和细节都不达标 |
| AI 概念图 → 3D 找型 → 人工重拓扑 | ✅ 适合：当"3D 草图/比例参考"，比纯 2D 概念图直观 |

---

## 7. 与 Blender 参数化管线的分工建议

现有管线（`10-props-blender.md`）：Python 参数化脚本 → Blender 生成 → FBX，21+18 件家具/设备已覆盖。
image-to-3D 不抢这块，**只补两个缺口**：

1. **不规则杂物**（noise props）：走廊里的纸箱堆、散落文件、破椅子、管线/阀门簇、垃圾。
   这类东西参数化建模性价比极低，AI 出形 + 减面到 800–1500 面直接进场景，玩家不会盯着看。
2. **概念图找型**：给美术/策划一张 AI 生成的房间概念图，先跑 image-to-3D 得到体块比例，
   再决定"这件要不要参数化重做"。省掉"看图想象体积"的沟通成本。

工作流建议（人机分工固定）：

```
概念图/实拍图
   └─ TripoSR (mc256) → 高模 glb（8万面）
        ├─ 减面到 800–1500 tris + 烘焙 1024/2048 贴图 → FBX → Unity（远景/杂物，直接可用）
        └─ 或作为 Blender 里的参考体块，人工重拓扑后再进管线（重点道具）
```

**不建议**：把 image-to-3D 接成"自动批量生产全部道具"的流水线——修缺腿/补洞/重展 UV 的时间
比参数化重建更长，且每次结果不可复现（同一张图多次运行会有细微差异）。

---

## 8. 已知坑（踩过的，全部有解）

1. **`torchmcubes` 编译**：Windows 要 nvcc + MSVC，且必须与 torch 的 CUDA 大版本一致。→ 用 scikit-image 替代（慢一点但零编译）。
2. **scikit-image marching_cubes 缠向相反**：直接替换会得到"法线朝内"的网格（有符号体积为负、渲染发黑）。→ `faces[:, ::-1]` 翻一次；同时删掉上游的 `[2,1,0]` 轴交换。
3. **transformers 版本漂移**：5.x 把 DINOv2 的 `encoder.layer.N.attention.attention.query` 改名为 `layers.N.attention.q_proj`。→ 加载前做键名映射（不要为此降级 transformers，会连带 tokenizers/numpy 一起降）。
4. **`hf_hub_download` 离线失败**：`configure()` 只认 hub。→ 支持本地 config 目录；ViT 权重本来就在 model.ckpt 里。
5. **烘焙 `grid is on cpu`**：`positions_to_colors` 建 tensor 时没指定 device。→ 显式 `.to(device)`。
6. **先旋转再烘焙 = 贴图全错**：烘焙是用**网格顶点坐标**去 triplane 查询颜色，先把网格转成 Y-up 会让采样点错位（本次实测得到灰蓝脏色）。→ **先烘焙，后旋转**。
7. **Blender glTF 导入后模型躺着**：TripoSR 是 Z-up，glTF 是 Y-up，导入器会把 gltf-y 映射到 blender-z。→ 在导出 GLB 时先绕 X 轴 −90°（`run_tsr.py` 已内置），不要靠 Blender 里手动转。
8. **OBJ 导入把 +90°X 存在 object.rotation_euler 上**：直接覆写 `rotation_euler` 会把模型放倒。→ 用 `matrix_world = R @ matrix_world` 再 apply。
9. **正向朝向**：TripoSR 重建的物体**面向 +X**（用官方 chair 图实测：az=90° 的"侧视"才看到正脸）。→ 绕 Z −90° 使其面向 −Y，导出 FBX 到 Unity 即 +Z 朝前。
10. **glTF 有 `COLOR_0` 但没有 material**：Blender 里显示纯白。→ 建材质把 `Color Attribute → Principled Base Color`（并加 Gamma 2.2，否则颜色被当线性值显得发白）。
11. **Blender `DECIMATE` 卡在 3,513 面**：collapse 会保护 UV 缝，减不到 1,500。→ 减面放 Python（`fast-simplification`）里做，再展开/烘焙。
12. **xatlas 烘焙图背景是噪点**：未被三角形覆盖的像素位置是 (0,0,0)，查询出垃圾颜色。→ 用 alpha 通道遮罩（上游已写 alpha=0，仅预览图看着脏）。
13. **Blender 后台模式忽略相对输出路径**：`-b` 下 `scene.render.filepath` 用相对路径会静默不写文件。→ 一律传绝对路径。
14. **pivot 在几何中心**：TripoSR 输出以 bbox 中心为原点，摆场景会半埋进地板。→ 导出前把 pivot 移到脚底（`--pivot-bottom`，本次 FBX 已处理，`min_z=0`）。
15. **Unity 不认顶点色**：FBX 里的 `Color` 层 Unity 默认 shader 不采。→ 要么写顶点色 shader，要么走"烘焙贴图"FBX（`chair_lo_final.fbx` 这种）。
16. **下载大文件别用 pwsh 的 Start-Process 拼路径**：路径里有空格会变成 `Could not resolve host: FROM`。→ 用 `curl.exe -L -o "带引号路径"`。

---

## 9. 复现命令（照抄可用）

```powershell
# 1) 生成 + 减面 + 烘焙 + 导出（GPU，约 6.4s）
F:\ComfyUI\venv\Scripts\python.exe tools\img2mesh\run_tsr.py `
  --image tools\img2mesh\src\TripoSR\examples\chair.png `
  --out tools\img2mesh\out\chair_lo `
  --mc-resolution 256 --format obj --bake-texture --texture-resolution 1024 --target-faces 1500

# 2) 烘焙图 + UV → 带材质 FBX（+ 4 视角 QA 图）
F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe -b -P tools\img2mesh\blender_mesh.py -- `
  texture_fbx --mesh "F:\...\out\chair_lo\mesh.obj" --texture "F:\...\out\chair_lo\texture.png" `
  --out "F:\...\out\chair_lo\chair_lo_final" --tris 1500 --face-forward --pivot-bottom

# 3) 只出高模 GLB（80k 面，带顶点色）
F:\ComfyUI\venv\Scripts\python.exe tools\img2mesh\run_tsr.py `
  --image <png> --out tools\img2mesh\out\<name> --mc-resolution 256 --format glb

# 4) 高模 GLB → 1500 面 FBX（带顶点色）
F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe -b -P tools\img2mesh\blender_mesh.py -- `
  tofbx --mesh "F:\...\out\<name>\mesh.glb" --out "F:\...\out\<name>\<name>_1500" `
  --tris 1500 --face-forward --pivot-bottom

# 5) 把现成 props FBX 渲成白底输入图（反向验证用）
F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe -b -P tools\img2mesh\blender_input.py -- `
  --mesh "F:\Workspace\ESCAPE FROM WORK\game\Assets\_Project\Art\Props\P_plant.fbx" `
  --out  "F:\Workspace\ESCAPE FROM WORK\tools\img2mesh\inputs\P_plant.png"
```

`run_tsr.py` 会把分阶段耗时/显存峰值写进 `<out>/metrics.json`，便于回归对比。

---

## 10. 结论

**建议接入，但只作为"杂物/找型"的补充工具，不进主线资产流程。**

理由：
1. 成本极低——显存 2.3GB、单件 8 秒、无 Docker、无账号、无 API 费用；一次投入把工具链留在 `tools/img2mesh/`，以后随时可用。
2. 现有参数化管线覆盖的是"规整家具/设备"（21+18 件），恰好是 AI 最不擅长的硬边规整物；而 AI 擅长的"不规则杂物"正是现在最缺、最费工的一类。
3. 质量标准匹配场景：中远景道具 1500 面 + 1024 贴图，画面里和手工模型几乎不可区分；近景重点道具仍走参数化/人工。

**不做的部分**：不接 InstantMesh / TRELLIS（Windows 编译地狱 + 16GB 显存红线）；Hunyuan3D-2 等到有"必须更好的质量"需求时再用第三方 WinPortable 试（届时下载量约 10GB，需单独预算）。

**下一步（如果需要）**：
- 用 AI 出的"仓库杂物概念图"（SDXL/ComfyUI 现成）跑 3–5 件杂物，进场景做视觉验收；
- 若要近景质量，评估 InstantMesh/Hunyuan 的第三方 Windows 便携包，或接在线 API（成本见 `ai-3d-generation-reference.md`）。
