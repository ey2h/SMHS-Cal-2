# UI 自动化测试

## 环境准备
1. 安装 .NET 10 SDK
2. 安装 Playwright 浏览器：
   ```bash
   cd src/GlassManagement.UITests
   dotnet build
   pwsh bin/Debug/net10.0/playwright.ps1 install chromium
   ```

## 运行测试
```bash
cd src/GlassManagement.UITests
dotnet test --logger "console;verbosity=detailed"
```

## 查看截图
测试截图保存在 TestResults/screenshots/ 目录

## 单独运行某个测试
```bash
dotnet test --filter "LoginTests"
dotnet test --filter "Step1Tests"
dotnet test --filter "NavigationTests"
dotnet test --filter "AdminTests"
dotnet test --filter "ThemeTests"
dotnet test --filter "SectionToolTests"
```

## 测试清单

| 测试类 | 场景 | 测试数 |
|--------|------|--------|
| LoginTests | 登录功能 | 6 |
| NavigationTests | 步骤导航 | 4 |
| Step1Tests | 基本参数 | 8 |
| Step2Tests | 风荷载计算 | 3 |
| Step3Tests | 立柱计算 | 5 |
| Step4Tests | 横梁计算 | 2 |
| Step5to7Tests | 连接/埋件/胶 | 3 |
| Step8Tests | 汇总 | 2 |
| ThemeTests | 主题切换 | 4 |
| AdminTests | 管理页面 | 7 |
| SectionToolTests | 截面工具 | 4 |
| **合计** | | **48** |
