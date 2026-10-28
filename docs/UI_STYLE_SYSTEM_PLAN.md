# 界面样式系统规划（Unified Style System）

> 目标：把「V31 像素级复刻界面」与「新写工具屏」收敛到**一套令牌 + 一套组件类 + 一个布局骨架**，
> 新增界面不再各自拍色值，改主题时只改一处。
> 原则：**先统一语义，再统一数值**。凡改动必须做到「视觉零变化」，否则逐屏对拍会失效。

---

## 一、现状诊断（2026-10-28 盘点）

| 指标 | 数值 |
|---|---|
| Calc 视图 axaml | 74 个 |
| 含硬编码颜色的视图文件 | **75 个（全部）** |
| 样式类使用量 | `hb` 5619 / `hbFrame` 560 / `hbFrameTitle` 554 / `hbTitleBar` 35 / `hbResultPanel` 15 |
| 已定义的令牌 | 15 个（`HbFormBg` / `HbBorder` / `HbText` / …），**但 0 处被样式引用** |

### 1.1 三个结构性问题

**① 令牌是死的。** `App.axaml` 定义了 `SolidColorBrush` 令牌，但同文件里 400+ 个 `Setter` 全部写死
`Value="#F0F0F0"` 这种字面量，令牌从未被 `{DynamicResource}` 引用过。改主题等于改 400 处。

**② 令牌值与实际用值不一致。**

| 令牌 | 令牌值 | 视图里实际在用的值 | 差距 |
|---|---|---|---|
| `HbBorder` | `#7A7A7A` | `#A0A0A0`（40 处） | 不一致 |
| `HbFrameBorder` | `#9AA5B1` | `#A0A0A0` | 不一致 |
| `HbTextMuted` | `#6B7280` | `#6B6B6B`（32 处） | 不一致 |
| `HbResultBg` | **不存在** | `#C0FFFF`（7 处） | 缺令牌 |
| `HbBorderStrong` | **不存在** | `#808080`（48 处） | 缺令牌 |
| `HbStatusBar` | **不存在** | `#F0F0F0` | 缺令牌 |

**③ 两套色系混用。** 复刻系（`#F0F0F0/#A0A0A0/#1F2937/#E8EEF7/#C0FFFF`）与现代 Tailwind 系
（`#1E293B` 7 处、`#334155` 8 处、`#4B5563` 15 处、`#9AA0A6` 15 处、`#64748B` 3 处、`#2563EB` 5 处）同屏出现，
后者是新写屏（外遮阳/雷击/工具屏等）随手取的色，与复刻屏观感割裂。

---

## 二、目标架构（四层）

```
L3  页面组合    每个 View 只写业务布局，外壳一律用 HbShell
L2  布局骨架    HbShell（标题条 + 内容区 + 状态栏）  ── 已有 hbTitleBar/hbStatusBar 的封装
L1  组件类      .hb .hbFrame .hbFrameTitle .hbResultPanel .hbBtn .hbInput .hbPanel .hbStatus …
L0  设计令牌    Application.Resources 里的 Hb* 语义令牌（唯一色值来源）
```

**分层规则（硬约束）**

| 层 | 允许做的事 | 禁止 |
|---|---|---|
| L3 | `Classes="hb"`、`Classes="hbFrame"`、引用 `{DynamicResource HbXxx}` | 写任何 `"#RRGGBB"` |
| L2 | 组合 L1 组件、定义固定几何（高 19 / 内边距） | 定义颜色 |
| L1 | 通过 `{DynamicResource}` 取 L0 令牌 | 写十六进制 |
| L0 | 定义色值（唯一允许出现十六进制的地方） | 定义布局 |

---

## 三、L0 令牌表（补齐后 22 个）

**语义命名**，值是当前实际在用的色（保证迁移后视觉零变化）：

