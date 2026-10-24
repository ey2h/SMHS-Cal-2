# 参数库对表与缺口分析（param_library_gap.md）

> 生成日期：2026-10-22 ｜ 作者：Agent（换机接管续做）
> 数据源：`C:\code\Glass\参数库.mdb`（汇宝 V35 参数库，OleDb 导出）
> 本机重建：`C:\code\Glass\local-data\hb_library.sqlite`（由 `local-data\hb_mdb\*.csv` 重建，**全列有值**）
> 目标库：`C:\code\Glass\src\GlassManagement.Api\Data\SMHS_*.db`

## 0. 重要更正（换机后第一个坑）

本机原有的 `local-data/hb_library.sqlite` 是**坏导入产物**：每张表只有 1 列有值、其余列全 NULL（如 `立柱` 仅 `Ix惯性矩` 有 950 值、其余 0）。它把 `立柱` 膨胀成 950 行、`地区参数` 膨胀成 5706 行，与证据文档（`立柱=95`、`地区参数=634`）严重不符。

**结论：旧 hb_library.sqlite 不可用。** 已改用 `参数库.mdb` 经 OleDb 正确导出 59 表 CSV（`local-data/hb_mdb/`，60 文件），并重建 `hb_library.sqlite`。重建后行数与证据一致：

| 表 | 坏 sqlite(旧) | 重建后(正确) | 证据文档 | SMHS |
|---|---|---|---|---|
| 立柱 | 950(仅1列) | **95**(全列) | 95 | ProfileColumns 95 |
| 横框 | 938(仅1列) | **67**(全列) | 67 | ProfileBeams 67 |
| 地区参数 | 5706(仅1列) | **634**(全列) | 634 | Regions 634 |
| 后锚固材料参数 | 576(仅1列) | **48**(全列) | 48 | AnchorMaterials 48 |

导出/重建脚本：`local-data\export_hb_mdb.ps1`（OleDb，需在 PowerShell 直接执行，子进程 `-File` 在本机沙箱下会报 Jet "network access interrupted"）、`local-data\build_hb_sqlite.py`。

## 1. 六组已确认 1:1 映射的列级核对

### 1.1 地区参数(634) ↔ Regions(634) —— ✅ 完全覆盖
| 汇宝列 | SMHS 列 |
|---|---|
| 地区名称 | Name |
| 50年基本风压 | WindPressure50 |
| 100年基本风压 | WindPressure100 |
| 50年基本雪压 | SnowPressure50 |
| 100年基本雪压 | SnowPressure100 |
| 抗震烈度 | SeismicIntensity |
| 基本地震动峰值加速度 | PeakAcceleration |
| αmax | AlphaMax |
| 极限温差 | TemperatureDiff |

无缺口（SMHS 仅多一个 `Id`）。

### 1.2 立柱(95) ↔ ProfileColumns(95) —— ⚠️ 缺 3 列
| 汇宝列 | SMHS 列 | 状态 |
|---|---|---|
| 型材代号 | ProfileCode | ✅ |
| 截面积 | SectionArea | ✅ |
| Ix惯性矩 | MomentOfInertiaX | ✅ |
| Iy惯性矩 | MomentOfInertiaY | ✅ |
| Wx1抵抗矩 | SectionModulusX | ✅ |
| **Wx2抵抗矩** | — | ❌ **缺**（SMHS 仅 1 个 X 模量） |
| **壁厚** | —（塞在 Specification 文本？） | ❌ **缺独立列** |
| **面积矩** | —（SMHS 是 RadiusOfGyration 回转半径，非面积矩） | ❌ **缺** |
| 受剪腹板总宽度 | ShearArea | ⚠️ 存在但**值错**（SMHS 现 ShearArea=6，语义应为本列） |
| 备注 | Remark | ✅ |

**缺口**：`Wx2抵抗矩`、`壁厚`、`面积矩` 三列；另 `RadiusOfGyrationX/Y` 现 =0 未计算、`ShearArea` 值需纠正。

