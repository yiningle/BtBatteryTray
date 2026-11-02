# BtBatteryTray

托盘常驻的蓝牙设备电量监控（WinUI 3 / Windows App SDK 2.6，.NET 10，unpackaged）。

只覆盖「设置 → 蓝牙和其他设备」里能显示电量的设备：读取 PnP 属性 `{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2`，并只保留 `DEVPKEY_Device_IsPresent` 为 true 的设备。

## 开发构建

用 Rider / Visual Studio 打开根目录的 `BtBatteryTray.sln`，或命令行：

```pwsh
dotnet build BtBatteryTray.sln
.\src\BtBatteryTray\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\BtBatteryTray.exe
```

开发构建依赖本机的 .NET 10 Desktop Runtime；Windows App SDK 运行时随 exe 一起输出。ARM64 用 `-p:Platform=ARM64`。

## 打包发布

用 [Velopack](https://velopack.io) 打包，`vpk` 作为本地 dotnet 工具记录在 `dotnet-tools.json` 里，不需要全局安装：

```pwsh
.\build\release.ps1                                   # 版本取 csproj 的 <Version>，x64
.\build\release.ps1 -Version 0.2.0 -Runtime win-x64, win-arm64
.\build\release.ps1 -Force                            # 重新打同一个版本
```

产物在 `artifacts\release\<rid>\`：

| 文件 | 用途 |
| --- | --- |
| `BtBatteryTray-<rid>-Setup.exe` | 安装包：装到 `%LOCALAPPDATA%\BtBatteryTray`，无需管理员，建开始菜单快捷方式，可在「设置 → 应用」卸载 |
| `BtBatteryTray-<rid>-Portable.zip` | 便携版：解压后运行根目录的 `蓝牙电量监控.exe`，不写安装信息 |
| `*.nupkg`、`releases.*.json`、`RELEASES-*` | 以后开启自动更新时作为更新源上传，现在可以忽略 |

.NET 与 Windows App SDK 运行时都已打包进去，目标机器不需要预装任何东西（代价是约 100 MB）。

发版流程：改 csproj 的 `<Version>`（SemVer，必须比上次大）→ 运行 `release.ps1` → 分发 Setup.exe / Portable.zip。

### CI/CD（GitHub Actions）

`.github/workflows/ci.yml`：

- push 到 `main` 或提 PR：构建并打包 x64、ARM64，安装包作为 workflow artifact 保存（版本号为 `<Version>-ci.<运行序号>`）。
- 推送 `vX.Y.Z` 标签：同样打包，再自动创建 GitHub Release 并附上 Setup.exe 与 Portable.zip。标签必须与 csproj 的 `<Version>` 一致，否则构建失败。

```pwsh
# 改好 csproj 的 <Version> 并提交后
git tag v0.2.0
git push origin v0.2.0
```

卸载时，`Program.cs` 里的 Velopack 卸载钩子会先让正在运行的托盘实例退出，再删除开机自启注册表项和通知注册；用户设置保留。

安装包和程序目前没有代码签名，下载运行时 Windows SmartScreen 会提示「未知发布者」。有证书后给 `vpk pack` 加 `--signParams`（或 `--azureTrustedSignFile`）即可。

## 功能

- 托盘图标只是入口：平时是跟随任务栏浅色/深色模式的单色圆环，不显示任何电量；有设备低于阈值（且未关闭提醒）时变成红色圆环。
- 鼠标悬停在图标上约 0.3 秒弹出电量卡片：每个设备一行，含设备类型图标（键盘/鼠标/耳机等）、名称、电量条和百分比，低电量项标红；移开后自动收起，点击图标时不弹出。
- 左键单击打开设置；右键菜单：立即刷新、设置、开机自启、退出。
- 低电量通知：低于阈值提醒一次，回升到阈值 +5% 以上后才会再次提醒；可按设备关闭。
- 重复启动时不会开第二个实例，而是打开已运行实例的设置窗口。

## 数据位置

`%APPDATA%\BtBatteryTray\`：`settings.json`（阈值、间隔、静音设备）和 `log.txt`（每次轮询结果与错误）。开发版、安装版、便携版共用这份数据；不放在 `%LOCALAPPDATA%\BtBatteryTray`，因为那是安装目录，重装时会被清空。
开机自启写在 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\BtBatteryTray`，以注册表为准。

## 代码结构

```
BtBatteryTray.sln
.github/workflows/ci.yml       CI 打包 + 标签发版
dotnet-tools.json              vpk（Velopack CLI）版本
build/release.ps1              发布脚本
src/BtBatteryTray/
├─ Program.cs, App.xaml        入口（单实例、激活重定向）与组装
├─ Assets/                     应用图标（exe、安装包、设置窗口共用）
├─ Models/                     BtBattery、DeviceKind
├─ Services/                   电量读取、轮询、低电量通知、设置、开机自启、日志
├─ Tray/                       托盘图标（H.NotifyIcon）、悬停检测、圆环图标绘制
├─ ViewModels/                 CommunityToolkit.Mvvm 视图模型
└─ Views/                      设置窗口、悬停电量卡片
```

| 路径 | 作用 |
| --- | --- |
| `Services/BatteryReader.cs` | 枚举 PnP 设备读取电量、设备类型，名称回退到 `DEVPKEY_Device_FriendlyName` |
| `Services/BatteryMonitor.cs` | `DispatcherQueueTimer` 轮询（在 UI 线程上触发） |
| `Services/LowBatteryAlerter.cs` | 带回差的低电量通知 |
| `Services/SettingsService.cs` | 可观察的设置，修改即写入 JSON |
| `Tray/TrayIconController.cs` | 托盘图标、右键菜单、悬停卡片的显示与收起 |
| `Tray/RingIconRenderer.cs` | 按任务栏主题绘制圆环图标 |
| `Views/TrayHoverCard.xaml` | 悬停时的电量卡片（不抢焦点的无边框窗口） |
| `Views/SettingsWindow.xaml` | 设置窗口 |
