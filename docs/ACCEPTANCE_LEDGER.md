# SMHS 复刻 · 验收台账（唯一进度源）

> **用法（强制）**：任何新一轮动手前，先在本表搜关键词。
> - 标 **✅已对拍** = 已有 V31 运行期证据 + 单元测试守护，**不要重测、不要重写**。
> - 标 **🟡引擎✅/UI缺** = 引擎验收过、界面还没接线，**只做 UI，不要动引擎**。
> - 标 **⚠未取证** = 有实现但缺证据链，**只补取证，不要重造**。
> - 标 **⬜未做** = 真空白。
>
> 排期前的唯一 TODO 源 = 本表第 6 节；过程细节见 `.workbuddy/memory/YYYY-MM-DD.md`（日志只记过程，不记状态）。
>
> **最后校验**：2026-10-28 18:40 实测 —— `Calc 582 全绿 / DesktopTests 135 全绿`（含 34 窗体 headless UI 冒烟）

---

## 0. 一页总览

| 维度 | 总量 | 已验收 | 缺口 |
|---|---|---|---|
| V31 菜单叶子项 | 147（18 顶级） | 业务计算线全通 | 见 §6 |
| 计算引擎 | 59 文件 | 全有测试 | 无（`StructuralSealantEngine` 孤儿已删除） |
| 计算界面 View | 49 | 49 接线 | 无（15 个独立工具已接线，共用 SimpleToolView） |
| 计算书章节（8 份样本） | 86 章 txt | 2 章（第 2/3 章） | 12 章待做（全隐框线） |
| 数据表来源登记 | — | 见 `DATA_SOURCE_REGISTER.md` | ✅ 0 处（MeasuredFit 已删、肋应力 9 档已直读，2026-10-28） |

测试基线不能凭记忆，**必须现跑**（命令见 §7）。

---

## 1. 幕墙计算主线（已全闭环 ✅）

