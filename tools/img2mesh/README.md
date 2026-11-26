# tools/img2mesh — 本地 image-to-3D 原型（TripoSR）

调研与结论见 `docs/3d-rebuild/12-img2mesh.md`。这里只放可复现的脚本、权重与样例产物。

```
weights/TripoSR/{model.ckpt,config.yaml}   1.68GB 权重（hf-mirror 下载）
weights/dino-vitb16/config.json            DINOv2 ViT-B/16 配置（权重在 model.ckpt 里）
src/TripoSR/                               上游源码 + 4 处本地补丁（见文档 3.3 节）
run_tsr.py                                 图 → 网格（可选减面/烘焙）→ glb/obj + metrics.json
blender_mesh.py                            render / tofbx / texture_fbx（headless Blender）
blender_input.py                           把现成 props FBX 渲成白底输入图
inputs/                                    反渲得到的输入图
out/<name>/                                每个样例：input.png + mesh + fbx + QA 渲染
```

## 环境

- Python：`F:\ComfyUI\venv\Scripts\python.exe`（torch 2.11.0+cu128）。一次性依赖：
  `pip install trimesh omegaconf xatlas moderngl glcontext fast-simplification`
- Blender：`F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe`

## 一键跑一件

```powershell
F:\ComfyUI\venv\Scripts\python.exe tools\img2mesh\run_tsr.py `
  --image tools\img2mesh\src\TripoSR\examples\chair.png `
  --out tools\img2mesh\out\chair_lo `
  --mc-resolution 256 --format obj --bake-texture --texture-resolution 1024 --target-faces 1500

F:\Tools\blender\blender-4.2.9-windows-x64\blender.exe -b -P tools\img2mesh\blender_mesh.py -- `
  texture_fbx --mesh "F:\Workspace\ESCAPE FROM WORK\tools\img2mesh\out\chair_lo\mesh.obj" `
  --texture "F:\Workspace\ESCAPE FROM WORK\tools\img2mesh\out\chair_lo\texture.png" `
  --out "F:\Workspace\ESCAPE FROM WORK\tools\img2mesh\out\chair_lo\chair_lo_final" `
  --tris 1500 --face-forward --pivot-bottom
```

实测：GPU 段 ≈6.4s（峰值显存 2.3GB），Blender 段 ≈1–2.4s，输出 1,500 tris 带贴图 FBX。

注意：`--format glb`/`obj` 的输出已经是 Y-up；`texture_fbx` 会自动加 UV 材质并把贴图复制到 `<name>.fbm/`。
