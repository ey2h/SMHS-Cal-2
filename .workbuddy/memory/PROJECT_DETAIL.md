# 项目长期记忆（Glass / 幕墙计算复刻，产品名 SMHS）

> 只放**铁律 + 关键地址 + 当前状态**。细节全在 `docs/`、`local-data/recon/`，按需读，别往这里堆。

## 一、铁律（违反即返工）

1. **四面对拍**：任何量必须 `V31 实机 ｜ V35 实现 ｜ 复刻 ｜ 规范` 四列并排，缺一列不算完成。
   取证顺序：V35 脱壳镜像 → V31 实机 → 规范；V31≠V35 以 V35 为准并登记差异。
   优先级：**运行期实值 > 静态反汇编 > 程序文案 > 推测**。
2. **严禁反推**：不许枚举/拟合/比值反推后当 V35/V31 列。取值只认三源：①代码 ②参数库数据文件 ③程序状态直读
   （槽数组/控件属性/内存实值）。反推只能写「待验证假设」，差一末位也不许凑数 ⇒ 写「未定位」。
3. **不看计算书推公式**：计算书/帮助/模板串只给分支贴标签，公式以代码为准。
   反例：模板 `5qL⁴/384EI*(1-2.4(A/L)²)` vs 代码 `[5-24(A/L)²]`。
4. **型材数据一律运行时读参数库**（`参数库.mdb` → CSV），不许源码另编。
5. **过程参数必须列表**：一列一参数、四面并排、派生量给算式与代入值。
6. **逐面板复刻**：第一页 → 立柱 → 横梁 → 玻璃 → 连接 → 埋件 → 胶类 → 板材线。门禁 = 该页字段 100% ✅，判据 **逐位一致（0 误差）**。
7. 命名：不得出现 HuiBao/汇宝，用 **SMHS**；`Views.Calc` / `GlassManagement.Calc` / 启动 `--calc`。
8. **界面载体只认 `XxxCalcView`**（深绿背景 + `hbResultPanel` #C0FFFF）。`CW{NN}_*.axaml` 是早期静态复刻体，
   **用户已明确弃用**，不得作载体、不得接线。Catalog / ViewLocator / 向导一律指向 `XxxCalcView`（误判过两次）。
9. **「预演」是总开关**：不点预演中间量/分支根本不执行，读内存/槽/输出全空。多跨两模型须进多跨页点预演。
10. **所有测试从第一页开始**（wk/μs/场地/地区/地形全由第一页链产出下传，禁止后续屏覆盖 wk 凑数）。
    V31 默认 = **C 类地形 + 地区未选（回落参数库首行北京 0.45kPa）+ 标高 100 + 50 年 + 墙面区 1.0 + 封闭式 + ≥1kPa 下限**
    （曾默认 B 类+上海 → wk 1.848 vs 1.285 整屏错）。测试统一走 `DesktopTests/FirstPage.cs`。

## 二、环境与路径

- V31：`local-data/recon/v31_keep.ps1`（托管启动）+ `v31_attach.ps1`；脱壳 `unpack/unpacked_v31.exe`（24.8MB，3638 函数）
- V31 内存转储 `v31mem_clean/v31_mem_00401000.bin`（base 0x401000）
- V35：`MQJSSV35.EXE`（ASPack）；静态 `unpack/unpacked_runtime.exe`（base 0x400000）。
  **V35 实机本机不可用**（点计算弹「未注册」+ 80010108）⇒ 只能静态挖，不碰注册。
- **三套偏移不可混用**：`unpacked_v31.exe` → `VA−0x400C00`；`v31mem_clean/*.bin` → `VA−0x401000`；`unpacked_runtime.exe` → `VA−0x400000`
- 参数库 `C:\code\Glass\参数库.mdb` → `export_hb_mdb.ps1` → `local-data/hb_mdb/*.csv`（59 表）
- msvbvm60 真实基址 **0x734D0000**（0x731F0000 是 comctl32）
- IDA Pro 9.5 `C:\Program Files\IDA Professional 9.5`；i64 在 `unpack/unpacked_v31.exe.i64`
- git：origin `git@github.com:ey2h/SMHS-Cal-2.git`，SSH `~/.ssh/id_ed25519` + `ssh.github.com:443`（无 GCM，别用 https）

