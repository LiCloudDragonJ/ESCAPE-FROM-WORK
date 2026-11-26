# ESCAPE FROM WORK — 导视 / 标识贴花贴图（Signage）

办公巨构「EFW TOWER」的导视系统贴图集，供灰盒阶段先把墙面上"该有的牌子"贴上去。
全部为 **PNG RGBA**、**独立贴花（decal）**，不要求可平铺；牌面四周留 2–4px 透明描边 + 圆角，避免硬切边。

- 生成脚本：`generate_signage.py`（Pillow 纯代码绘制，无外部图片依赖，幂等，可反复重跑）
- 重跑方式：
  ```
  C:\Users\17613\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe generate_signage.py
  ```
- 缩略图总览：`_contact_sheet.png`（2134×2002，带文件名标签）
- `.meta` 由 Unity 首次导入时生成，本目录不手工提供。

---

## 一、文件清单

| 文件名 | 尺寸 (px) | 用途 | 建议摆放高度与尺寸 |
| --- | --- | --- | --- |
| `Exit/S_Exit_Green.png` | 1024×512 | 安全出口吊牌（ISO 7010 E001 风格：奔跑小人 + 门洞 + 箭头，绿底白图，中英双语） | 600×300 mm，门洞正上方 2100–2400 mm（吊挂或贴门楣） |
| `Exit/S_Exit_ArrowLeft.png` | 512×512 | 左侧出口方向牌（绿底白箭头 + EXIT / 出口） | 300×300 mm，走道墙面 / 转角，中心 1400–1600 mm |
| `Exit/S_Exit_ArrowRight.png` | 512×512 | 右侧出口方向牌 | 同上，与 Left 成对使用 |
| `Floor/S_Floor_Plate.png` | 512×512 | 楼层号母版（占位 "00F"，金属灰底 + EFW TOWER 品牌栏），供程序化改字 / 换图 | 400×400 mm 方形牌，电梯厅 1500–1800 mm |
| `Floor/S_Floor_40F.png` … `S_Floor_50F.png` | 1024×512 | 楼层号牌，共 11 张（40F–50F），金属拉丝底 + 超大数字 + 右侧 EFW TOWER 品牌栏 | 800×400 mm，楼梯间 / 电梯厅墙面，中心 1500–1800 mm；也可作电梯门套上方的楼层提示条 |
| `Evac/S_Evac_Map.png` | 1024×768 | 楼层疏散平面图牌（标题、抽象平面、YOU ARE HERE 红点、双出口绿色疏散箭头、图例、注意事项、底部警示条） | 600×450 mm，疏散楼梯门口旁墙面，中心 1400–1600 mm（视平线） |
| `Elevator/S_Elevator_Panel.png` | 512×1024 | 电梯轿厢按钮面板（40–50 层按钮阵列、上下行指示、警铃、开门/关门、喇叭孔，深色拉丝金属） | 200×400 mm，轿厢内操纵盘位置，中心 900–1200 mm（可整体当贴图贴在按钮盒表面） |
| `Elevator/S_Elevator_Hall.png` | 1024×512 | 电梯厅楼层指示牌（ELEVATOR + 当前楼层大数字 45F + 上下行箭头，绿色点亮为当前方向） | 700×350 mm，电梯门上方门套，中心 2200–2400 mm |
| `Wayfinding/S_Way_Stairs.png` | 512×512 | 楼梯间标识（阶梯图形 + 楼梯 STAIRS） | 250×250 mm，楼梯门旁，中心 1500 mm |
| `Wayfinding/S_Way_Toilet.png` | 512×512 | 卫生间标识（男/女图形 + 卫生间 TOILET） | 250×250 mm，卫生间门旁，中心 1500 mm |
| `Wayfinding/S_Way_Server.png` | 512×512 | 机房 / 设备间标识（机柜图形 + 机房 SERVER ROOM） | 250×250 mm，机房门旁，中心 1500 mm |
| `Wayfinding/S_Way_Arrow.png` | 512×256 | 长条方向箭头牌（白底深灰箭头，可整牌染色 / 调色） | 400×200 mm，走道吊挂或墙贴，中心 1600–1800 mm |
| `Room/S_Room_Plate_Master.png` | 512×256 | 门牌母版（占位 "5001"，白底深灰字 + 左侧蓝色竖条 + 右下部门名） | 250×125 mm，门框侧墙，中心 1500–1600 mm |
| `Room/S_Room_5001.png` … `S_Room_5012.png` | 512×256 | 门牌，共 12 张（5001–5012），部门名按 8 个轮换：行政 ADMIN / 财务 FINANCE / 研发 R&D / 会议 A MEETING A / 会议 B MEETING B / 档案 ARCHIVE / 机房 SERVER / 茶水间 PANTRY | 同上 |
| `_contact_sheet.png` | 2134×2002 | 全部 35 张贴图的缩略图拼版（带文件名标签），用于验收 / 资产盘点 | 仅工具图，不进游戏 |

