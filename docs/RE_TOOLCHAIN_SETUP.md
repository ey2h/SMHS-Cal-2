# 逆向工具链：选型、配置与复现（V35 / V31）

> 目的：把「能不能开始干活」这件事变成一条命令，而不是每次会话重新摸索。
> 配套脚本：`local-data/recon/toolchain/setup_toolchain.py`（自检/安装）、`local-data/recon/ida_blocks.py`（巨型过程分块反编译）。
> 最后核对：2026-10-24。

---

## 1. 选型：用哪几件工具，为什么是它们

| # | 工具 | 承担什么 | 为什么选它 | 为什么不用替代方案 |
|---|---|---|---|---|
| 1 | **IDA Professional 9.5 + Hex-Rays**（经 `ida-mcp` 的 idalib 后端） | 反编译，把汇编还原成 C 伪代码 | 本机已装且 Hex-Rays 对 VB6 原生码还原质量最好；idalib 无 GUI、可脚本化、**默认不落盘**（不动原始 IDB） | Ghidra：本机未装 JDK/Ghidra，且对 VB6 thunk 调用识别不如 IDA；纯 capstone：读控制流太慢，A 类两个误判就是没 decompile 的代价 |
| 2 | **capstone 5.0.7** | 逐指令反汇编、常量地址→数值解析、算子序列归一化 | 只做「字节 → 指令」这一层，输出可逐条核对；用来**交叉验证 Hex-Rays**（伪代码可能省略细节，指令不会） | 只用 IDA：伪代码会折叠常量和中间变量，做「逐指令同构比对」时必须回到反汇编 |
| 3 | **ctypes + `ReadProcessMemory`** | V31 运行期 VARIANT 槽取证（`probe_v31_slots.py`） | 唯一能拿到「程序实际用了哪个值」的手段；秒级读取、不落盘，避免 176 MB 内存 dump | 全量内存 dump（`dump_v31_mem.py`）：慢，且事后才知道要找什么 |
| 4 | **PowerShell + Win32 / UIA** | V31 无人值守驱动（`v31_attach.ps1`）、V35 无界面驱动（`v35_hidden_drive.ps1`） | 这套 VB6 控件 `WM_GETTEXT/EM_SETSEL+WM_PASTE/BM_CLICK` 都能用，无需模拟鼠标；RDP 下模拟点击对 VB6/Avalonia 都不可靠 | pyautogui / SendInput：RDP 会话下失效（已实测）；UIA 单独：菜单拿不到 `InvokePattern` |
| 5 | **uv / uvx** | 隔离运行 `ida-mcp`（自带 idapro、ida-nexus 依赖） | IDA 侧依赖重且与 venv 冲突，uvx 每次按需解析、不污染项目 venv | 直接 pip 装进 venv：会和 capstone/项目依赖打架（早前已踩过 sys.path 冲突） |
| 6 | **dotnet SDK 10.0.402**（`~/.dotnet/dotnet.exe`） | 编译复刻版做数值对拍 | 复刻是 Avalonia；对拍必须跑真程序 | — |

### 明确**不用**的

- **REA（rea-agents）**：装了但全程未使用。它要自带 Ghidra 12.1.4 + JDK 21，本机没有；IDA 这条路已经够用。
- **注册绕过 / 脱壳机 / 内存补丁**：边界 B1 写死不做。V35 注册失效就退回静态分析，不碰 `Reg.dll`、不碰主板/硬盘绑定。

---

## 2. 两个输入文件的关系（最容易搞混的一点）

| 用途 | 文件 | 说明 |
|---|---|---|
| **运行 / 实机驱动 / 读内存** | `C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\MQJSSV35.EXE` | 装的那个，13,372,298 字节，**ASPack 加壳** |
| **静态分析 / 反编译 / 查地址** | `local-data/recon/unpack/unpacked_runtime.exe` | 脱壳后的完整内存映像，36,040,704 字节 |

**两者是同一个程序，地址 1:1 对应。** 证据：

