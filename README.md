# MotionControl - 运动控制系统上位机

工业运动控制 HMI 应用程序，基于 WPF 和 .NET 8 开发，用于控制 ZMC EtherCAT 运动控制器。

## 项目特性

- ✅ **32 轴运动控制** - 支持最多 32 个伺服轴的同步控制
- ✅ **EtherCAT 通信** - 基于 ZMC432EtherCAT 控制器的实时通信
- ✅ **IO 监控** - 16 路数字输入 + 16 路数字输出监控
- ✅ **报警管理** - 实时报警监控和历史记录
- ✅ **多轴联动** - 支持轴组和主从轴配置
- ✅ **状态机管理** - 单轴和系统级状态机控制

## 技术架构

### 分层设计

```
┌─────────────────────────────────────────┐
│         Presentation Layer               │
│  (ViewModels, Commands, UI Components)  │
├─────────────────────────────────────────┤
│         Application Layer                │
│    (Application Services, DTOs)         │
├─────────────────────────────────────────┤
│           Control Layer                  │
│  (Polling, StateMachines, Control)      │
├─────────────────────────────────────────┤
│          Device Layer                    │
│   (Abstractions, ZMC Implementation)    │
├─────────────────────────────────────────┤
│          Domain Layer                    │
│    (Entities, ValueObjects, Enums)      │
└─────────────────────────────────────────┘
```

### 项目结构

```
src/
├── MotionControl.App/                    # WPF 应用入口
│   ├── Bootstrap/                       # 启动配置
│   │   ├── HostBuilderFactory.cs       # Host + DI 配置
│   │   └── MachineFactory.cs           # 设备工厂
│   ├── MainWindow.xaml                 # 主窗口
│   └── appsettings.json                # 应用配置
│
├── MotionControl.Presentation/          # 表示层
│   └── ViewModels/                     # 视图模型
│       ├── MainWindowViewModel.cs
│       ├── AxisMonitorViewModel.cs
│       ├── AxisDebugViewModel.cs
│       ├── DashboardViewModel.cs
│       └── AlarmViewModel.cs
│
├── MotionControl.Application/           # 应用层
│   ├── Interfaces/                     # 服务接口
│   │   ├── IMotionAppService.cs
│   │   └── ISystemAppService.cs
│   ├── Services/                       # 应用服务
│   └── DTOs/                           # 数据传输对象
│
├── MotionControl.Control/              # 控制层
│   ├── Services/                       # 控制服务
│   │   ├── ControllerPollingService.cs
│   │   ├── AxisPollingService.cs
│   │   ├── IoPollingService.cs
│   │   └── AlarmPollingService.cs
│   ├── StateMachines/                  # 状态机
│   │   └── AxisStateMachine.cs
│   └── Interfaces/                     # 控制接口
│
├── MotionControl.Device.Abstractions/   # 设备抽象层
│   ├── Controllers/
│   │   └── IMotionController.cs
│   ├── Models/
│   │   ├── AxisFeedback.cs
│   │   └── AxisMoveCommand.cs
│   └── Results/
│       └── DeviceResult.cs
│
├── MotionControl.Device.Zmc/            # ZMC 设备实现
│   ├── Controllers/
│   │   └── ZmcMotionController.cs
│   ├── Native/
│   │   ├── ZmcNativeApi.cs
│   │   └── ZmcAxisNativeFacade.cs
│   ├── Translators/
│   │   └── ZmcStatusTranslator.cs
│   └── Config/
│       └── ZmcControllerOptions.cs
│
├── MotionControl.Domain/                # 领域层
│   ├── Entities/
│   │   ├── Axis.cs
│   │   ├── AxisGroup.cs
│   │   ├── Machine.cs
│   │   ├── Alarm.cs
│   │   └── IoPoint.cs
│   ├── Enums/
│   │   ├── AxisState.cs
│   │   ├── ServoState.cs
│   │   ├── MotionMode.cs
│   │   └── SystemState.cs
│   └── ValueObjects/
│       ├── AxisId.cs
│       └── SoftLimit.cs
│
├── MotionControl.Diagnostics/           # 诊断层
│   └── Services/
│       ├── SafetyInterlockService.cs
│       └── SafetyMonitor.cs
│
└── MotionControl.Infrastructure/        # 基础设施层
    ├── Configuration/
    │   ├── AxisMappingOptions.cs
    │   └── AxisMappingItem.cs
    └── Logging/
        └── StructuredLogService.cs
```

## 开发环境

### 系统要求

- **操作系统**: Windows 10/11
- **开发工具**: Visual Studio 2022 (17.0 或更高版本)
- **.NET SDK**: .NET 8.0
- **目标平台**: Windows x64

### 必需组件

- .NET 8.0 SDK
- Visual Studio 2022 工作负载：
  - .NET 桌面开发
  - .NET Core 跨平台开发

