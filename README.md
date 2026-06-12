# Switch

`Switch` 是一个面向 Windows 的本地控制台，把多个本地网页、系统命令和运行日志统一放到一个桌面 GUI 里管理。

![Switch 界面预览](full_screen_capture.png)

当前首个公开版本：`v1.0.0`

## 功能

- 多站点网页视图
- 左侧维护站点列表，支持新增、编辑、删除、切换、打开
- 每个站点保留独立 `WebView2` 视图，适合同时管理多个本地服务
- 命令控制台
- 左侧维护命令列表，支持新增、编辑、删除、启动、停止
- 支持 `Direct`、`cmd`、`PowerShell` 三种运行模式
- 支持开机后自动启动命令
- 支持异常退出自动重试
- 日志工作区
- 右侧可在“网页”和“日志”之间切换
- 查看当前命令的后台输出
- 支持清空日志、复制日志
- 托盘与桌面集成
- 关闭窗口或最小化后缩到系统托盘
- 托盘菜单支持恢复窗口、显示控制台、刷新当前页面、停止全部命令、开机自启、退出
- 界面体验
- 左侧控制台可收起/展开
- 按钮和主要操作已中文化
- 适合本地开发、多服务调试、内网页面管理

## 默认站点

项目默认带两个本地站点示例：

- `http://127.0.0.1:8080/#/`
- `http://127.0.0.1:8099/`

## 构建

直接执行：

```bat
build.cmd
```

或者在 PowerShell 中执行：

```powershell
cd C:\Users\Hakeem\Desktop\Project\local_web_tray_shell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建完成后生成：

```text
dist\Switch.exe
```

## 运行

先启动你要访问的本地网页服务，再运行：

```powershell
.\dist\Switch.exe
```

如果需要确认打包后的程序能正常加载嵌入依赖，可以执行：

```powershell
.\dist\Switch.exe --self-test
```

## 发布

- 版本号由根目录 [VERSION](VERSION) 管理
- 变更记录见 [CHANGELOG.md](CHANGELOG.md)
- GitHub Release 可附带 `Switch.exe` 和版本化压缩包

## 配置文件

程序配置默认保存在：

```text
%LocalAppData%\SwitchShell\switch-config.json
```

配置内容包括：

- 站点列表
- 命令列表
- 命令运行模式
- 自启动开关
- 自动重试参数

## 技术实现

- `WinForms`
- `WebView2`
- 单文件 `Switch.exe` 打包
- WebView2 相关 DLL 以嵌入资源方式打进可执行文件

说明：

- 运行机器仍然需要安装 `WebView2 Runtime`
- 构建脚本会自动生成 `assets\switch.ico`

## 开源协议

本项目使用 [MIT License](LICENSE) 开源。
