# 玻璃幕墙数据库管理系统 / Glass Curtain Wall Database Management System

## 概述 / Overview
基于JGJ102-2003、JGJ133-2001等中国建筑标准的幕墙工程计算系统。
A curtain wall engineering calculation system based on Chinese construction standards.

## 技术栈 / Tech Stack
- **Backend**: .NET 10 WebAPI + Entity Framework Core + SQLite
- **Frontend (Web)**: Blazor Server + Tailwind CSS
- **Frontend (Desktop)**: Avalonia 11.2.3
- **Standards**: JGJ102-2003, JGJ133-2001, JGJ336-2016, GB50429-2007, GB50009-2012

## 功能 / Features
### 计算模块 / Calculation Modules
- 玻璃计算 (单片/中空/夹胶SGP) / Glass (Single/Insulating/Laminated SGP)
- 铝合金立柱 (双跨梁) / Aluminum Mullion (Double-span beam)
- 铝合金横梁 (三角荷载) / Aluminum Transom (Triangular load)
- 铝单板面板 + 加劲肋 / Aluminum Panel + Stiffener
- GRC板 / UHPC板 / 石材板 / GRC/UHPC/Stone Panel
- 连接件 (螺栓/承压) / Connection (Bolt/Bearing)
- 焊缝 / Weld
- 预埋件 (JGJ102附录C) / Embed Part
- 结构胶 / Structural Sealant
- 热工计算 / Thermal Calculation
- 门窗 / Door & Window
- 单元幕墙 / Unit Curtain Wall
- 点式钢管 / Point-supported Steel

### 项目参数 / Project Parameters
- 634个城市数据库 / 634 Cities Database
- 风荷载完整计算链 (μz→βgz→β→μs1→wk) / Full Wind Load Chain
- 地震作用 / Seismic Action
- 效应组合 / Load Combination

### 可视化 / Visualization
- 力学简图 (SVG) / Structural Diagrams
- 应力/挠度云图 (Navier级数解) / Stress/Deflection Contour Maps

## 快速开始 / Quick Start

### 运行后端+Web界面 / Run Backend + Web UI
```bash
cd GlassManagement.Api
dotnet run
```
访问 / Visit: http://localhost:5000/

### 运行桌面端 / Run Desktop App
```bash
cd GlassManagement.Desktop
dotnet run
```

### Swagger API文档 / API Docs
http://localhost:5000/swagger

## 项目结构 / Project Structure
```
├── GlassManagement.Api/          # WebAPI + Blazor Server
│   ├── Controllers/              # 35个API控制器
│   ├── Components/               # Blazor组件
│   │   ├── Pages/                # 11个页面
│   │   ├── Shared/               # 5个共享组件
│   │   └── Layout/               # 布局组件
│   ├── Data/                     # 数据服务
│   ├── Services/                 # 应用状态
│   └── wwwroot/                  # 静态文件(备用HTML界面)
├── GlassManagement.Core/         # 共享模型层
│   └── Models/                   # 数据模型+计算模型
├── GlassManagement.Desktop/      # Avalonia桌面端
│   ├── Views/                    # XAML视图
│   ├── ViewModels/               # MVVM ViewModel
│   └── Services/                 # API客户端
└── GlassManagement.sln           # 解决方案文件
```

## 计算精度验证 / Calculation Accuracy
| 项目 | 计算书值 | 系统值 | 误差 |
|------|---------|--------|------|
| 立柱弯矩 M1 | 3,653,392.5 N·mm | 3,653,469.2 | 0.002% |
| 立柱应力 σ | 91.865 MPa | 91.866 | 0.001% |
| 横梁应力 σ | 70.248 MPa | 70.239 | 0.01% |
| SGP玻璃应力 | 7.121 MPa | 7.121 | <0.01% |
| SGP玻璃挠度 | 1.125 mm | 1.126 | 0.09% |
| SGP系数Γ | 0.8967 | 0.8969 | 0.02% |

## 规范引用 / Standards Referenced
- JGJ 102-2003 《玻璃幕墙工程技术规范》
- JGJ 133-2001 《金属与石材幕墙工程技术规范》
- JGJ 336-2016 《人造板材幕墙工程技术规范》
- GB 50429-2007 《铝合金结构设计规范》
- GB 50009-2012 《建筑结构荷载规范》
- GB 50017-2017 《钢结构设计标准》
- GB 55001-2021 《工程结构通用规范》
- GB 55002-2021 《建筑与市政工程抗震通用规范》
- JGJ/T 151-2008 《建筑门窗玻璃幕墙热工计算规程》