> **⚠️ 深挖阶段核对更正（2026-10-22）**：经 95/95 逐行比对，`SMHS.SectionModulusY` 实际 = 源 `Wx2抵抗矩`（如 `60/80系列`：源 Wx2=22.883，SMHS SectionModulusY=22.883；`SectionModulusX`=Wx1）。即 **Wx2 已存在，只是错放在 `SectionModulusY` 列（列名误标）**，无需新增列。真实缺口仅为 `壁厚`、`面积矩` 两列 + 计算 `RadiusOfGyration`（现=0）。`ShearArea` 现状已与源 `受剪腹板总宽度` 一致（差异 0 行），无需纠正。（旧结论基于坏 sqlite，已作废。）

### 1.3 横框(67) ↔ ProfileBeams(67) —— ⚠️ 缺 8 列
| 汇宝列 | SMHS 列 | 状态 |
|---|---|---|
| 型材号 | ProfileCode | ✅ |
| 截面积 | SectionArea | ✅ |
| Ix/Iy惯性矩 | MomentOfInertiaX/Y | ✅ |
| Wx1抵抗矩 | SectionModulusX | ✅ |
| **Wx2抵抗矩** | — | ❌ 缺 |
| **Wy1抵抗矩** | — | ❌ 缺 |
| **Wy2抵抗矩** | — | ❌ 缺 |
| **面积矩Sx / Sy** | —（SMHS 是回转半径） | ❌ 缺 2 列 |
| **腹板总宽度tx / ty** | — | ❌ 缺 2 列 |
| **连接面壁厚** | — | ❌ 缺 |
| 备注 | Remark | ✅ |

**缺口**：`Wx2抵抗矩`、`Wy1抵抗矩`、`Wy2抵抗矩`、`面积矩Sx`、`面积矩Sy`、`腹板总宽度tx`、`腹板总宽度ty`、`连接面壁厚` 共 8 列。

> **⚠️ 深挖阶段核对更正（2026-10-22）**：同立柱规律，`SMHS.ProfileBeams.SectionModulusY` 大概率已存 Wx2（待修复主键后复核）。但 **ProfileBeams 现有导入 bug：`ProfileCode` 全为 `'0'`**（67 行无一可用型材号），无法与 `横框.csv` 的 67 个型材号匹配，故本表补库**阻断**，须先修复导入管线再处理。真实缺口（修复后）预计为 `Wy1/Wy2/Sx/Sy/tx/ty/连接面壁厚` 7 列（Wx2 已含于 SectionModulusY）。

### 1.4 螺栓1–7(合计 324) ↔ Bolts(324) —— ✅ 基本覆盖
汇宝 7 张螺栓表（螺栓1..7，行数 48/48/48/48/48/48/36=324）合并入 SMHS `Bolts`。
| 汇宝列 | SMHS 列 |
|---|---|
| 型号 | Model |
| 公称直径 | Diameter |
| 有效直径 | EffectiveDiameter |
| 材质 | Material |
| 拉应力 | TensionStress |
| 剪应力 | ShearStress |
| 备注 | Remark |

缺口：无列级缺口；但导入时须把来源表（螺栓1..7）写入 `Category`（SMHS 已有该列），否则无法区分螺栓种类。

### 1.5 后锚固材料参数(48) ↔ AnchorMaterials(48) —— ⚠️ 缺 3 列
| 汇宝列 | SMHS 列 | 状态 |
|---|---|---|
| 材料 | Material | ✅ |
| 代号 | Model | ✅ |
| 螺杆直径d | Diameter | ✅ |
| 有效截面积As | EffectiveArea | ✅ |
| fstk / fyk / fud,t / fud,v | Fstk/Fyk/FudT/FudV | ✅ |
| **性能等级** | — | ❌ 缺 |
| **有效锚固深度 hef** | — | ❌ 缺 |
| **钻孔直径** | — | ❌ 缺 |
| 备注 | Remark | ✅ |

**缺口**：`性能等级`、`有效锚固深度hef`、`钻孔直径` 三列。