| 业务线 | 引擎 | 证据 | 测试 | 状态 |
|---|---|---|---|---|
| 基本参数/风荷载 | `WindLoadEngine` | 第一页链直读 | 多测试间接覆盖 | ✅已对拍 |
| 立柱（单/双/多跨/连续） | `ColumnSpanEngine` `DoubleSpanColumnEngine` `IndeterminateColumnEngine` `ContinuousBeamColumnEngine` | `multispan_presult_v31*.json` `verify_multispan.py` | `DoubleSpanColumnTests` `IndeterminateColumnTests` `ContinuousBeamColumnTests` `MultiSpanAnalyticalTests` | ✅已对拍 |
| 横梁 | `HorizontalBeamEngine` | 屏直读 | `HorizontalBeamTests`(11) | ✅已对拍 |
| 单元板块横梁 | `UnitBeamEngine` | 屏直读 | `UnitBeamTests`(10) | ✅已对拍 |
| 铝合金立柱 | `AlloyColumnEngine` | 屏直读 | `AlloyColumnTests`(13) | ✅已对拍 |
| 玻璃 | `GlassEngine` | 玻璃屏链 | `GlassEngineTests` + 6 个复合玻璃测试 | ✅已对拍 |
| 玻璃面板（JGJ102 表） | `GlassPanelEngine` | 参数库表 | `GlassPanelEngineTests`(5) `CoefficientTableTests`(15) | 已定性非 V31 主链（见 §3） |
| 连接件 | `ConnectEngine` | 屏直读 | `ConnectEngineTests`(8) | ✅已对拍 |
| 埋件 | `EmbedEngine` | 屏直读 | `EmbedEngineTests`(76) | ✅已对拍 |
| 胶类 | `SealantEngine` | 屏直读 | `SealantEngineTests`(8) | ✅已对拍 |
| 结构胶（宽/厚） | `StructuralSealantEngine` | — | **无** | ✅已删除（2026-10-28，备份 local-data/recon/deleted/） |
| 金属板（8 种） | `PlateEngine` | `V31_PLATE_UI.md` | `PlateEngineTests`(43) | ✅已对拍 |
| 石材（短槽/通槽/背栓/金属框） | `StonePlateEngine` | `V31_STONE_PLATE_UI.md` | `StonePlateEngineTests`(52) | ✅已对拍 |
| 人造板 | `ManmadePlateEngine` | `V31_MANMADE_PLATE_UI.md` | `ManmadePlateEngineTests` | ✅已对拍 |
| 背栓点挂 | `BackboltPlateEngine` | `V31_BACKBOLT_PLATE_UI.md` | `BackboltPlateEngineTests` | ✅已对拍 |
| 全玻璃幕墙（8 构型） | `FullGlassEngine` | `b7_probe27_fullglass_sigma.json` + 肋厚表直读 | `FullGlassEngineTests`(22) | ✅已对拍 |
| 点式肋（四点驳接） | `DianshiGlassEngine` | `b7_probe25_rib_sigma.json` | `DianshiGlassEngineTests` | ✅已对拍 |
| U 型玻璃 / 地板玻璃 | `UGlassEngine` `FloorGlassEngine` | 参数库 U 表 | `UGlassFloorEngineTests` | ✅已对拍 |
| 门窗（玻璃/竖梃/横梃/连接件） | `DoorConnectorEngine` `DoorMullionEngine` | `door_conn_probe4a.json`(40 case) `cw29_inventory.json` | `DoorConnectorEngineTests`(12) `DoorMullionEngineTests`(10) | ✅已对拍 |
| 采光顶（8 坡型共用） | `SkylightEngine` | 单坡屏直读 | `SkylightEngineTests` | ✅已对拍 |
| 雨篷（5 模块） | `AwningEngine` | 悬臂梁/焊缝/玻璃/埋件逐位 | `AwningEngineTests` | ✅已对拍 |
| 热工 · 透明（CW26） | `ThermalGlassEngine` | `b7_probe23_multipane.json` `b7_probe22_airgap.json` `b7_probe28_residential.json` | `ThermalGlassEngineTests`(11) | ✅已对拍 |
| 热工 · 非透明（CW27） | `ThermalWallEngine` | `b7_probe11_cw27.json` `b7_probe12_cw27.json` | `ThermalWallEngineTests`(17) | ✅已对拍 |
| 物理性能 16 子项 + 137 隔声 | `WindResistanceEngine` `WaterTightnessEngine` `DoorWaterTightnessEngine` `SoundInsulationEngine` | `b7_probe29_kangfengya.json` `b7_probe32_panxing.json` | 各测试共 49 | ✅已对拍 |
| 外遮阳 134 + 加权 136 | `ExternalShadingEngine` `WeightedAverageEngine` | GB50189 表 A.0.1 | `ExternalShadingEngineTests`(4) `WeightedAverageEngineTests`(3) | ✅已对拍 |
| 荷载组合 | `LoadCombinationEngine` | 规范组合表 | `LoadCombinationEngineTests`(14) | ✅已对拍 |

---

## 2. 独立工具线（B8）—— 引擎 ✅ / UI ✅（2026-10-28 全接线）

「其他计算」菜单 22 项。引擎全部有测试，界面已全部接线（15 项独立工具共用 `SimpleToolView`，见 §2.2）。

### 2.1 已有界面 ✅

| 菜单 | 引擎 | View | 证据 |
|---|---|---|---|
| 截面计算 | `SectionCalculationService` | `SectionCalcView` | Green 定理抽查 |
| 常用静力计算 | `StaticCalcService`（7 支撑条件） | `StaticCalcView` | 7 条件对拍 |
| 建筑预计雷击次数计算 | `LightningEngine` | `LightningView` | 524 城 Td（`*.inx`）+ V31 实机 0.272 对拍（`t7_lightning_v31.json`） |
| 插值计算器 | `InterpolationEngine` | `InterpolationView` | b7 直读 0.12945 |
| 方程求解 | `EquationEngine` | `EquationView` | b7 直读（二次/三次） |
| 牛腿计算 | `CorbelEngine` | `CorbelView` | `b8_probe_corbel.json` 73 链 + `b8_corbel_phi_table.json` 31 点 |
| 型材优化 | `CuttingStockEngine` | `CuttingStockView` | ⚠FFD 替代，非 V31 私有启发式 |
| 埋件单独计算 / 转角立柱 | `EmbedEngine` `CornerColumnEngine` | `EmbedCalcView` + `CW31` 静态壳 | 转角立柱 **引擎未接线**（壳是静态复刻） |
| 其它玻璃计算 | `OtherGlassEngine` | `OtherGlassCalcView` | 屏直读 |