| 令牌 | 值 | 用途 | 现状 |
|---|---|---|---|
| `HbFormBg` | `#F0F0F0` | 窗体底 | ✅ 已有 |
| `HbControlBg` | `#FFFFFF` | 输入框/列表底 | ✅ 已有 |
| `HbTitleBarBg` | `#E8EEF7` | 标题条底 | ✅（原名 `HbTitleBar`） |
| `HbTitleText` | `#24405E` | 标题文字 | ✅ 已有 |
| `HbBorder` | `#A0A0A0` | 通用边框 / 分隔线 | ⚠ 值需改（原 `#7A7A7A`） |
| `HbBorderStrong` | `#808080` | 结果框边框 | 🆕 新增 |
| `HbBorderSoft` | `#CCCCCC` | 弱分隔（表格线） | 🆕 新增 |
| `HbText` | `#1F2937` | 正文 | ✅ 已有 |
| `HbTextMuted` | `#6B6B6B` | 次要文字 | ⚠ 值需改（原 `#6B7280`） |
| `HbTextDisabled` | `#9A9A9A` | 禁用 | ✅（原名 `HbDisabled`） |
| `HbResultBg` | `#C0FFFF` | 结果输出框底 | 🆕 新增 |
| `HbStatusBarBg` | `#F0F0F0` | 状态栏底 | 🆕 新增 |
| `HbButtonBg` | `#E3E3E3` | 普通按钮底 | ✅ 已有 |
| `HbHover` | `#DEE7F5` | 悬停 | ✅ 已有 |
| `HbPressed` | `#C8D6EA` | 按下 | ✅ 已有 |
| `HbAccent` | `#2F6FED` | 强调 / 选中 | ✅ 已有 |
| `HbWarnBg` | `#FEF3C7` | 提示条底 | 🆕 新增 |
| `HbDanger` | `#EF4444` | 不满足 / 错误 | 🆕 新增 |
| `HbSuccess` | `#10B981` | 满足 | 🆕 新增 |
| `HbMetricL` `HbMetricM` `HbMetricS` | 0 | 间距刻度（0/4/8/12/16） | 🆕 用 `Thickness` 资源 |

**废弃**：`HbPanelBg`（`#FBFBFD`，仅 1 处）、`HbFrameBorder`（与 `HbBorder` 重复）、
现代主页那套 `PageBgBrush / AccentBrush / TextPrimaryBrush …` 保留给 `MainWindow` 主页，**不得进 Calc 视图**。

---

## 四、L1 组件类清单（收敛后 12 个）

现有 12 个类名够用，只做两件事：**全部改为引用令牌** + **补 2 个缺口**。

| 类 | 承载 | 迁移动作 |
|---|---|---|
| `.hb` | 正文文本（宋体 12） | Setter 取 `HbText` |
| `.hbFrame` | 分组框（灰底 + 1px 边） | 取 `HbFormBg` + `HbBorder` |
| `.hbFrameTitle` | 分组标题（骑线） | 取 `HbText` |
| `.hbResultPanel` | 结果框（青底 + 深边） | 取 `HbResultBg` + `HbBorderStrong` |
| `.hbPanel` | 面板底 | 取 `HbFormBg` |
| `.hbTitleBar` / `.hbTitleText` | 标题条（高 19） | 取 `HbTitleBarBg` / `HbTitleText` |
| `.hbStatusBar` / `.hbStatus` / `.hbStatusHint` | 状态栏 | 取 `HbStatusBarBg` / `HbText` / `HbTextMuted` |
| `.hbBtn` / `Button.hb` | 按钮（59×24） | 取 `HbButtonBg` + `HbBorder` |
| `.hbInput` | 输入框变体 | 取 `HbControlBg` + `HbBorder` |
| 🆕 `.hbOutput` | 等宽输出文本（Consolas 12） | 新建，替代各屏手写的 FontFamily |
| 🆕 `.hbWarn` | 提示条（黄底） | 新建，取代 3 处手写 `#FEF3C7` |

---

## 五、L2 布局骨架：HbShell

新写屏目前各写一遍「标题条 + 底部按钮 + 输出框」，几何一致性靠抄。抽成两个可复用外壳：

```xml
<!-- 独立工具屏：标题条 + 内容 + 底部按钮/输出 -->
<ctl:HbToolShell Title="{Binding Title}" Output="{Binding Output}" CalcCommand="{Binding CalcCommand}">
  <StackPanel>…业务字段…</StackPanel>
</ctl:HbToolShell>

<!-- 普通计算屏：标题条 + 内容 + 状态栏 -->
<ctl:HbShell Title="幕墙计算-立柱计算" Status="就绪">
  <Grid>…业务布局…</Grid>
</ctl:HbShell>
```