### 1.6 幕墙自重参数(17) ↔ CurtainWallWeights(17) —— ✅ 完全覆盖
| 汇宝列 | SMHS 列 |
|---|---|
| 幕墙类别 | Category |
| 立柱及主体自重 | ColumnWeight |
| 计算横梁时自重 | BeamWeight |
| 软件初始默认值适用范围 | ApplicationRange |

无缺口。

## 2. 汇宝有、SMHS 尚无对应表的清单（需决策落库方式，见计划 Q3）

### 2.1 型材子类型表（24 张，SMHS 仅通用 ProfileColumns/ProfileBeams）
汇宝按构件细分了 20+ 张表，SMHS 用两张通用表承载。是否扩展通用表加 `Subtype` 列，还是新建子表，需定。
- 单元幕樘：单元横框一/二/三（中横框）(各4)、单元立柱一/二(各7)
- 采光顶：主龙骨(88)、副龙骨(67)
- 吊顶：主龙骨(69)、副龙骨(68)
- 雨篷龙骨(84)、钢管点玻龙骨(288)
- 转角立柱 /（单元一）/（单元二）(各1)
- 门窗：横梃一(1)、横梃二（含钢芯）(60)、立梃一(1)、立梃二（含钢芯）(54)
- 栏杆：横龙骨 /（钢芯）(各1)、竖龙骨 /（钢芯）(各1)
- 钢铝结合：钢横框(28)、钢立柱(57)、铝横框(4)、铝立柱(6)
- 百叶叶片(1)、U型玻璃(7)

### 2.2 面板/板材类（约 11 张；SMHS `PanelMaterials` 现有 26 行但缺物理参数）
铝单板(26)、不锈钢板(3)、钛合金板(4)、铜板(1)、钢板(3)、铝塑复合板(1)、蜂窝铝板(1)、单体搪瓷板(8)、复合搪瓷板(13)、铝镁硅合金板(1)。
**缺口**：这些表的弹性模量 / 泊松比 / 密度 / 线膨胀系数 在 SMHS `PanelMaterials` 中多为 null（证据已确认铝单板四项全 null）。

### 2.3 热工类（无 SMHS 对应库）
热工系数(144)、热工数值(15)，以及 `汇宝安装目录\pic\热工\规范\50176\*.inx` 共 62 个城市热工分区表（ANSI/GBK）。需新建 `SMHS_Thermal.db`（计划 Q3）。

### 2.4 查表类（连续梁/静力手册/线材优化）
多跨1(236)、多跨2(328)、静力手册(7)、线材优化原始/合并后(各302)。属计算时查表，需建查表服务或常量表。

## 3. GlassTypes（玻璃）
SMHS `GlassTypes` = **0 行（空表）**。汇宝 `参数库.mdb` 中**没有**独立玻璃表——玻璃强度当前在汇宝计算逻辑里硬编码。填表来源需从帮助文档（B04 玻璃章节）+ 黑箱计算书（④）反推，或从规范取值。属 P0（玻璃验算现无数据支撑）。

## 4. 优先级补库清单（对应计划 §2 待办）

- **P0**：① 补 `ProfileColumns` 的 `Wx2抵抗矩`/`壁厚`/`面积矩` 三列并回填；② 补 `ProfileBeams` 8 列；③ 补 `AnchorMaterials` 的 `性能等级`/`hef`/`钻孔直径`；④ 新建并填充 `GlassTypes`（规范+黑箱来源）；⑤ 纠正 `ProfileColumns.ShearArea`/`RadiusOfGyration` 错误值。
- **P1**：① 11 类板材补弹性模量/泊松比/密度/线膨胀系数；② 24 张型材子表落库（扩展通用表或新建，待 Q3 决策）。
- **P2**：① 热工 `SMHS_Thermal.db` + 62 个 `.inx`；② 查表类（多跨/静力手册/线材优化）。

## 5. 验收口径
逐表行数与源 CSV 一致；抽样值一致；新增数据可经 API 查询。
