# 逆向抓取经验手册（可复用）

> 沉淀自 2026-10-22 ~ 10-24 对汇宝 V31/V35 的实操。目的：**下次换一个量、换一个构件，照这个流程走，不要重新摸索。**
> 纪律前提：公式只认代码；不反推；每个结论必须给地址。

---

## 0. 开工前先确认的三件事

| 项 | 结论（踩过坑的） |
|---|---|
| **用哪个镜像** | `local-data/recon/unpack/unpacked_runtime.exe`（运行时 dump）。静态脱壳的 `unpacked.exe` 里 `0x401E80–0x401F80` **全 0**，常量探针会误报"常量未找到"。 |
| **VA ↔ 文件偏移** | 本镜像 **文件偏移 = VA − 0x400000**（段对齐恰好一致，已验证）。 |
| **有没有符号** | 没有。VB6 本机码剥离了模块变量名（ASCII 标识符 13,630 个里没有一个 `BetaGz`/`MuZ`）。⇒ 只能靠"结构 + 行为"定位。 |

---

## 1. 定位一个量/公式的五步法

> 适用于「我知道程序算了个 X，但它藏在 181KB 的单函数里」。

### Step 1 常量 xref —— 先缩小战场

公式里必有特征常量（`384`、`24`、`8`、`2.4`…）。先 dump 常量值，再扫引用：

```bash
# 读一批常量
python -c "import struct;b=open('local-data/recon/unpack/unpacked_runtime.exe','rb').read();\
print(struct.unpack_from('<d',b,0x4113C8-0x400000)[0])"     # → 384

# 扫真引用（只认操作数，排除字节巧合）
python local-data/recon/v35_xref_exact.py 0x4113C8 0x402720
```

**关键**：`v35_xref_exact.py` 会区分"真正的操作数引用"和"字节巧合"。全镜像裸扫 `find()` 会给出上百个假阳性。

### Step 2 确定函数包络

把所有命中地址排序，取**连续区间**，两端就是函数边界。
本次实测：立柱计算 = `0x1D442DA–0x1D70913`（181,817 字节 / 43,566 条指令），
风荷载 = `0xA62C07–0xA86FEE`（148,455 字节 / 32,947 条指令）。

### Step 3 顺序反汇编 —— **不要从任意地址开始**

```bash
python local-data/recon/v35_varflow.py dis --start 0x1D68120 --end 0x1D68280
```

`dis` 从**函数起点**顺序反汇编再按地址截取，保证指令边界正确。
直接对中间地址 `md.disasm(code, addr)` 会错位——实测 `0x1D44660` 解出 `test eax, ebx; cli`，
真正的边界是 `0x1D4465B` 之后。

### Step 4 枚举「定稿点」—— 这是 VB6 的天然锚点

每个算完的量，VB6 都会 `rtcRound` 到指定位数再落地。模式固定：

```asm
fstp  qword ptr [ebp-0x510]     ; 原始值
mov   dword ptr [ebp-0x518], 5  ; vt = VT_R8
push  3                         ; 位数
lea   eax,[ebp-0x518] ; push eax
lea   eax,[ebp-0x528] ; push eax
call  0x419DDE                  ; rtcRound
call  0x419C58                  ; VARIANT → double
fstp  qword ptr [ebp-0xfc]      ; 取整后
```

```bash
python local-data/recon/v35_round_points.py       # 立柱函数：189 个定稿点
```

**用途**：定稿点 = 输出量的全集。189 个点就是 189 个输出量，一个不多。

### Step 5 栈变量读写索引 + 算式骨架

```bash
# 建索引（约 10 秒，591 个栈变量）
python local-data/recon/v35_varflow.py build --start 0x1D442DA --end 0x1D70913

# 查单个变量全部读写点
python local-data/recon/v35_varflow.py var --off -0x320

# 看一段代码的数据进出
python local-data/recon/v35_varflow.py range --start 0x1D68120 --end 0x1D68280

# 全部定稿点的算式骨架（自动识别 Pow / 乘除 / 加减）
python local-data/recon/v35_formula_points.py
```

