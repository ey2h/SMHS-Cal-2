# 项目长期记忆（Glass / 幕墙计算复刻，产品名 SMHS）

> 本文件只放**铁律 + 关键地址 + 当前状态**。细节全部在 `docs/` 与 `local-data/recon/`，按需读对应文档，不要往这里堆。

## 一、铁律（违反即返工）

1. **四面对拍**：任何量必须 `V31 实机 ｜ V35 实现 ｜ 复刻 ｜ 规范` 四列并排，缺一列不算验证完成。
   取证顺序：**先挖 V35（脱壳镜像）→ 再 V31 实机 → 再规范条文**；V31≠V35 时以 V35 为准并登记差异。
   优先级：**运行期实值 > 静态反汇编 > 程序文案 > 推测**。
2. **严禁反推**（用户两次强调、一次警告）：不许用实测结果枚举/拟合/比值反推内部取值，再当 V35/V31 列。
   取值只认三源：① V35/V31 代码 ② 参数库等数据文件 ③ 程序状态直读（槽数组/控件属性/内存实值）。
   反推只能写「待验证假设」，**不得计入面数**。差一个末位也不许凑数 ⇒ 写「未定位」。
3. **不看计算书推公式**：计算书/帮助/模板串只用于给分支贴标签，公式一律以代码为准。
   已抓到反例：模板 `5qL⁴/384EI*(1-2.4(A/L)²)` vs 代码 `[5-24(A/L)²]`。
4. **型材数据一律运行时读参数库**（`参数库.mdb` 导出 CSV），不许源码另编一套。
5. **过程参数必须列表**：一列一个参数、四面并排、派生量给算式与代入值，不许只给最终结果。
6. **逐面板复刻**：第一页 → 立柱 → 横梁 → 玻璃 → 连接 → 埋件焊缝 → 胶类。门禁 = 该页字段 100% ✅，
   否则每个 ❌ 必须有 V31 地址或明确写「未定位」。判据 = **逐位一致（0 误差）**。
7. 命名：复刻版不得出现 HuiBao/汇宝，用 **SMHS**；`Views.Calc` / `CW{NN}_*` / 项目 `GlassManagement.Calc` / 启动 `--calc`。

## 一之二、V31 驱动铁律：预演是总开关

- **「预演」= 激活几乎所有计算的开关**（用户强调）。不点预演，中间量/公式分支根本不会执行，
  读内存、读槽、读输出都拿不到东西；静态挖到的分支必须用预演去激活验证。
- **多跨两个模型（多跨铰接连续静定梁 / 一次超静定梁）在立柱页点预演无效**：
  必须先「下一步」进入多跨数据输入/计算新页面，在新页面点「预演」才会真算。
- 驱动脚本：`v31_attach.ps1`（colbase/next-multi/grid-row 等）、`v31_model_probe2.ps1`（切模型→预演→抓图名）

## 二、环境与路径

- V31（可运行、可脱壳、驱动对拍）：`local-data/recon/v31_keep.ps1`（托管启动）+ `v31_attach.ps1`（40+ action）
- V31 脱壳 PE：`local-data/recon/unpack/unpacked_v31.exe`（24.8MB，3638 函数，ImageBase 0x400000）
- V31 内存转储：`local-data/recon/v31mem_clean/v31_mem_00401000.bin`（base 0x401000，覆盖 0x401000–0x1CD0000）
  **文件偏移 = VA − 0x400C00**（不是 −0x400000，后者偏 0x400 字节）
- V35：运行 `MQJSSV35.EXE`（ASPack）；静态 `local-data/recon/unpack/unpacked_runtime.exe`（base 0x400000，偏移 = VA − 0x400000）
  **V35 实机在本机不可用**（点计算即弹「未注册」+ 80010108 退出）⇒ V35 侧只能静态挖；按边界不碰注册。
- 参数库：`C:\code\Glass\参数库.mdb` → `local-data/export_hb_mdb.ps1` → `local-data/hb_mdb/*.csv`（59 表）
- msvbvm60 真实基址 **0x734D0000**（0x731F0000 是 comctl32）——判断指针归属必须用模块基址+RVA
- IDA Pro 9.5 `C:\Program Files\IDA Professional 9.5`；MCP `ida` 条目待连接器管理页点"信任"

## 三、关键地址（现役）