### 2.2 引擎 ✅ / UI ✅（2026-10-28 已全部接线）

15 项统一走「通用工具壳」：`SimpleToolViewModel`（字段列表驱动）+ `SimpleToolView`（单一 XAML）+ `ToolField`（文本/下拉两态）；
`ViewLocator` 一条规则覆盖全部，`CalcFormCatalog` 各一条，`CalcMenuMap.BySub` 各一条。
守护测试 `SimpleToolVmTests`（18 条：默认算例出基准值 / 全接线 / 空输入回落 / 下拉切换）。

| 菜单 | 引擎（已测试） | 界面载体 | 状态 |
|---|---|---|---|
| 焊缝强度计算 | `WeldEngine` + `WeldSectionTable` | `WeldToolViewModel` | ✅ |
| 螺栓群计算 | `BoltGroupEngine` | `BoltGroupToolViewModel` | ✅ |
| 埋件厚度计算 | `AnchorPlateEngine` | `AnchorPlateToolViewModel` | ✅ |
| 建筑植筋计算 | `RebarAnchorEngine` | `RebarAnchorToolViewModel` | ✅ |
| 钢销/螺栓(扭+剪)计算 | `PinBoltEngine` | `PinBoltToolViewModel` | ✅ |
| 型材螺纹计算 | `ThreadEngine` | `ThreadToolViewModel` | ✅ |
| 自攻钉及自攻自钻螺钉计算 | `SelfTappingScrewEngine` | `SelfTappingScrewToolViewModel` | ✅ |
| 排烟面积计算 | `SmokeVentEngine` | `SmokeVentToolViewModel` | ✅ |
| 开启扇通风面积 | `OpeningVentEngine` | `OpeningVentToolViewModel` | ✅ |
| 百叶窗通风面积 | `LouverVentEngine` | `LouverVentToolViewModel` | ✅ |
| 型材米重计算 | `WeightPerMeterEngine` | `WeightPerMeterToolViewModel` | ✅ |
| 型材热应力计算 | `ThermalStressEngine` | `ThermalStressToolViewModel` | ✅ |
| 型（板）材热应力伸缩量计算 | `ThermalExpansionEngine` | `ThermalExpansionToolViewModel` | ✅ |
| 多点锁及开启扇扇框计算 | `MultiPointLockEngine` | `MultiPointLockToolViewModel` | ✅ |
| 转角立柱计算 | `CornerColumnEngine` | `CornerColumnToolViewModel`（替换 CW31 静态壳） | ✅ |

---

## 3. 未验收 / 半成品（⚠）

