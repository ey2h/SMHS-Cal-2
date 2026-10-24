# 汇宝 V35 载荷解密与程序框架提取（成果冻结）

> 目标：从 `MQJSSV35.EXE` 软件本体取出真实计算逻辑，用于 C# 复刻。
> 结论：**3.5MB 加密载荷已在运行内存中完整解密并提取**，VB6 程序框架与计算过程名全部恢复。

---

## 1. 保护架构（已完全查清）

```
MQJSSV35.EXE (13.37MB, SHA-256 A0976F73…3004B)
├─ ASPack 壳
├─ SRT / Silicon Realms 商业保护壳（Security.dll，从 .pdata 内存加载，非磁盘模块）
│   ├─ 许可校验（Reg.dll + 硬件绑定：SATA硬盘 / 主板蓝牙 / 限期联网）
│   └─ 载荷解密（LCG 密钥流 + 16 轮 Feistel + RLE，密钥运行时计算）
└─ .pdata 段 = PDATA000 容器
    ├─ 记录0: zlib → Security.dll (290,816 B, export SetFunctionAddresses)
    ├─ 记录1: zlib → SRT 授权提示字符串（24,680 B）
    └─ 记录2: 3.5MB 加密载荷 ← 真正的 VB6 程序
```

### 磁盘上 .text 全零，运行时才填充
| | 磁盘文件 | 运行内存 |
|---|---|---|
| `.text` (31.5MB) | 87% 全零，0 个函数序言 | **87% 非零，2,319 个 `55 8B EC` 序言，1,522 处中文** |

原始 PE 的 `.text` 是 `rs=0, rp=0`（无原始数据，纯虚拟段），
SRT 壳在许可流程中把载荷解密并写入 `.text`——这就是"必须从软件里找"的真相所在。

### 已逆向出的密码原语（静态，供参考）
- 密钥调度 `sub_225180B`：18 个硬编码 DWORD + LCG `x' = (31411981·x + 1) mod 1e8`（乘数即立即数 `0x1DF5E0D`）+ 密钥材料 XOR + Feistel 混合 → 4096B 密钥流 + 72B 密钥
- Feistel `sub_2251DAE`（16 轮）/ 轮函数 `sub_2251E63`
- 解压 `sub_225232E`：RLE，`0xFF` 逃逸（`FF xx` = 重复上一字节 xx+3 次，`FF FF` = 字面 0xFF）
- 容器块格式：`[DWORD size ^ key][data]`，size 最高位 = 原始块
- 授权用种子 `826366241`；试用期掩码 `0xAC9F76A0`

> 说明：**未做**任何许可绕过/注册机。载荷是在用户正常启动软件、SRT 壳自行解密后，从进程内存中取出的。

---

## 2. 产物清单

| 文件 | 内容 |
|---|---|
| `local-data/recon/hb_t20s_24600.dmp` | **273MB 全内存快照**（含解密后 .text，原始证据） |
| `local-data/recon/unpack/text_runtime.bin` | 解密后的 `.text` 原始字节（31,666,176 B） |
| `local-data/recon/unpack/unpacked_runtime.exe` | **重建镜像**：把解密 .text 回填到 unpacked.exe（IDA 可直接分析） |
| `local-data/recon/unpack/unpacked_runtime.exe.i64` | IDA 数据库（已保存，1,271 个函数已识别） |
| `local-data/recon/vb6_names.json` | 204 个 VB6 模块/窗体/过程名清单 |
| `local-data/recon/unpack/vb6_*.py` | VB6 元数据解析脚本（头/对象表/过程表/名单） |

### 明文特征自检（证明是汇宝本体，不是自研 Glass）
```
汇宝 66 / 幕墙门窗 8 / 参数库 9 / 计算书 65 / 抗弯 62 / 挠度 38 / 荷载 185
GlassManagement 0 / 羿中 0
样例：汇宝幕墙门窗结构计算软件…实用、实惠、准确、完善的幕墙…
      MQJSSV35bk.汇宝幕墙门窗结构计算软件..\HELP.CHM
```

---

## 3. VB6 程序框架（已恢复）

### 3.1 VBHeader（VA 0x41A750，魔数 `VB5!`）
```
wRuntimeBuild   = 0x2636
szLangDll       = vb6chs.dll          (VB6 中文运行时)
dwLCID          = 0x804
lpSubMain       = 0xA52054            (Sub Main 入口，代码区)
lpProjectData   = 0x41B368
wFormCount      = 0x2B  = 43 个窗体
wExternalCount  = 0x0F  = 15 个外部控件
dwThunkCount    = 0xE9  = 233 个 API thunk
lpGuiTable      = 0x41D444
lpExternalTable = 0x427348
```
运行时加载的模块印证（273MB dump 的 ModuleList，共 114 个）：
`msvbvm60.dll @0x731F0000`、`VB6CHS.DLL @0x66630000`、`Reg.dll @0x10000000`、
`msjet40.dll`/`msjetoledb40.dll`（Access 引擎）、`MSCOMCTL/MSFLXGRD/TABCTL32/MSDATGRD/DBGRIS32/CnCalendar/MSINET` 等。

