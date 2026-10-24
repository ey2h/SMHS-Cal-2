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
| ⚠ **立柱函数更正**：`sub_89EA1D`（0x89EA1D–0x8A9F7E，46,433B，72 个 rtcRound）——**执行期未命中验证前仅算强证据**；依据：它引用「抗弯矩预选」`0x4D2318`、「抗弯强度计算值」`0x4D2554`，且是 5 个同构兄弟函数（面板×5）中的第一个 |
| V31 `sub_18BD010` | `0x18BD010–0x18E9555`（181,573B / 189 rtcRound）——**实机在立柱页点预演时入口与内部 `0x18BD50F` 均未命中**，判定它不在立柱页路径上，极可能是「常用静力计算」那套 45 模型的函数 |
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
