# RhinoX 燧光 MR 测试项目

这是一个可直接用 Unity 打开的 RhinoX 燧光 MR 测试项目骨架，目标是先把基础场景、测试 Prefab 和 OpenXR 依赖铺好，方便你后面继续接入 RhinoX 设备 SDK 或业务逻辑。

## 当前内容

- 已建立标准 Unity 项目目录：`Assets`、`Packages`、`ProjectSettings`
- 已加入 XR 相关依赖：`XR Management`、`OpenXR`、`Input System`
- 已提供首开自动引导脚本：
  - 自动生成测试场景：`Assets/Scenes/RhinoX_MR_Test.unity`
  - 自动生成测试 Prefab：`Assets/Prefabs/RhinoX_MR_TestRig.prefab`
- Prefab 内含：
  - 主相机
  - 平行光
  - 地面
  - 浮动测试目标
  - 面向相机的信息面板

## 使用方式

1. 用 Unity Hub 打开当前目录 `E:\xyz\RhinoXTest`
2. 建议使用 Unity `2022.3.21f1`
3. Unity 首次导入完成后，会自动生成场景和 Prefab
4. 打开 `Assets/Scenes/RhinoX_MR_Test.unity` 开始测试

## 可视化调整优先

这套结构尽量把可调整内容放在场景和 Prefab 上，而不是写死在代码里：

- 调整相机位置：直接改 Prefab 中 `CameraRoot`
- 调整测试物体位置/大小：直接改 `FloatingTargetRoot` 和 `Target`
- 调整显示面板：直接改 `InfoPanel`
- 旋转和浮动效果：在 `RhinoXFloatingTarget` 组件里改参数
- 面板朝向：在 `RhinoXBillboard` 组件里改参数

## 后续建议

如果你下一步要接 RhinoX 燧光 MR 的真实能力，建议按下面方式接：

- 设备初始化：单独做一层 `RhinoX SDK Adapter`
- 输入映射：优先走 Input System 或 OpenXR Action
- 场景交互：继续挂在 Prefab 上做可视化调参
- 设备特性验证：单独扩一个 `Diagnostics` 场景

如果你要，我下一步可以继续直接把：

- OpenXR Project Settings 一并补好
- 一个更完整的 MR UI 面板做出来
- 手势/注视/射线交互测试页补进去