## 三、关键地址总表（现役）

| 对象 | 区间 / 地址 |
|---|---|
| **立柱计算本体** | `0xD1E9E0`（Frida 挂 rtcRound 取 returnAddress 命中 0xD2xxxx 反查）；qd 链 `0xD23320–0xD23436` |
| 立柱槽数组 | `ds:[0x1C91184]`（2=L1 3=a 4=b 7=wk 8=qk 9=qd 19=−M 22..27=系数 44=A 45=lgA 46=μs1ψ 47=μs合 76=g 153=中横V） |
| 第一页槽数组 | `ds:[0x1C91168]`（16B/槽 VARIANT，BSTR 长度在 **ptr−4**） |
| **横梁** | `0x8ABD3A–0x8B7481`（42 定稿点）、**B>H 分支 `0x8CDF34`**（B≤H 在 `0x8AEA00`）、槽 `ds:[0x1C911A0]` |
| 双跨 / 连续梁 | `0xD35D8C–0xD42762` / `0xD42762–0xD4DB91` |
| 多跨静定 / 超静定 | `0xD53595–0xD73EE5` / `0xD73EEF–0xD8EF31` |
| 钢铝结合 | `0xCE08FA–0xCE6BE2`（⚠ 起点是中段 `Select Case`，先 `get_func_attr` 取函数首） |
| 风荷载 | `0xA6E000–0xA74000`；V35 对应 `0xA62C07–0xA86FEE` |
| 连接 | `0xA8FA83–0xAA0002` |
| 胶类厚度链 | `0xC59177 / 0xC5941D–0xC5944B`（`0xC5944B Div` 左操作数是 round3 后 2.509） |
| 埋件 / 焊缝几何 | `0x191EEC6–0x1928377` + `0x1A63DCF–0x1A687FD`（L 型）/ `0x1A6D340–0x1A6E200`（三边围焊） |
| **板材（板块计算）** | 函数区 `0xF31300–0xF36700`；系数调用 `0xF35309`（强度 K）/ `0xF35219`（挠度 K）走 `call [eax+0x6f8]` → `0x4299AC` 桩表 → jmp `0xF4B69A`；m 表 `0x407A60–0x407AA8`、α 表 `0x4029E0–0x402A30`、插值 `0xF4BDEC / 0xF4C1D1`；槽数组 `0x1C911BC` |
| V35 立柱 / 计算书主函数 | `0x1D442DA–0x1D70913`（多 244B）/ `0x1B36238`（局部 0xAA4，九阶段） |
| 旧误判（**已废**） | `sub_18BD010` / `sub_89EA1D` 簇 **不是**立柱本体（Frida 0 命中；断点自检 0 命中不作证据） |

**thunk 名表** `local-data/recon/v31_thunks.json`（251 个，IAT 指针−0x734D0000 对 msvbvm60 导出表）：
rtcRound `0x41371A`、VarDiv `0x413708`、VarMul `0x413714`、VarAdd `0x413552`、VarSub `0x41377A`、VarPow `0x41370E`、
rtcLog `0x4135B8`、`__vbaVarCopy 0x4135BE`、`__vbaVarMove 0x413678`、`__vbaR8Var 0x413624`、
`__vbaFreeVarList 0x4136BA`（曾误当 Add，209 条全是噪声）、rtcCos `0x4137F2`、rtcAtn `0x4137FE`、rtcSqr `0x4137F8`。
**语义统一 `out = c2 op c1`**。VB6 字符串无绝对地址 xref ⇒ 搜字符串找代码**行不通**，改走 Variant 实值轨迹。

## 四、已闭合口径（细节见 docs）