| 项 | 现状 | 缺的证据 |
|---|---|---|
| 型材优化 192 | FFD 引擎 + UI 已通 | ⚠**未还原**：① `0x527690` 是窗体序列化数据非算法；② VB6 字符串池无 xref 静态定位不可行；③ 运行期差分卡在「左表 MSFlexGrid 内容无法程序化读写 + 剪切板输入格式未知」。结论：保留 FFD，方案行不保证逐行一致。harness `b8_t5_cutdiff.py` 已就绪待人工喂件 |
| 牛腿 170 | ✅ **单一水平斜梁形态，完整闭合**（默认算例 200.08/430.149/46.075/120.916/10.878 逐位 + 扰动 H=1200 命中） | **「样式二/三/竖直四」= 历史误记，已彻底推翻**：干净盘点（`b8_corbel_inventory.py`）证实牛腿窗体只有一套输入（尺寸 L/H/θ + 荷载4 + 主梁/斜梁截面），无样式切换入口；「下一步」翻页进结果页非样式切换。早期「样式4种」是把多模块容器里百叶窗朝向 combo 误归牛腿（源头 2026-10-28 日志 P14 判型行）。扭剪帧=独立菜单174(PinBoltEngine 已闭合)。剩余仅 φ 表 λ∈[37.8,151.1] 外推为已知限制 |
| 雷击 181 | 主公式 + 524 城 + **周边 10 档全对拍** | ✅ Ng=0.1·Td（2010 版，`t7_lightning_v31.json`）；**周边「等效面积要求」低组 5 档（H=90）+ 高组 5 档（H=150）全部反解对拍 ✅**（低组：单座0.272/−0.75D²/πD²/4/−1.5D²/[LW]；高组 D=H：0.482/−0.5H²/πH²/4/−H²/[LW]，证据 `t7_surround_options.json`+`t7_groupB2.json`；引擎 `Surround` 枚举 10 档 + 11 条守护测试）；★关键语义：**D 公式跟选项组走不跟实际 H 走**（H=150 选低组预演 N=0.228=√7500 代入值，程序不校验 H）；仅「各部位高不同」1 档未定（N=0.005 与 H 无关，单点无法定型） |
| 玻璃面板 JGJ102 | 4 组表实现 | ✅ 已定性：**非 V31 主链**（V31 四边支承走 `GlassEngine` 表，`0xA78E85–0xA7A037` 逐地址已读 + 运行期乘数 0.0631/0.00606 命中）；`GlassPanelEngine` 4 组表为 Demo-only 规范值 |
| 双跨立柱 α 表 | 实现 | ✅ 已定性：**非 V31 主链**（V31 双跨走 `ContinuousBeamColumnEngine`，0.00677/0.1/0.6/1.5 已读）；`ColumnSpanEngine` α 表为 Demo-only 规范值 |
| 结构胶 `StructuralSealantEngine` | 已删除 | ✅ 孤儿（全仓零引用、无测试），2026-10-28 删除，备份 `local-data/recon/deleted/` |
| 隔声 | 质量定律式 + 8 结构 + 三分级 | **✅ 基本闭合（2026-10-28 T11）**：① 气隙项修正——实机气隙档扫描（5/6/9/12/15mm → R=31.203/31.303/31.603/31.903/32.203）证实 **R=13.5·lg(总厚×2.56)+13+0.1×气隙总厚(mm)**；**公式来源=指令级还原**：frida 链 Mul(13.5,lg)+Add(13)+Add(1.2)（ra 0x172BD0C/D20/D34）+ IDA `fmul ds:dbl_401DA0`(=0.1) 指令，常量池实锤 13.5/2.56/0.1（0x40D9A0/0x40D9A8/0x401DA0），8 结构默认态+气隙 5 档 13 点全对拍（引擎已修+6 条守护）；**组合墙隔声已实现**（链级还原 ra 0x1731478-0x1731524 + 扰动实证 3 点：R_组合=R_玻璃+10·lg((t_墙+t_等效)/t_等效)，`ComputeWall`+3 条对拍；第二框干净默认内部值 39.06，外层显示 '15' 系两层不同步） |② 「50121 边界外推」**消解**：V31 厚度为固定档位 combo（4/5/6/8/10/12/15/19/20），R 对数关系永不产生整数边界，边界状态在 V31 输入域不可达（引擎 r<20 与 r≤20 在可达域内无差别）；三标准分级 5 点直读已对拍；**★2026-10-28 T12c 分级纠正：实机隔声帧 RichText 直读，三标准分级恒 = 国标级 + 1**（26.639→3/4/2、31.903→4/5/3、34.703→4/5/3、34.78→4/5/3 四点验证；旧 probe35 期望 2/3/1 系读错 RichText，已改引擎+测试） |
| 多跨 `MultispanCheck` [12] | 0 FAIL | 风荷载段容差已放宽 1e-6→5e-4（引擎 V35 round4 vs 期望未舍入），属已知 |
| CW19 物理性能 | ✅ **5 计算页输出通道逐字复刻（2026-10-28 T12）** | 139/149 抗风压 + 140/150 水密 + **137 空气声隔声**；模板 = V31 实机 RichText 原文（条文 + 表2/表16/表17 ASCII 框线 + 分步计算 + 结论），`PhysicalPerformanceOutput` 4 方法 + **11 条逐字对拍**；★坑1：CW19 单 FormDC 多 Frame 复用、隐藏隔声帧 RichText 常驻，**必须按可见计算帧筛选**（b12→b12b）；★坑2：模板统一 LF、输出转 CRLF（V31 RichText 为 CRLF），否则逐字对比失败；12 说明页无计算（IE 条文 PhysicalPerformanceTexts 已覆盖），148=147 别名、156 菜单未打开（均非独立项） |
| 21 样式切换 | 已证 V31 无 diff | — |
| 断热/冷弯薄壁勾选 | 已证 V31 无 diff | — |

