# 计算页样式统一 — 实施记录（2026-10-25）

**设计依据**：`docs/ui/BEAM_UI_SPEC.md`（334 行）+ `docs/ui/BeamCalcView.draft.axaml`。
**关于 Figma**：本机无 Figma 授权/凭据，`figma` 技能无法出稿（子线程已在 spec 第 9 节注明）。
因此以 spec 的 token 表与 draft 作为「设计稿」落地，视觉结论全部可回溯到 spec 条款。

---

## 一、改了哪些界面

| 界面 | 文件 | 改动 |
|---|---|---|
| 基本参数输入 | `Views/Calc/BasicParamsView.axaml` | 标题条改用统一类 `hbTitleBar` + `hbTitleText`（视觉不变，去内联重复） |
| 立柱计算 | `Views/Calc/ColumnCalcView.axaml` | 同上 |
| 多跨数据输入 | `Views/Calc/MultiSpanInputView.axaml` | **补上缺失的标题条**（原先完全没有，是四屏里唯一的例外）；外包 `DockPanel`，去掉写死的 `Width=919 Height=408` |
| 横梁计算 | `Views/Calc/BeamCalcView.axaml` | 标题条改统一类；状态栏改 `hbStatusBar`/`hbStatus`/`hbStatusHint`；按钮归一到 `hbBtn`（59×24）；辅助灰 `#6B7280` → `#6B6B6B` |
| 全局样式 | `App.axaml` | 新增 `hbTitleBar` / `hbTitleText` / `hbStatusBar` / `hbStatus` / `hbStatusHint` / `hbBtn`；补 `Button.hb` / `TextBox.hb` 两个历史空类 |

## 二、主要调整点

### 1. 标题条（四屏统一）
```
高 19 / 底 #E8EEF7 / 下边线 1px #A0A0A0 / 标题 11px #1F2937 / 左内边距 8
```
改动前：三屏各写一遍内联属性（值相同但分散），多跨屏**没有**标题条。
改动后：四屏统一引用 `Border.hbTitleBar` + `TextBlock.hbTitleText`。

### 2. 状态栏（统一实现，仅横梁屏有内容绑定）
```
底 #F0F0F0 / 上边线 1px #A0A0A0 / 内边距 8,2 / 正文 12px #1F2937 / 右侧来源提示 11px #6B6B6B
```
只给横梁屏加了实际状态栏——其它三屏的 ViewModel 没有独立 `StatusText` 属性，
强行加会变成死控件（假交互），按 spec「不造假交互」原则不做。

### 3. 按钮尺寸归一
V31 原值统一 **59×24**。横梁屏此前是 62×24（新写的），已改为 `hbBtn`（59×24），
与第一页/立柱/多跨的按钮完全同宽同高。

### 4. 灰色归一
横梁屏新代码里用了现代灰 `#6B7280`（Tailwind slate），与其它三屏的 VB6 灰 `#6B6B6B` 不一致。
统一为 `#6B6B6B`（spec 第 3 节 token）。

### 5. 圆角与阴影
计算页**全部 `CornerRadius=0`、无任何 `BoxShadow`**（已逐条核对 `App.axaml`）：
- 全局 `Button` / `TextBox` / `ComboBox` / `RadioButton` / `CheckBox` / `DataGrid` / `hbFrame` / `hbResultPanel` 均显式 0
- 仅有的圆角 8 与阴影，属于「现代主页专用类」（`Button.Primary` / `Border.Card` / `Border.Section` / `Border.ResultArea`），
  `App.axaml` 里已用注释隔离，计算页不使用 —— 保持主页与复刻页两套视觉互不污染

### 6. 空样式类补实
V31 导出的控件普遍带 `Classes="hb"`，但 `Button` / `TextBox` 上**没有对应样式**（历史空类，一直靠全局兜底）。
已显式定义 `Button.hb`（59×24、方角）、`TextBox.hb`（高 18、方角、内边距 2,0），
保证「带 hb 类」与「不带」在四屏内视觉完全一致。

### 7. 字体字号
保持 `SimSun`：正文/控件 **12**，标题条/单位提示/状态栏次要信息 **11**，行高 15（文本）/ 18（表格行）。
未做改动——四屏本来就一致，spec 也以此为准。

## 三、有意**不动**的部分（及原因）

| 项 | 原因 |
|---|---|
| `BasicParamsView` / `ColumnCalcView` 内部 Canvas 绝对坐标 | 这两屏的控件坐标是 V31 实机取证值（`docs/V31_BEAM_UI.md` 第二节），改布局会破坏与 V31 的像素对照关系 |
| `MultiSpanInputView` 内部 Canvas（919×408、示意图 `#00FFFF`） | 同上，示意图形状/坐标来自 V31 实测截图 |
| 主页 `Button.Primary` / `Border.Card` 的圆角 8 与阴影 | 属应用外壳（工具入口），非幕墙计算复刻页；两套视觉已注释隔离 |
| 三屏未加状态栏 | ViewModel 无对应属性，加了就是假交互 |

## 四、验收

| 项 | 结果 |
|---|---|
| 编译 | **0 错 0 警** |
| 横梁屏端到端（`--selftest=beam --dump-preview`） | 预选 Wx 904.139 / Wy 2583.574 / Ix 25624.721 与 V31 逐位一致（样式改动未影响计算） |
| Calc 单测 | 83/83 |
| 视觉截图 | **未取到**：本机窗口被 IDE 遮挡，`PrintWindow` 拿到的 `MainWindowHandle` 是辅助窗口（rect 0×0）。样式正确性由「类引用一致性 + 编译 + 数值回归」保证，视觉终检请在实机 `--calc` 下人工过目 |

## 五、待办

1. 为第一页/立柱/多跨补 `StatusText`（各自 VM），再把状态栏铺到四屏——需要先确认每屏要显示什么，不宜拍脑袋定文案。
2`Button.hb` 的 Width=59 是强制值：若将来有需要更宽的按钮（如「快速查找型材」105 宽），
   必须用局部内联 Width 覆盖（Avalonia 本地值优先），已在 beam 页验证可行。
