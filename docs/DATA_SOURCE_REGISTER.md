# 复刻数据全表来源登记表（2026-10-27）

> 用户铁律：**所有表格都要找来源**。本表逐一登记复刻代码里每一张数据表的出处与实证方式。
> 来源分类：
> - **参数库** = `参数库.mdb` → `local-data/hb_mdb/*.csv`（59 表，运行时读取）
> - **安装目录文件** = V31 安装目录下的明文数据文件（`.inx` 等）
> - **程序内置** = V31 常量池 / 代码 If链-Switch / 下拉自带候选（参数库与安装目录全盘扫描无此值）
> - **运行期直读** = Frida/消息级取证从 V31 实机链或输出直读（含扰动联动）
> - **⚠ 规范值待取证** = 按规范/手册值录入，尚未在 V31 程序中找到实证

## 一、参数库表（hb_mdb CSV，运行时读取，快照仅兜底）

| 复刻位置 | 表 | 实证 |
|---|---|---|
| `ParamLibrary.Regions`（地区参数） | 地区参数.csv 634 行 | V35 参数库 OleDb 全量导出；probe10/11 运行期联动（北京 αmax=0.16→上海 0.08 逐位） |
| `ParamLibrary` 立柱/单元立柱/横框系列 | 立柱.csv 等 95+ 行 | 截面值与实机 W/I 槽逐位 |
| `ParamLibrary.DeadLoad` | 幕墙自重参数.csv 17 行 | 表注释直读 |
| `ParamLibrary.Bolt`（螺栓1..7） | 螺栓1-7.csv | 有效直径列原文（M5 4.25314 … M10 8.59566）逐位 |
| `ParamLibrary.Glass` + `玻璃材料（复刻补充）.csv` | 复刻补充表 | 参数库无玻璃表（两 mdb MD5 相同、GetSchema 全量、无强度列）；补充 CSV 值=程序槽 0x1C911BC[19] 直读 84 等 |
| `ThermalWallEngine` 默认 3 层 | 热工数值.csv 行 1-3（层配置表：层号/材料/厚度/λ/修正系数） | probe11 fp 位一致（千丝板10/0.3、钢筋混凝土240/1.74、聚苯乙烯100/0.033） |
| `ThermalGlassEngine.LambdaGlass` 0.76 + 层材料 λ（0.3/1.74/0.033） | 热工系数.csv（材料导热系数库，16 组，**可通过「参数库转换器」id=199 添加/导入**） | 玻璃 0.76=组0、千丝板 0.3=组0、钢筋混凝土 1.74=组1、聚苯乙烯 0.033=组8「20（灰板）」；CW27 隐藏帧网格即此表（Adodc 数据源） |
| 多跨几何 A/L 交替 | 多跨1/多跨2.csv | 截图 grid_zoom.png + 表结构互证 |
| 后锚固（植筋/锚栓）参数 | 后锚固材料参数.csv | 慧鱼锚栓 fstk/fyk/fud 列直读 |
| U 玻璃截面库（UGlassEngine.Sections） | U型玻璃.csv 行 1 | 面积 1812.412 原文、W/I 同表换算（5.49 cm³→5490 mm³、18.23 cm⁴→182300 mm⁴）；E=70000 运行期挠度链除数（uboli_var.json） |

## 二、安装目录数据文件

| 复刻位置 | 文件 | 实证 |
|---|---|---|
| `ThermalCityTable`（479 城/31 省） | `pic\热工\规范\50176\{prov}.inx`（GBK 逗号文本） | 参数库全库 GBK 搜「城市/上海」0 命中；sh.inx 原文 `上海,3A,3,0.5,-3.0` 与市 ListBox 条目逐位；parse_thermal_inx.py→csv→生成 cs。省序实测前 15 命中（北京,天津,上海,重庆,黑龙江…），16..31 序未实测（已登记） |

## 三、程序内置（参数库/安装目录扫描无值）

