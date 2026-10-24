# 汇宝 V35 复刻规划：语言选型 · 界面映射 · 操作流程

> 前置事实（已完成的逆向成果见 `docs/HUIBAO_PAYLOAD_RECOVERED.md`）：
> 汇宝 V35 是 **VB6 原生程序**（43 窗体 / 15 外部控件 / 233 API thunk / Sub Main @0xA52054），
> 计算过程名已恢复：`jisuan*`、`QIUNAODU*`（求挠度）、`CubicEquation`（三次方程）、
> `jsjiemian*`、`jsguifan`、`jsdd/jslg/jsrg/jstaocai/jsyupeng`、`JSJIEGUO`、`jisuanshu`、`Xcdm*` 等。

---

## 1. 结论：语言与栈

| 层 | 选型 | 理由 |
|---|---|---|
| **主语言** | **C# / .NET 10** | 引擎（`GlassManagement.HuiBaoCalc`）、模型（`Core`）、测试已就位，不可能换语言 |
| **桌面 UI** | **Avalonia 11（XAML + MVVM）** | 现有 `GlassManagement.Desktop` 就是 Avalonia；窗体/DataGrid/TabControl 与 VB6 表单模型最接近，1:1 迁移成本最低 |
| **Web/远程（可选）** | **Blazor Hybrid / Blazor Server** | 已有 `Desktop.Blazor` + `Ui.Shared`，同一套计算引擎可复用 |
| **后端（可选）** | **ASP.NET Core（已有 Api）** | 工程库、云端同步、多端共用 |
| **计算书** | **OpenXML SDK（DOCX）**，模板沿用 `jss\模板.doc` / `mmbk.CAM` | 汇宝输出 Word；OpenXML 可精确控制小数点、版式、表格 |
| **数据** | **SQLite + EF Core（已有）+ 参数库导入** | 参数库来自 `参数库.mdb`（Jet）；一次性转 SQLite，运行时不再依赖 Jet |
| **打印/预览** | Avalonia 自绘 + DOCX 打印 | 与汇宝"预演窗口"对应 |

### 为什么不是这些
- **VB6 / VB.NET**：无未来、无 64 位、生态死；且我们已有 C# 引擎。
- **WinForms**：最像 VB6（控件一一对应），但高分屏/DPI/现代布局弱，长期维护差；仅在"必须最快 1:1 搬控件"时才考虑。
- **WPF**：与 Avalonia 能力重叠；项目已经选了 Avalonia，不混用。
- **Electron/Web 优先**：幕墙计算是重桌面交互（密表格 + 大量输入 + 本地参数库 + Word 输出），Web 化收益低、成本高。

---

## 2. 目标架构（分层）

```
┌──────────────────────────────────────────────────────────────┐
│ UI 层   GlassManagement.Desktop (Avalonia 11, XAML + MVVM)   │
│   Views/ (窗体)  ViewModels/ (CommunityToolkit.Mvvm)          │
│   —— 1 个汇宝窗体 → 1 个 Avalonia Window/UserControl         │
├──────────────────────────────────────────────────────────────┤
│ 应用层  Application (操作流程状态机 + 输入校验 + 预演编排)    │
│   ProjectFlow / StepGuard / PreviewService                   │
├──────────────────────────────────────────────────────────────┤
│ 计算层  GlassManagement.HuiBaoCalc (纯函数, 零 UI 依赖)      │
│   GlassPanelEngine / ColumnSpanEngine / BeamEngine           │
│   StructuralSealantEngine / BoltEngine / EmbedEngine         │
│   LoadCombinationEngine / GuifanData / SectionProperties     │
├──────────────────────────────────────────────────────────────┤
│ 数据层  Core (EF Core + SQLite) + 参数库/型材库/规范表       │
├──────────────────────────────────────────────────────────────┤
│ 报告层  Report (OpenXML → DOCX) ← 模板: jss\模板.doc         │
└──────────────────────────────────────────────────────────────┘
        ▲ 可选：Api (ASP.NET Core) 为 Web/多端提供同一引擎
```

**硬约束**：计算层只接受强类型 `record` 输入，返回带"中间量"的结果对象
（每个中间量都对得上汇宝计算书里的那一行），这样才有"小数点一致"的可能。

---

## 3. 汇宝 43 窗体 → 我们的模块映射