- **取整 = `VbaRound(x,d)`**：`p=10^d`（循环累乘）→ `y=x*p`（**只乘一次**）→ 四舍六入五成双 → `n/p`。
  缩放是 IEEE double（SSE2），非 x87 80 位。「必须逐次×10」「必须用 decimal」两条已作废。
  ⚠ 例外：**qk 走十进制半偶、qd 走 VbaRound**（B01/B08/B09/B10/B12 位模式钉死）。
  中间量一律 double，**decimal 只用于显示**（唯一例外：玻璃份额 round6 遇平局走 decimal）。
- 取整位数：βgz/μz/β 4 位；μsi/wk/qk/qd/σ/τ/lim 3 位。
- **wk 两套口径**：支承（立柱/横梁/连接/埋件）= `μs1·ψ(A)`；面板（玻璃/胶类）= `μsl=1.0` 不折减。
  两屏读同一槽 `slot[0x45]`（`0xA7CE09` 链），立柱填 0.926、玻璃填 1.0。→ `WIND_LOAD_TWO_SCOPES.md`
- `wk = w0·βgz·μz·[μs1·ψ(A)+μs_int]`，下限 w0≥0.3、wk≥1.0；βgz `1+2·2.5·I10·(z/10)^−α`；β 下限 `max(βgz, 1+0.7/√μz)`。
  **ψ(A) 分母 = 1.4**：`slot[46]` 唯一写入点 `0xE2CE3E`，链上 `0xE2CD45 fld 0x4017F8`。
  广东：g 2.5→3.0（w0≥0.5 门槛）+ 封闭式内压 0.2→0.3。
- 立柱链顺序不可换：K1/K2 乘在 qk 取整**之后**；`lim` 先 round3 再算 iReq；抗剪用显示值 wk×1.65、抗弯用 raw wk。
- **挠度限值 20mm 上限**：`lim = min(L/180, 20)`（`dbl_4019E0`）；3600 是边界。挠度**分项取 3 位再相加**。
- 横梁：预选 `Wx = round3(H·g·1.3)·B²/8/[σ]`、`Wy = (qd + round3(B·g·0.8·1.4)/2)·B²/12/[σ]`；水平第二项是**地震不是自重**
  （`gp = round3(r·g·5αmax·1.4)/2`，αmax 来自参数库「地区参数」，全局 `0x1C913D8`）；挠度垂直 `5(H·g)B⁴/(384EIx)`/限 B/250、
  水平 `qk·B⁴/(120EIy)`/限 B/180；构件式 `r=min(B,H)`、`u=r/(2B)`：`M_y = q_y·B²·(3−(r/B)²)/24`、`k_df=(3.125−5u²+2u⁴)/240`；
  **单元式按 B 不取 min**；取整 0.527 / 未取整 0.5265 双源；抗剪输出标签 tx/ty 与实际方向相反；
  bug：「计算中不考虑地震作用」**不控横梁页**地震项。→ `V31_BEAM_UI.md`
- **玻璃 7 构型全闭合**。双夹层-中空是 **4 片**、41 星、容许值无后缀。两段代码口径相反：
  双中空 qe=round6(qg)/θ round3/t_e round3；双夹层-中空 qe=未取整 qg/θ round4/t_e 不 round3。→ `V31_GLASS_PAGE.md`
- 单元式：`f1左=γ0·(N/2A1n + M·I1x/(I1x+I2x)/γW1nx)`；`q_k左=round3(q_k·I1/(I1x+I2x))`；抗弯取 min(Wx1,Wx2)
- 钢铝结合：M_钢 最后 /1.05、抵抗矩 min(Wx1,Wx2)、钢份额用 qd → `V31_ALLOY_CODE.md`
- **板材（铝单板）**：`qd = 1.5·wk + 0.7·(27t/1e6)`、`qk = wk + 0.5·(27t/1e6)`（两处 **round6** 必需）、
  `σ = 6·m·qd·a²·K/t²/1000`、`D = E·t³/(12(1−ν²))`、`w = α·qk·a⁴·K/D`、限值 `a/60` 或 `a/90`；
  `m/α` 按 `x=a/b` 11 点表插值 `round5(v1−(v1−v2)(r1−x)/(r1−r2))`；`slot[8]=round3(qk·a⁴/(E·t⁴))`、`slot[16]=round3(wk·a⁴/(E·t⁴))`。
  **K = 13 段折线插值 round5**（λ 为横轴，σ 用 λ_q、w 用 λ_w **两条独立链不可混用**）；
  加强肋：`M=qd·Lx·L²/24·(3−(Lx/L)²)`、`σ=M/2028`、`w=wk·Lx·L⁴/(240EI)·(3.125−5u²+2u⁴)`、限 `L/180`（**挠度用 wk 非 qk**）；
  m_肋 x<0.5 走兜底 `(0.1212+0.0843)/2`，α_肋 直接钳位 0.00504。→ `V31_PLATE_UI.md`