| 对象 | 地址 |
|---|---|
| V31 立柱函数 `sub_18BD010` | `0x18BD010–0x18E9555`（181,573B / 43,508 指令 / 189 个 rtcRound）——**原判定不变**（曾据"断点未命中"想推翻，但断点自检 0 命中 ⇒ 那批未命中不作证据，已撤回）|
| 面板函数分簇（旁证） | A 簇 `0x89EA1D/0x8C796E/0x8D70FD/0x8E7E5B/0x8F8F6D(+0x9066B3)` 引「抗弯矩预选/抗弯/抗剪」；B 簇 `0xD291C6/0xD34ED9/0xD418AF/0xD4CCE9` 引「惯性矩/挠度」；立柱输出跨两簇 ⇒ 非"一函数一面板" |
| V35 对应立柱函数 | `0x1D442DA–0x1D70913`（181,817B，差 244）|
| V31 跳板 → 立柱 | `vtable+0xA0 = 0x4579DC` → `e9 rel32` → 真函数；方法表 `0x41B40C`（+0x14=跳板）|
| V31 槽数组 | `ds:[0x1C91168]`，16B/槽 VARIANT，BSTR 长度在 **ptr−4**；`[44]`A `[45]`lgA `[46]`μs1ψ `[47]`μs合 `[76]`g |
| V31 立柱链变量区 | `0x1FBE400`（qk/qd/M/wReq/lim/iReq/σ/qdRaw/V，每次预演刷新）|
| V31 常量池 | `0x401E88–0x401F78`；ψ 分母 1.4 @ `0x4017F8`（全镜像唯一）|
| thunk 名表 | `local-data/recon/v31_thunks.json`（251 个）：rtcRound `0x41371A`、VarDiv `0x413708`、VarMul `0x413714`、VarPow `0x41370E`、rtcLog `0x4135B8` |
| V31 风荷载函数 | `0xA6E000–0xA74000`（βgz/μz 四类块、内压分支）；V35 对应 `0xA62C07–0xA86FEE` |

## 四、已闭合的公式（有代码地址，勿再猜）

- `wk = w0·βgz(z)·μz(z)·[μs1·ψ(A) + μs_int]`；下限 w0≥0.3、wk≥1.0
- βgz `1+2·2.5·I10·(z/10)^−α`；β 下限 `max(βgz, 1+0.7/√μz)`（GB55001 4.6.5-2，曾漏）
- μz = C·(z/10)^p；A/B/C/D 系数与截断高度见 `docs/WINDLOAD_CODE_MAP.md`
- **ψ(A) 分母 = 1.4（不是 lg25）**：`slot[46]` 唯一写入点 `0x00E2CE3E`，链上 `0xE2CD45 fld 0x4017F8`
- 广东规范两处差异：g 2.5→3.0（w0≥0.5 门槛）+ 封闭式内压 0.2→0.3
- **取整必须 `decimal` + 银行家舍入**（V35 走 Variant Decimal）；βgz/μz/β 4 位、μsi/wk/qk/qd/σ/τ/lim 3 位
- 立柱链顺序不可换：K1/K2 乘在 qk 取整**之后**；`lim` 先 round3 再算 iReq；抗剪用显示值 wk×1.65；抗弯用 raw wk
- 单元式：`f1左=γ0·(N/2A1n + M·I1x/(I1x+I2x)/γW1nx)`；`q_k左 = round3(q_k·I1/(I1x+I2x))` 后算 df；抗弯取 min(Wx1,Wx2)
- 多跨静定梁：内力按 L（非 L+A）、限值按 L+A、输出与 N 无关；限值 ≤4500→/180，>4500→/250+7
- 玻璃：σ=6mηqa²/t²、df=ημqa⁴/D，D=Et³/[12(1−ν²)]，η=1 或 θ^−0.5

## 五、当前状态（2026-10-25）

- 第一页 40 案：**279/280 逐位（99.6%）**，仅 B10 未定位（qd 2.0475 平局；立柱函数只读控件文本，
  qk/qd 计算在窗体层另一函数 → 未找到就不凑）
- 立柱页：单跨简支/悬挑/单元式已逐位；缺 G1 双跨梁、G2 连续梁、G3 轴力 N、G4 挠度规范下拉、G5 钢铝结合
- 横梁/玻璃/连接/埋件/胶类五面板：零实现（Pure XAML，0 Binding）
- 待落盘：`ida_out` 产物生成脚本（可复现性）