输出形如 `Pow(3,a) · A /3 /E /I` —— 这就是 `A·a³/(3EI)`。

---

## 2. VB6 运行时函数指纹（认这 5 个就够）

| 地址 | 作用 | 判据（怎么认出来的） |
|---|---|---|
| `0x419DDE` | `rtcRound(src, dst, digits)` | 前必 `push <位数>` + 源 VARIANT `vt=5` |
| `0x419C58` | VARIANT → double | 紧跟 `rtcRound`，随后 `fstp` 落地 |
| `0x419E38` | VARIANT → double（换值入口） | 属性读取后紧跟 |
| `0x419E6E` | `Pow(base, exp)` | 调用前压两个 double 到栈 |
| `0x419AE4` | 除零保护除法 | 调用前有 `cmp dword ptr [0x220d000], 0` 分支 |

**`[0x220d000]` 是 VB6 的浮点状态标志**。见到 `cmp [0x220d000],0` + `jne`，
就是在"正常除法 / 带异常处理的除法"之间二选一。判公式时两种分支等价，别当成两个公式。

---

## 3. 四个反复踩的坑

### 坑 1：计算书 ≠ 代码
模板写 `5qL⁴/384EI·(1−2.4(A/L)²)`，代码是 `[5−24(A/L)²]·…`（`0x1D681D0 fmul 24.0` → `0x1D681D6 fsubr 5.0`）。
**模板等效 `5−12x²`，代码是 `5−24x²`**。`A=0` 时两式相同，所以单跨对拍一直没暴露。
⇒ 模板只能给分支贴标签，**公式一律以代码为准**。

### 坑 2：复用槽
同一个 `ebp` 偏移在不同分支装**不同量**：
- `ebp-0x510` = 当前量的原始值暂存（189 个点都用它）
- `ebp-0x14c` 在 `0x1D4FB25` 装"长度"，在 `0x1D532C6` 又装另一个"长度"——**两次都不是抵抗矩 W**
⇒ 必须用「哪次写入 + 哪个分支」区分，不能按偏移命名。

### 坑 3：按位置命名的诱惑
把"第一个读进来的数值"叫 `q`（线荷载），结果它是通过 `call [eax+0x0A0]` 从控件属性读的，
和另一个同槽属性 `B` 分工是「A 进挠度 / B 进内力」——语义对了，但名字是猜的。
⇒ **先用 `A`/`B` 占位，等拿到属性名再改**。

### 坑 4：静态分析的边界
`call [eax+0x0A0]` 的目标在 **VB6 的 `ThunderRT6*` 控件实现**（运行时 DLL，不在 EXE 内）。
⇒ 静态只能到「vtable 槽号」。要属性真名必须运行时解析或解 VB6 类型库。

---

## 4. 运行时取证（V31 在跑的时候用）

### 4.1 槽数组 = 模块级变量表

```bash
python local-data/recon/v31_slot_probe_v2.py            # 扫 0..1023 槽
python local-data/recon/v31_slot_probe_v2.py --csv a.csv
python local-data/recon/v31_slot_probe_v2.py --diff a.csv b.csv   # 两状态求差
```

- 基址指针：`ds:[0x1C91168]`（**每次启动变**，必须先读指针再读数组）
- 每槽 **16 字节 VARIANT**：`+0` vt，`+8` 值
- **BSTR 长度在 `ptr-4`**，不是 `+0`；且部分槽长度不可信，按 UTF-16 的 NUL 截断更稳
- `VT_I2`(vt=2) 的值在 `+8` 的低 2 字节，**不能当 double 读**

### 4.2 内存里能捞到公式串

`slot[7]` = `'1+2*2.5*0.14*(100/10)&-0.15&'`（βgz 的代入式），广东档变 `2*3*0.14`。
`&...&` = 上标，`~...~` = 下标。**这是程序自己写的代入记录**，属于程序状态直读。