## 五、当前状态（2026-10-26）

- 第一页 40 案 **280/280**；立柱矩阵 **105/105**；多跨静定 **43/44**（残差 r=0.2 第二跨支座 102.824 vs 102.821）；
  多跨超静定 **30/30**；钢铝结合 **60/60**；单元式横梁全闭合；`verify_beam 30/30 + trap 53/53`
- **测试基线：Calc 299（260+39 板材）/ Desktop 94（72+22 板材屏）** 全绿
- 引擎：Column / DoubleSpanColumn / IndeterminateColumn / HorizontalBeam / AlloyColumn / Glass / Connect / Embed / Sealant / WindLoad / **Plate** / VbaRound
- **阶段 1（现有 8 屏补全）完成；阶段 2 = B2 板材线**：金属板 CW14(14 项) / 石材 CW02(12) / 人造板 CW123(24) / 背栓点挂(9)
  - 板材线 6 屏链（`v31runs/chain_metal6.txt`）：基本参数 902×540 → 立柱 936×442 → 横梁 949×425 → **板块 795×530（专属）** → 连接 796×510 → 埋件 788×490
  - 金属板板块屏：引擎 `PlateEngine.cs` + 27 测试 + 文档 + 界面 `MetalPlateCalcView`（795×530）
    + `MetalPlatePageTests` 15 例 + 路由（4 个板材菜单 → 板块屏；玻璃线 → 玻璃屏）已交付
  - **★ wk 必须传 N/mm²（`CalcSession.PanelWindNmm2`），不许 `PanelWindKpa/1000`**：
    后者比 `0.00137` 大 1 ulp，qd 半值 2100.5 变 2100.5000000000005 ⇒ 进位 0.002101，
    t=3 强度算成 46.567（实机 46.545）。详见 `V31_PLATE_UI.md §7.1`
  - 肋应力**不除 γ0**（V31 `0xF4A1F8` 除的是 BSTR `"1.00"`）；写成 `/Gamma0` 会得 85.074（实机 93.581）
  - **肋材质 = 8 个单选（不是下拉）+ 冷弯薄壁 CheckBox**，窗体资源 `0x52E650`：
    [σ] 90/150/90/200/135/160 / Q235 215 / Q355 315；**钢另有 γ=1.05**（93.581→89.125，池 `1.05@0x4020E0`）；
    E 铝 70000 / 钢 206000（池 `0x4038B8`/`0x4038C0`）；**冷弯薄壁**：钢 [σ]→205/310 且 γ→1.00，铝无影响。
    ⇒ `0xF4A1F8` 除的 BSTR 就是 γ（"1.00"/"1.05"），**与 γ0 无关**
  - 肋参数「50」= 肋剖面宽度：算法一不读，算法二读
  - **算法二骨架已定位**（`plate_algo2_vartrace.json`）：M=qB²/8、w=5·round3(q/1.5)·B⁴/384/E/I、
    q=(2·c10·qd·Lx·B²−M_肋)/(c11·t·B)/1000·t；**c10/c11 分段表未定位 ⇒ 不实现、不拟合**
  - 桩表 `0x4299AC` 共 15 个函数（步长 13B），前 6 定位；`fn07=0xF4E04A→1100`、`fn09=0xF5426D→0.036`