| 复刻位置 | 数据 | 实证 |
|---|---|---|
| `ThermalGlassEngine.FrameK` 9 项（9.139/3.318/2.8/2.2/2.7/2.0/1.8…） | 框材质传热系数 | probe10 frida `Mul 1.217 Kf` op2 运行期直读 + 九项扰动全链逐位；钢框=普通铝同值→钢框 case 零 diff |
| `WindLoadEngine` μz/βgz/I10/α/z 截断 | 风荷载剖面系数 | V35 常量表 0x401E88–0x401F18、μz 四类块地址、V31 五点实机验证 |
| `GlassEngine` EtaTable/BendingTable/DeflectionTable | η/m/μ 表 | 静态读出 0xA78E85–0xA7A037 逐地址；运行期乘数 0.0631/0.00606 命中 |
| `PlateEngine`/`StonePlateEngine`/`ManmadePlateEngine`/`BackboltPlateEngine` 系数表 | m/α/K/LugCoef/密度 | 常量池逐地址原文 + 扰动直读（manmade_mtable.json 等） |
| `FullGlass/DianshiGlass` 四点驳接 m/μ 表 | 常量池原文 | docs/V31_DIANSHI_UI.md：m 表 0x404B18(0.136)~0x404AB8(0.154)、μ 表 0x408D90~0x408D28；插值实测 0.1424/0.0186 命中 |
| `EmbedMaterials` 混凝土 fc/ft 13 级 | If 链字面量 | 逐级 fld 地址（0x192252E…）代码级闭合 |
| 挂件材质 8 项、螺栓材质 11 项 ft/fv（石材/人造板/背栓三屏同构） | 实机逐项切换直读 | stone_material.txt 逐项枚举（8418/6405=230/175×As36.6 反核）；参数库螺栓表材质名是另一套（普通碳钢C级/奥氏体A50），**屏下拉内置档为程序 Switch 值** |
| `DoorConnectorEngine` 材质 11 项 fv（90/200/…/40） | jumptable 014052D2 switch | 11 case 全实测（door_conn_matrix） |
| 铆钉/自攻承载力 25+10 格（400…3324）、膨胀 3956.4、附框 3.5/1346 | 查表非公式 | door_conn_probe4a.json 40 case 全命中（本轮 grep 复核 21 值 0 缺失） |
| `WeldSectionTable` 转接件 20 行 | 下拉候选 CB_GETLBTEXT | embed_weld_probe.txt 直读；前 5 行槽钢与立柱.csv 槽钢段逐位；12# 参数库未收录（照抄程序值）；矩形 15 行=程序自带 I/W（含 6×130 程序值 109.9） |
| `MaterialAllowables` 8 材质（90/55…315/180） | V35 A002 材料表 sweep | v31_mats.ps1 → sweep_mats.csv 逐材质程序打印直读（许用列直取） |
| `DoorMullionEngine.Materials` 10 材质 | 实机联动 | door_mullion_matrix 54 组（E 从挠度链除数直读） |
| `WeightPerMeterEngine` 钢材7850/铝材2700 | 下拉候选+切换计算 | **2026-10-27 b8_mizhong_probe.json（CB_GETLBTEXT=[钢材,铝材]）+ b8_mizhong_density.json（切换+计算 7.85/2.7 逐位、扰截面积 500→1.35 联动）**；参数库 61 表 grep 2700/7850 无密度列命中 |
| `ColumnSpanEngine.SimplySupportedAlpha`、`MultiSpanStaticEngine.SimplySupportedAlpha` | 5/384 | 公式本身（V31 0x1B65B75 实证过一次） |
| `ContinuousBeamColumnEngine` 0.00677/0.1/0.6/1.5 | V31 链 Mul 直读 | 0xD475D7/0xD4B8EC/0xD4C777 运行期实值 |
| Magnus 露点 6.11/7.5/237.3/10 | IDA 常量地址 | 0x40BCB8/0x4055E0/0x40BCB0/0x401710 |
| 城市 te 加权 0.3/0.7、Hsi=1/8、SHGC 分母 16/20、SC 基准 0.87、Rsi0.18/Rse0.04/R_air0.11、采暖 18/非采暖 12 | 运行期链直读 | b7_probe8e/11/12 rounds+var 地址级 |
| 气隙物理常量（λ_gas 空气 0.0241、PVB λ 0.16/厚 0.76mm、ε 0.837、4σ 2.268e-7、T 273K、气隙 9mm） | 程序内置（参数库 61 表全库 CSV 扫描 0.0241/0.837/PVB 均无） | b7_probe23 frida 链直读：`0.0241/0.009`、`0.76/1000/0.16=0.00475`、`2.268e-7×273³/(2/0.837−1)=3.321060097486844` |
| 规范限值（K≤2.2/3、SHGC≤0.35/0.45/0.3、K墙≤0.8、wk≥1.0、w0≥0.3） | 程序输出文案实录 | probe 输出逐字 |
| 居住/工业建筑限值（居住玻璃 K≤2.5/SHGC≤0.4、居住墙 K≤1、工业玻璃 K≤3.4/SHGC≤0.6、工业墙 K≤1.1、工业 t_i=16） | 程序内置（GB55015/GB51245 条文值，参数库全库无对应出处） | b7_probe28_residential.json 输出逐字 + t_i=16 结露链精确命中（玻璃 tsi 3.74/露点 −1.546、非透明 15.441/−1.546） |