| 判据 | 加壳 EXE | 脱壳镜像 | |
|---|---|---|---|
| `TimeDateStamp` | `0x6A48BCFC` | `0x6A48BCFC` | 同一构建 |
| `ImageBase` | `0x400000` | `0x400000` | 地址可直接对应 |
| `SizeOfImage` | `36,040,704` | 文件大小正好 `36,040,704` | 镜像即完整映像 |
| 入口点 | `0x1E8B243`（壳的入口） | `0x1E7F9C0`（原程序入口） | 差在壳 |

**为什么静态分析不能直接读磁盘那个 EXE**：加壳后代码段的 `RawSize = 0`，源码被搬进 `.aspack` 段并压缩。

```
段        磁盘 VirtualSize  磁盘 RawSize   镜像 VirtualSize  镜像 RawSize
.text     0x1E0C000        0x0            0x1E0C000         0x1E0C000   ← 磁盘上代码为 0 字节
.data     0x19000          0x0            0x19000           0x19000
.aspack   0xE000           0xE000         —                 —
```

同一个地址 `0xA702E0`：

```
磁盘 MQJSSV35.EXE : 38 41 74 FB CF C6 AB C1 AC 9D FF 61      ← 压缩数据
脱壳镜像          : DD 05 10 1F 40 00 DD 9D D4 FD FF FF      ← fld qword [0x401F10]（真指令）
```

**最硬的验证**：脚本 `local-data/recon/verify_v35_image.py` 把**运行中进程**的内存与镜像逐字节比对：

```bat
:: 先启动 V35，再执行
python local-data\recon\verify_v35_image.py
```

实测（pid 22728）：

```
        VA    字节        一致  首处不一致
0x00A62C07    32        全等  -
0x00A702E0    48        全等  -
0x00A7178B    32        全等  -
0x00A73604    32        全等  -
0x00A73BB2    32        全等  -
0x00401F10    32        全等  -
抽样 6 处，全等 6 处
→ 静态地址与运行期内存逐字节一致：脱壳镜像就是该进程的内存映像，地址可直接对应。
```

⇒ 所以「跑的是 MQJSSV35.EXE、查地址用脱壳镜像」不矛盾，是同一份代码的两个入口。
唯一要注意的是：**改哪个都不行** —— 磁盘 EXE 动过会破坏签名/自校验，镜像只作只读分析用。

---

## 3. 版本与路径（照抄即可）

| 项 | 值 |
|---|---|
| Python（venv） | `C:\Users\Jack\.workbuddy\binaries\python\envs\default\Scripts\python.exe` · 3.13.14 |
| capstone | 5.0.7（锁版本，`requirements.txt`） |
| uvx | `C:\Users\Jack\.workbuddy\binaries\python\envs\default\Scripts\uvx.exe` · 0.13.5 |
| IDA | `C:\Program Files\IDA Professional 9.5\ida.exe`（9.x 已统一 `ida.exe`，无 `ida64.exe`） |
| V35 主程序 | `C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\MQJSSV35.EXE`<br>sha256 `A0976F7383C0FF9924B2DC0D4E33D773F1FB9007D1D6ECBD86915708BE93004B` |
| V31 主程序 | `C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V31.0D\MQJSSV31.EXE`（注意目录带 **D**） |
| 脱壳镜像（唯一静态输入） | `local-data/recon/unpack/unpacked_runtime.exe` · 36,040,704 bytes |
| dotnet | `%USERPROFILE%\.dotnet\dotnet.exe` · SDK 10.0.402 |

---

## 4. 一键复现

```bat
:: ① 自检（不装任何东西，只报告）
C:\Users\Jack\.workbuddy\binaries\python\envs\default\Scripts\python.exe ^
  C:\code\Glass\local-data\recon\toolchain\setup_toolchain.py

:: ② 新机器上补依赖（只装 requirements.txt 里那三个纯 Python 包）
C:\Users\Jack\.workbuddy\binaries\python\envs\default\Scripts\python.exe ^
  C:\code\Glass\local-data\recon\toolchain\setup_toolchain.py --install

:: ③ 机器可读的报告（落盘 toolchain-report.json）
... setup_toolchain.py --json
```

