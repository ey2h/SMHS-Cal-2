# 复刻版 MVVM 分层与公式出处清单

> 本文回答两件事：**① 每个公式/流程在 V31 源码中的出处依据；② 各功能模块与 MVVM 三层的对应关系。**
> 计算书与手册只作原理解释，**公式一律以程序代码为准**（已抓到反例：模板写 `5qL⁴/384EI*(1-2.4(A/L)²)`，
> 代码实为 `[5-24(A/L)²]`）。

---

## 一、分层总览

```
GlassManagement.Calc            ← Model 层（纯计算，零 UI 依赖）
├── Engines/HorizontalBeamEngine.cs     横梁算式（每个系数带 V31 地址）
├── Engines/WindLoadEngine.cs           风荷载 + VbaRound（全项目唯一的取整实现）
├── Engines/MaterialAllowables.cs       材质设计值 f / fv
├── Engines/{Column,DoubleSpan,Continuous,Indeterminate,MultiSpan,Alloy}*  立柱各分支
└── Data/ParamLibrary*.cs               参数库 59 表（mdb → CSV → 强类型）

GlassManagement.Desktop/ViewModels  ← ViewModel 层（状态 + 命令 + 编排，不出现 UI 控件类型）
├── Calc/CalcSession.cs                 共享会话（跨屏参数唯一的真源）
├── Calc/CalcWizardViewModel.cs         向导导航（1/3 基本参数 → 2/3 立柱 → 3/3 横梁）
├── Calc/BeamCalcViewModel.cs           横梁屏：输入 → 引擎 → PreviewLine 列表
├── Calc/ColumnCalcViewModel.cs         立柱屏
└── Calc/PreviewLine.cs                 预演输出的一行（Text/Color/Decoration）

GlassManagement.Desktop/Views/Calc ← View 层（只有绑定 + 布局）
├── BeamCalcView.axaml + .cs            横梁屏；**code-behind 仅 InitializeComponent**
├── ColumnCalcView.axaml + .cs          立柱屏
├── ViewLocator.cs                      ViewModel → View 映射
└── CW*.axaml                           从 V31 控件树导出的像素级快照（无绑定，仅作对照，不参与计算）
```

**约束（可机械校验）**：`Views/Calc/BeamCalcView.axaml.cs` 除构造函数外无任何成员；
`BeamCalcView.axaml` 中不出现 `Click=`、不出现数值字面量控制计算；所有可交互控件都绑定 `Command`。

---

## 二、横梁模块：公式 ↔ 源码出处（逐条可追溯）

对拍脚本 `local-data/recon/verify_beam_trap.py` → **64/64 逐位一致**（6 组样本）。
取证方法：`v31_frida_ops.py` 挂 `__vbaVarMul/Div/Add/Sub/Pow` 读每次运算「实参 → 出参」（语义 `out = c2 op c1`）。

### 2.1 输入与形状参数（流程）

| 量 | 取值规则 | 源码/实机依据 | 状态 |
|---|---|---|---|
| 跨度 B | 立柱左右两侧分格宽**取大** | 改值实验 4 组：900/1100→1100、1200/1100→1200、1200/1300→1300、1600/1300→1600；帮助 `html/B03.htm`「自动选最大一跨，不计立柱宽」 | ✅ |
| 自重高度 H | **上**分格高，不平均 | 上 1500/下 1380 → 槽 `[19]=0.450=1500×g`（若平均应 0.432）；帮助「非挂式横梁承上部板块自重」 | ✅ |
| `r` | `r = min(B, H)` | B>H 时 qk 实参 `0x8CDF34` 用 1000；B≤H 时 `0x8AEB0F` 用 1100 | ✅ |
| `u` | `u = r/(2B)` | `0x8D2DB7` H/2 → `0x8D2DD6` /B | ✅ |
| 自重 g | 参数库「幕墙自重参数」计算横梁时自重 ÷10⁶ | 单片 300 / 中空+夹层 550 N/m²，切玻璃时槽 `[10]` 0.0003↔0.00055 同步 | ✅ |
| αmax | 参数库「地区参数」表 αmax 列 | 全局 `0x1C913D8`；切上海（7 度 0.08）后预选 Wy 2583.574→3007.074 | ✅ |
| f / fv | 材质牌号设计值 | 横梁页默认 6063-T5 → f=90 / fv=55（与预演输出一致） | ✅ |
| 截面 | 参数库「横框」表 | 轴惯性矩 11.581→115810（`0x8B3727`）、抗弯矩 3.662→3662（`0x8B3C15`）、面积矩 2.994→2994（`0x8B4565`） | ✅ |

