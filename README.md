# GitHub Desktop 中文助手 (GitHubDesktopZh)

为 GitHub Desktop 官方客户端提供简体中文汉化支持。

## 功能特性

- 自动检测 GitHub Desktop 安装版本
- 精确版本匹配的汉化补丁下载
- SHA-256 校验确保资源安全
- 自动备份与恢复机制
- 开机启动与托盘常驻
- 自动检测官方更新并适配

## 系统要求

- Windows 10/11 x64
- GitHub Desktop 官方安装版（Squirrel 安装）

## 安装

1. 下载 `GitHubDesktopZh-Setup.exe`
2. 运行安装程序（无需管理员权限）
3. 按照向导完成安装

## 使用

安装后自动检测 GitHub Desktop 并提供汉化选项。

### 主界面

- 查看当前安装状态
- 一键检查更新
- 重新汉化
- 自动检查/自动汉化/开机启动开关

### 设置

- 自定义 GitHub Desktop 安装路径
- 配置资源仓库 URL
- 调整检查间隔
- 管理备份数量

## 技术架构

- .NET 8 WPF + WPF-UI (Win11 风格)
- 单文件发布 win-x64
- Inno Setup 安装程序

## 项目结构

```
GitHubDesktopZh/
├── src/
│   ├── GitHubDesktopZh.Core/    # 核心逻辑
│   ├── GitHubDesktopZh.App/     # WPF 应用程序
│   └── GitHubDesktopZh.Tests/   # 单元测试
├── resources/                   # 补丁索引
├── setup/                       # 安装脚本
└── scripts/                     # 构建脚本
```

## 开发

```bash
# 构建
./scripts/build.ps1

# 运行测试
dotnet test
```

## 许可证

MIT License

## 贡献者

- **yuzai114514** - 项目所有者，提出需求和测试
- **mimo v2.5** - 代码开发（AI 助手）