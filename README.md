# 设备电量（C# 原生实现）

Windows 上读取无线外设电量的托盘小工具。零第三方依赖：只用 .NET 自带的 WinForms +
P/Invoke 调 `setupapi.dll` / `hid.dll` / `kernel32.dll`，不需要 hidapi、pyusb。

## 设备匹配方式

**按本机 HID 枚举结果动态匹配**，不依赖写死的设备列表：

1. `--list` 那套 SetupAPI 枚举拿到本机所有 HID 接口；
2. 按物理设备分组（同一接收器的多个接口归为一行）；
3. 每组依次套用已知协议：ASUS ROG OMNI → SteelSeries → Razer → 标准 HID 电量字段；
4. 匹配到的每组显示为一行，读不到电量的设备显示具体原因；
5. 一个接收器挂多台设备时（ROG OMNI）按配对列表拆成多行，并用 PID 反查真实产品名。

也就是说：插上什么就显示什么，换设备不用改代码；没有已知协议时仍会尝试标准 HID 电量字段。

### 内置协议

| 设备 | VID:PID | 读取方式 |
|---|---|---|
| ASUS ROG OMNI RECEIVER | `0B05:1ACE` | 三通道：`FF02` 发 `01 A0 00 00` 取配对列表（回复第 5 字节起每 4 字节一个 PID）；`FF00` 发 `02 12 01` 读键盘（字节 6 电量 / 9 充电）；`FF01` 发 `03 12 07` 读鼠标（字节 5 电量 / 10 充电） |
| XInput 无线手柄（飞智 VADER4 等） | `045E:028E` | 走 `xinput1_4.dll` 的 `XInputGetBatteryInformation`；XInput 只给 5 档，映射成 15/40/70/100% |
| SteelSeries Arctis 7 / Arctis Pro | `1038:1260` / `1038:12AD` / `1038:1252` / `1038:1280` | 31 字节报文写 `06 18`，回复第 2 字节即电量百分比 |
| Razer 无线鼠标 | `1532:*`（排除 Dock） | 90 字节 Feature Report，命令 `07/80`，回复字节 9（0–255） |

标准 HID 电量字段覆盖 `0x0006:0x0020`、`0x000D:0x003B`、`0x0085:*` 等 usage。

## 兼容性

下面这些是本机实际插过、跑过 `--once` 验证的设备。

### 已验证可用

| 设备 | VID:PID | 结果 |
|---|---|---|
| ROG Strix Scope II 96 RX（配 ROG OMNI 接收器） | `0B05:1ACE` → 键盘通道 | 100%，走 `FF00` 通道 |
| ROG Keris II Ace（配同一个 ROG OMNI 接收器） | `0B05:1ACE` → 鼠标通道 | 75%，走 `FF01` 通道 |
| 飞智黑武士 VADER4（Xbox 兼容模式） | `045E:028E` | 40%（XInput 档位 3/4），插线时标"有线连接" |

一台 ROG OMNI 接收器同时挂键盘和鼠标时，会按配对列表拆成两行。

### 已接但读不到电量

| 设备 | VID:PID | 现象 |
|---|---|---|
| Razer Viper Ultimate | `1532:007B` | `mi_00` 控制接口 `CreateFile` 返回 `err=5`；其余 12 个接口能打开但 `feat=0`、`SetFeature` 返回 `err=1`，没有可发 90 字节报文的端点 |
| Razer Mouse Dock | `1532:007E` | 同上，并且按设计已从 Razer 协议里排除 |
| SteelSeries Arctis 7 | `1038:12AD` | 接收器有应答但电量字节为 0，音频天线当前是休眠状态；耳机开机后再刷新即可 |

## 已知限制

- **XInput 手柄**：XInput 只返回 5 档电量（空/低/中/满/未知），换算成 15/40/70/100%，
  精度不如私有协议。手柄插着线时飞智仍会上报档位，界面会标成"有线连接"。
- **Razer**：实测没有绕过办法。`mi_00` 连 `dwDesiredAccess=0` 都返回 `err=5`（被系统鼠标栈独占，
  与 Synapse 无关），其余接口虽然能打开，但 `GetPreparsedData` 返回 `err=6`、`feat=0`，
  `HidD_SetFeature` / `DeviceIoControl(IOCTL_HID_*)` / `WriteFile` 全部 `err=1` 或 `err=6`。
  要发 90 字节报文的接口打不开，能打开的接口发不了报文。想读这条线只能用 Razer SDK
  （需要装 Synapse 服务）或自建 HID 驱动，两者都超出本工具的零依赖范围。
- **SteelSeries**：接收器应答但耳机没开机时，电量字节为 0，界面会提示"确认耳机已开机"。
- **ROG OMNI 键盘 / 鼠标**：两者走的是不同的厂商页和命令，混用会读不到数据（键盘用
  `FF00` + `02 12 01`，鼠标用 `FF01` + `03 12 07`）。设备休眠时接收器仍会应答，但电量字节为 0，
  界面会提示"动一下设备后重试"，不会误报成 0%。
- **ROG OMNI 产品名**：接收器自己只报 `ROG OMNI RECEIVER`，真实型号靠配对列表里的 PID 反查
  （表在 [AsusOmniProtocol.cs](Devices/AsusOmniProtocol.cs)）。表里没有的新型号会显示成
  `ROG 设备 0xXXXX`，把 PID 补进表里就能显示名字。

## 用法

```powershell
.\build.ps1              # 打包成 dist\BatteryMonitor.exe（自包含单文件）
.\build.ps1 -Clean       # 先清 bin/obj/dist 再打包
.\build.ps1 -Run         # 打包后直接启动
```

