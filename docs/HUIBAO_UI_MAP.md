# 汇宝 V35 界面 1:1 提取（从程序自身窗体对象）

> 来源：**运行中进程里程序自己创建的窗体控件对象**（不是手册、不是截图）。
> 方法：解密载荷恢复后，程序在主 MDI 窗体下创建了 34 个窗体、4025 个窗口对象；
> 用 EnumWindows/EnumChildWindows 读取每个对象的**类名、标题、屏幕坐标、尺寸、可见性**，
> 再按父句柄还原层级、换算成窗体相对坐标，生成 Avalonia XAML。

## 1. 提取结果

- 顶层窗口 583 个 → 去重后 **34 个窗体**，共 **3396 个控件**（主 MDI 窗体内）
- 控件类型分布：`TextBox 828`、`OptionButton 608`、`ComboBox 510`、`Frame 490`、
  `CommandButton 393`、`Edit 171`、`ScrollBar 93`、`CheckBox 79`、`RichText 60`、
  `DataGrid 55`、`Adodc 55`、`PictureBox 27`、`ListBox 8`、`Timer 3`、`ProgressBar 2`

## 2. 窗体清单（按控件数排序）

| 窗体 | 尺寸 | 控件数 | 主要控件 |
|---|---|---|---|
| 汇宝幕墙门窗结构计算软件V35.0（MDI 主窗体） | 768×579 | 3396 | TextBox 805 / Option 605 / Combo 509 / Frame 487 |
| 门窗系统计算 | 924×564 | 243 | Option 95 / TextBox 32 / Frame 31 |
| 雨蓬计算 | 893×513 | 172 | TextBox 37 / Option 35 / Frame 30 |
| 文件引用规范设置 | 780×500 | 169 | TextBox 46 / Option 32 / Command 26 |
| 建筑护栏计算 | 904×578 | 157 | Combo 36 / Edit 24 / TextBox 24 |
| 点式钢管结构设计计算 | 797×521 | 148 | Option 35 / Combo 26 / Frame 21 |
| **幕墙计算-立柱计算** | 936×442 | 148 | Option 33 / Command 32 / TextBox 21 |
| 非透明幕墙热工计算 | 995×485 | 141 | TextBox 67 / Command 21 |
| 埋件系统计算 | 787×489 | 139 | TextBox 50 / Combo 30 |
| 幕墙计算-埋件与焊缝计算 | 788×490 | 136 | TextBox 36 / Combo 27 |
| 幕墙计算-胶类、伸缩及紧固计算 | 784×537 | 129 | TextBox 31 / Combo 30 |
| 玻璃幕墙热工计算 | 897×553 | 119 | TextBox 35 / Frame 25 |
| **幕墙计算-板块计算** | 782×536 | 115 | TextBox 59 / Frame 24 |
| **幕墙计算-横梁计算** | 949×426 | 111 | Option 23 / Command 20 |
| 杆件计算 | 749×546 | 106 | Option 34 / Frame 12 |
| 幕墙（门窗）物理性能计算 | 885×541 | 106 | TextBox 47 / Combo 16 |
| 转角立柱计算 | 915×566 | 99 | Combo 26 / TextBox 22 |
| 点式肋（无肋）驳接幕墙设计计算 | 909×434 | 97 | Option 39 / Combo 18 |
| **幕墙计算-基本参数输入** | 907×612 | 91 | Option 31 / Frame 16 |
| 全玻璃幕墙设计计算 | 903×438 | 89 | Option 35 / Combo 18 |
| 金属板吊顶结构计算 | 806×509 | 75 | Option 17 / Frame 13 |
| 面板计算 | 876×406 | 67 | Combo 15 / Option 14 |
| 幕墙计算-连接计算 | 796×513 | 59 | TextBox 22 / Frame 9 |
| 金属板幕墙计算-板块计算 | 795×526 | 55 | Option 17 / Frame 11 |
| **幕墙计算-玻璃计算** | 819×361 | 43 | Option 19 / Combo 8 / Command 5 |
| 截面特性计算(支持AutoCAD/中望/浩辰) | 854×499 | 40 | TextBox 15 / Command 9 |
| 线材优化数据输入 | 918×568 | 34 | Command 15 / TextBox 5 |
| 生成计算书（支持Office/WPS） | 603×330 | 33 | Command 13 / CheckBox 5 |
| Form1 | 781×522 | 30 | Combo 13 / TextBox 8 |
| 常用建筑结构静力计算 | 899×494 | 24 | Command 8 / Frame 4 |
| 欢迎进入软件注册 | 684×413 | 19 | TextBox 6 / Command 5 |
| 关于 | 792×437 | 8 | Command 4 / Frame 3 |
| 帮助 | 592×316 | 4 | PictureBox / Combo / Edit |