### 2.2 荷载

| 量 | 公式 | 源码出处（地址 + 实参） |
|---|---|---|
| qk | `qk = round3(r·wk/1000)` | `0x8AEB0F` 1100×0.001285=1.4135；`0x8CDF34` 1000×0.001264=1.264 |
| qd | `qd = round3(qk×1.5)` | `0x8AEBF4` 1.414×1.5=2.121；`0x8CE019` 0.994×1.5=1.491 |
| 地震系数 | `kq = 5·αmax` | `0x8AED5D` 0.16×5=0.8；`0x8CE32C` 同（0.16 读自 `0x1C913D8`，`0x8AEC74`/`0x8CE099` fld） |
| gp（地震项） | `gp = round3(r·g·kq·1.4)/2` | `0x8CE44F` 800×0.00024=0.192 → `0x8CE534` ×1.4=0.2688 → `0x8CE657` 0.269×0.5=0.1345 |
| q_y | `q_y = round3(qd + gp)` ← **二次取整** | `0x8CE664` 得 1.6255，但 `0x8CE886` 用的是 **1.626**（2000×800 组唯一能暴露） |

### 2.3 内力与截面预选

| 量 | 公式 | 源码出处 |
|---|---|---|
| M_y | `M_y = q_y·B²·(3−(r/B)²)/24` | `0x8CE8BE` r/B → `0x8CE8D2` Pow(2) → `0x8CE8DF` Sub(3−r²) → `0x8CE8EC` ×q_y·B² → `0x8CE900` /24 |
| M_x | `M_x = q_x·B²/8`，`q_x = round3(H·g·1.3)` | `0x8CEA04` H×g=0.3 → `0x8CEAE9` 1.3×0.3=0.39 → `0x8CEC3D` /8 |
| 预选 Wx | `M_x / f` | `0x8DEDA6` 109687.5/90=1218.75 |
| 预选 Wy | `M_y / f` | `0x8CE90D` 区段 → 904.139 / 3007.074 等 |
| 抵抗矩取法 | **双向各自** `min(Wx1,Wx2)`、`min(Wy1,Wy2)` | 40/60：Wx=min(4100,3662)=3662、Wy=min(5763,6639)=5763（与预演「选用值」一致） |
| 挠度系数 | `k_df = (3.125 − 5u² + 2u⁴)/240` | `0x8D2DF7` 5u² → `0x8D2E04` 3.125−5u² → `0x8D2E64` u⁴ → `0x8D2E7E` +2u⁴ → `0x8D2E9F` /240 |
| 预选 Iy | `qk·B⁴·k_df/(E·lim_h)` | `0x8D2EBE` /E → `0x8D2EDD` /lim_h(8.333)=118575.279 |
| 预选 Ix | `5·(H·g)·B⁴/(384·E·lim_v)` | `0x8D306D` (H·g)×5 → `0x8D30B4` /384 → `0x8D30F2` /lim_v(6)=47084.263 |
| 限值 | `lim_v = B/250`、`lim_h = round3(B/180)` | `0x8D0612` 250→6；`0x8D00FA` 180→8.333333 |
| 挠度 df | `df_v = 5(H·g)B⁴/(384EIx)`、`df_h = qk·B⁴·k_df/(E·Iy)` | `0x8B73DA…` / `0x8D2Dxx…` |

### 2.4 应力

