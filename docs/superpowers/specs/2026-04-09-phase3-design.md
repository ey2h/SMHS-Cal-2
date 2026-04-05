# Phase 3 Extend Design (快速增量改造)

## 1. 目标与成功标准
- 目标：在不进行大规模重构的前提下，快速把 Phase 3 的工作落地为一个可测试、可迭代的实现路径，确保 UI 流程、后端对接契约和 CI 验证可用。
- 成功标准：
  - 用户在 Phase 3 页面完成区域选择、Panel 编辑、粘贴输入 JSON、导出输入 JSON、提交阶段 3，云图数据绑定有效，提交状态可视化。 
  - 常见失败场景具备前端校验并给出清晰提示（区域未选、Panel 为空、PanelId 重复、Panel Type 非法）。
  - 提交按钮在前端根据可提交条件禁用，避免误操作。
  - 与后端 /api/calculation/phase3 的对接可用，返回 GlassOutput，CloudVizPane 能展示数据。
  - 本地快速验收与 CI 流水线覆盖核心路径。

## 2. 范围与边界
- 包含：Phase 3 的前端增强、后端接口复用、CloudViz 展示绑定、输入导入/导出、基本测试骨架、CI 初始配置。
- 不包含：大规模领域模型重构、Phase 2 的变更重构、跨端 UI 风格以外的改动。

## 3. 架构概要
- 前端：Phase3Calculation.razor 为主容器，包含 RegionsSelector、Panel 编辑区域、粘贴/复制 JSON、提交按钮，以及 CloudVizPane 云图区域。
- 服务/领域边界：当前实现将核心逻辑保留在前端，若后续需要，可引入 Phase3Service/IFase3Service 以实现更清晰的职责分离。
- 后端：复用现有 /api/calculation/phase3，输入/输出契约保持 GlassInput/GlassPanelInput/GlassOutput，前端仅对接并渲染 CloudVizData。

## 4. 主要数据模型（前后端契约）
- GlassInput: ProjectId, RegionId, Panels: GlassPanelInput[]
- GlassPanelInput: PanelId, Type (GlassPanelType), Width, Height, Thickness, Layers: GlassLayerInput[]
- GlassLayerInput: MaterialId, Thickness
- GlassOutput: PanelsResults: PanelResult[], CloudViz: CloudVizData
- PanelResult: PanelId, Type, Metrics: { Thickness, Deflection, MaxStress }, Status
- CloudVizData: Points: CloudVizPoint[]
- CloudVizPoint: X, Y, Value

## 5. 组件划分（快速增量）
- Phase3Calculation.razor：主容器，处理区域选择、Panel 编辑、粘贴/复制、提交、云图渲染。
- Panel 编辑：直接在 Phase3Calculation.razor 内实现快速列表编辑，后续如需要可抽出为子组件。
- RegionsSelector：复用现有控件。
- CloudVizPane：复用现有实现渲染云图。
- Phase3Input/Output 逻辑：前端组装 GlassInput 并通过 HTTP POST 提交到 /api/calculation/phase3。

## 6. 数据流与错误处理
- 数据流：用户输入 -> BuildPhase3InputJson -> POST /api/calculation/phase3 -> 解析 GlassOutput -> CloudVizData -> CloudVizPane 渲染
- 错误处理：前端提供 Phase3Status 提示，网络/序列化/接口错误统一提示；后端若返回错误，将错误信息转化为前端友好信息。

## 7. 验证与测试策略
- 单元测试：Phase3InputIsValid、ArePanelIdsUnique、Phase3IsSubmittable 的边界测试
- 集成测试：对 /api/calculation/phase3 的端到端行为进行 API 层测试（在 CI/可测试环境中实现）
- UI 测试：核心路径覆盖（区域选择、Panel 编辑、粘贴、复制、提交、云图渲染）
- CI：添加对 Phase 3 路径的构建与 API 测试，确保 PR 打开即有回归保障

## 8. 风险与缓解
- 风险：快速改造可能遗漏某些极端输入场景，影响边界行为。
- 缓解：加强前端校验、提供清晰错误提示、逐步增加单元测试与集成测试覆盖。

## 9. 里程碑与验收计划
- 里程碑1：Phase3Calculation.razor 增强完成，Paste/Copy、提交链路可用
- 里程碑2：Phase3 与后端 /api/calculation/phase3 的对接稳定
- 里程碑3：CI 流水线覆盖核心路径

## 10. 风险与减缓措施
- 如后续需要进一步拆分，可在不影响当前大目标的前提下逐步引入 Phase3Service、Unit Tests、UI 自动化测试等。

## 11. 附件
- 现有契约文档、Phase 2/Phase 3 的 UI 风格参考、合并日志等。 