---

## 4. 计算书 P17（`docs/recon/calcbooks/`）

**取证结论**：V31「生成计算书」= **注册锁**，不是缺模块（窗体/模板/`.CAM` 索引齐全，点按钮弹「试用版本不能出计算书」）。
⇒ 对拍源 = 安装目录 `计算书样本文件集/*.DOC`（8 份 / **86 章** txt 已转录），**不需要 V35**。

| 章 | 全隐框玻璃幕墙（样本 15 章） | 状态 |
|---|---|---|
| 01 | 计算引用的规范、标准及资料 | ⬜ |
| 02 | 基本参数 | ✅`BookText.Basic()` 逐行守护 |
| 03 | 幕墙承受荷载计算 | ✅`BookText.Loads()` 135 行 / 23 插值 |
| 04 | 幕墙立柱计算 | ⬜ |
| 05 | 幕墙横梁计算 | ⬜ |
| 06 | 幕墙玻璃的选用与校核 | ⬜ |
| 07 | 连接件计算 | ⬜ |
| 08 | 幕墙埋件计算(土建预埋) | ⬜ |
| 09 | 幕墙转接件强度计算 | ⬜ |
| 10 | 幕墙焊缝计算 | ⬜ |
| 11 | 胶类及伸缩缝计算 | ⬜ |
| 12 | 幕墙板块压板计算 | ⬜ |
| 13 | 玻璃副框计算 | ⬜ |
| 14 | 玻璃承托件计算 | ⬜ |
| 15 | 附录 常用材料力学及物理性能 | ⬜ |

其余 7 份样本（铝单板 12 / 石材 GRC 12 / UHPC 11 / 单元幕墙 13 / 点式钢管 6 / 节能 8 / 门窗 9 章）尚未开章。
做法固定：**样本 → 脚本生成 C# 模板（不手抄）→ 注入 `BookText` + 扩展 `BookTextVars` → 逐行守护测试**。

---

## 5. 数据来源

统一登记在 **`docs/DATA_SOURCE_REGISTER.md`**（五级出处：参数库 / 安装目录 `.inx` / 程序内置 / 运行期直读 / ⚠规范值待取证）。
新增任何数据表必须同步登记，否则视为未完成。

---

## 6. 唯一 TODO 源（按优先级）

| # | 任务 | 类型 | 预估 |
|---|---|---|---|
| T2 | 计算书 04–15 章（§4） | 模板 + 守护 | 大 |
| T3 | ~~结构胶孤儿文件判定~~ → **✅ 已删除**（备份 `local-data/recon/deleted/StructuralSealantEngine.cs`） | 清理 | 小 |
| T4 | 计算书其余 7 份样本开章 | 模板 + 守护 | 大 |
| T5 | ~~型材优化算法一/二 IDA 还原~~ → **⚠ 已收口：未还原，保留 FFD**（`0x527690` 是窗体序列化数据非算法；VB6 字符串池无 xref 静态不可定位；差分卡「左表 MSFlexGrid 不可程序化读写 + 剪切板输入格式未知」。harness `local-data/recon/b8_t5_cutdiff.py` 就绪待人工喂件，出差异报告） | 取证 | 中 |
| T6 | ~~牛腿样式二/三/竖直四 + 扭剪帧~~ → **✅ 已闭合：都是历史误记**。干净盘点证实 V31 牛腿=**单一水平斜梁形态**（无样式切换入口，「样式二/三/竖直四」是把多模块容器里百叶窗朝向 combo 误归牛腿）；扭剪帧=独立菜单 id=174（`PinBoltEngine` 已闭合）。φ 表 λ∈[37.8,151.1] 外推为已知限制 | 取证 + 引擎 | 大 |
| T7 | ~~雷击 Ng 版本直读 + 周边建筑修正~~ → **✅ 全部闭合**：Ng 已直读（2010 版）+ 周边低组 5 档（H=90）+ 高组 5 档（H=150）全反解对拍（Calc 562）。仅「各部位高不同」1 档未定（单点无法定型，登记遗留） | 取证 | 小 |
| T8 | ~~玻璃面板/双跨 α 表规范值取证~~ → **✅ 已定性（两者均非 V31 主链，权威表见 GlassEngine/ContinuousBeamColumnEngine，已提取）** | 取证 | 小 |
| T9 | **V35 限期版授权核验**（联网+时钟，见 `docs/V35_LAUNCH_FIX.md` §7） | 环境/授权 | 小 |
| T10 | ~~雷击 181 窗体打开通道~~ → **✅ 已解决：WM_COMMAND 181 只发给 MDI 即可；此前弹「主龙骨」是误发给了 Main 窗。城市选择流程已定（选择→省/市列表→确定→预演）** | 取证前置 | 小 |