- 屏尺寸：横梁 949×425、玻璃 819×361、基本参数 902×540、连接 796×510、埋件 788×490、胶类 784×537、板块 795×530、立柱 936×442
- **5 处静默常量兜底待清理**：`BasicParamsViewModel.cs:263/266-267`、`ColumnCalcViewModel.cs:121-122`、`MultiSpanViewModel.cs:154`
- 18 个业务屏未复刻 → 计划 `C:\Users\Jack\.workbuddy\plans\radiant-vortex-curie-PjnmviJg.md`

## 六、文档索引

- 规程 `COMPARE_PROTOCOL_4WAY.md`；经验 `RE_EXPERIENCE_PLAYBOOK.md`；工具链 `RE_TOOLCHAIN_SETUP.md`；参数表 `PROCESS_PARAM_TABLE.md`
- 立柱：`V31_COLUMN_MODEL_FORMULAS.md`(93条)、`V31_COLUMN_CODE_MAP.md`、`V31_MODEL_CASE_MAP.md`(45分支，选择子=`Form->[+54h]`)、`V31_COLUMN_MATRIX.md`
- 梁：`V31_DOUBLESPAN_CODE.md`、`V31_CONTINUOUS_AND_MATERIAL.md`、`V31_MULTISPAN_CODE.md`、`V31_INDETERMINATE_CODE.md`、`V31_BEAM_UI.md`、`BEAM_LOAD_MODEL_NOTES.md`
- 其它面板：`V31_SEALANT_UI.md`、`V31_CONNECTION_CODE_MAP.md`、`V31_GLASS_PAGE.md`、`V31_GLASS_COMPOSITE_FLOW.md`、`V31_ALLOY_CODE.md`、`V31_P2_P3_P4_CODE_MAP.md`、`V31_EDITABLE_PARAMS.md`、`V31_OTHERGLASS_UI.md`、`V31_EMBED_UI.md`、**`V31_PLATE_UI.md`**
- 风荷载：`WINDLOAD_CODE_MAP.md`、`WIND_LOAD_TWO_SCOPES.md`、`WIND_GUST_SPEC_AND_A_ANOMALY.md`
- 取整 `ROUNDING_ALIGNMENT_V35.md`；槽映射 `V31_SLOT_MAP.md`；模板字典 `FORMULA_TEMPLATE_DICTIONARY.md`；单元式 `COMPARE_UNIT_COLUMN_CODE.md`
- 计算书：`V35_CALCBOOK_GENERATION_MECHANISM.md`、`PR_CALCBOOK_MULTISCENARIO_DRAFT.md`
- **参数库 59 表没有型材材料许用值表**；铝/钢 f、fv 只能实机切换读输出。板类表（铝单板/钢板）有强度列但是板材。

## 七、工具与坑

- `local-data/recon/`：`v31_slice.py`（x87+Variant 符号执行）、`dump_screen.py --form <屏> --tag <名>`、`menu_next_probe.py`、
  `connect_frida.py`（`--navmenu` / `--grid "fx,fy,ix,iy"` / Variant 轨迹）、`shot_win.ps1`、`v31_frida_*.py`、
  `ida_map.py`/`ida_blocks.py`/`dis_range.py`、`uia_invoke.ps1`、板材专用 `metalplate_vt.py` / `metalplate_rib.py` / `metalplate_coef_hook.py`
- **Python 有 Pillow + frida**；找控件 `V.find_rel(form, cls, x, y)`（**没有 `B.rect()`**）；VB6 下拉改选中后必须
  `PostMessage(form, 0x0111, (1<<16)|0, cb)` 补 CBN_SELCHANGE
- 调试器：`.tools/dbg/x64dbg/release/x32/x32dbg.exe`；`v31_bp_pic.py`。
  **血的教训**：调试器进程被打死时 Windows **连带杀掉被调试进程**（V31 死过 3 次）⇒ 单次 <15s、带看门狗、杀 python 前先 detach
- V31 控件：`GetWindowTextW`/`WM_SETTEXT` 无效 → 读 `WM_GETTEXT(StringBuilder)`、写 剪贴板+`EM_SETSEL`+`WM_PASTE`；
  SetField 前必须自己 `Clipboard::SetText`（否则「请输入正确数字」+ 输出全空）
