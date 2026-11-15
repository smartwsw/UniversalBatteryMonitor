# 设备电量

Windows 托盘小工具，显示无线鼠标、键盘、手柄的剩余电量。

自动识别插着的设备，不用手动添加。单文件程序，拷到任何 Windows 10 / 11 机器上直接运行，
不需要安装 .NET 运行时。

[English](README.md)

## 功能

- **自动识别外设**：插上什么就显示什么，换设备不用改配置
- **托盘电量**：图标显示列表第一台设备的圆环电量，鼠标悬停列出前 3 台，每台一行
- **操作**：左键刷新，双击打开面板，右键出菜单
- **拖动排序**：设备行可以按住上下拖动，顺序会保存；把想常看的设备拖到最上面，托盘跟着换
- **低电量通知**：任意设备低于 20% 弹一条 Windows 通知，每台只提醒一次，充电或重新连上后重新生效
- **刷新间隔可调**，关闭窗口可选择最小化到托盘
- **开机自启**：管理员模式下由计划任务直接以管理员身份启动，不弹 UAC
- **电量配色**：≥60% 绿，30–59% 黄，<30% 红
- 全程本地读取，**不联网、不写日志**

## 支持的设备

每个单元格是设备或接收器在系统里的 ID 和状态：
![已验证](https://img.shields.io/badge/%E5%B7%B2%E9%AA%8C%E8%AF%81-2ea44f) 真机实测可读，
![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) 已实现但没在真机上试过，
![不支持](https://img.shields.io/badge/%E4%B8%8D%E6%94%AF%E6%8C%81-9e9e9e) 不支持。

### ASUS ROG

一台 ROG OMNI 接收器可以同时挂键盘和鼠标，会拆成两行分别显示。
配对 ID 是接收器配对列表里报的型号编号，和设备单独插线时的 USB ID 不是同一个。

#### 键盘

| 设备 | 接收器 | 配对 ID | 状态 |
| --- | --- | --- | --- |
| ROG Strix Scope II 96 RX | `0B05:1ACE` | `0x1B7A` | ![已验证](https://img.shields.io/badge/%E5%B7%B2%E9%AA%8C%E8%AF%81-2ea44f) |
| ROG Strix Scope II 96 | `0B05:1ACE` | `0x1AB0` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Azoth | `0B05:1ACE` | `0x1A85` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Azoth Extreme | `0B05:1ACE` | `0x1B42` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Azoth Extreme SE | `0B05:1ACE` | `0x1CF1` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Falchion RX Low Profile | `0B05:1ACE` | `0x1B06` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |

#### 鼠标

| 设备 | 接收器 | 配对 ID | 状态 |
| --- | --- | --- | --- |
| ROG Keris II Ace | `0B05:1ACE` | `0x1B1A`、`0x1B18` | ![已验证](https://img.shields.io/badge/%E5%B7%B2%E9%AA%8C%E8%AF%81-2ea44f) |
| ROG Gladius III Aimpoint | `0B05:1ACE` | `0x1A72` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Harpe Ace Aim Lab Edition | `0B05:1ACE` | `0x1A94` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Harpe Ace Extreme | `0B05:1ACE` | `0x1B68`、`0x1B69` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Harpe Ace Mini | `0B05:1ACE` | `0x1B65` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Keris II Origin | `0B05:1ACE` | `0x1C0E` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Keris II Origin KJP | `0B05:1ACE` | `0x1D4E` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Keris Wireless Aimpoint | `0B05:1ACE` | `0x1A68`、`0x1A6A` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |
| ROG Strix Impact III Wireless | `0B05:1ACE` | `0x1AD7` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |

### Razer

| 设备 | USB ID | 状态 |
| --- | --- | --- |
| Razer Viper Ultimate | `1532:007B` | ![已验证](https://img.shields.io/badge/%E5%B7%B2%E9%AA%8C%E8%AF%81-2ea44f) |
| Razer Mouse Dock | `1532:007E` | ![不支持](https://img.shields.io/badge/%E4%B8%8D%E6%94%AF%E6%8C%81-9e9e9e) |

Razer 需要管理员权限，普通权限下这一行会提示改用管理员重启。

### 手柄

| 设备 | USB ID | 状态 |
| --- | --- | --- |
| 飞智黑武士 VADER4（Xbox 兼容模式） | `045E:028E` | ![已验证](https://img.shields.io/badge/%E5%B7%B2%E9%AA%8C%E8%AF%81-2ea44f) |
| 其他 XInput 手柄 | `045E:028E` | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |

手柄只能读到 5 档电量，不如厂商私有协议精确。

### 蓝牙

键盘、鼠标、手柄切到蓝牙后走标准蓝牙电量服务，不依赖厂商私有协议，
不经过接收器。

| 设备 | USB ID | 状态 |
| --- | --- | --- |
| ROG Strix Scope II 96 RX WL | `0B05:1B7B` | ![已验证](https://img.shields.io/badge/%E5%B7%B2%E9%AA%8C%E8%AF%81-2ea44f) |
| 其他蓝牙键鼠手柄 | — | ![未验证](https://img.shields.io/badge/%E6%9C%AA%E9%AA%8C%E8%AF%81-dfb317) |

## 使用

### 获取程序

从 [Releases](https://github.com/smartwsw/UniversalBatteryMonitor/releases) 下载 `BatteryMonitor.exe`
直接运行，或用源码自行打包：

```powershell
.\build.ps1              # 打包成 dist\BatteryMonitor.exe
.\build.ps1 -Clean       # 先清掉 bin / obj / dist 再打包
.\build.ps1 -Run         # 打包后直接启动
```

默认打包 `win-x64` 自包含单文件（约 71 MB），需要别的架构传 `-Runtime win-arm64` 或 `-Runtime win-x86`。

### 命令行参数

| 参数 | 作用 |
|---|---|
| 无参数 | 打开面板 |
| `--tray` | 启动后直接驻留托盘（开机自启用的就是这个） |
| `--once` | 命令行读一次就退出，不弹窗 |
| `--list` | 列出本机所有 HID 接口，排查设备问题时用 |
| `--version` | 打印版本号 |

### 面板

面板里可以改刷新间隔、开关开机自启、选择关闭窗口时是否最小化到托盘。
窗口默认打开在屏幕右下角，设备列表用深色细滚动条，支持滚轮和拖拽滑块。

## 注意事项

- **雷蛇设备需要管理员权限**：接收器的控制接口只对管理员进程开放，普通权限下该行会提示
  改用管理员重启。托盘右键菜单里有「以管理员身份重启」，程序不会自动提权，避免开机自启弹 UAC。
  Razer Mouse Dock 不在支持范围内。
- **手柄精度有限**：XInput 只给 5 档电量（空 / 低 / 中 / 满 / 未知），换算成 15 / 40 / 70 / 100%，
  不如厂商私有协议精确。手柄插着线时仍会上报档位，界面会标成"有线连接"。
- **设备休眠时读数为 0**：ROG OMNI 接收器在设备休眠时仍会应答，但电量字节是 0。
  动一下设备再刷新即可，界面会提示而不是误报成 0%。
- **ROG OMNI 新型号名称**：接收器自己只上报 `ROG OMNI RECEIVER`，具体型号靠配对信息反查。
  列表里没有的新型号会显示成 `ROG 设备 0xXXXX`，补进型号表就能显示名字。
