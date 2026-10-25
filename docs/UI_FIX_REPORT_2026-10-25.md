# UI 完善与本轮修复报告（2026-10-25）

对象：`src/GlassManagement.Desktop`（Avalonia 11.2.3，SMHS 复刻版）
验证：`dotnet build` 0 错 0 警 + 实机启动截图（UIA 驱动，1920x1230 物理像素 / 150% 缩放）

---

## 一、样式统一（下拉框 / 单选框）

### 1.1 问题定位

`App.axaml` 里 TextBox / Button / RadioButton / CheckBox 都有**自定义 VB6 紧凑模板**，
**只有 ComboBox 没有换模板**，仅改了 `MinHeight=0` 等属性。而 Fluent 的 ComboBox 模板自带：

- 右侧 **32px** 宽的箭头列 → 本项目下拉框宽度多为 `57 / 60 / 65 / 73 / 87 / 97px`，文本区被压到 25–40px；
- 下拉列表项 `ComboBoxItem` 默认高 **32px**，与 VB6 的 ~16–18px 差一倍，展开后列表比输入框高出一截；
- 缺 `:focus` / `:disabled` 状态样式（TextBox 有，ComboBox 没有），键盘 Tab 进去没有反馈。

### 1.2 修改内容（`App.axaml`）

| 项 | 改前 | 改后 |
|---|---|---|
| 模板 | Fluent 默认（32px 箭头列） | 自定义 `ColumnDefinitions="*,12"`，箭头为 6×4 实心三角 |
| 列表项高度 | 32px | `Padding="4,1"` + `MinHeight=0` → 实测约 18px |
| 列表项选中态 | Fluent 浅蓝 | VB6 蓝底白字 `#316AC5` + 白字 |
| 列表项悬停 | Fluent 灰 | `#DEE7F5`，文字保持深色 |
| 弹出层 | Fluent 圆角阴影 | 直角 + `#7A7A7A` 1px 边，`MinWidth` 绑定父 ComboBox 宽度 |
| 状态 | 仅 pointerover | 补 `:focus`（蓝边）、`:disabled`（灰底灰字灰三角） |
| 最大高度 | 无限制 | `MaxDropDownHeight=240` |

单选框 / 复选框本轮未重写模板（已是 VB6 紧凑模板），只修了尺寸契约：
`RadioButton` / `CheckBox` 的显式高度 **17 → 15**（447 处），与「行距 16」对齐后不再压盖。
（VB6 实测：Option/Check 行高 15、行距步进 16；17 高 + 16 行距 → 相邻两行重叠 1px，
热工页「单片玻璃 / 中空玻璃」文字糊在一起就是这个原因，已由截图复核消失。）

---

## 二、数据库表结构核对

### 2.1 先纠正一个前提

复刻版**没有独立数据库**。数据链路是：

```
参数库.mdb  ──(local-data/export_hb_mdb.ps1)──▶  local-data/hb_mdb/*.csv (59 张)
                                                      │
                    ┌─────────────────────────────────┴──────────────────────┐
        ParamLibrary.cs（生成期内嵌快照，4 张表）          ParamLibrary.Loader.cs（运行时读 CSV）
```

所以「核对表结构」= **V31 mdb ↔ CSV ↔ 内嵌快照**三方对齐。
另外发现：程序连接的是 `.\jss\参数库.mdb`（安装目录，1,077,248 B），
与我一直在用的仓库根副本（1,167,360 B）**是两份不同文件，但都是 59 张用户表、同名同结构**。

### 2.2 比对结果：**0 项不一致**

用 `local-data/dump_mdb_schema.ps1`（ADO `GetSchema(Tables/Columns/Indexes)`）导出 mdb 的
列名/类型/长度/可空/主键/行数 → `local-data/hb_mdb_schema.json`（132 KB，59 表），
再用 `local-data/recon/compare_db_schema.py` 与 CSV 逐项比：

| 比对面 | 结果 |
|---|---|
| 表集合 | 59 = 59，无缺失、无多余 |
| 列名与顺序 | 59 张表全部一致 |
| 列数 | 全部一致 |
| 行数 | 全部一致 |
| 列类型 | mdb 全部为「文本」，CSV 值推断无「文本列装纯数字」的情况（0 列） |
| 主键 | 已导出（`hb_mdb_schema.json` 的 `primaryKey`），CSV 不承载该元数据 |

内嵌快照 `ParamLibrary.cs` 只含 4 张表（地区参数 634 行、立柱 95 行、单元立柱一 7 行、单元立柱二 7 行），
均从同一批 CSV 生成，行数与 CSV 一致；其余 55 张表由 `ParamLibrary.Loader.cs` 运行时读 CSV。

### 2.3 仍需注意的两点（不是结构差异，是行为差异）

1. **CSV 不承载约束**：mdb 的主键/索引/默认值在导出时丢失。若后续要让复刻版强约束，
   需要额外导出并校验；目前复刻版不写库，只读，因此不影响计算。
2. **中文文件名使 ACE 打不开**：`参数库.mdb` 直接连会报「网络访问已中断」，
   复制成 `C:\temp\pdb.mdb` 即可。这是驱动行为，不是文件损坏（`dump_mdb_schema.ps1` 已按此处理）。

---

## 三、重叠 / 遮挡 / 裁切

### 3.1 先修审计工具（否则修的是假问题）

原 `ui_overlap_audit_v2.py` 用「`<Canvas` 出现次数」当层号（数字 depth），
不同 `TabItem` 里的 Canvas 都落在同一个 depth → 13 个互斥页面被拿去两两比较。
典型假阳性：`CW26` 报 **786 条重叠**。