- 点控件用 **PostMessage**（SendMessage 遇模态框/`DataGridWndClass` **死锁**）；预演偶发不刷新 → 每组点两次；
  L2≥L1/2 弹「无效输入」须先收掉；立柱屏两套 Frame 坐标会撞 → `FindByRelStrict`（只认可见）
- DataGrid 选行只能鼠标消息（`WM_MOUSEMOVE/LBUTTONDOWN/LBUTTONUP`），**键盘 VK_DOWN 无效**
- 长扫描每案立即落盘（丢过 37 案）；含中文 `.ps1` 必须 UTF-8 BOM + CRLF
- 构建：仓库根有设备名文件 `nul` → 必须 `-p:OutputPath=...`；**换输出目录必须加 `-t:Rebuild`**；
  DesktopTests 用 `.tmpbuild/dt/`；`CS2012 被 VBCSCompiler 锁定` → `taskkill //F //IM VBCSCompiler.exe`；dotnet 用 `/c/Users/Jack/.dotnet/dotnet.exe`
- Avalonia：静态字段**不能绑定**（必须暴露属性）；无子 HWND（`PrintWindow` False）；`ComboBox` 无 `Text`/`IsEditable`；
  Radio/Check 的 Content 必须是字符串；RDP 下鼠标点击无效必须走 UIA；CLI 批量入口须在显示主窗口前 `Environment.Exit`

## 八、胶类 / 埋件屏要点（已闭合，细则见 docs）

- 胶类 7 控件全 ComboBox，候选 `CB_GETLBTEXT` 直读：厚 `06..12`(sel0)/宽 `06..30`(sel2)/中空厚 `06,09,10,12,15`(en0)/中空宽 `06..30`(en0)/
  位移能力 `0.1,0.12,0.125,0.14,0.15`/长期 `0.01`/短期 `0.2`。**实际宽/厚 = (153,200)/(153,178)**（不是中空层 222/244）；
  `(153,266) 位移能力` **只进厚度算式**；**厚度分子必须先 round3**；标签 `长期荷载强度设计值(MPa)` **带半角括号**、字号 **10**
- 埋件转接件：规格+抵抗矩+惯性矩+截面积**四个下拉共用一个行号**；20 行表见 `WeldSectionTable`
- 埋件焊缝：L 型 `p=Lv+Lh`；三边围焊 `p=2Lh+Lv−6hf`；`he=0.7hf`；Lh/Lv 是可改文本框 `(680,164)/(680,185)`
- 埋件材料：13 条 `If 等级="Cxx"`；fc 链 `0x19224FB–0x1923168`（槽18）、ft 链 `0x1925853–0x19264B7`（槽48）；
  **ft 的 C65/C70/C75/C80 是四条独立分支、字面量都写 2.04**（"表越界取末项"旧结论作废）
- `0x1C911F4` 是**通用 Variant 临时槽区**（被引 12470 次）；搜 `fld qword ptr[abs]` 必须带 ModRM `05`（`DD 05 <addr32>`）
- 材质只有 HPB300/HRB400，**fy=300 且 α=0.14**，切换驱动 en=0 的「锚筋外型」；**e0 一个框同时进弯矩力臂和挠度悬臂**（`lim=e0/125`）
- **埋件 asReq 两分支**：①`0x19244B7` = `V/(αr·αv·fy)+N/(0.8·αr·αb·fy)+M/(1.3·αr·αb·fy·z)`；
  ②`0x1924A7D` = `N/(0.8·αr·αb·fy)+M/(0.4·αr·αb·fy·z)`（**无剪力项**）。屏幕取 **max**（`0x1925120`），
  **但 `la` 恒用分支2**（`0x1926B20`），分子先 round3。αr=1.0、αv=round3((4−0.08d)√(fc/fy))（HPB300 0.638 / HRB400 0.605）、
  αb=round3(0.6+0.25t/d)=0.767；As实配 = n·3.14·d²/4（**π 取 3.14**）