开发时也可以直接 `dotnet build -c Release` 后用 `bin\Release\net6.0-windows\BatteryMonitor.exe`。

`build.ps1` 默认打 `win-x64` 自包含单文件（约 62 MB），目标机器**不需要装 .NET 运行时**。
需要别的架构就传 `-Runtime win-arm64` 或 `-Runtime win-x86`。

| 参数 | 作用 |
|---|---|
| 无参数 | 打开面板 |
| `--tray` | 启动后直接驻留托盘（开机自启用的就是这个） |
| `--once` | 命令行读一次就退出，不弹窗 |
| `--list` | 列出所有 HID 接口（VID/PID、usage page、报告长度），排查设备问题时用 |

面板里可以改刷新间隔、开关开机自启、选择关闭窗口时是否最小化到托盘。
设备行可以**按住上下拖动排序**，顺序存进配置，下次启动保持不变。

托盘只保留**列表里第一台**设备的图标，画成安卓系统那种圆环电量条（环内是数字）；
左键刷新，**双击打开面板**，右键出菜单。鼠标悬停在托盘图标上会列出**前 3 台**设备，每台一行。
把想常看的设备拖到列表最上面，托盘跟着换。

任何设备电量掉到 **20% 以下**会弹一条 Windows 通知（每台设备只提醒一次，充电或重新连上后
会重新武装）。设备列表用深色细滚动条，支持滚轮和拖拽滑块；面板默认开在屏幕右下角。

电量配色：≥60 绿 / 30–59 黄 / <30 红。

## 代码结构

| 文件 | 说明 |
|---|---|
| [Native/Win32.cs](Native/Win32.cs) | 所有 P/Invoke 声明与结构体 |
| [Hid/HidEnumerator.cs](Hid/HidEnumerator.cs) | SetupAPI 枚举 HID 接口 |
| [Hid/HidInterface.cs](Hid/HidInterface.cs) | 接口元数据（VID/PID、usage、报告长度、产品名） |
| [Hid/HidDevice.cs](Hid/HidDevice.cs) | 句柄封装：Feature/Output 读写、异步 Input 读、能力表解析 |
| [Devices/DeviceLevel.cs](Devices/DeviceLevel.cs) | 托盘用的轻量读数（不含原始报文） |
| [Devices/DeviceReader.cs](Devices/DeviceReader.cs) | 枚举 → 分组 → 套用协议 → 生成界面用的 `Reading` |
| [Devices/BatteryReader.cs](Devices/BatteryReader.cs) | 标准 HID 电量字段 |
| [Devices/XInputBattery.cs](Devices/XInputBattery.cs) | XInput 手柄电量（5 档映射成百分比） |
| [Devices/SteelSeriesProtocol.cs](Devices/SteelSeriesProtocol.cs) | SteelSeries Arctis 电量查询 |
| [Devices/RazerProtocol.cs](Devices/RazerProtocol.cs) | Razer 控制协议电量查询 |
| [Devices/AsusOmniProtocol.cs](Devices/AsusOmniProtocol.cs) | ROG OMNI 接收器：配对列表 + 键盘 / 鼠标分通道电量 |
| [Ui/MainForm.cs](Ui/MainForm.cs) | 主面板（GHelper 风格纵向设备列表，支持拖动排序） |
| [Ui/TrayManager.cs](Ui/TrayManager.cs) | 托盘图标，只显示列表第一台设备 |
| [App/BatteryIcon.cs](App/BatteryIcon.cs) | 圆环电量图标（GDI+ 现画，无资源文件） |
| [App/app.ico](App/app.ico) | 程序图标（已嵌进 exe，标题栏 / 资源管理器用） |
| [App/Autostart.cs](App/Autostart.cs) | 开机自启（HKCU Run） |
| [App/AppConfig.cs](App/AppConfig.cs) | 配置读写 |
| [build.ps1](build.ps1) | 单文件 exe 打包脚本 |

配置存在 `%LOCALAPPDATA%\BatteryMonitor\config.json`。

## 实现要点

**收 Input Report 用独立的 OVERLAPPED 句柄。**
在同一个 HID 句柄上一边阻塞 `ReadFile`、一边发 `WriteFile`/IOCTL，实测会让进程直接退出
（连异常都没有）。所以读用单独一个带 `FILE_FLAG_OVERLAPPED` 的句柄 + 事件超时 + `CancelIoEx`，
见 [HidDevice.cs](Hid/HidDevice.cs)。

**系统独占的顶层集合只能 0 权限打开。**
鼠标/键盘/笔/触摸屏被系统占用，`CreateFile` 传 `dwDesiredAccess=0` 仍能读属性和 Feature Report，
但发不了指令。厂商接口（`0xFFxx`）不受此限。

**托盘图标先设 `Icon` 再设 `Visible`。** 反过来设置的话，托盘里出现的是空白图标。

**卡片里的行高一律实测，不写死像素。** 30pt 的 AutoSize 标签会被 AutoSize 行裁掉半截，
写死的像素行高在高分屏上又会把底部详情切掉。现在行高全部由 `TextRenderer.MeasureText`
按当前字体算出来，见 [MainForm.cs](Ui/MainForm.cs) 的 `FitTypography`。

**开机自启只读不改。** 程序启动时只**读**注册表里的真实状态，不会因为读到一次不一致
就替用户删/建启动项。

**不联网、不写日志。** 全程只有本地 HID 调用，没有任何网络请求；默认不产生日志文件。