## 四、⚠ 规范值待取证（未接线计算线，仅 Demo/备用）

| 复刻位置 | 数据 | 状态 |
|---|---|---|
| ~~`GlassPanelEngine` 4 组表（4e/3e/2e/点支 m、μ：0.0468/0.0650/0.1000/0.159…）~~ | JGJ102-2003 表6.1.2-1/-2/-3/-4 规范值 | ✅ 2026-10-28 定性：**非 V31 主链**（四边支承走 GlassEngine 表 `0xA78E85–0xA7A037` 已读）；仅 Calc.Demo，就地标注 |
| ~~`ColumnSpanEngine` 双跨 α 表（0.73/0.65/0.59/0.53/0.49/0.42）~~ | 静力手册/计算书表4-4 | ✅ 2026-10-28 定性：**非 V31 主链**（双跨走 ContinuousBeamColumnEngine，0.00677/0.1/0.6/1.5 已读）；仅 Calc.Demo，就地标注 |
| `MultiSpanStaticEngine.MeasuredFit*`（1.0743/1.1463/1.0364） | **实测拟合系数**（非程序公式） | ✅已删除（2026-10-28 P16：全部改 V31 逐指令解析式） |
| `FullGlass/DianshiGlass` RibAllowable 15→17.0 | 肋高 393.895 **反推** | ✅9 档全直读（6/8/10/12→19.5、15/19→17.0、20/22/25→14.0），FullGlass/Dianshi 双实现 |
| `DianshiGlassEngine` RibThicknessOptions、FullGlass 同 | 下拉候选 9 档 | 盘点直读（属程序下拉，非数表） |
| StonePlateEngine.DefaultThickness 9 种板默认厚 | Select Case 字面量 | 0x1BE2557–0x1BE2749 反汇编直读 ✔（已属程序内置级） |
| ThermalCityTable 省序 16..31 | 未实测 | 已在类注释登记 |

> ✅ 剩余真待取证 = 0 处（MeasuredFit 已删、RibAllowable 9 档已直读，2026-10-28）。

## 五、待办（本轮新登记）

1. ~~GlassPanelEngine/ColumnSpanEngine 接入计算线~~ → 已定性为非 V31 主链（台账 T8 已 ✅）。
2. `MultiSpanStaticEngine` 三个拟合系数 → V35 动态取值链取真实公式（docs/V35_CALC_CODE_MAP.md）。
3. 肋厚折减表其余档（15 直读、19/20/22/25）待扰动。
4. 城市表省序 16..31 待 V31 省框滚到底实测。