合计 **35 张** 可用于游戏的贴图 + 1 张缩略图总览。

---

## 二、设计规范

| 项目 | 取值 |
| --- | --- |
| 安全绿 | `#1E7B4D`（出口牌、疏散箭头、状态点亮、品牌小色块） |
| 墨黑 | `#202327`（文字、深色底、图标） |
| 纸白 | `#ECEDE8`（浅色牌底、图面） |
| 警示黄 | `#E8B32A`（疏散图底部警示条） |
| 品牌蓝 | `#2A5C8A`（门牌左侧竖条） |
| 警示红 | `#B0302A`（YOU ARE HERE 红点、消火栓/警铃） |
| 中文 | 微软雅黑 Regular / Bold（`C:\Windows\Fonts\msyh.ttc`、`msyhbd.ttc`） |
| 英文 | Arial Regular / Bold（`arial.ttf`、`arialbd.ttf`），大标题做 +tracking 字距 |
| 牌面质感 | 金属牌用 numpy 生成拉丝噪声 + 倒角高光；纸质牌用 1–2% 噪声纸纹 |
| 透明 | 牌面本体不透明；四边留 3px 透明 + 圆角（半径 8–12px）。方向箭头 / 图标为真透明背景 |

## 三、Unity 导入建议

- Texture Type：`Sprite (2D and UI)` 或 `Default`，**Alpha Is Transparency 勾选**，sRGB 勾选，Mip Maps 开启。
- 贴花使用（URP Decal Projector / HDRP Decal）：用 `Albedo` 通道直接喂给 Decal 材质的 Base Map，Alpha 走 Opacity；注意贴花投影会影响所有不透明表面。
- 若用 Quad + Unlit/Transparent 平贴：材质 Blend 设为 `SrcAlpha OneMinusSrcAlpha`，`ZWrite Off`，`Queue Transparent`，贴面偏移 1–2cm 防 z-fighting。
- 分辨率换算：楼层牌 800mm 宽 ↔ 1024px ≈ **1.28 px/mm**，1–2m 观看距离下数字与中文均清晰；疏散图按 600mm 宽使用 ≈ 1.7 px/mm，桌面级阅读足够。
- 同一张图在游戏里允许按需缩小使用（512 宽的出口方向牌贴 300mm 仍 >1.7 px/mm）。

## 四、脚本结构（便于二次修改）

```
generate_signage.py
├─ 字体 / 调色板常量（GREEN, INK, PAPER, YELLOW, BLUE, RED …）
├─ 文本工具：ink_bbox / ink_size / fit_font / draw_text_c / draw_text_l / draw_text_r / draw_tracked_l
├─ Pen：4× 超采样画笔（line / poly / circle / rect / rrect），坐标仍按最终像素写
├─ brushed_metal / metal_edge：拉丝金属底 + 倒角
├─ rounded_alpha：统一加圆角 + 透明边
├─ 图标：icon_running_man / icon_arrow_* / icon_tri_* / icon_stairs / icon_person
│        / icon_bell / icon_extinguisher / icon_door_arrows / icon_server
└─ gen_exit_green / gen_exit_arrows / gen_floors / gen_evac_map
   / gen_elevator_panel / gen_elevator_hall / gen_wayfinding / gen_rooms / gen_contact_sheet
```

- 所有尺寸、坐标都是"最终像素"，超采样只在 `Pen` 内部处理，改数值即所见即所得。
- 改楼层范围：`gen_floors()` 里 `range(40, 51)`；改门牌：`gen_rooms()` 的编号列表与 `DEPTS` 部门表。
- 改文字语言 / 部门名：直接改对应 `draw_text_*` 的字符串；字号用 `fit_font()` 可避免溢出。

## 五、已知不足

- 疏散平面图是**示意性布局**，与程序化生成器的真实房间划分无关；只表达"两个出口 + 楼梯 + 电梯不能用"。
- 电梯面板的按钮是平面绘制，没有凹凸 / 接触阴影；近看（<0.5m）会露馅，1m 外正常。
- 门牌只有 12 个编号（5001–5012）与 8 个部门名轮换，未覆盖全楼层所有房号，需要时按母版批量生成。
- 中文使用系统字体渲染成位图，未做字体授权确认（微软雅黑为系统字体，随 Windows 授权使用）。
