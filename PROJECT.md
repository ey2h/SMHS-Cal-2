# 项目概览 - GlassManagement

## 项目目标
玻璃管理系统 API，支持多种玻璃类型的管理、登录验证、脉冲验证及多语言切换。

## 技术栈
- 后端：.NET 10 (ASP.NET Core)
- 项目路径：`src/GlassManagement.Api`
- 运行方式：`cd src/GlassManagement.Api && dotnet run`

## 当前版本
**V28** - 已发布群内，待测试

## 测试重点（V28）
1. ✅/❌ 登录验证流程
2. ✅/❌ 9种玻璃类型读写
3. ✅/❌ 脉冲验证功能
4. ✅/❌ 多语言切换

## 分工
| 角色 | Bot | 职责 |
|------|-----|------|
| 开发 & 指挥 | @Ey2hwubot | 开发功能、打包版本、上传 Google Drive |
| 测试 | @Ey1hwubot | 下载版本、执行测试、反馈结果 |

## 文件交换
- Google Drive：https://drive.google.com/drive/folders/1Miaesje0UBFnKlq6lFDXiJoajuDdeAyu
- Bot 通信：BotRelay https://six-glasses-smile.loca.lt

## 长记忆配置（supermemory）
```bash
openclaw plugin install @supermemory/openclaw-supermemory
```
openclaw.json 中添加：
```json
{
  "plugins": {
    "slots": {
      "memory": "openclaw-supermemory"
    },
    "entries": {
      "openclaw-supermemory": {
        "enabled": true,
        "config": {
          "containerTag": "唯一标识"
        }
      }
    }
  }
}
```
然后 `openclaw gateway restart`。

## 版本历史
| 版本 | 状态 |
|------|------|
| V28 | 待测试 |