## 六、文档索引（细节看这里）

- 规程 `docs/COMPARE_PROTOCOL_4WAY.md`；经验手册 `docs/RE_EXPERIENCE_PLAYBOOK.md`
- 立柱 93 条公式表 `docs/V31_COLUMN_MODEL_FORMULAS.md`；代码地图 `docs/V31_COLUMN_CODE_MAP.md`
- **立柱 45 分支（图号→公式）`docs/V31_MODEL_CASE_MAP.md`**：选择子 = `Form->[+54h]` 图名串，
  01=悬臂15种 / 02=简支21种 / 03=带悬臂简支9种（依据帮助 B24 原文）；图号↔界面模型仍待定位
- 取整对齐 `docs/ROUNDING_ALIGNMENT_V35.md`；第一页 40 案 `docs/COMPARE_BASIC_PAGE40.md`
- 风荷载代码地图 `docs/WINDLOAD_CODE_MAP.md`；槽映射 `docs/V31_SLOT_MAP.md`；模板字典 `docs/FORMULA_TEMPLATE_DICTIONARY.md`
- 单元式 `docs/COMPARE_UNIT_COLUMN_CODE.md`；工具链 `docs/RE_TOOLCHAIN_SETUP.md`；流程参数表 `docs/PROCESS_PARAM_TABLE.md`

## 七、工具（`local-data/recon/`）

`v31_slice.py`（x87+Variant 符号执行，还原 111/189 定稿点算式）、`v31_thunk_names.py`、`compare_basic40.py`、
`v31_attach.ps1`（basicsweep/colbase/psidisc/unitmid/midrig/hold）、`dump_v31_mem.py`、`v31_slot_probe_v2.py`、
`ida_map.py`（风荷载索引）、`ida_blocks.py`（分块反编译）
`ida_out/`：`v31_column_insns.txt`（43,508 行）、`v31_roundpoints_full.txt`、`v31_roundpoint_expr.tsv`

## 七之二、调试器（2026-10-25 装）

- 便携 **x64dbg**：`C://code//Glass//.tools//dbg//x64dbg//release//x32//x32dbg.exe`（32 位用 x32）
  来源 `https://down.52pojie.cn/Tools/Debuggers/x64dbg_2026-05_27.zip`（直连可下，代理会 403）
- 自建 ctypes 调试器 `local-data/recon/v31_bp_pic.py`（attach + 0xCC 断点 + 读寄存器/内存 + 看门狗强制 detach）
- **血的教训**：调试器进程被打死时，Windows 会**连带杀掉被调试进程**（V31 因此死了 3 次）。
  所以：单次运行必须 <15s、自带看门狗、绝不留后台调试器；杀掉 python 前先 detach。

## 八、踩过的坑（速记）

- V31 控件 `GetWindowTextW`/`WM_SETTEXT` 无效 → 读 `WM_GETTEXT(StringBuilder)`、写 剪贴板+`EM_SETSEL`+`WM_PASTE`
- SetField 前必须自己 `Clipboard::SetText`，否则粘上残留 → 报「请输入正确数字」、输出全空
- 立柱屏两套 Frame 坐标会撞 → 必须 `FindByRelStrict`（只认可见）
- 长扫描每案立即落盘（丢过 37 案）；含中文 .ps1 必须 UTF-8 带 BOM
- 构建坑：仓库根有设备名文件 `nul` → `dotnet build` 报 Stack overflow；用 `-p:OutputPath=C:/code/Glass/.tmpbuild/` 绕行
- Avalonia UIA：Radio/Check 的 Content 必须是字符串；RDP 下鼠标点击对 Avalonia 无效，必须走 UIA

## 二之二、git 推送授权（2026-10-25 配置）

- 根因：`credential.helper=manager` 但机器上**没装 GCM 可执行文件** → `could not read Username`。
- 已配 **SSH**：`~/.ssh/id_ed25519`（ed25519，无口令）+ `~/.ssh/config` 走 `ssh.github.com:443`
  （22 端口不可用时用 443）；`git@github.com` 已验证可连通（Permission denied(publickey) = 能到）。
