# 已知坑（改之前先看）

## 退出崩溃

`Closing` 里同步 `Close()` 会 `InvalidOperationException`。必须：`e.Cancel=true`、`Hide()`、`await ShutdownAsync()`、再 `Dispatcher.InvokeAsync(Close)`。`_isShuttingDown` 防止重入。

## 串口选了不保存

可编辑 `ComboBox` 的选中项不会写回 `PortName`。`OkButton_Click` 要：

1. `Text` / `SelectedItem` 的 `UpdateSource()`
2. 显式 `_config.PortName = SelectedItem ?? Text`
3. 当前口不在系统列表时插入列表
4. 模板里 `PART_EditableTextBox` 的 `Text` 必须 TwoWay

## 连接状态被日志冲掉

`ApplyConnectedStatusText`：只要还有通道 `Connected`，状态栏和日志标题保持「已连接」。清空日志只清列表。轮询失败不要 `SetState` 回 Connecting/Disconnected。

## 从机 ID

只在通道参数里改。确定后 `foreach tag.SlaveId = channel.SlaveId`。不要在标签对话框加从站输入。

## 自定义窗口

`WindowStyle=None` + `WindowChrome CaptionHeight=0`。标题栏按钮加 `IsHitTestVisibleInChrome`。最大化走工作区矩形，不要 `WindowState=Maximized` 挡任务栏。

写入对话框曾经把 `MinWidth` 和 `WindowStyle` 粘在一起导致系统白标题栏回来，改 XAML 属性时检查每个属性独占一行。

## 曲线历史

`LoadTagsForChannel` 会 new 新的 `TagItemViewModel`。必须 `TrendStore.GetOrCreate(tag.Id)`，加载当前值时 `Update(..., recordTrend: false)`，避免重复最后一个点。

## XAML 重复块

改主界面时旧 DataGrid 容易没删干净，出现两套「实时数据点」。改完数一下 `Grid`/`Border` 开闭标签。

## 主题 Style 截断

往 `ScadaTheme.xaml` 插新 Style 时，不要吃掉下一个 Style 的起始标签（`StatCard` 曾经被 `DataMonitorCard` 截断）。

## 用户本机改动

通道名绿色、边距、「轮询间隔(ms)」等是用户本地偏好。rebase/合并时保留，不要格式化整份 XAML。

## Cloud Agent

这里是 Linux，WPF 编不过。功能改完用静态检查（标签配对、构造函数参数、资源 Key）代替编译。