### 3.2 模块与窗体
- **标准模块**：Module1–Module9、Module2net、Module3net、Module8net、Class1/Class1_1/Class1_2、GUIF、VBRUN、tools
- **窗体**：Form1、Form2、frmAbout、frmBrowser、frmSplash、frmSplashnet、TabDlg、
  JSFRM2、JSFRM3、JSFRM5B、JSFRM6、jsfrm1、jsfrm4、jsfrm5、jsfrm5a、jsfrm5c、
  jsfrm7、jsfrm7A、jsfrm7b、jsfrm8、jsfrm9、jsfrma

### 3.3 计算过程（复刻的核心目标）
| 过程名 | 推断含义 |
|---|---|
| `jisuan`, `jisuan_0/1/2` | 计算（主入口，按工况分 3 支） |
| `jisuan1_0/1/2`, `jisuan2_0/1/2` | 计算分支 1 / 分支 2 |
| **`QIUNAODU`, `QIUNAODU6`** | **求挠度**（含 6.x 版本变体） |
| **`CubicEquation`** | **三次方程求解**（梁挠度/连续梁方程） |
| `jsjiemian`, `jisuanjiemiantexing` | 计算截面 / 截面特性 |
| `jsguifan` | 计算规范选择（JGJ102 / 地标 / 人工） |
| `jsdd`, `jslg`, `jsrg`, `jsrg1`, `jsother`, `jsother1`, `jstaocai`, `jsyupeng` | 顶点/立柱/入口/其它/套裁/雨篷 |
| `JSJIEGUO`, `JSDS1`, `JSDS2`, `JSQBL`, `JISUANJIEMACAC` | 计算结果 / 计算书导出 |
| `jisuanshu` | 计算书生成 |
| `QIU1/2/3/5/7`, `QIUm1/m2/M/M6`, `QIUFAI/1/2` | 求值系列（弯矩/系数等） |
| `qiu_zq_4_4a`, `qiu_zq_4_4B`, `qiuzhejian_fhb`, `qiu_jljssc4_29` | 规范表 4.4 / 折减 / 剪力系数 |
| `Xcdm`, `Xcdm1/2`, `Xcdm_hc_`, `Xcdm_HC_1/2`, `XCDM_AUTO_0/1/2` | 型材断面（含合金 hc）自动计算 |
| `boli_Click`（玻璃）、`ganjian_Click`（杆件）、`cgdbl`, `cgdgj`, `cgdwlxn` | 面板/杆件计算入口 |
| `Enc`, `jiajiemi`, `KeyString`, `setregkey`, `CheckRight`, `CRCFileCheck` | 加解密与授权（**不在复刻范围**） |
| `GetMainBoardId`, `DiskID32`, `ReadPhysicalDriveInNT`, `GetVolumeInformationA` | 硬件指纹（**不在复刻范围**） |

### 3.4 数据/常量位置
- 中文串与公式文字集中在 **VA 0x4D0000–0x570000**（如 `抗弯` @0x52E6B0、`挠度` @0x538D89、`荷载` @0x4D616C、`应力` @0x53EFA1、`计算书` @0x534969）
- VB6 名称/过程表集中在 **VA 0x4D0000–0x600000**（过程名指针表起点示例 0x4D43CC）
- **VB6 代码区**：`Sub Main @0xA52054` 起，函数序言密集于 **0xA3xxxx–0xA6xxxx**
- `JGJ102` 出现 56 次、`Q235` 52 次、`Q355` 34 次（材料与规范常量表）

---

## 4. 下一步（按优先级）

1. **把过程名绑到代码地址**：解析 VB6 对象表 → 方法表（vtable），建立 `过程名 → VA` 全映射；
   已有线索：ProjectData 0x41B368 → 对象表 0x4D1AEC；名称表 0x4D43CC 起。
2. **反编译计算函数**：`jisuan*`、`QIUNAODU*`、`CubicEquation`、`jsjiemian*`，提取：
   - 分支条件（抗震/非抗震、JGJ102/地标/人工、单跨/双跨/悬臂）
   - 系数表索引逻辑（m/μ/α、γE/γw/ψE/ψw）
   - 每步中间量与舍入/格式化（决定小数点后位数）
3. **核对已复刻的玻璃模块**：用 `QIUNAODU` 的真实实现校验 η、D、μ 的取值与顺序。
4. 逐模块产出：立柱 → 横梁 → 结构胶 → 螺栓 → 预埋件 → 转接件。

---

## 5. 复现步骤（可重跑）

```bash
# 1. 启动汇宝（正常双击），让它走到注册/未注册提示（此时载荷已解密）
# 2. 抓全内存
python local-data/recon/unpack/watch_dump.py     # 或 dump_now.py
# 3. 提取解密 .text 并重建镜像
python local-data/recon/unpack/extract_decrypted_text.py
# 4. IDA 打开 unpacked_runtime.exe 分析
# 5. 解析 VB6 框架
python local-data/recon/unpack/vb6_parse.py
python local-data/recon/unpack/vb6_nameinv.py
```