- origin 已切为 `git@github.com:ey2h/SMHS-Cal-2.git`。
- **需用户一次性操作**：把 `~/.ssh/id_ed25519.pub` 内容贴到 GitHub → Settings → SSH and GPG keys → New SSH key。
- 之后 `git push origin feat/phase3-extend` 即可，无需再输密码。

## 三之二、★ 立柱计算函数 = 0xD1E9E0（2026-10-25 Frida 实证，取代旧结论）

- **实证方法**：Frida 挂 `rtcRound(0x41371A)`，取 `this.returnAddress` 统计 → 预演时命中集中在 **0xD2xxxx**；
  反查函数首（0x55 8B EC）= **0xD1E9E0**（>46KB，含 1.5 引用 0xD233AF/0xD27E25/0xD2E74D）。
- **qd 链（0xD23320–0xD23436）**：
  `__vbaVarMul` → `rtcRound(.,3)` → 写 **slot[8]=qk**；`fld 1.5(VT_R8)` → `__vbaVarMul(slot[8]×1.5)` → `rtcRound(.,3)` → 写 **slot[9]=qd**；
  槽数组基址 **dword_1C91184**（第三个槽数组）。
- **实测类型**（hook 0xD23431）：slot7=**wk=0.001285 N/mm²**、slot8=**qk=1.414 (VT_R8)**、qd 输入=**2.121 (VT_R8)**。
  ⇒ qk/qd 全是 **double**，乘法是 double×double，**不是 decimal**。
- 旧结论 `sub_18BD010`/`sub_89EA1D` 均**不是**立柱计算本体（Frida 实测 0 命中；rtcRound 对照 3+ 次命中证明链路有效）。
- B10 直接调 rtcRound 实测（v31_frida_round.py）：入参 double →
  3.0495…→3.05 / 4.0515…→**4.052** / 4.1325…→**4.132** / 2.0475…→**2.047**。
  这 4 个结果**同时**否定「double 就近」和「decimal 半偶」，舍入算法仍需用真实 qk 位模式复现。

## 四之二、★ rtcRound 算法定案（2026-10-25，位模式 4/4 全等）

```
double VbaRound(x, d):
    y = x;  重复 d 次 y *= 10        // ★ 必须逐次乘 10，不能一次乘 10^d
    n = 四舍六入五成双(y)
    return n / 10^d                  // 除法，不是 n*10^-d
```
- 实证：Frida 直接 call V31 的 `0x41371A`（`v31_frida_round.py`/`v31_frida_chain.py`），读回 VT_R8 位模式：
  3.0495…→3.0499999999999998 / 4.0515…→4.0519999999999996 / 4.1325…→4.1319999999999997 / 2.0475…→2.0470000000000002
- 关键：一次乘 1000 会把 4.0515… 算成 4051.4999999999995（→4051，错）；逐次乘 10 得 4051.5（→4052，对）
- 已实现：`WindLoadEngine.VbaRound`（`R()` 委托它）；`ColumnCalcViewModel` 的 qk/qd 改回 **double** 路径；
  单测 `tests/GlassManagement.Calc.Tests/VbaRoundTests.cs`（9 例，含 4 个平局点位模式比对）全过
- ⚠ 此前"必须用 decimal"的结论作废（v35 侧另说，v31 实测是 R8）
- **复刻铁律**：参与计算的中间量一律 **double**（与 V31 的 VT_R8 对齐），**禁止 decimal 参与运算**；
  decimal 只可用于显示/格式化。且**运算顺序**必须与代码一致（N/mm²×mm、逐次×10、n/10^d），
  否则 double 也照样差末位。

## 四之三、★ 第一页 40 案 = 280/280（2026-10-25 07:30）

- 立柱函数 `0xD1E9E0`；qd 链 `0xD23320–0xD23436`；槽数组 `dword_1C91184`（slot7=wk(N/mm²)、8=qk、9=qd）。
- **两步不同语义**（各自被实机钉死）：
  * qk：十进制半偶（`2.7005000000000003 → 2.701`，B08/B09 证据）
  * qd：`VbaRound`（逐次×10→半偶→`n/10^d`；B01/B08/B10/B12 位模式 4/4）
- VbaRound **不是** rtcRound 全局等价（对"略高于平局"的 2.7005 会舍错），只在 qd 可达输入上等价。
- 复刻基线生成：`dotnet run --project src/GlassManagement.Desktop -- --calc --selftest=basic40 --dump-preview=...`
  再跑 `python local-data/recon/compare_basic40.py`；当前 **280/280 = 100.0%**。