```bash
python local-data/recon/v31_heap_strings.py --grep "2.146" --min-len 4
```

### 4.3 全量提取公式模板（当字典用，不当证据）

```bash
python local-data/recon/extract_formula_templates.py --exe --pid <pid>
# → docs/FORMULA_TEMPLATE_DICTIONARY.md（466 条）
```

---

## 5. V31 自动化驱动（改参数 → 取结果）

```powershell
$env:V31_ACTION="inner-hold"; $env:V31_INNER="4"     # 停在 0.8μs1 档并预演
powershell -NoProfile -ExecutionPolicy Bypass -File local-data/recon/v31_attach.ps1
```

**必须知道的四条**：
1. 启动 ~12s 弹模态窗，先点「试用软件」才能用菜单；
2. 基本参数/立柱是 **MDI 子窗体**，要 `EnumChildWindows` 递归找；
3. 这套 VB6 控件 `WM_SETTEXT` 无效 → 读用 `WM_GETTEXT`，写用**剪贴板 + EM_SETSEL + WM_PASTE**；
4. **「上一步」会重建窗体，句柄失效** → 每轮必须重找（踩过：两轮结果一模一样）。

**PowerShell 脚本含中文必须 UTF-8 带 BOM**（PS5.1 按 GBK 读会语法全乱）。

---

## 6. 工具现役清单

### 现役（这次实际用的）

| 脚本 | 用途 |
|---|---|
| `v35_xref_exact.py` | 常量真引用扫描（排除字节巧合），产出 `v35_xref.json` |
| `v35_varflow.py` | `build` 建栈变量读写索引 / `var` 查变量 / `range` 看区间 / `dis` 顺序反汇编 |
| `v35_round_points.py` | 枚举 `rtcRound` 定稿点，反查原始值栈槽 |
| `v35_formula_points.py` | 定稿点回溯还原算式骨架 |
| `v35_disasm.py` | 任意窗口反汇编（地址须在指令边界，否则用 `varflow dis`） |
| `ida_blocks.py` | 巨型过程分块反编译（**必须显式传 `--func-start/--func-end`**，否则默认风荷载函数导致 0 块） |
| `v31_slot_probe_v2.py` | 槽数组读取（取代旧的 `probe_v31_slots.py`） |
| `v31_heap_strings.py` | 全进程 UTF-16 字符串扫描 |
| `v31_range_scan.py` | 区间扫 double |
| `v31_attach.ps1` | V31 自动化（40+ 个 action） |
| `extract_formula_templates.py` | 公式模板字典生成 |

### 已知过时 / 有坑

| 脚本 | 问题 |
|---|---|
| `probe_v31_slots.py` | BSTR 长度读 `+0`（应 `ptr-4`）、`VT_I2` 当 double 读、只扫前 256 槽 |
| `unpacked.exe`（静态脱壳） | 常量段全 0，只能用来跑反编译看代码结构，**不能取常量值** |
| 早期 `ida_decomp_*.py` / `trace_block.py` / `analysis.py` | 已被 `v35_varflow.py` 取代 |

---

## 7. 一页速查

```bash
# 我要找一个公式 → 
python local-data/recon/v35_xref_exact.py <常量地址>          # 1. 缩小战场
python local-data/recon/v35_varflow.py build --start A --end B  # 2. 建索引
python local-data/recon/v35_formula_points.py                  # 3. 出骨架
python local-data/recon/v35_varflow.py var --off -0xNNN        # 4. 追单个变量

# 我要确认运行时某个值 →
python local-data/recon/v31_slot_probe_v2.py                   # 槽数组
python local-data/recon/v31_heap_strings.py --grep "<值>"      # 内存字符串

# 我要换一组输入重跑 →
$env:V31_ACTION="gd-hold"; ... v31_attach.ps1                  # 驱动 V31
```
