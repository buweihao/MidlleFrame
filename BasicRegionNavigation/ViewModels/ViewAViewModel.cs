using BasicRegionNavigation.Controls;
using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Models;
using BasicRegionNavigation.Services;
using CommunityToolkit.Mvvm.ComponentModel; // 核心引用
using CommunityToolkit.Mvvm.Input;        // 核心引用
using Core;
using HandyControl.Controls;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MyConfig.Controls;
using MyDatabase;
using Prism.Events; // 假设 IEventAggregator 来自 Prism
using SkiaSharp;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Expression = System.Linq.Expressions.Expression;
using Timer = System.Timers.Timer;

namespace BasicRegionNavigation.ViewModels
{
    // 修改 1: partial + ObservableObject
    public partial class ViewAViewModel : ObservableObject
    {
        private readonly IModbusService _modbusService;
        /// <summary>
        /// 本页面专用，表示用户选中需要查看的模组
        /// </summary>
        [ObservableProperty]
        private string _moduleNum = "0";

        /// <summary>
        /// 用于给前端绑定的各个控件的数据源，会跟随模组index变化而更新
        /// </summary>
        [ObservableProperty]
        private ModuleModel _currentModule;

        // 模组缓存
        private readonly ConcurrentDictionary<string, ModuleModel> _modulesCache = new ConcurrentDictionary<string, ModuleModel>();




        public ViewAViewModel(IModbusService modbusService)
        {
            _modbusService = modbusService;

            // 1. 初始化所有模组 (假设有2个)
            InitializeModules(new[] { "1", "2" });

            // 2. 【核心】单一入口监听
            _modbusService.OnModuleDataChanged += HandleDataChanged;

            InitializeSubscriptions(_modbusService);
        }
        // 在 MainViewModel 或初始化逻辑中
        public void InitializeSubscriptions(IModbusService modbusService)
        {
            // 假设我们要配置 "模组1"
            string moduleId = "1";

            // --- A. 订阅状态 (Status) ---
            // 这里的字符串 + "Status" = CurrentStatus 里的属性名
            // 例如: "FeedLift1" -> FeedLift1Status
            var statusFields = new string[]
            {
                "FeedLift1",
                "FeedLift2",
                "HangOk",          // 对应 HangOkStatus (注意：你的属性是 HangOkSensor，这里需修正，见下文注意)
                "DropNgSensor",    // 对应 DropNgSensorStatus (如果你的属性叫 DropNgSensor，这里要传 DropNgSensor 吗? 见下文)
                "UnLoadModule1",   // 对应 UnLoadModule1Status
                "DropModule1"
            };

            modbusService.SubscribeDynamicGroup(
                moduleId: moduleId,
                category: ModuleDataCategory.Status, // <--- 关键：标记为状态
                locationPrefix: "IO",               // 假设PLC地址前缀是 IO
                fields: statusFields
            );

            // --- B. 订阅产能 (Capacity) ---
            // 这里的字符串 + "Capacity" = CurrentStatus 里的属性名
            // 例如: "UnLoadModule1" -> UnLoadModule1Capacity
            var capacityFields = new string[]
            {
                "UnLoadModule1", // 注意：这里和 Status 用了同一个词根！
                "UnLoadModule2",
                "DropModule1",
                "DropModule2"
            };

            modbusService.SubscribeDynamicGroup(
                moduleId: moduleId,
                category: ModuleDataCategory.Capacity, // <--- 关键：标记为产能
                locationPrefix: "Data",               // 假设PLC地址前缀是 Data
                fields: capacityFields
            );
        }
        private void InitializeModules(string[] ids)
        {
            foreach (var id in ids)
            {
                var model = new ModuleModel(id);
                _modulesCache.TryAdd(id, model);
            }

            // 默认显示第一个
            if (ids.Length > 0) CurrentModule = _modulesCache[ids[0]];
        }

        // 3. 交通指挥：收到数据 -> 查找字典 -> 定点更新
        private void HandleDataChanged(string moduleId, ModuleDataCategory category, object data)
        {
            if (_modulesCache.TryGetValue(moduleId, out var targetModule))
            {
                // 在 UI 线程更新 (如果 Modbus 是后台线程)
                // Application.Current.Dispatcher.Invoke(() => ...

                targetModule.DispatchData(category, data);
            }
            else
            {
                // 收到了一个不存在的模组ID的数据，忽略或记录日志
            }
        }

