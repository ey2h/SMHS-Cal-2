Phase 3 Extend Acceptance Criteria
- 目标：Phase 3 延展具备稳定的 UI 验证点与 API 骨架，能够在本地/CI 验证通过后推进正式合并。
- 验收要点
 1. 前端 /phase3 页面加载正常，区域选择与 Panel 编辑工作流可用
 2. 提交 Phase3 请求后，后台 /api/calculation/phase3 能返回符合 GlassOutput 结构的 CloudViz 数据
 3. Phase 3 的 UI 与 Phase 2 的 Apple Glass 风格保持一致，风格令牌生效
 4. Phase 2 的后端与 Phase 3 的前端在合并后能正确协同工作，云图数据在跨端呈现一致
 5. CI（GitHub Actions）能够在 PR 上跑通 API 测试用例
- 交付物
  - 依赖的 API 端点：/api/calculation/phase3、Phase3Calculation.razor、Phase3Tests.cs
  - 合并日志更新与分支说明