新增 `ui_audit_v3.py`：改成**层栈**（进入 Canvas/TabItem/ScrollViewer 分配唯一层 id，退出弹出），
只有真正同屏的控件才比较。同一文件 CW26 的重叠从 786 降到 251（其余为真实密集布局）。

### 3.2 根因：生成器没有记录可见性

`local-data/recon/unpack/gen_xaml_v11.py` 从 VB6 实机枚举控件生成 `CW*.axaml`，
但**全程没有 `IsWindowVisible` 过滤**（脚本里 grep 不到该调用）。
于是原版「按状态切换显示的多个多行输出框」被堆到同一坐标，全部可见、互相遮挡。

实例：`CW11_Form11.axaml`（= 幕墙计算-立柱计算）在 `(624,3)/(637,8)/(625,10)/(545,12)`
叠了 4 个 292×352~364×335 的只读 TextBox，code-behind 17 行、不写任何 TextBox
→ 纯视觉残留。

### 3.3 本轮改动

| 类别 | 脚本 | 数量 | 说明 |
|---|---|---|---|
| 叠置只读框去重 | `fix_overlap_v1.py` | **16 个**（9 个文件） | 被更大同类框覆盖 >85% 的删除，保留外层输出框 |
| 选择控件高度 | `fix_ui_v1.py` | **447 处 / 33 文件** | 17 → 15，消除相邻行 1px 压盖 |
| TabControl 溢出 | `fix_ui_v1.py` | **3 处** | CW03 896×408→874×399、CW06 800→796、**CW26 1035×582→894×546**（原来右侧 141px 被 Canvas 裁掉，页签「Frame8 结构用途」看不见） |
| 文字裁切加宽 | `fix_clip_v1.py` | **33 项** | 仅加到「右侧最近控件的左边界 -1」，修裁切不制造新重叠 |

### 3.4 前后对比（`ui_audit_v3.py` 统计）

| 指标 | 改前 | 改后 | 变化 |
|---|---|---|---|
| 文字裁切 | 77 | **62** | −15 |
| 重叠 | 2831 | **2649** | −182 |
| 过密 | 370 | **320** | −50 |

### 3.5 实机验证

- 热工页（CW26，13 个 Tab、ComboBox 最密集）：改前页签第二行被裁、单选框文字互压；
  改后页签正常换行显示，单选框不再挤压（`shots/rg_after.png` / `rg_final.png`）。
- 下拉框：新模板渲染为「白底 + 1px 灰边 + 右侧 6×4 实心三角」，与同页 TextBox 边线对齐。

---

## 四、仍未解决（如实列出）

### 4.1 需人工判断的裁切 41 项

右侧紧邻控件、没有加宽空间。典型：

| 文件 | 位置 | 内容 | 现宽/需宽 | 建议 |
|---|---|---|---|---|
| `ColumnCalcView.axaml` | L178/179 | `Wx1抵抗矩` / `Wx2抵抗矩` | 46 / 51.2 | 表头改 `Wx1` / `Wx2` 或字号 12→11 |
| `CW33_Form33.axaml` | L12 | `软件所监测到本机物理硬件` | 93 / 120 | 该行可放宽（右侧是空白区） |
| `CW33_Form33.axaml` | L16 | 长注册说明 RadioButton | 440 / 525 | 允许换行或加 ToolTip |
| `CW32_Form32.axaml` | L55 | `优化进行中，请稍候……` | 737 / 1147.5 | 该按钮文字本就多行，建议去掉 Width 用 Auto |

### 4.2 重叠 2649 条的构成（需要逐页与原版对照）

审计口径是「同层两个交互控件包围盒相交 >1px」。VB6 原版布局本来就很密（标签紧贴输入框、
Frame 边框压着标题），所以这 2649 条里混着三类：

1. **真错位**：如 `CW26` 的「选择」Button `(43,29) 169×19` 与 ListBox `(48,32) 49×388` 相交 —— 转换平移偏差；
2. **原版如此**：Frame 标题压在子控件上方（VB6 Frame 标题画在边框线上）；
3. **状态互斥**：同一位置的多套控件（应只显示一套，但生成器丢了可见性）。

要区分必须**逐页与 V31 实机截图比对**。本轮只处理了能机械判定的第 3 类，
第 1、2 类没有批量改（怕改错），已建清单待逐页核对。

### 4.3 屏幕尺寸适配

本轮只在 1920×1080 / 150% 缩放下实机验证。`CalcMainWindow` 有
`MinWidth=760 MinHeight=560` + 外层 ScrollViewer，1280×800 下应可滚动查看，
但 **125% / 150% 下的逐页截图复核尚未做完**，需要补测。

---

## 五、工具与产物

| 文件 | 用途 |
|---|---|
| `local-data/recon/ui_audit_v3.py` | 层栈式排版审计（裁切/重叠/过密），`--json` 可导出 |
| `local-data/recon/fix_overlap_v1.py` | 叠置只读框去重（`--dry` / `--apply`） |
| `local-data/recon/fix_ui_v1.py` | 选择控件高度统一 + TabControl 裁进 Canvas |
| `local-data/recon/fix_clip_v1.py` | 文字裁切加宽（带右侧边界保护） |
| `local-data/dump_mdb_schema.ps1` | 导 mdb 表结构到 JSON（含主键） |
| `local-data/recon/compare_db_schema.py` | mdb ↔ CSV ↔ 内嵌快照三方比对 |
| `local-data/recon/ui_goto.py` | UIA 驱动点菜单 + 截图（Avalonia 鼠标模拟无效，必须走 UIA） |
| `local-data/recon/shot_hwnd.py` | 按 HWND 截图（DPI 感知） |