## 一之三、★ 铁律：所有测试必须从第一页开始（用户强调）

- wk / μs / 场地 / 地区 / 地形 **全部由第一页计算链产出后传到立柱页**，
  不允许在立柱页或测试代码里覆盖 wk 凑数（`--selftest` 里也禁止）。
- 立柱页对拍前必须先确认第一页默认值与 V31 一致：V31 启动默认 = **C 类地形** + **地区未选
  （回落参数库首行「北京」0.45kPa）** + 标高 100 + 50 年 + 墙面区 1.0 + 封闭式 + ≥1kPa 下限。
  复刻曾默认 B 类 + 上海，导致 wk=1.848 而 V31=1.285，立柱屏全部对不上。

## 三之三、★ 双跨梁 = 函数 0xD35D8C（2026-10-25 全链闭合，14/14）

- 边界 0xD35D8C–0xD42762；槽数组 `dword_1C91184`（slot2=L1、3=a、4=b、19=-M、22..27=系数）。
- 两跨：`a=L2, b=L1-L2`，**等跨时 a-1 / b+1**（L2=1500→1499/1501；L2=1200→1200/1800）。
- `M=round3(qd(a³+b³)/8/L1)`；`V=max(a·qd/2+M/a, b·qd/2+M/b)`；`τ=Sx·round3(V)/I/t`；
  `lim=Lc/180`；`df=qk·kf·Lc⁴/(24EI)`；`I_req` 同式但除 `round3(lim)`。
- **kf 是一条 20 步的解析链**（k1..k12，含 Atn/Cos/Sqr，两个 π：3.14 与 3.1415926），
  见 `docs/V31_DOUBLESPAN_CODE.md`；**k3=0.62475 不能取整**（取整会偏 0.24%）。
- 完整实现：`src/GlassManagement.Calc/Engines/DoubleSpanColumnEngine.cs`；
  单测 `tests/.../DoubleSpanColumnTests.cs`（4 例）；对拍 `compare_doublespan.py` → 14/14 = 100%。
- 取证工具（新增）：`v31_frida_ops.py`（hook 全部 Variant helper 读实参）、`v31_frida_vars.py`
  （读 [ebp-X] 局部量）、`v31_frida_rounds.py`（定稿点入出参）、`v31_model_run.ps1`（切模型+设跨度+预演落盘）。
- helper 语义：Div/Mul/Pow/Add **一律 args[2] op args[1]**；Sub=0x41377A、Neg=0x413804、
  Atn=0x4137FE、Cos=0x4137F2、Sqr=0x4137F8。

## 三之四、立柱页矩阵对拍（2026-10-25，84/84 + 型材 3/3）

- **矩阵**：单跨受拉×3 跨度 / 单跨受压×2 / 双跨梁×7 = 12 组 × 7 项 = **84/84 逐位一致**。
  单跨受拉与受压在构件式下**结果完全相同**（V31 实测）。
- **挠度限值有 20mm 上限**：`lim = min(L/180, 20)`（常量 `dbl_4019E0=20`，引用点 0xD3CBB3/0xD3DE6F）；
  3600 是边界（=20），3700/4500/5000 都是 20；`I_req` 用截断后的 lim。
- **L2 必须 < L1/2**：等于时自动 −1（→1499），大于时弹「无效输入」不计算。
- **wk→qk 必须走十进制**：V31 slot[7]=0.0012650000000000001（比浮点 1.265/1000 大 1ulp），
  → qk 输入 1.3915000000000002 → 1.392；double 直算得 1.391（错一档）。修法：wk 先 `Math.Round((decimal)wk3,3)`。
- **型材**：4000 组 I/W/S/t 全从 `立柱.csv` 走；预选量（抗弯/惯性矩）与型材无关（V31 同）。
- 工具：`v31_col_matrix.ps1`、`v31_limit_probe.ps1`、`v31_profile_run.ps1`、`v31_py.py`（纯 python 驱动）、
  `kill_dialogs.py`、`compare_matrix.py`。
- **驱动坑**：① 预演单次点击偶发不刷新 → 每组点两次；② L2≥L1/2 弹「无效输入」必须先收掉；
  ③ 点控件用 PostMessage（SendMessage 遇模态框死锁）；④ python ctypes 需声明 GlobalAlloc restype=c_void_p。
