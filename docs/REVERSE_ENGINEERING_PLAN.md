# 汇宝 V35 逆向 → SMHS 项目完善：任务计划与进度

> 最后更新：2026-10-21 23:5x（本机）
> 目标项目：`D:\Code\Glass`（GitHub `ey2h/SMHS-Cal-2`，solution `GlassManagement.sln`）
> 目标基准：汇宝幕墙门窗结构计算软件 V35.0（`C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\`）
> 本文档 + `REVERSE_ENGINEERING_EVIDENCE.md` 构成换机接手所需的全部上下文。

---

## 0. 一句话现状

汇宝的**数据层已全部拿到并清洗完毕**（59 张参数表 / 3,598 行、634 城市地区参数、截面特性库、地区热工表、计算书 Word 模板），**代码层（控制流）仍是黑的**（`.text` 在磁盘上 0 字节、9.16 MB 压缩 overlay）。已选定路线 **A（明文层 + 黑箱扰动）**，主线四阶段中 ② 完成约 60%、③④ 待开工、① 只做了现状清点。

---

## 1. 边界（写死在计划里，避免换机后跑偏）

| 编号 | 约束 | 说明 |
|---|---|---|
| B1 | **不做注册绕过/脱壳/注册机** | 不构造、不提供、不协助规避 `Reg.dll` + 主板/硬盘绑定 + 时限检测 |
| B2 | 路线 A 只用**明文层 + 黑箱对照** | 明文层 = 参数库/地区表/计算书样本/帮助文档；黑箱 = 在正版环境跑工况、比对输出 |
| B3 | 路线 B（动态脱壳/内存转储 + Ghidra）**需用户逐次明确授权** | 属于"避开技术措施"，法律暴露在持证人一侧；本机也缺分析引擎 |
| B4 | 汇宝派生的原始数据（`hb_library.sqlite`、`.mdb` 导出 CSV）**不得推送到公开仓库** | 属厂商受版权保护的数据；见 §6 未决问题 Q1 |

---

## 2. 阶段计划

### ② 参数库对表与补库 —— **进行中（约 60%）**

已完成：
- `参数库.mdb`（`Standard Jet DB`，1,085,440 B）**59 张表 / 3,598 行全部导出** → CSV + `_index.json` + 单文件 `hb_library.sqlite`（680 KB）。
- 与本项目 6 个 SQLite 库 / 11 张表做列级对照，产出缺口报告（`param_library_gap.md`）。
- 已确认 6 组 1:1 对应：`地区参数↔Regions(634)`、`立柱↔ProfileColumns(95)`、`横框↔ProfileBeams(67)`、`螺栓1–7↔Bolts(324)`、`后锚固材料参数↔AnchorMaterials(48)`、`幕墙自重参数↔CurtainWallWeights(17)`。

待办（按优先级）：
- [ ] P0 补列：`Wx1/Wx2`、`Wy1/Wy2` 双抵抗矩、`面积矩 S`、`受剪腹板总宽度`、锚固 `hef/钻孔直径/性能等级`。
- [ ] P0 新建 `GlassTypes` 数据（现为 **空表 0 行**，玻璃强度目前靠硬编码）。
- [ ] P1 补 11 类板材（搪瓷板/钢板/不锈钢/钛合金/铜/铝塑/蜂窝铝板/铝镁硅…约 60 行，含弹性模量/泊松比/密度/线膨胀系数）。
- [ ] P1 补 20+ 张构件型材表（单元幕墙横框一/二/三＋立柱一/二、采光顶主/副龙骨、吊顶主/副龙骨、雨篷龙骨、钢管点玻龙骨、转角立柱、门窗立梃/横梃、栏杆横龙骨/竖龙骨含钢芯、钢铝结合 4 表、百叶叶片、U 型玻璃）。
- [ ] P2 热工：`热工系数(144)`、`热工数值(15)` + 62 个 `*.inx` 地区热工分区表。
- [ ] P2 查表类：`多跨1(236)`、`多跨2(328)`、`静力手册(7)`、`线材优化原始/合并后(302)`。

验收：逐表行数与源 CSV 一致；抽样值一致；新增数据可经 API 查询。

### ③ 计算书模板拆解 → 版式对齐 —— **未开工**

已完成：`jss\mmbk.CAM`（24,064 B）识别为 **Word 复合文档模板**（`Word.Document.8`，含"设计计算书/设计：/校对：/审核：/批准：/公司名称/日期/目录"、样式"标题 8/9"、`PowerPoint Water Mark Object`）。

待办：
- [ ] 用 Word COM 打开（先复制为 `.doc`），导出：样式表、标题层级、表格骨架、签章栏、页眉页脚、水印。
- [ ] 与 `CalcBookWordService` 现产出（`calcbook_test_output_s1..s11.docx`）逐项 diff。
- [ ] 补 受力简图 / 弯矩图 / 剪力图 / 挠度图 四类图（当前多为占位图，见 `docs/next_steps.md`）。

验收：生成 docx 的样式名与标题层级与模板一致；Word 中 F9 更新域后目录稳定。

### ① 计算内核下沉 + 全量 REST 面 —— **未开工（仅现状清点）**

现状事实：
- 计算实现散落在 `src\GlassManagement.Api\Controllers\` **46 个 Controller**（`GlassCalculationController.cs` 44,752 B、`DoorWindowCalculationController.cs` 20,997 B、`AluminumCalculationController.cs` 17,542 B…）。
- `GlassManagement.Core` **只有 6 个文件**（`Models\CalculationModels.cs` 61,755 B、`Data\GlassDbContext.cs`、`Models\*` 等），**没有计算代码**。

待办：
- [ ] 清点全部端点（分类 CALC/DATA/AUTH/REPORT/DIAGRAM），产出 `api_surface.md`。
- [ ] 在 `Core` 新建 `Calculation/` 服务层，按计算类别拆服务（荷载/立柱/横梁/面板/连接/埋件/胶/汇总）。
- [ ] DTO 归位 `Core.Contracts`，消除重复 DTO 与魔法字符串枚举（`PanelType` 字符串）。
- [ ] API 层改为薄适配；新增统一 `/api/calc/**` 面（含 OpenAPI 描述），**保留旧路由兼容**。
- [ ] 全量计算 API 化：任一计算都能以 JSON 入参→JSON 出参独立调用，不依赖界面流程状态。

验收：`dotnet build` 0 error；`tests/CalcBookTestHarness` 与 `GlassManagement.UITests` 通过；Swagger 列出全部计算端点；玻璃 52 组合回归 52/52。

### ④ 控制流黑箱扰动清单 —— **未开工（需正版运行环境）**

目的：用受控工况把"软件内部的分支与取舍"测出来（不读代码也能定逻辑）。

- [ ] 实验矩阵设计（每条给出输入变量、期望判断的输出量、对照次数 ≥2）：
  - 立柱：跨度/层高/支承条件（简支↔双跨↔连续↔带悬挑）跳变点 → 模型选择判据；`Wx1` 与 `Wx2` 分别在什么位置被取用。
  - 横梁：挂式 ↔ 非挂式 的判定条件；三角荷载 vs 集中力的选用。
  - 面板：玻璃类型切换（单片/夹胶/中空/双中空/三玻两腔）对厚度等效与挠度限值的影响。
  - 连接：螺栓/焊接/半焊半栓 的选择；角片参数取值。
  - 埋件：预埋/后锚/对穿/槽型 的适用条件；`hef` 与孔径的取值规则。
  - 荷载链：体型系数分区、`βgz/μz/K1/K2` 的截断高度与梯度高度实际取法。
- [ ] 每组实验记录：输入 JSON → 输出（应力、挠度、判定、以及计算书里打印的每个中间系数）。
- [ ] 结论沉淀为 `Core/Calculation/*` 里的显式规则表 + 单测。

验收：每条分支有 ≥2 组能判定的对照数据；规则能写成代码并复现汇宝输出。

---

## 3. 进度总表

| 阶段 | 内容 | 状态 | 产出 |
|---|---|---|---|
| 侦查 | 二进制结构 / 数据文件 / 计算书样本 / 帮助文档语料 | ✅ 完成 | `REVERSE_ENGINEERING_EVIDENCE.md` |
| ② | 参数库导出与对表 | 🟡 60% | `hb_library.sqlite`、`param_library_gap.md`、`mdb_vs_smhs.md` |
| ③ | 计算书模板拆解 | ⬜ 未开工 | — |
| ① | 内核下沉 + 全量 API | ⬜ 未开工 | `api_surface.md`（子代理任务，可能未完成） |
| ④ | 黑箱扰动实验 | ⬜ 未开工（缺许可环境） | — |

---

## 4. 换机接手清单

### 4.1 环境依赖

| 依赖 | 用途 | 本机实测 |
|---|---|---|
| Windows x64 | 全部 | ✅ |
| .NET 10 SDK | 构建/运行项目 | ✅ 10.0.402 |
| Node ≥ 22.5 | 采集脚本（`node:sqlite` 需 ≥22.5，带 ExperimentalWarning） | ✅ v22.23.2 |
| Microsoft Access Database Engine（`Microsoft.ACE.OLEDB.12.0`）或 `Jet.OLEDB.4.0` | 读 `.mdb` | ✅ 12.0/16.0 + Jet4 均在注册表 |
| Microsoft Word | 读 `.DOC` 计算书样本、`mmbk.CAM` 模板（PowerShell COM） | ✅ 可用 |
| LibreOffice（可选） | 批量 `.DOC→docx`；本机无 `soffice` | ❌ |
| Ghidra + JDK（可选，仅路线 B） | 静态分析 | ❌ 本机无（`GHIDRA_INSTALL_DIR` 未设） |

### 4.2 路径约定

- 项目仓库：`D:\Code\Glass`（`src\` 下为正式工程；根目录还有旧布局与构建产物）
- 采集工作区（**建议整目录打包带走**）：`D:\Code\333`
  - 脚本：`inside_recon.js` `inside2.js` `inside3.js` `inside4.js` `hb_mdb_dump.ps1` `build_hb_sqlite.js` `mdb_vs_smhs.js` `mine_schema.js` `inside*`、`docs_convert.js`、`asar_grep.js`、`asar_extract.js`
  - 产物：`hb_mdb\*.csv`、`recon-out\*`、`huibao-docs\`、`huibao-docs-std\`、`hb_calcbook.txt`、`hb_embed\`
- 汇宝安装目录（数据源）：`C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\`（新机器若无此软件，②的数据部分需从本机带走 `hb_mdb\` 或 `hb_library.sqlite`）

### 4.3 复现命令

```powershell
# 1) 汇宝参数库 → CSV + index
pwsh -NoProfile -File D:\Code\333\hb_mdb_dump.ps1
#   → D:\Code\333\hb_mdb\*.csv, _index.json  (59 表 / 3598 行)

# 2) CSV → 单文件 SQLite（同时生成列级缺口报告）
node D:\Code\333\build_hb_sqlite.js
#   → D:\Code\333\recon-out\hb_library.sqlite, param_library_gap.md

# 3) 本项目 SQLite schema 清点
node D:\Code\333\mine_schema.js

# 4) 二进制内部结构 / 熵图 / 内嵌 PE / zlib 块
node D:\Code\333\inside_recon.js "C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\MQJSSV35.EXE"
node D:\Code\333\inside2.js      "<同上 EXE>" "D:\Code\333\hb_embed" "<...>\jss\jss.dat" "<...>\jss\A006.CAM"
# 5) .CAM / .inx / 参数库按正确编码解码
node D:\Code\333\inside3.js "<...>\jss\参数库.mdb" "<...>\pic\热工\规范\50176\hb.inx"

# 6) 计算书样本 → 纯文本（Word COM，需 FullLanguage）
$w=New-Object -ComObject Word.Application; $w.Visible=$false
$d=$w.Documents.Open("D:\Code\Glass\计算书样本\全隐框玻璃幕墙计算书.DOC",$false,$true)
[IO.File]::WriteAllText("D:\Code\333\hb_calcbook.txt",$d.Content.Text,[Text.Encoding]::UTF8)
$d.Close($false); $w.Quit()
```

### 4.4 编码/格式坑（已踩过，别再踩）

- 汇宝 `.inx`、`.CAM`（数值型）是 **ANSI/GBK**；`.mdb` 内部文本是 **UTF-16LE**；混用时会出现"怪字"，先判编码再解析。
- `A006.CAM` 这类是 `","` 分隔的数值导出表（2,113 个分隔符 / 81 条记录），不是二进制。
- `mmbk.CAM` 扩展名是 `.CAM` 但内容是 **OLE 复合文档（Word 模板）**，需按 Word 处理。
- `jss.dat`（615,334 B，熵 6.295）**不是干净数据文件**：解出来是 Delphi RTL 字符串 + `DoSetMemFailed, error code=0x%` 等，与计算无关，别在它上面浪费工时。
- Word COM 脚本在受限 PowerShell 语言模式下会失败；本项目环境为 `danger-full-access` 时可正常用。
- 内联 `node -e "...\""` 在本机反复因转义失败 —— **一律写 .js 文件再跑**。

---

## 5. 风险与注意

1. **公开仓库 + 厂商派生数据**：`hb_library.sqlite`、`hb_mdb\*.csv`、`.inx` 内容属汇宝数据，**不要 commit 到公开仓库**（见 Q1）。
2. **计算书样本**（`计算书样本\*.DOC`）同样来自厂商随附样本，入库前确认仓库可见性。
3. 路线 A 无法保证 100% 复刻控制流；差异点必须在 ④ 中用对照实验定位，不得靠"看起来合理"的公式硬猜。
4. 本项目既有 76+ 端点与 11 张表：① 的重构必须**先加不删**，避免打断现有 UI 与测试。

---

## 6. 未决问题（换机后需决策）

| 编号 | 问题 | 影响 |
|---|---|---|
| Q1 | `SMHS-Cal-2` 仓库是 public 还是 private？ | 决定汇宝派生数据（参数表/样本计算书）能否入库 |
| Q2 | 新机器是否有正版汇宝运行环境？ | 决定 ④ 黑箱实验能否开工 |
| Q3 | 新增参数表放哪：扩展现有 6 库，还是新建 `SMHS_Section.db` / `SMHS_Thermal.db`？ | 决定 ② 落库方式 |
| Q4 | ① 重构是否允许保留旧路由（先加不删）？ | 决定改造节奏与测试策略 |
| Q5 | 能否导出 2–3 份**新**的正版计算书（一根立柱 / 一块玻璃 / 一个连接节点）？ | ④ 的对照样本是否充足 |
