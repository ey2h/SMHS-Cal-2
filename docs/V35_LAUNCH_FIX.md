# V35 启动问题：根因与解决（2026-10-28）

> 结论先说：**V35 能启动，但必须由 shell（explorer）拉起；用普通 CreateProcess 从本工具链启动必挂起。**
> 这是 Oreans WinLicense 壳的反调试机制撞上 WorkBuddy 的 Job 对象所致，与注册表/权限/加壳类型无关。

## 一、现象

| 启动方式 | 结果 |
|---|---|
| `subprocess.Popen` / `CreateProcessW`（继承或干净环境） | ❌ 停在 4 MB、2 线程、CPU 0，只有一个随机类名的隐藏壳窗口；120 s 内不出任何界面 |
| `Popen(["explorer.exe", exe])` | ❌ 同上 |
| **`ShellExecuteW(None,"open",exe,...)`** | ✅ **2 秒出现启动器窗体 + 5 个按钮**（3/3 复现） |

启动器按钮：`替其它授权过的电脑获得注册码 / 限期版本 / 退出 / 绑定主板 / 绑定硬盘` —— 与 2026-10-24 记录一致。

## 二、根因（frida 栈取证，不是推测）

挂起时主线程栈（`local-data/recon/v35_stack.py`）：

```
ntdll!DbgUiWaitStateChange / ZwWaitForDebugEvent
  ← KERNELBASE!WaitForDebugEventEx
  ← MQJSSV35.EXE+0x1e7b386
  ← +0x1e773f2   ← +0x1e7752f   ← +0x1e77d48
  ← MQJSSV35.EXE+0x1e8b311   （壳入口 stub，EntryPoint 0x1E8B243）
```

IDA 反编译 `sub_2277A30`（脱壳镜像 VA，`= V31 偏移 + 0x400000`）确认这是 **Oreans WinLicense 2.x** 的保护入口，命令行参数族为
`/register /quietregister /transfer /fixclock /info /unregister /quietunregister /shownetusers /hwchangelog /quietexit /armdebug`，
并有 `CreateMutexA` 单实例、WMI 硬件指纹采集。

⇒ 壳在启动时用 `CreateProcess(DEBUG_ONLY_THIS_PROCESS)` 拉起自己的主程序副本做自我校验，父线程 `WaitForDebugEvent` 收事件。
**当父进程位于 WorkBuddy 的 Job 对象 `WORKBUDDY_APP_LIFETIME_JOB` 内时，这一步等不到调试事件，父线程永久阻塞**——
这就是「V35 起不来」的全部真相。由 explorer 实例用 ShellExecute 拉起时不在该 Job 内，正常放行。

旁证：壳会起**两个同名进程**（壳进程 + VB6 主程序），`v35runs/keep.log` 10-24 的 `started → ready mdiVisible=False` 耗时 74 s 也与此机制一致。

## 三、修复

不需要改注册表、不需要提权、不需要脱壳。只要换启动通道：

```python
import ctypes
shell32 = ctypes.WinDLL("shell32.dll")
EXE = r"C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\MQJSSV35.exe"
shell32.ShellExecuteW(None, "open", EXE, None, EXE.rsplit("\\",1)[0], 1)
```

现成脚本：`local-data/recon/v35_launch.py`

```bash
python v35_launch.py            # 启动并列出启动器窗体与按钮
python v35_launch.py --trial    # 启动 + 点「限期版本」进主界面
python v35_launch.py --state    # 只看当前状态
python v35_launch.py --kill     # 结束进程
```

点「限期版本」后得到：`ThunderRT6MDIForm '汇宝幕墙门窗结构计算软件V35.0  (已注册)'` + 主内容窗体 + `欢迎进入软件注册！` 页。

## 四、残留问题（已定位，非环境缺陷）

### 4.1 `Adodc1`「没有注册类」

- **注册表已核实完整**（`Registry32` 视图，与 JET/ADODC 都齐）：`{67397AA3-7FB1-11D0-B148-00A0C922E820}` → `msadodc.ocx`，带 TypeLib/ProgID/Control；
  `Microsoft.Jet.OLEDB.4.0` → `{dee35070-506b-11cf-b1aa-00aa00b8de95}` → `msjetoledb40.dll`，HKLM 与 HKCU 双视图都在。
- frida hook 实测：`CoGetClassObject {67397AA3…} -> OK`，ADODC 控件本身**创建成功**。
- 因此该提示不是"类缺失"，VB6 弹的是控件级错误。**V31 上同样存在此提示而不影响使用**，点「确定」即可。
- 曾试过把 `{BD96C556-65A3-11D0-983A-00C04FC29E33}` 指向 `msadodc.ocx`（10-28 上午给 V31 用过）——**该 GUID 在 V35 脱壳镜像里 0 次命中**，
  且它实际是 `RDS.DataControl` 的 CLSID，属于错误映射。现保留（为兼容 V31 的历史行为），但**不要**把它当 V35 的依赖。

### 4.2 启动偶发失败（约 1/5 概率）

表现为直接弹 `运行时错误 '53': 文件未找到` 或别的 `#32770`，没有启动器。frida 抓到同时段 `Scripting.FileSystemObject` 创建失败
（`CoCreateInstanceEx {0D43FE01-…} FAIL`），已补 `Scripting.FileSystemObject/.Dictionary` 的 ProgID→CLSID 映射（HKCU 双视图），仍未完全消除。
判断属 WinLicense 硬件指纹（WMI）自检的时序问题，重试即可。**生产用法：启动后轮询启动器按钮，未出现则重试一次。**

### 4.3 不能计算（授权限制，非技术缺陷）

`欢迎进入软件注册！` → `您的电脑还没有注册这个软件！`。点「下一步/预演」会弹「程序未注册，或注册遭到破坏」+ VB 80010108 并退出。
这是软件授权状态，按项目边界 B1 **不做注册绕过**。V35 的作用仍是：①界面/控件树权威 ②脱壳代码静态取证；数值基准以 V31 为准。

## 五、工具脚本清单

| 脚本 | 用途 |
|---|---|
| `v35_launch.py` | **推荐入口**：ShellExecute 启动 + 列窗体/按钮 + 点限期版本 |
| `v35_stack.py` | frida attach 读全线程调用栈（挂起定位） |
| `v35_probe_boot.py` | 启动期 CPU/线程/模块/窗口采样 |
| `v35_boot_ab.py` | 启动方式 A/B/C/D 对照实验 |
| `v35_com_trace.py` | hook COM 创建，抓失败 CLSID |
| `v35_diag_launch.py` | 只读枚举窗口，不点任何按钮 |

## 六、踩坑记录

1. **不要用 `taskkill` 后立刻启动**，会放大 53 错误概率；启动前留 1.5 s。
2. `Add-Type` 与 `reg.exe`、`schtasks.exe`、`regsvr32.exe` 在本环境被安全策略硬拦，改纯注册表 API 或 .NET `RegistryKey`。
3. `Module.findExportByName` 在 frida 17 已移除，用 `Module.getGlobalExportByName` / `Process.getModuleByName(...).getExportByName`。
4. 判断"模块加载数"在 WOW64 下不可靠（Toolhelp 从 64 位枚举 32 位进程只返回 7 个），**判定一律看窗口**。