## 3. 操作流程（从窗体与按钮还原）

```
MDI 主窗体（768×579，标题带"(已注册)"状态位）
 ├─ 幕墙计算-基本参数输入   ← 工程/风荷载/地震/组合系数
 ├─ 幕墙计算-板块计算       ← 玻璃/面板配置（上一步 / 预演 / 下一步 / 退出）
 ├─ 幕墙计算-玻璃计算       ← 玻璃配置：外片/中片/内片 × (普通/浮法/半钢化/钢化/夹丝) + 厚度
 ├─ 幕墙计算-立柱计算
 ├─ 幕墙计算-横梁计算
 ├─ 幕墙计算-连接计算
 ├─ 幕墙计算-胶类、伸缩及紧固计算
 ├─ 埋件系统计算 / 幕墙计算-埋件与焊缝计算
 ├─ 面板计算 / 杆件计算 / 转角立柱计算
 ├─ 点式钢管结构设计计算 / 点式肋（无肋）驳接幕墙设计计算 / 全玻璃幕墙设计计算
 ├─ 金属板幕墙计算-板块计算 / 金属板吊顶结构计算 / 雨蓬计算 / 建筑护栏计算
 ├─ 门窗系统计算 / 幕墙（门窗）物理性能计算
 ├─ 玻璃幕墙热工计算 / 非透明幕墙热工计算
 ├─ 截面特性计算 / 线材优化数据输入
 └─ 生成计算书（Office/WPS）
每个计算窗体统一是「上一步 / 预演 / 下一步 / 退出」四键 + 右侧 RichTextBox 预演区。
```

**统一交互范式**：左侧 Frame 分组输入 → 右侧 `RichTextWndClass`（365×270）实时显示计算过程 →
`预演` 立即判定满足/不满足 → `下一步` 进入下一模块 → `退出` 关闭。

## 4. 产物

| 文件 | 内容 |
|---|---|
| `local-data/recon/hb_window_tree2.tsv` | 原始窗口树（4025 行，含父句柄/类/坐标/尺寸/标题） |
| `local-data/recon/hb_ui_model.json` | 结构化 UI 模型（34 窗体 / 坐标已转相对） |
| `src/GlassManagement.Desktop/Views/HuiBao/HB*.axaml` (34 个) | **生成的 Avalonia XAML**，Canvas 绝对定位 1:1 |
| `src/GlassManagement.Desktop/Views/HuiBao/HB*.axaml.cs` | 对应 code-behind |
| `local-data/recon/unpack/{parse_ui_tree,gen_xaml,enum_windows3}.py/.ps1` | 可重跑的提取与生成链路 |

## 5. 已知缺口（下一步）

1. **VB6 的 Label / Line / Shape 是无窗口控件**，不出现在窗口树里 → 这些静态文字（字段名、单位、分组说明）
   要从窗体设计数据（`.data` 的 form blob）或 `.text` 里的 GBK 静态串补齐；
   已定位静态串区：`0x4D0000–0x570000`（如 `抗弯`@0x52E6B0、`挠度`@0x538D89、`荷载`@0x4D616C）。
2. 控件与**事件过程名**的绑定（`Command1_Click`、`boli_Click`…）需从对象表的 method table 取，
   已找到过程名表起点 `0x4D43CC`（`jisuan*`/`Xcdm*` 系列）。
3. `ComboBox` 的列表项（材料/规范下拉内容）来自运行时数据（参数库），需要在初始化后枚举或从 MDB 取。