- 未完成：自定义材质（填值 ok，预演无输出）、连续梁、多跨（要「下一步」进「多跨数据输入」Frame）、钢铝结合。

## 三之五、连续梁 = 函数 0xD42762；连续梁/自定义材质已闭合（2026-10-25）

- **连续梁（四点三跨）**：`0xD42762–0xD4DB91`。
  `M=round3(0.1·qd·L²)`、`V=0.6·qk_raw·L·1.5`、`df=qk·0.00677·L⁴/(E·I)`、
  `I_req` 同式除 `round3(lim)`、`lim=min(L/180,20)`。
  ★ **剪力和弯矩取的 q 不同**：剪力用未取整 qk_raw（bay×wk_Nmm2），弯矩用取整 qd，挠度用取整 qk。
  系数全是代码字面量：0.1 / 0.6 / 0.00677 / 1.5。
- **「其它材质」控件归属**（探针填 111/222/333 反查）：`(529,162)`=[σ]、`(529,185)`=[τ]、
  `(529,140)`=E（禁用恒 70000）、`(391,162)` 不影响输出。实测 f=90 → `W_req=M/90`。
  复刻字段：`Session.CustomMaterial/CustomElasticModulus/CustomAllowBending/CustomAllowShear`。
- **矩阵总账 105/105**（15 组×7 项）：单跨受拉3 + 单跨受压2 + 双跨7 + 连续梁3。
- **多跨**：必须「下一步」进 `Frame '多跨数据输入' (9,29)` 再预演；输出格式完全不同
  （第1跨/第3跨/第2跨支座处 三段，含折算强度 √(σ²+τ²)，限值 18.333=3300/180）。
  控件：DataGrid(329,157)、输入 (102,179)/(102,221)/(102,296)、确认数据(171,346)、预演(801,383)。
  公式**未挖**，是下一步。
- 钢铝结合也确认有独立 Frame `'钢铝结合型材参数' (9,29)`，同样要「下一步」。

## 三之六、多跨铰接连续（静定）= 函数 0xD53595（2026-10-25 部分闭合 28/28）

- 页面：立柱页点**预演无输出**，必须「下一步」进 `Frame '多跨数据输入' (9,29)`；
  控件 总跨数(102,179)/跨长A₁(102,221)/跨长L₁(102,296)/确认数据(171,346)/预演(801,383)。
  驱动：`v31_py.py multi --model 多跨静定 --n --a --l`（自带「上一步」回退）。
- 函数 **0xD53595–0xD73EE5**（133KB）；已闭合（4 组样本 28/28，`verify_multispan.py`）：
  * `X=(L²−A²)/L`（有效跨，`0xD5B301` 用 1−(A/L)²）
  * `M1=qd·X²/8`、`V1=qd·X/2`
  * `M2=qd·A(A+X)/2`、`V2=V1+qd·(A/2)(2+A/L)`
  * `σ=M/W`、`τ=Sx·V/(I·t)`（Sx=13606）、`σ_eq=√(σ²+τ²)`（**不是** √(σ²+3τ²)）
  * `lim=min((A₁+L₁)/180, 20)`；风荷载从属面积 `A_trib=(A₁+L₁)×分格宽`
- **第3跨已闭合**（36/36）：记 `t=(1−(A/L)²)/2`，`S₃=V1−qd·A·t`、`M3=M1−S₃·A·t`、
  `V3=rtcRound(S₃·A/L, 0)`（**0 位**取整，故恒为整数）。`Form+84h`=各跨剪力序列、
  `+4Ch`=A、`+68h`=L（`v31_frida_arrays.py` 直读）。减数块取的是 `arr84[3]`。
- 仍未闭合：两处挠度。程序自带文本 `"d~f1~=d~f1,mid~+d~f2,C~/2="`（`0xD662DF`），
  实测 `df1=4.3485+13.829`、`df2=2.855+4.549`，两项来源未拆（局部量被多分支复用）。
- ★ **thunk 名纠正**（用 IAT 指针−0x734D0000 对 msvbvm60 导出表）：0x41377A=`__vbaVarSub`、
  0x4136BA=**`__vbaFreeVarList`**（此前误当 Add，209 条记录全是噪声）、0x4135BE=`__vbaVarCopy`、
  0x4137F2/FE/F8=`rtcCos/rtcAtn/rtcSqr`。语义统一 `out=c2 op c1`。