### 硬件要求

- ZMC432EtherCAT 运动控制器
- EtherCAT 总线
- 伺服驱动器和电机（最多 32 轴）
- 数字 IO 模块（可选）

## 快速开始

### 1. 克隆项目

```bash
git clone https://github.com/skcycle/UI.git
cd UI
```

### 2. 打开解决方案

使用 Visual Studio 2022 打开 `MotionControl.sln`

### 3. 还原 NuGet 包

在 Visual Studio 中：
- 右键点击解决方案 → "还原 NuGet 包"

或者在命令行中：
```bash
dotnet restore
```

### 4. 配置控制器

编辑 `src/MotionControl.App/appsettings.json`：

```json
{
  "ZmcController": {
    "IpAddress": "192.168.0.11",      // ZMC 控制器 IP 地址
    "AxisCount": 32,                   // 轴数量
    "PollingIntervalMs": 200           // 轮询间隔（毫秒）
  },
  "AxisMapping": {
    "AxisNames": [
      "Axis 1", "Axis 2", ...
    ],
    "Axes": [
      {
        "AxisNo": 1,
        "Name": "Axis 1",
        "Group": "GroupA",
        "IsMaster": true
      }
    ]
  }
}
```

### 5. 编译运行

在 Visual Studio 中按 `F5` 运行，或使用命令行：

```bash
dotnet build
dotnet run --project src/MotionControl.App
```

## 核心功能

### 轴监控

- 实时位置、速度、扭矩显示
- 伺服状态监控
- 轴状态指示（空闲、运动、报警等）
- 软限位和硬限位监控

### 轴调试

- 使能/去使能控制
- 回零操作
- 点动控制（Jog+ / Jog-）
- 绝对定位和相对定位
- 急停控制

### 报警管理

- 实时报警列表
- 报警历史记录
- 报警确认和清除
- 报警分级（警告/错误/致命）

### IO 监控

- 数字输入状态实时显示
- 数字输出控制
- IO 点位命名和分组

### 轴组控制

- 多轴同步运动
- 龙门轴同步
- 主从轴配置

## 配置说明

### 轴映射配置

每个轴可以配置以下属性：

| 属性 | 说明 | 示例 |
|------|------|------|
| AxisNo | 控制器轴号 | 1-32 |
| Name | 轴名称 | "X轴" / "主轴" |
| Group | 所属轴组 | "GroupA" |
| IsMaster | 是否为主轴 | true/false |
| MasterAxisName | 主轴名称 | "Axis 1" |

### 轮询配置

```json
{
  "PollingIntervalMs": 200  // 轮询间隔（毫秒）
}
```

推荐值：
- 高速应用：50-100ms
- 标准应用：100-200ms
- 低速应用：500-1000ms

## 架构特性

### 依赖注入 (DI)

项目使用 `Microsoft.Extensions.DependencyInjection` 进行依赖注入：

```csharp
services.AddSingleton<IMotionController, ZmcMotionController>();
services.AddSingleton<IAxisControlService, AxisControlService>();
services.AddHostedService<PollingHostedService>();
```

### 后台服务

轮询服务使用 `BackgroundService` 实现：

```csharp
public sealed class PollingHostedService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await axisPollingService.PollAsync(stoppingToken);
            await ioPollingService.PollAsync(stoppingToken);
            await alarmPollingService.PollAsync(stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }
}
```

### 状态机

单轴状态机管理轴状态转换：

```csharp
public sealed class AxisStateMachine
{
    public AxisState GetNextState(Axis axis)
    {
        if (axis.HasAlarm) return AxisState.Alarm;
        if (!axis.IsHomed && axis.ServoState == ServoState.On)
            return AxisState.Standstill;
        return axis.State;
    }
}
```

## 开发路线

### 已完成 ✅

- [x] 项目脚手架和分层架构
- [x] Domain 模型设计（Axis, Machine, Alarm, IoPoint）
- [x] Application 服务层
- [x] Control 轮询服务
- [x] Device 抽象层和 ZMC 实现
- [x] WPF 主界面壳
- [x] DI + Host 启动模型
- [x] 配置绑定和 Options 模式
- [x] 状态机骨架

### 进行中 🚧

- [ ] ZMC SDK 真实实现
- [ ] EtherCAT 网络状态监控
- [ ] 更多状态机逻辑
- [ ] 单元测试

### 计划中 📋

- [ ] 配方管理
- [ ] 用户权限管理
- [ ] 历史数据记录和趋势图
- [ ] 远程监控和诊断
- [ ] 多语言支持
- [ ] 多控制器支持

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request！

## 联系方式

- GitHub: https://github.com/skcycle/UI
- 项目维护: OpenClaw Team

---

**注意**: 此项目正在积极开发中，部分功能可能尚未完全实现。