`HbToolShell` 已由本轮 15 个独立工具屏验证过需求（`SimpleToolView` 就是它的第一个实现，
后续把 `SimpleToolView` 内部改成调用 `HbToolShell`，两者合一）。

---

## 六、迁移策略（四批，每批可独立验证）

| 批次 | 范围 | 风险 | 验证 |
|---|---|---|---|
| **B1** | L0 令牌补齐 + 值对齐 + `App.axaml` 内 400 处 Setter 改引用令牌 | 低（值不变） | 编译 + 抽 3 屏截图比对 |
| **B2** | 新写屏（外遮阳/雷击/插值/方程/牛腿/型材优化/工具屏 15 个，共 21 屏）去掉 Tailwind 色，改用令牌 | 低（本就没对拍色值） | 桌面测试 + 截图 |
| **B3** | 复刻屏（54 个）逐个把 `"#F0F0F0"` 换 `{DynamicResource HbFormBg}` | 中（逐屏回归） | 每屏改完跑对应 DesktopTests |
| **B4** | 抽出 `HbShell` / `HbToolShell`，各屏改用 | 中 | 全量测试 + 人工抽看 |

> B3 是机械替换，可用脚本批量做（`sed` 式定点替换 + 人工复核 diff），
> 但**必须一屏一提交**，避免一次性 75 文件改动无法定位回归。

---

## 七、治理：禁止新增硬编码颜色

加一条静态门禁测试（`StyleGovernanceTests`），扫 `Views/**/*.axaml`：

- 允许出现十六进制的文件：仅 `App.axaml`（L0 令牌定义处）
- 其余视图出现 `="#RRGGBB"` 即失败，并列出文件:行
- 存量按「批次白名单」递减：B2 完成后移除 21 屏，B3 完成后白名单清空

这条门禁是让「一套样式」不回潮的唯一手段——否则下次加屏又会各自拍色值。

---

## 八、当前落地状态（2026-10-28）

| 项 | 状态 |
|---|---|
| L0 令牌层 `Themes/Tokens.axaml` | ✅ **已建**：31 个 key（22 色 + 4 间距 + 现代兼容），值全部等于改造前实际用值 |
| L0 令牌挂载 | ✅ `App.axaml` 的 `Application.Resources` 用 `ResourceInclude` 合并 |
| App.axaml 样式令牌化 | ✅ **77 处 `{DynamicResource Hb*}`，颜色字面量 0 处**（原 80 处） |
| 删除重复定义 | ✅ 移除 `App.axaml` 内旧的 14 个复刻调色板（改由 Tokens.axaml 单点维护） |
| L2 工具屏外壳 | ✅ `SimpleToolView`（15 个独立工具屏在用），已全量令牌化、零硬编码 |
| 治理门禁 | ✅ `StyleGovernanceTests`（4 条）：禁新增硬编码 / 令牌完整性 / App.axaml 不回退 / 豁免清单只减不增 |
| 存量豁免 | `docs/ui_style_exempt.txt` = **74 个文件**（逐批递减，清零即治理完成） |
| B2 新写屏去 Tailwind 色 | 15 个工具屏 ✅；外遮阳/雷击/插值/方程/牛腿/型材优化 6 屏待做 |
| B3 复刻屏批量替换 | 未开始（74 个文件，机械替换 + 一屏一提交） |

**验证基线**：`Calc 550 全绿 / DesktopTests 134 全绿`（含 15 工具屏 + 4 条样式门禁）。

**说明**：老的那套 Avalonia（`GlassManagement.Ui.Shared` / `Desktop.Blazor`）是给网页端的，
其现代调色板（`PageBgBrush` / `AccentBrush` 等）保留在 `App.axaml` 内供主页使用，**不进 Calc 视图**，
本轮不触碰。Calc 视图一律走 V31 复刻语言（宋体 12 / 直角 / #F0F0F0 灰底 / #C0FFFF 结果框 / 行高 20）。
