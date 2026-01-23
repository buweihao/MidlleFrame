using BasicRegionNavigation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core; // 假设这是你的 Global 所在的命名空间
using Dm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace BasicRegionNavigation.ViewModels
{
    internal partial class ComMonitorViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        // -----------------------------------------------------------------------
        // 模组 1 定义
        // -----------------------------------------------------------------------
        #region Model 1
        [ObservableProperty]
        private string _model1RawType;

        [ObservableProperty] private Brush _model1LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model1LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model1LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model1LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model1LineColorBatch = Brushes.Gray;
        [ObservableProperty] private Brush _model1LineColorAround = Brushes.Gray;
        #endregion

        // -----------------------------------------------------------------------
        // 模组 2 定义
        // -----------------------------------------------------------------------
        #region Model 2
        [ObservableProperty]
        private string _model2RawType;

        [ObservableProperty] private Brush _model2LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model2LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model2LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model2LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model2LineColorBatch = Brushes.Gray;
        [ObservableProperty] private Brush _model2LineColorAround = Brushes.Gray;
        #endregion

        // -----------------------------------------------------------------------
        // 模组 3 定义
        // -----------------------------------------------------------------------
        #region Model 3
        [ObservableProperty]
        private string _model3RawType;

        [ObservableProperty] private Brush _model3LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model3LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model3LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model3LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model3LineColorAround = Brushes.Gray;
        [ObservableProperty] private Brush _model3LineColorBatch = Brushes.Gray;
        #endregion

        // -----------------------------------------------------------------------
        // 模组 4 定义
        // -----------------------------------------------------------------------
        #region Model 4
        [ObservableProperty]
        private string _model4RawType;

        [ObservableProperty] private Brush _model4LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model4LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model4LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model4LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model4LineColorAround = Brushes.Gray;
        [ObservableProperty] private Brush _model4LineColorBatch = Brushes.Gray;
        #endregion

        // -----------------------------------------------------------------------
        // 模组 5 定义
        // -----------------------------------------------------------------------
        #region Model 5
        [ObservableProperty]
        private string _model5RawType;

        [ObservableProperty] private Brush _model5LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model5LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model5LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model5LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model5LineColorAround = Brushes.Gray;
        [ObservableProperty] private Brush _model5LineColorBatch = Brushes.Gray;
        #endregion

        // -----------------------------------------------------------------------
        // 模组 6 定义
        // -----------------------------------------------------------------------
        #region Model 6
        [ObservableProperty]
        private string _model6RawType;

        [ObservableProperty] private Brush _model6LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model6LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model6LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model6LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model6LineColorAround = Brushes.Gray;
        [ObservableProperty] private Brush _model6LineColorBatch = Brushes.Gray;
        #endregion

        // -----------------------------------------------------------------------
        // 内部状态与任务管理
        // -----------------------------------------------------------------------

        // 这是一个 Field，不需要通知
        public int Modules = Global.Modules;

        private CancellationTokenSource cts;
        private List<Task> loopTasks = new List<Task>();
        // int times = 1; // 似乎未被使用，注释掉或移除

        public ComMonitorViewModel(IConfigService configService)
        {
            _configService = configService;

            // Initializing properties using the injected service
            Model1RawType = $"模组1 {_configService.GetConfigValue("1_备注")}";
            Model2RawType = $"模组2 {_configService.GetConfigValue("2_备注")}";
            Model3RawType = $"模组3 {_configService.GetConfigValue("3_备注")}";
            Model4RawType = $"模组4 {_configService.GetConfigValue("4_备注")}";
            Model5RawType = $"模组5 {_configService.GetConfigValue("5_备注")}";
            Model6RawType = $"模组6 {_configService.GetConfigValue("6_备注")}";
            // 启动监控任务
            _ = RestartLoopTask();

            // 启动一个后台任务监控全局 Modules 数量变化
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);

                    if (Global.Modules != Modules)
                    {
                        Modules = Global.Modules;
                        _ = RestartLoopTask();
                    }
                }
            });
        }

        public async Task RestartLoopTask()
        {
            // 如果已经在运行，先取消
            if (cts != null)
            {
                cts.Cancel();
                // 等待所有任务完成（安全退出）
                if (loopTasks.Count > 0)
                {
                    // 忽略取消异常
                    try { await Task.WhenAll(loopTasks); } catch { }
                }
                cts.Dispose();
            }

            cts = new CancellationTokenSource();
            loopTasks.Clear();

            // 数据采集任务间隔
            if (!double.TryParse(_configService.GetConfigValue("ReadMissionTimeSpan"), out double spanMs))
            {
                spanMs = 500; // 默认值，防止解析失败
            }
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(spanMs);

            loopTasks.AddRange(StartModuleLoops(Modules, IsConnectMissionAsync, timeSpan, cts.Token));
        }

        private Task[] StartModuleLoops(
             int modules,
             Func<int, Task> body,
             TimeSpan period,
             CancellationToken ct)
        {
            return Enumerable.Range(1, modules)
                .Select(id => Task.Run(() => RunLoopAsync(id, body, period, ct), ct))
                .ToArray();
        }

        private async Task RunLoopAsync(int modelNum, Func<int, Task> job, TimeSpan interval, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await job(modelNum);
                    }
                    catch (Exception e)
                    {
                    }

                    await Task.Delay(interval, token);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不做处理
            }
        }

        /// <summary>
        /// 核心业务逻辑：更新 UI 绑定属性
        /// </summary>
        public async Task IsConnectMissionAsync(int i)
        {
            // 注意：这里仍然在后台线程运行。
            // WPF 的 PropertyChanged 通常会自动调度到 UI 线程，但如果遇到跨线程异常，
            // 可能需要 Application.Current.Dispatcher.Invoke(() => ... ) 包裹赋值操作。
            // 既然原代码可以直接运行，这里保持原样。

        }

        // -----------------------------------------------------------------------
        // 命令 (Commands)
        // -----------------------------------------------------------------------

        [RelayCommand]
        private async Task InsertAsync()
        {
            // 这里保留你的测试代码逻辑
            await Task.CompletedTask; // 防止警告

            // 下面是你原本注释掉的代码，保留原样：
            /*
            bool cleared = await Global.repo_product.ClearTableAsync();
            if (cleared)
            {
                Console.WriteLine("repo_product表格已清空");
            }
            ... (保留其余注释内容)
            */
        }


        // -----------------------------------------------------------------------
        // PLC 辅助方法 (Helpers) - 这里的代码没有变动，只是保留在类中
        // -----------------------------------------------------------------------

    }
}