自检覆盖 16 项：python 版本、capstone / Pillow / python-docx、IDA、uvx、脱壳镜像指纹、V35 指纹、V31 存在性、两个映射 JSON、五个关键驱动脚本。
退出码 `0` = 可进入可操作状态，`1` = 有 FAIL。

**若脱壳镜像丢失**，重建链条是：`MQJSSV35.EXE`（ASPack 加壳）→ 脱壳 → 得到 `unpacked_runtime.exe`；本仓库未保存脱壳器，历史上用 aidbg 产出，需重做时按 `docs/REVERSE_ENGINEERING_EVIDENCE.md` 记录的方法执行。

---

## 5. 冒烟测试：五条命令，任一条失败就别往下测

```bat
:: 1) 静态：能不能解析 PE 段表
python local-data\recon\pe_probe.py

:: 2) 静态：按地址范围抽常量 + VARIANT 算子序列（验证 capstone 链路）
python local-data\recon\trace_block.py ^
  local-data\recon\unpack\unpacked_runtime.exe 0x400000 0xA71700 0xA71900

:: 3) 静态：V35 / V31 同名代码块归一化比对（验证比对工具）
python local-data\recon\compare_gust_block.py

:: 4) 动态：Hex-Rays 能不能出伪代码（验证 ida-mcp 链路，本轮新增）
python local-data\recon\ida_blocks.py --start 0xA702E0 --end 0xA703D0

:: 5) 动态：V31 运行期槽位读取（需 V31 在跑）
python local-data\recon\probe_v31_slots.py
```

---

## 6. 巨型过程分块反编译（本轮新增能力）

**问题**：风荷载主过程 `0xA62C07`–`0xA84006` 约 **136 KB**，是单个函数；`0xA62C07` 之后到 `0xA72000` 之间**没有第二个 `55 8B EC` 序言**。整函数 `decompile` 返回 `None`。

**做法**（`ida_blocks.py`）：

1. `uvx ida-mcp stdio` 起 idalib 会话；
2. **优先复用 IDA 已识别的函数**（IDA 的指令边界是权威的，不自己猜）：取覆盖 `START` 的函数的**基本块表**（`ida_gdl.FlowChart`）；
3. `del_func` 掉巨型函数，再对**每个基本块**单独 `add_func(start, end)` + `decompile`；单块返回 `None` 时按「本块 + 后续 1~2 块」合并重试（长块常因此救回来）；
4. 逐块打印伪代码，失败块标注 `<unrecoverable>` / `<fail …>`，**不静默跳过**；
5. **收尾复原**：删掉本次建的所有微函数，把原巨型函数按原始起止重建 → IDB 与运行前一致。

```bat
:: 风荷载模块核心段（默认）
python local-data\recon\ida_blocks.py

:: 整段巨型过程（块多，建议后台跑）
python local-data\recon\ida_blocks.py --start 0xA62C07 --end 0xA84006 --max-blocks 4000 --out wind_load_all.c

:: IDB 被改脏了 / 换机器重来 —— 删库重建，保证可复现
python local-data\recon\ida_blocks.py --fresh
```

### 5.1 实测结果（`0xA6E000–0xA72000`，风荷载核心段）

```
giant func: 0xa62c07 - 0xa86fee  (148455 bytes)
basic blocks in [0xa6e000,0xa72000): 341
=== blocks ok=199 fail=142 ===
=== restored: removed 341 micro func(s), giant 0xa62c07-0xa86fee rebuilt=True (insns=32947) ===
```

- **199 块出伪代码**，**142 块退化为带注解的反汇编**（这些块含完整 VARIANT 算术链，Hex-Rays 单独建函数时栈帧推断失败，属预期）。
- 产出：`local-data/recon/ida_out/windload_blocks_A6E000_A72000.txt`（约 16 万字符）。
- 收尾复原成功（341 个微函数全清、巨型函数重建 32947 条指令），所以**可以反复运行、结果一致**。

**全量跑（整段巨型函数）**：

```
basic blocks in [0xa62c07,0xa86fee): 3895
=== blocks ok=2023 fail=1872 ===
```

产出 `local-data/recon/ida_out/windload_full_A62C07_A86FEE.txt`（**1,534,658 字符 / 47,463 行**），耗时 **42 秒**（有 IDB 缓存）。

