# Glass 开发交付包（2026-03-08）

## 包含内容
- `db/SMHS_Region.db`
- `db/SMHS_Bolt.db`
- `db/SMHS_Profile.db`
- `db/SMHS_Material.db`
- `db/SMHS_Glass.db`
- `db/SMHS_User.db`

## 本次代码更新摘要
- 恢复并替换参数库数据库文件（SMHS_*.db）。
- 静力计算结果 `BeamCalcResult` 新增 4 个 SVG 字段：
  - `MechanicalDiagramSvg`
  - `MomentDiagramSvg`
  - `ShearDiagramSvg`
  - `DeflectionDiagramSvg`
- 新增梁图生成器 `BeamDiagramGenerator`，覆盖主要梁型与荷载图输出。
- `StaticCalcService` 计算完成后自动附加 4 张 SVG。
- `DiagramController` 新增通用接口：`POST /api/Diagram/beam/all`。
- 新增 Word V2 计算书服务与接口：
  - `POST /api/CalcBookPdf/glass-curtainwall/word-v2`

## 构建状态
- `dotnet build src/GlassManagement.Api/GlassManagement.Api.csproj -v minimal`
- 结果：构建成功（0 error，存在 System.Drawing 平台告警）。