| 汇宝窗体/过程（已恢复） | 职责 | 复刻落点 |
|---|---|---|
| `Form1` / `Form2` | 主框架、菜单、项目树 | `MainWindow` + `ProjectTreeView` |
| `TabDlg` | 多标签参数页 | `TabControl` 参数页 |
| `frmSplash` / `frmSplashnet` | 启动画面 | `SplashWindow` |
| `frmAbout` / `frmBrowser` | 关于 / 内置浏览器 | `AboutWindow` / `WebView` |
| `jsfrm1/4/5/5a/5c/7/7A/7b/8/9/a` | 各计算项目输入页 | 每项一个 `*InputView` |
| `JSFRM2/3/5B/6` | 计算执行页 | `CalculateView`（统一壳 + 引擎选择） |
| `JISUANJIEMACAC` / `jsjiemian` / `jisuanjiemiantexing` | 截面/界面特性计算 | `SectionTool`（已有项目） |
| `JSJIEGUO` / `JSDS1/2` | 计算结果汇总 | `ResultSummaryView` |
| `jisuanshu` / `JSQBL` | 计算书生成 | `ReportService`（OpenXML） |
| `Xcdm*` / `XCDM_AUTO_*` | 型材断面自动计算 | `SectionAutoEngine` |
| `jsguifan` | 规范选择（JGJ102/地标/人工） | `GuifanProfile` 枚举 + `guifan.dat` |
| `boli_Click` / `ganjian_Click` | 玻璃/杆件入口 | `GlassPanelEngine` / `MemberEngine` |

窗体总量 ≈ 43 → 一期先做 **主框架 + 玻璃 + 立柱 + 横梁 + 结果 + 计算书** 6 个界面，其余按项目滚动补齐。

---

## 4. 操作流程（复刻汇宝的 7 步）

```
① 工程信息    地区/高度/地面粗糙度/基本风压 w0/抗震/重要性系数
      ↓（写 Project 上下文，全局共享）
② 选计算项目  玻璃 | 立柱 | 横梁 | 结构胶 | 螺栓 | 预埋件 | 转接件 | 雨篷 | 套裁 | 截面
      ↓
③ 输入参数    分格尺寸(a/b/B/L1/L2)、玻璃配置、型材截面、材料、连接方式
      ↓
④ 预演        即时校验：满足/不满足 + 差值 + 控制项（对应汇宝"预演"）
      ↓
⑤ 正式计算    LoadCombination → 各模块 → 结果对象（含全部中间量）
      ↓
⑥ 计算书导出  DOCX：填充 `模板.doc` 的标签/表格，保留汇宝的小数位与版式
      ↓
⑦ 工程保存    存 SQLite（工程/方案/参数快照），可重算、可对比
```

**与汇宝一致性要点**
- 荷载组合分系数来自 `guifan`（γG/γw/γE/ψw/ψE），随"规范选择"变化 → 必须是数据驱动，不能硬编码
- 每个模块的"标准值/设计值"两套输入要分开（qk 与 q），否则挠度会错
- 所有中间量按汇宝的舍入点截断（先算后舍，不要中途 round）

---

## 5. 分期与验收

| 期 | 交付 | 验收标准 |
|---|---|---|
| **P0（3-5 天）** | 把解密的 `jisuan*`/`QIUNAODU*`/`CubicEquation` 反编译成伪代码，形成"公式-分支-系数"清单 | 每个模块能画出完整判定树 |
| **P1** | 主框架 + 玻璃模块 1:1 界面 + 引擎对接 | 同输入 → 与汇宝 σ/df 小数点后 2 位一致（玻璃已达成 σ=5.58/df=2.26） |
| **P2** | 立柱（单跨/双跨/悬臂）+ 横梁（三角/矩形/双向弯）+ 结果页 | M/σ/df/τ 全项一致 |
| **P3** | 结构胶 + 螺栓 + 预埋件 + 转接件 | 每项含中间量对齐 |
| **P4** | 计算书 DOCX 模板对齐 + 工程库 | 导出文档与汇宝同版式、同数值 |
| **P5** | 其余窗体（雨篷/套裁/型材截面/规范切换） | 功能覆盖清单 100% |

**回归基线**：用汇宝实机（现已在跑）对同一组输入做"预演"，把结果存成 JSON 快照作为金标准；
每次改引擎跑 `dotnet test`，比对快照。这比拿计算书样本更硬（计算书有模板占位符 bug，如挠度行印 `0.15` 实际按 α=0.49 算）。

---

## 6. 风险与对策

| 风险 | 对策 |
|---|---|
| VB6 原生代码反编译可读性差（大量 `__vba*` 辅助调用） | 以"过程名 + 字符串锚点 + FPU 常量"三路交叉定位；必要时动态断点取中间量 |
| 汇宝分支极多（43 窗体 × 规范 × 工况） | 先按"一个模块一条主线"落地，分支用策略模式逐步补 |
| 参数库（MDB）结构复杂 | 已能导 CSV/SQLite；按表建实体，保留原表名便于核对 |
| 计算书版式细节 | 直接解析 `模板.doc` 的 RTF/OLE 结构，按标签替换而非重排 |
| 精度差异 | 统一用 `double` + 与汇宝相同的舍入点；只在显示层格式化 |

---

## 7. 一句话答案

**用 C# / .NET 10 复刻，界面用 Avalonia（XAML+MVVM，已有工程），计算书用 OpenXML 输出 DOCX，
流程按"工程信息 → 选项目 → 输入 → 预演 → 计算 → 出书 → 存库"七步走，
计算内核以解密出的 `jisuan*`/`QIUNAODU*`/`CubicEquation` 为准逐模块校准。**