### 5.2 输出可读性增强（两块，都是踩坑后加的）

**① VB6 字符串反解**：伪代码里的 `&unk_4DAEDC` 会变成 `&unk_4DAEDC("A类地形")`，一眼知道是哪个模板串。

```
*(_DWORD *)(a1 - 540) = &unk_4DAEDC("A类地形");
*(_DWORD *)(a1 - 540) = &unk_4DADF8("，按广东荷载规范取3.0(注：本计算采用广东荷载规范…");
```

这里有两个必须记住的布局陷阱：

| 陷阱 | 正确写法 |
|---|---|
| `push offset unk_XXXX` 指向的是**字符数据本身** | 长度前缀在 **va−4**，不是 va |
| 长度前缀是**字节数**，不是字符数（`len=8` → "A类地形" 4 个汉字） | 按 `ln` 字节读，终止符在 `data+ln` |

踩反的后果很隐蔽：不是报错，而是**一条字符串都解析不出来**（终止符被当成文本里的控制字符）。

**② thunk 名解析**：`call sub_419D78` 会附带 `; -> __vbaVarDup`，直接可判语义。
（映射表 `local-data/recon/unpack/vb6_thunk_names.json`，254 条。）

### 5.3 产出怎么查：索引工具 `ida_map.py`

148 KB 分块产出约 47,000 行、3,895 个块，直接翻不现实。`ida_map.py` 把产出建成可查询索引：

```bat
:: 建索引（生成同目录 .index.tsv，并打印各语义锚点的块地址）
python local-data\recon\ida_map.py build

:: 按语义找块（常量名 / 字符串 / 函数名都行）
python local-data\recon\ida_map.py grep 取0.8μ
python local-data\recon\ida_map.py grep dbl_401EA8

:: 按地址取整块（--ctx 2 连带前后各 2 块）
python local-data\recon\ida_map.py at 0xA73BEF
```

`build` 会顺带输出锚点摘要，例如：

```
βgz_A(0.12)     1 处  00A702E0
βgz_B(0.14)     1 处  00A708CF
内压0.2           1 处  00A73604
β下限(0.7)        1 处  00A7178B
```

结果整理成了 `docs/WINDLOAD_CODE_MAP.md`（哪一段在算什么、每个常量对应哪个块、槽位约定、以及一条否定结论：
**wk 的最终乘积不在本函数内**，`__vbaVarMul` 全函数只有 14 个块且全部已定位，`fmul` 为 0 次）。


### 5.4 三条必须知道的坑

| 现象 | 原因 | 处理 |
|---|---|---|
| 第二次运行报 `NO FUNCTION covering 0x…` | idalib 把 IDB 落盘成 `unpacked_runtime.exe.i64`（275 MB），上次 `del_func` 的改动被持久化 | 脚本已内置「收尾复原」；已被改脏就 `--fresh` 删库重建 |
| 全新库里也没有覆盖该地址的函数 | IDA 自动分析**不会**把这段识别成函数（没有入口 xref） | 必须显式给 `--func-start/--func-end`（默认 `0xA62C07`/`0xA86FEE`，依据是 `55 8B EC 83 EC 0C 68 C6 9A 41 00` 的 MSVC 序言 + SEH 帧） |
| `add_func` 返回 `False`、`undecodable=1` | 起点落在指令中间（`0xA6FF00` 的 `ff ff` 是上条指令尾巴，真边界 `0xA6FF02`；capstone 在该点解不出任何指令） | **绝不用 START 去对齐或解码**，块边界一律取 IDA `FlowChart` 的结果 |

其他已知限制：
- 分块建函数会让**栈帧推断**在块边界处失真，伪代码里的 `local variable` 编号不可跨块复用；要读具体数值必须回 `trace_block.py` 核常量。
- 块数上限 `--max-blocks`（默认 1200）防止一次吐太多；超限只输出前 N 块。
- 单次会话首次打开 36 MB 镜像要做完整自动分析，**约 2–5 分钟**属正常（`--fresh` 实测 8 分 57 秒）；有 IDB 缓存时 15–22 秒。
- 同一时刻只能有一个 idalib 会话持有该库；会话没关就再起一个会报 `no open database instance`。