### 多跨静定 挠度（2026-10-25 续）
- `df1 = df1mid + df₂C`；记 `r=A/L`：
  `N₂ = A²·V1·L/3`、`c₁ = 1−4r²−3r³`、`c₂ = 1+r`、
  `df1mid = [(1+r)N₂ − c₁·A·qk·L³/24]/(2EI)`、`df₂C = 5qkL⁴/(384EI)·(1−2.4r²)`。
  证据：`0xD66109–0xD662D2`、`0xD65B27–0xD65BA1`。
- `df2 = round3(bracket₂)/2 + round3(df₂C − X₂/EI)`，其中 `X₂ = S₃·A·L²/16`
  （`0xD6B6FA` fdiv 16 确认）、`bracket₂` 用 `arr84[4] = V1 − D(1−r)`。
- ★ **迭代规律**：`d₁ = D = qd·A·t`，`d_{k+1} = D − r·d_k`，`arr84[k] = V1 − d_{k−1}`；
  不动点 `D/(1+r)`。
- ★ **挠度必须分项取 3 位再相加**（V31 实测 `Add(4.3155, 21.524)`）：df1 同样 `round3(bracket₁)/2 + round3(df₂C)`。
  全精度相加会差 5e-4 量级。⇒ 两处挠度 8/8 逐位。
- 唯一残差：r=0.2（A=600/L=3000）第2跨支座折算强度 102.824 vs 102.821（3e-3），未定位。
- 多跨静定总账 `verify_multispan.py` = **43/44**。

## 三之七、多跨铰接连续**一次超静定** = 函数 0xD73EEF（2026-10-25 全闭合 30/30）

- 函数 **0xD73EEF–0xD8EF31**（~110KB）；定位：挂 `__vbaVarMul` 统计返回地址 → 热点 0xD7Bxxx–0xD7Cxxx
  （按跨数 ×10）→ 反查 `55 8B EC`。
- 页面：**必须「下一步」**进 `Frame '多跨数据输入'`；**4 个输入框**（静定只有 3 个）：
  总跨数(102,179)、跨长A₁(102,221)、**跨长A₂(102,258，默认600)**、跨长L₁(102,296)。
- 运行期数组：`+0x4C`=A₁、`+0x12C`=A₂、`+0x110`=L、`+0xD8`=−M、`+0xF4`=M'（负）、`+0xA0`=P。
  ★ A₁≠A₂ 是实机现象（A₁=400/A₂=700 两数组分别读出 400/700）。
- 公式：
  `M=qd·A₁²/2+P·A₁`；`T=(L³+A₂³)·6qd/24`；`M'=−(T−M·A₂/6)/2/(A₂+L)`；
  `P=rtcRound(qd·L/2+M'/L, 0)`（**0 位整数**）；
  控制截面 `max|M'| > max M` → 跨中控制（σ=|M'|/W，0xD816EC），否则支座控制（σ=M/W，0xD7E8DD）；
  `q1=qd·A₁/2+|(−M)−M'|/A₂`；`V=max(q1, P+qd·A₁)`（支座）/ `max(q1, |qd·L/2+M'/L|)`（跨中）；
  `τ=Sx·V/(I·t)`；`σeq=√(σ²+3τ²)`；`lim=min((A₁+L)/180,20)`；
  `df=round3(num1/EI)−round3(num2/EI)/2`，`num1=5qkL⁴/384+M'L²/16`、
  `num2=P·A₁³/3−A₁⁴qk/8+A₂⁴qk/24−M·A₂/3+M'·A₂/6`（**A₁/A₂ 混用**，A₁=A₂ 时看不出）。
- 常量：4019A8=3、403C50=4、4018F8=2、4027F8=6、402F88=16、4019E0=20。
- 对拍 **30/30 逐位**（6 组：L=2700/2750/2800、A₁=800/A₂=600、A₁=700/A₂=800、L=3000 跨中控制）。
  实现 `Engines/IndeterminateColumnEngine.cs`；单测 6 例（总 60/60）。
- 仍需确认：① 分支判据只有数值证据（未找到显式比较指令）；
  ② 两候选取大的「物理意义」未解；③ A₁/A₂ 输入框的界面标签文字未取。
