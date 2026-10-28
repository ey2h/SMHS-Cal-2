# Glass / SMHS 幕墙计算复刻 — 长期记忆（索引）

> 全文细节已归档到同目录 `PROJECT_DETAIL.md`（地址总表、已闭合口径、工具坑），按需读；本文件只留铁律 + 状态。
> **动手前先查 `docs/ACCEPTANCE_LEDGER.md`**（唯一进度源：✅已对拍 / 🟡引擎✅UI缺 / ⚠未取证 / ⬜未做 + 唯一 TODO 源）。本文件与日志只管过程，别拿日志当进度表。

## 一、铁律（违反即返工）

1. **四面对拍**：`V31 实机 ｜ V35 实现 ｜ 复刻 ｜ 规范`，缺一列不算完成。优先级 **运行期实值 > 静态反汇编 > 程序文案 > 推测**；V31≠V35 以 V35 为准并登记差异。
2. **严禁反推**：取值只认 ①代码 ②参数库数据文件 ③程序状态直读。反推只能写「待验证假设」；差一末位也不许凑数 ⇒ 写「未定位」。
3. **公式以代码为准**，计算书/帮助/模板串只作分支标签，不作公式来源。
4. 型材/材料数据一律**运行时读参数库**（`参数库.mdb` → `local-data/hb_mdb/*.csv`，59 表），不许源码另编。
5. 逐面板复刻，门禁 = 该页字段 **100% 逐位一致（0 误差）**；过程参数一列一参数、四面并排。
6. 命名只用 **SMHS**（禁 HuiBao/汇宝）；**界面载体只认 `XxxCalcView`**，`CW{NN}_*.axaml` 已弃用、不得接线。
7. **「预演」是总开关**：不点则中间量/槽/输出全空。**所有测试从第一页开始**，wk 由第一页链产出下传，禁止后续屏覆盖凑数。
   V31 默认 = C 类地形 + 地区未选（回落北京 0.45kPa）+ 标高 100 + 50 年 + 墙面区 1.0 + 封闭式 + wk≥1.0 下限。

## 二、环境

- 三套偏移**不可混用**：`unpack/unpacked_v31.exe` → `VA−0x400C00`；`v31mem_clean/*.bin` → `VA−0x401000`；V35 `unpack/unpacked_runtime.exe` → `VA−0x400000`。
- **V31 启动（唯一正确姿势，2026-10-28 实测）**：`local-data/recon/v31_wait_trial.py`
  ① 工作目录必须 = 安装目录（否则报「参数库文件（参数库.mdb）缺失，请重新安装本系统」→ 不出按钮 → 约 20s 自退 code=-1）
  ② 环境变量必须完整（`CommonProgramFiles`/`ProgramFiles`/`LOCALAPPDATA`/`COMPUTERNAME`/`OS` 等；本 shell 被剥离，用 `v31_launch_clean.build_env()` 从注册表补齐）。
     缺它 ⇒ COM 展不开 `%CommonProgramFiles%\System\msadc\msadce.dll` ⇒ `CoCreateInstance {3FF292B6-B204-11CF-8D23-00AA005FFE58}` 返回 0x80040154 ⇒ ADODC 连弹「Adodc1 没有注册类」。**这是启动方式造成的，不是注册表缺陷。**
  ③ **先出 logo 介绍画面（ThunderRT6FormDC 567×281，无任何按钮），按钮约 T+21s 才出现**，超时要给 ≥120s，不得提前判定「按钮缺失」。
  ④ **V31 点「试用软件」；V35 点「限期版本」（另有绑定主板/绑定硬盘）——两条通道互不通用，不可混用**（用户 2026-10-28 明确）。
  ⑤ 进程必须后台常驻（本 shell 的子进程命令结束即被回收）：`v31_wait_trial.py --keep` 以后台任务跑。
- msvbvm60 基址 **0x734D0000**；thunk 名表 `local-data/recon/v31_thunks.json`（语义统一 `out = c2 op c1`）。
- IDA Pro 9.5，i64 `unpack/unpacked_v31.exe.i64`。
- 构建：dotnet 用 `/c/Users/Jack/.dotnet/dotnet.exe`；仓库根有设备名 `nul` ⇒ 必须 `-p:OutputPath=`；换输出目录要 `-t:Rebuild`；`CS2012 锁定` → `taskkill //F //IM VBCSCompiler.exe`。
- 点控件一律 **PostMessage**（`SendMessage` 遇 `DataGridWndClass`/模态框死锁）；V31 SetField 走剪贴板 + `EM_SETSEL` + `WM_PASTE`。

## 三、当前状态