---

## 7. 各版本的可操作性边界（实测，别再重复试）

| 对象 | 能做什么 | 不能做什么 |
|---|---|---|
| **V35（V53）** | 启动、打开 `基本参数输入` 窗体、枚举隐藏控件、`WM_SETTEXT/WM_GETTEXT/BM_CLICK` 全部有效 | **一点「下一步/预演」就弹「程序未注册，或注册遭到破坏」+ VB 运行时错误 80010108**，程序退出。`v35runs/` 内**从未产出过任何数值结果** |
| **V35 静态** | 脱壳镜像全量可读：常量表、thunk 映射、字符串（含计算书模板）、代码块同构比对、Hex-Rays 分块反编译 | — |
| **V31** | 全自动驱动：填基本参数 → 下一步 → 选力学模型 → 预演 → 读 RTF；并可在预演瞬间读 VARIANT 槽 | 需要**可见的交互式会话**（RDP 断开或锁屏会失效）；分格宽等输入有范围校验，越界会让「预演」按钮变 NULL |
| **复刻（SMHS）** | `--calc --selftest=preview / sweep / column-only` + `--dump-preview=<file>` 全自动出数，供与 V31 逐项比对 | — |

> 因为 V35 只能静态，**数值基准一律以 V31 为准，V35 负责实现级同构取证**。这两条腿缺一不可：只有 V31 会得出「原程序=规范」的错误结论（β 下限那次就是），只有 V35 又拿不到运行期实值。

---

## 8. 产物与目录

```
local-data/recon/
├─ toolchain/
│  ├─ requirements.txt          版本锁定的依赖清单（capstone / Pillow / python-docx）
│  ├─ setup_toolchain.py        自检 / --install / --json
│  └─ toolchain-report.json     最近一次自检结果（自动生成）
├─ verify_v35_image.py          验证「脱壳镜像地址 == 运行中进程内存」（6/6 逐字节全等）
├─ ida_out/
│  ├─ windload_blocks_A6E000_A72000.txt    风荷载核心段分块反编译（341 块）
│  ├─ windload_full_A62C07_A86FEE.txt      整段巨型函数（3895 块 / 47,463 行）
│  └─ windload_full_A62C07_A86FEE.index.tsv 块索引（地址/类型/锚点）
├─ ida_map.py                   索引与定位：build / grep / at
├─ ida_blocks.py                巨型过程分块反编译
├─ ida_decomp_block.py          单块隔离反编译（更早期版本，保留兼容）
├─ trace_block.py               按范围抽常量 / NEG / VARIANT 算子
├─ compare_gust_block.py        V35 vs V31 代码块归一化比对
├─ probe_v31_slots.py           V31 运行期 VARIANT 槽读取
├─ probe_slots.py               通用版（可指定进程 + 槽基址）
├─ scan_slot_writes.py          字节级扫描「谁在写某个槽」
├─ v31_attach.ps1               V31 无人值守驱动（含 mus-sweep / terrain-sweep）
├─ v35_hidden_drive.ps1         V35 无界面驱动（受注册限制）
└─ unpack/
   ├─ unpacked_runtime.exe      脱壳镜像（静态分析唯一输入）
   ├─ vb6_thunk_names.json      thunk → msvbvm60 导出名
   └─ msvbvm60_exports.json     msvbvm60 导出表
```

---

## 9. 与「四面对拍」的衔接

工具链服务于既有铁律（见 `docs/COMPARE_PROTOCOL_4WAY.md`）：任何一个量必须四列并排 **V31 实机 ｜ V35 实现 ｜ 复刻 ｜ 规范**。

- 出「V35 实现」这一列 → 工具 1+2（Hex-Rays 定位 → capstone 核常量）
- 出「V31 实机」这一列 → 工具 3+4（驱动预演 + 槽取证）
- 出「复刻」这一列 → dotnet 构建 + `--selftest` + `--dump-preview`
- 「规范」这一列 → 条文/表格，且**必须能被程序自带文案或常量表印证**，否则只作旁证