        // 切换模组的方法 (供前端 ComboBox 绑定)
        public void SwitchModule(string newId)
        {
            if (_modulesCache.TryGetValue(newId, out var model))
            {
                CurrentModule = model;
            }
        }























        // 跨线程事件聚合器
        private readonly IEventAggregator _ea;
        // 取消令牌源
        private CancellationTokenSource cts = new CancellationTokenSource();

        // ========================== 属性区域 ==========================






        [ObservableProperty]
        private ObservableCollection<ISeries> _revenueSeries;

        [ObservableProperty]
        private int[] _myIntDataArray = new int[] { 10, 50, 25, 60, 90 };


        // 模组名称属性组
        [ObservableProperty] private string _model1Name;
        [ObservableProperty] private string _model2Name;
        [ObservableProperty] private string _model3Name;
        [ObservableProperty] private string _model4Name;
        [ObservableProperty] private string _model5Name;
        [ObservableProperty] private string _model6Name;
        [ObservableProperty] private string _model7Name;
        [ObservableProperty] private string _model8Name;
        [ObservableProperty] private string _model9Name;
        [ObservableProperty] private string _model10Name;
        [ObservableProperty] private string _model11Name;
        [ObservableProperty] private string _model12Name;

        // ========================== 构造函数 ==========================

        public void ModelNameInit()
        {
            Model1Name = "模组1" + Global.GetValue("1_备注");
            Model2Name = "模组2" + Global.GetValue("2_备注");
            Model3Name = "模组3" + Global.GetValue("3_备注");
            Model4Name = "模组4" + Global.GetValue("4_备注");
            Model5Name = "模组5" + Global.GetValue("5_备注");
            Model6Name = "模组6" + Global.GetValue("6_备注");
            Model7Name = "模组7" + Global.GetValue("6_备注");
            Model8Name = "模组8" + Global.GetValue("6_备注");
            Model9Name = "模组9" + Global.GetValue("6_备注");
            Model10Name = "模组10" + Global.GetValue("6_备注");
            Model11Name = "模组11" + Global.GetValue("6_备注");
            Model12Name = "模组12" + Global.GetValue("6_备注");
        }

        private void OnMyDataUpdated(Core.TableRowViewModel value)
        {
            // 你的逻辑代码...
            // var model = _cache.GetOrAdd(value.ModuleNum, i => GetModule(i));
            // ...
        }

        public void NotifyChanges(IEnumerable<AlarmInfo> newValue)
        {
            _ea.GetEvent<MyDataUpdatedEvent>().Publish(newValue);
        }

        public static string[] WarningName = new string[] { /* 省略长列表，保持原样 */ "上料模组1_传感器故障", "..." };

        // ========================== 命令 ==========================

        [RelayCommand]
        private async Task NavigateModule(string param)
        {
            // 保留原有逻辑结构
            // if (param != null && Global.GetValue("isViewAReadMission") == "1")
            // {
            //    ModuleNum = param;
            //    var model = _cache.GetOrAdd(int.Parse(ModuleNum), i => GetModule(i));
            //    CurrentModule = model;
            //    cts.Cancel();
            //    cts = new CancellationTokenSource();
            //    var loopTasks = new[] { ... };
            // }
            await Task.CompletedTask; // 占位
        }

        [RelayCommand]
        private void ShowText(string param)
        {
            MyConfigCommand.configHelper = Global._config;
            MyConfigCommand.ShowText(param);
        }

        private Brush GetBrushByStatus(string value)
        {
            return value switch
            {
                "3" => Brushes.Gray,
                "2" => Brushes.Red,
                "1" => Brushes.Lime,
                "0" => Brushes.Red,
                _ => Brushes.Aqua
            };
        }

        public void UpdateXLabelsByTime()
        {
            if (CurrentModule.CurrentColumnInfo.XAxes == null || CurrentModule.CurrentColumnInfo.XAxes.Length == 0)
                return;

            string[] labels;
            var currentClassTime = Global.GetCurrentClassTime();
            if (currentClassTime.Status == ClassStatus.白班)
                labels = new[] { "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19" };
            else
                labels = new[] { "20", "21", "22", "23", "0", "1", "2", "3", "4", "5", "6", "7" };

            CurrentModule.CurrentColumnInfo.XAxes[0].Labels = labels;
        }

        public void UpdateAlarmList(IEnumerable<AlarmInfo> newAlarms)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentModule.CurrentWarningInfo.AlarmList.Clear();
                foreach (var alarm in newAlarms)
                {
                    CurrentModule.CurrentWarningInfo.AlarmList.Add(alarm);
                }
            });
        }

    }

}