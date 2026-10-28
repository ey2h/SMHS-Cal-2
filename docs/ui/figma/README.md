# 横梁屏设计稿（SVG，可直接导入 Figma）

## 为什么是 SVG 而不是 Figma 原生稿

`figma` 技能已加载，但**本机 Figma 没有登录态**：内置浏览器打开 `https://www.figma.com/files`
会被重定向到 `https://www.figma.com/login`。没有账号就无法在 Figma 里新建文件，
也就无法产出 `.fig` 原生稿（他人无法代登录）。

因此改用**矢量 SVG 设计稿**：Figma 原生支持 SVG 导入并**保留为可编辑图层**
（文字仍是文字、矩形仍是矩形），这是无登录态下最接近「Figma 稿」的可交付物。

## 文件

| 文件 | 内容 | 对应实现 |
|---|---|---|
| `Beam_构件式.svg` | 构件式横梁屏（12 个输出量） | `Views/Calc/BeamCalcView.axaml` |
| `Beam_单元式.svg` | 单元式横梁屏（上下框 17 个输出量） | `Views/Calc/UnitBeamCalcView.axaml` |
| `preview_*.png` | 两张稿的渲染预览 | — |

尺寸均为 **936 × 461**（= 标题条 19 + 客户区 442），与规范 `docs/ui/BEAM_UI_SPEC.md` 完全一致。

## 怎么放进 Figma（三种任选）

1. **拖拽**：把 `.svg` 直接拖进 Figma 画布（推荐，最快）
2. **粘贴**：用浏览器打开 SVG → `Ctrl+A` `Ctrl+C` → Figma 画布 `Ctrl+V`
3. **导入插件**：Figma 社区搜 `SVG Import`，批量导入

导入后建议：
- 每个 SVG 会成为一个 Group，右键 `Frame selection`（`Ctrl+Alt+G`）转成 Frame
- 全选 → `Add Auto Layout`（`Shift+A`）让左右两栏自适应
- 把 `#C0FFFF`（结果面板）、`#E8EEF7`（标题条）、`#F0F0F0`（窗体底）登记为 **Color Style**，
  `SimSun 12/11` 登记为 **Text Style** —— 后续新屏直接引用，就是「设计系统」

## 设计 token（与 `App.axaml` 逐项对应）

| 用途 | 值 | App.axaml 类 |
|---|---|---|
| 窗体底 | `#F0F0F0` | `Border.hbFrame` 背景 |
| 标题条底 | `#E8EEF7` | `Border.hbTitleBar` |
| 边框线 | `#A0A0A0` | `hbTitleBar` / `hbFrame` / `hbStatusBar` |
| 结果面板 | `#C0FFFF` + `#808080` 1px | `Border.hbResultPanel` |
| 按钮 | `#E3E3E3` + `#7A7A7A`，59×24 | `Button.hbBtn` |
| 表格网格 | `#C8C8C8`，行高 18 / 表头 20 | DataGrid 局部样式 |
| 正文 / 辅助 | `#1F2937` / `#6B6B6B` | `TextBlock.hbStatus` / `.hbStatusHint` |
| 通过 / 不通过 | `#0000C0` / `#C00000` | `PreviewLine.Color` |
| 字体 | `SimSun` 12 / 11，行高 15 | 全局 |
| 圆角 / 阴影 | 全部 0 / 无 | 全局（仅主页现代类用 8） |

## 重新生成

```bash
python local-data/recon/gen_figma_svg.py docs/ui/figma
```

脚本按 token 常量绘制，改一处颜色即可全局重出稿 —— 这也是把「设计稿」纳入版本管理的好处：
代码改了样式，设计稿能一键跟上，不会两边漂移。

## 若后续拿到 Figma 登录

可以进一步做（当前环境做不了）：
1. 建立 **Component** 库：标题条、Frame、按钮、结果面板各做成主件，四屏引用
2. 用 **Variables**（颜色/间距/字号）替代硬编码，实现"改一处全屏生效"
3. 做 **Prototype** 连线：第一页 → 立柱 → 横梁（构件式/单元式两条支路）
4. 出 **切图**（2x/3x）与开发标注