| 量 | 公式 | 源码出处 |
|---|---|---|
| σ（抗弯） | **两方向正应力直接相加** `M_x/Wx + M_y/Wy` | `0x8B55E9` 81372.5/3662=22.220781 → `0x8B562E` 232521.667/5763=40.347331 → `0x8B563B` = 62.568112 |
| 剪力 V_v | `H·(B·g·1.3)/2` | `0x8D59FD` B×0.00039=0.585 → `0x8D5A30` 0.585×1000/2=292.5 |
| 剪力 V_h | `1.5·qk·B·(1−u)/2 + B·(r·g·kq)·(1−u)/2·1.4·0.5` | `0x8D5324` 1500×1.264=1896 →/2=632 →×1.5=948；`0x8D5617` 1500×0.24=360 →…→ 84；`0x8D58A6` 84+948=1032 |
| τ_h（打印标签 **tx**） | `V_h·Sy/(Iy·ty)` | `0x8D5B88` 1032×3546=3659472 → `/123650` → `/3`=9.865135 |
| τ_v（打印标签 **ty**） | `V_v·Sx/(Ix·tx)` | `0x8D5FE8` 292.5×2994=875745 → `/115810` → `/5`=1.512382 |

### 2.5 已登记的程序自身问题（复刻时应修，不照抄）

| # | 现象 | 证据 | 处置 |
|---|---|---|---|
| 1 | 勾选「计算中不考虑地震作用」后横梁 12 个输出**一字未变** | αmax 仍 0.16，Wy 仍 2583.574 | 复刻版应在该开关勾选时把 `kq` 置 0（并给出提示），而非照抄 |
| 2 | `g×0.8` 中的 0.8 未随开关变化（同 #1） | — | 同上 |
| 3 | 数值巧合：帮助 `B01.htm` 地震影响系数表 0.04/0.08/0.16/0.32 与 αmax 初值/运行值一致 | 数据段初值 0.08、运行 0.16 | 已确认为 αmax 而非巧合 |

### 2.6 未取证（禁止先写公式）

1. 单元式「有/无中横结构」是否改变横梁跨度与荷载分配（当前为构件式，该开关禁用）
2. `挂式结构` / `玻璃自重为集中力` / `按集中力模型` 三个开关是否换用集中力模型（帮助 B03 提到「两点集中力」）
3. 「>>完善抗扭参数<<」的抗扭计算（当前禁用）

---

## 三、ViewModel ↔ Model 调用关系（横梁为例）

```
BasicParamsViewModel
    └─ 算出 wk → CalcSession.WindStandardKpa
                    ↓（共享会话，不复制副本）
BeamCalcViewModel.PreviewCommand
    ├─ 读 Session: BeamSpanMm(=max 左右宽)、BeamUpperGridHeightMm、WindStandardKpa、
    │              AlphaMax(地区参数表)、DeadLoadGkTransom(幕墙自重参数表)
    ├─ 读 ParamLibrary.BeamProfiles[SelectedProfile]（横框表）
    ├─ 读 MaterialAllowables.Bending/Shear(BeamMaterial)
    └─ HorizontalBeamEngine.Compute(...) → BeamResult
            └─ 逐条转成 PreviewLine → Results（View 只绑 Text/Color）
```

**关键纪律**：`wk` 只能由第一页链路产出，横梁 VM 不允许覆盖（避免"测试里自己造 wk"导致的对拍失真）。

---

## 四、验证方式

| 层次 | 手段 | 当前结果 |
|---|---|---|
| Model | `verify_beam_trap.py`（读参数库 CSV 独立复算，与 V31 实机输出比） | **64/64** |
| Model | xUnit `HorizontalBeamTests`（9 例，含中间量钉值、上海 αmax 组） | 通过 |
| Model 全量 | `dotnet test` Calc 项目 | **81/81** |
| ViewModel | `--selftest=*` 无头驱动（不点鼠标，直接调命令） | 待补横梁 |
| View | 编译 0 错 0 警；code-behind 仅 InitializeComponent | 通过 |
| UI 视觉/交互 | 交由 UI（figma/电脑操作）技能处理 | 移交 |