---

## 7. 测试怎么跑（别凭记忆报数字）

```bash
# 引擎/计算（快，~10s）
cd /c/code/Glass && /c/Users/Jack/.dotnet/dotnet.exe test \
  tests/GlassManagement.Calc.Tests/GlassManagement.Calc.Tests.csproj --no-restore -v q
#   ⇒ 已通过: 571

# 桌面（~40s）
cd /c/code/Glass && /c/Users/Jack/.dotnet/dotnet.exe test \
  src/GlassManagement.DesktopTests/GlassManagement.DesktopTests.csproj --no-restore -v q
#   ⇒ 已通过: 134

# 多跨独立校验
#   dotnet run --project src/GlassManagement.Calc.MultispanCheck  ⇒ 0 FAIL
```

坑（2026-10-28 已定位并修）：
1. **`GlassManagement.Calc.csproj` 这个文件不存在**（实为 `GlassManagement.CalcCalc.csproj`，AssemblyName 仍是 Calc）。
   `GlassManagement.Api` 与 `GlassManagement.CalcCalc.Demo` 的 `ProjectReference`、以及 `GlassManagement.sln` 两处项目路径都写的这个名字
   → Api 报 MSB9008 + CS0234/CS0246，整个 sln 构建失败。已全部改为真实文件名，Api/Demo 原始缺陷一并修好。
2. **6 个项目的 `obj/project.assets.json` 是另一台机器（用户 `ey2h`）的产物**，`packageFolders` 写死
   `C://Users//ey2h//.nuget//packages` 与 `C://Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages`（本机都不存在）
   → ResolvePackageAssets 抛 MSB4018。已把 packageFolders 改成本机 `C://Users//Jack//.nuget//packages`。
3. `GlassManagement.UITests` / `GlassManagement.SectionTool` 依赖 Avalonia 11.2.5、Microsoft.Bcl.AsyncInterfaces 6.0.0
   等本机 NuGet 缓存里没有的版本，且本环境 restore 会崩（见下条），已**从 sln 移出**（项目文件保留，需要时单独 restore 后再加回）。
4. 仓库根的 `nul` 是 Git Bash 下 `taskkill … > nul` 误建的垃圾文件（未跟踪、已在 .gitignore），它是 NuGet
   `Value cannot be null. (Parameter 'path1')` 的诱因；但安全删除机制把它当设备名 `\\.\nul` 保护，无法删除。
5. 不带 `--no-restore` 会报 `Value cannot be null. (Parameter 'path1')`；换输出目录要加 `-t:Rebuild`；`CS2012 锁定` → `taskkill //F //IM VBCSCompiler.exe`。

**测试 → 覆盖项反查**：测试文件名基本等于引擎名（如 `CorbelEngineTests` ↔ `CorbelEngine`）。少数例外：
`BookTextTests`→计算书模板 / `CoefficientTableTests`→`ColumnSpanEngine`+`GlassPanelEngine` / `ThermalSmokeEngineTests`→`ThermalStressEngine`+`SmokeVentEngine` / `UGlassFloorEngineTests`→`UGlassEngine`+`FloorGlassEngine` / `StaticCalcEngineTests`→`StaticCalc/*` 7 条件 / `MultiSpanAnalyticalTests`→`MultiSpanStaticEngine`。