- 测试基线：**Calc 582 / DesktopTests 135 全绿**（2026-10-28 16:15 实测；数字必须现跑，命令见 ACCEPTANCE_LEDGER §7）。引擎：Column / DoubleSpanColumn / IndeterminateColumn / HorizontalBeam / AlloyColumn / Glass / Connect / Embed / Sealant / WindLoad / Plate / StonePlate / ManmadePlate / BackboltPlate / FullGlass / DianshiGlass / UGlass / FloorGlass / Weld / BoltGroup / AnchorPlate / RebarAnchor / CornerColumn / ThermalStress / SmokeVent / WeightPerMeter / ThermalExpansion / Thread / SelfTappingScrew / PinBolt / LouverVent / OpeningVent / OtherGlass / MultiPointLock / Skylight / Awning / DoorConnector / DoorMullion / **ThermalGlass(CW26)** / **ThermalWall(CW27)** / VbaRound。**孤儿 `StructuralSealantEngine` 已于 2026-10-28 删除。**
- 阶段 1（八屏补全）完成；阶段 2 = **B2 板材线**：金属板 ✅ → 石材 ✅ → 人造板 ✅ → 背栓点挂 ✅（8 种面板全闭合）。B2 完成。
- **B3 玻璃变体**：全玻璃幕墙（8 构型）✅ / 点式肋（四点驳接）✅ / U型玻璃 ✅ / 地板玻璃 ✅。B3 核心 4 屏完成。
- **B8 独立工具**：16 引擎闭环（焊缝/螺栓群/锚板厚度/植筋/转角立柱/热应力/排烟/米重/热应力伸缩/螺纹/自攻螺钉/扭剪钢销/百叶窗/开启扇/其它玻璃/多点锁）。**2026-10-28 独立工具线补全**：雷击181（LightningEngine，k·Ng·Ae + 524 城雷暴日 + **周边修正 10 档全对拍**：D 公式跟选项组走）、静力188（Api StaticCalcEngine 7 支撑条件下沉 Calc/Engines/StaticCalc + 桌面接线）、截面159（SectionTool Green 定理下沉 Calc/Engines/SectionCalc + 接线）、插值190/方程191（InterpolationEngine 线性插值 + EquationEngine 二次/三次卡尔丹）、牛腿170（CorbelEngine，V31 π=3.14 + 31 点 φ 表，**单一水平斜梁形态已完整闭合**；「样式二/三/竖直四」=历史误记已推翻）、型材优化192（CuttingStockEngine FFD，⚠非 V31 私有算法）。**★独立工具 15 项界面已于 2026-10-28 全部接线**（通用壳 `SimpleToolViewModel`+`SimpleToolView`+`ToolField`，ViewLocator 一条规则覆盖；守护 `SimpleToolVmTests` 18 条）。
- **B4 门窗 ✅ 全选项组合闭环（2026-10-27）**：玻璃复用 GlassEngine；**DoorConnectorEngine 重写**（V=round3(1.5wk·H·B+地震项)、单点力 round3(round3(1.25V/n))、**r=1→min(f1,f2)**（旧「取f2」是假象）、r>1 插值÷连接片自身厚、承载力查表 铆钉5类型×5规格+自攻、膨胀结构侧常数 3956.4、附框 A 恒定3.5/1346、卡槽跳过边框块、不计算 0 输出；**输出行前缀**：边框块标题带「门窗」但快固件行前缀不带（door_conn_probe4a 40 case 逐字））；**DoorMullionEngine 材质 10 项全表**（E 链直读：塑钢2200/玻纤40000/Q235 206000）+ 挠度限值三模式（自动L/100、人工一致、不一致H=L/分母V=L/500）；杆件 wk 第二链 round6(w0·βgz·μz·**1.168**)=0.001333（0x14184AE，与玻璃 wk 0.00137 分链）；**隐藏 UI 全补齐**：新屏 `DoorWindowCalcView/VM`（5 Tab：玻璃参数/21样式/竖向杆件+内框12材质/横向杆件+钢框12材质/连接件全选项+钢附框）。V31 每预演按钮**重写输出框**（非追加）。**非 0 态已闭（probe10/11）**：地震链=7·αmax·Gk_area·H·B·0.5，**αmax 由第一页地区行下传（北京0.16/上海0.08=参数库αmax列）**、**Gk_area 仅由幕墙线预演写全局槽（=0.0004；门窗线单独恒 0）**；横梃垂直自重 σ 含 Mvert/Wx2 项（上海态 165.367 逐位）；杆件引擎参数 = SeismicAlphaMax+GkAreaNmm2/QgkAreaNmm2（默认 0 基线不变）。地区行选择=真实**单击**(grid+80,+12)一次；MDI 菜单需带重发；输出区读取不判可见性。盘点 `cw29_inventory.json` 243 控件、combo 列表 CB_GETLBTEXT 可读出（推翻旧「读空」结论）；**隐藏 OptionButton 必须帧标题+帧内相对坐标点击**（绝对坐标跨会话点空）。遗留：21 样式切换水平输出不变（已证）；断热/冷弯薄壁勾选 V31 无 diff。隐藏 Tab 操作铁律：**必须 ShowWindow 预演按钮本身 + 先点玻璃预演初始化 wk**；DataGrid 双击用客户区坐标、只双击 row1（row2 空行必崩 error13）。
- **B5 采光顶** ✅ 单坡引擎+界面闭环（SkylightEngine，8 坡型共用）；**B6 雨篷** ✅ 引擎+界面闭环（AwningEngine 5 模块，悬臂梁/焊缝/玻璃/埋件逐位对拍）；**B7 热工（引擎线）** ✅：CW26 ThermalGlassEngine（K/SHGC/结露/框材质 9 项 Kf 程序内置：普通铝=钢9.139/断热3.318/聚氨酯塑钢带筋2.8/PVC 2.2·2.7·2.0/木1.8；湿度只进露点 φ 不进 te；Magnus 6.11/7.5/237.3）+ CW27 ThermalWallEngine（层热阻=参数库热工数值行 fp 位、+Rsi→round3、+R_air 不 round、+Rse→round5、K=round3(1/Σ)、tsi=R_air/R_total 链、采暖 t_i 18/非采暖 12）均逐位；**两屏界面已复刻（ThermalGlassCalcView/ThermalWallCalcView，#77）**；剩余：CW19 物理性能 17 项输出通道（#75）、隔声屏。城市选择消息级双击铁律（PostMessage DBLCLK 序列；真实鼠标点常被前台吃）；CB 条目读 CB_GETCOUNT/LBTEXT=0x0146/0x0148（勿误 0x018B/0x0189）。
- **全表来源登记（2026-10-27 用户「所有表格都是要找来源」）**：`docs/DATA_SOURCE_REGISTER.md` = 复刻每张数据表的五级出处登记（参数库/安装目录.inx/程序内置/运行期直读/⚠规范值待取证）。已实锤：米重 钢材7850/铝材2700 = 下拉+切换计算直读（b8_mizhong_density.json）；U玻璃截面=参数库U型玻璃表行1换算；四点驳接 m/μ=常量池原文；**肋厚折减 15→17.0 是唯一反推点（待直读）**；⚠规范值未程序取证：GlassPanelEngine JGJ102 4 组表、ColumnSpanEngine 双跨 α 表、MultiSpanStaticEngine MeasuredFit*（均未接线仅 Demo，注释已标）。米重等独立工具屏子窗=ThunderRT6FormDC 隐藏类（b7_probe4 forms() 法，MDIChild 法失效）。
- 屏尺寸：基本参数 902×540、立柱 936×442、横梁 949×425、铝板板块 795×530、石材板块 936×448、人造板板块 782×536、背栓板块 782×536、连接 796×510、埋件 788×490、胶类 784×537、玻璃 819×361。
- 5 处静默常量兜底**已清理（2026-10-28 P1）**：BasicParams z=100/k1=k2=1.0 补出处注释（GB55001-2021「一般取 1.0」）、ColumnCalc:121-122 死代码 k1/k2 已删、MultiSpan:154 确认是颜色兜底非数值。
- **2026-10-28 计划收官（P1-P16 闭环，仅 P17 计算书正文剩余）**：物理性能 16 子项（4 计算页+12 说明页，PhysicalPerformanceTexts 从 .mht 提取）、空气声隔声 8 构型（SoundInsulationEngine 质量定律+PVB 4.0+气隙 0.5）、外遮阳 134（ExternalShadingEngine GB50189 表 A.0.1）+ 加权计算器 136（WeightedAverageEngine）、热工扩展 4 项（居住/工业 K 限值）、CW26 多片玻璃 7 构型链、CW27 空气层 9 档 R 查表、玻璃肋厚 σ 折减表 9 档直读（6/8/10/12→19.5、15/19→17.0、20/22/25→14.0）、P16 多跨拟合系数全删改 V31 逐指令解析式。遗留：P17 计算书第 4-15 章（第 2/3 章已逐行守护）。详见 `.workbuddy/memory/2026-10-28.md` + `docs/ACCEPTANCE_LEDGER.md`。
- 剩余业务屏（B5 采光顶 7 坡型界面、B7 热工声学、B8 牛腿/雷击）→ 计划 `C:\Users\Jack\.workbuddy\plans\radiant-vortex-curie-PjnmviJg.md`。B4 门窗全线闭环（玻璃/连接件/竖梃/横梃本体）。
