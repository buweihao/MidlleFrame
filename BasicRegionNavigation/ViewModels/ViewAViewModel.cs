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

            StartStatusAndCapacitySimulation();
            StartProductInfoSimulation();
            StartPieInfoSimulation();
            StartColumnInfoSimulation();
            StartWarningSimulation();
        }
        // 在 MainViewModel 或初始化逻辑中
        public void InitializeSubscriptions(IModbusService modbusService)
        {
            // 假设我们要配置 "模组1"
            string moduleId = "1";

            // --- A. 订阅状态 (Status) ---
            // 字典映射：{ "UI属性名", "CSV中的点位后缀" }
            var statusMapping = new Dictionary<string, string>
            {
                // 周边墩子状态 (PLC_Peripheral)
                { "FeedLift1",      "PLC_Peripheral_FeedStation1Status" },
                { "FeedLift2",      "PLC_Peripheral_FeedStation2Status" },
                { "HangOk",         "PLC_Peripheral_HangerOkStation1Status" }, // 对应 HangOkStatus
                { "DropNgSensor",   "PLC_Peripheral_HangerNgStationStatus" },  // 对应 DropNgSensorStatus
                
                // 供料机与翻转台状态
                { "UnLoadModule1",  "PLC_Feeder_A_Status" },   // 供料机A状态
                { "DropModule1",    "PLC_Flipper_Status" }     // 翻转台状态
            };

            modbusService.SubscribeDynamicGroup(
                moduleId: moduleId,
                category: ModuleDataCategory.Status,
                fieldMapping: statusMapping
            );

            // --- B. 订阅产能 (Capacity) ---
            var capacityMapping = new Dictionary<string, string>
            {
                // 供料机产能
                { "UnLoadModule1", "PLC_Feeder_A_TotalCapacity" }, // 供料机A 产能
                { "UnLoadModule2", "PLC_Feeder_B_TotalCapacity" }, // 供料机B 产能
                
                // 翻转台产能
                { "DropModule1",   "PLC_Flipper_TotalCapacity" }   // 翻转台 产能
                
                // 注意：CSV中没有找到 DropModule2 (第二个翻转台?) 对应的点位，已移除以防报错
                // { "DropModule2", "???" } 
            };

            modbusService.SubscribeDynamicGroup(
                moduleId: moduleId,
                category: ModuleDataCategory.Capacity,
                fieldMapping: capacityMapping
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
        // 在 ViewAViewModel 类中添加此方法
        private void StartStatusAndCapacitySimulation()
        {
            // 开启后台任务：模拟状态 (Status) 和 产能 (Capacity)
            Task.Run(async () =>
            {
                var random = new Random();
                while (true)
                {
                    await Task.Delay(1000); // 1秒刷新一次

                    // 1. 构造 Status (状态) 数据
                    var statusData = new Dictionary<string, int>
            {
                // 周边墩子
                { "FeedStation1Status", random.Next(0, 4) },
                { "FeedStation2Status", random.Next(0, 4) },
                { "FeedStation3Status", random.Next(0, 4) },
                { "HangerOkStation1Status", random.Next(0, 2) },
                { "HangerOkStation2Status", random.Next(0, 2) },
                { "HangerNgStationStatus", random.Next(0, 2) },

                // 机械手
                { "ProductRobotStatus", random.Next(0, 4) },
                { "HangerRobotStatus", random.Next(0, 4) },

                // 供料机与翻转台
                { "FeederAStatus", random.Next(0, 4) },
                { "FeederBStatus", random.Next(0, 4) },
                { "FlipperStatus", random.Next(0, 4) }
            };

                    // 2. 构造 Capacity (产能) 数据
                    var capacityData = new Dictionary<string, int>
            {
                { "FeederACapacity", random.Next(100, 200) },
                { "FeederBCapacity", random.Next(100, 200) },
                { "FlipperCapacity", random.Next(50, 100) }
            };

                    // 3. 推送数据
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        HandleDataChanged("1", ModuleDataCategory.Status, statusData);
                        HandleDataChanged("1", ModuleDataCategory.Capacity, capacityData);
                    });
                }
            });
        }
        private void StartProductInfoSimulation()
        {
            // 开启后台任务：模拟产品信息 (ProductInfo)
            Task.Run(async () =>
            {
                var random = new Random();
                // 产品信息可能不需要像状态那样频繁刷新，这里设为 3 秒
                while (true)
                {
                    await Task.Delay(3000);

                    // 1. 构造产品信息字典
                    // Key 必须对应 CurrentProductInfo 类中的 FieldMapping 配置
                    var productData = new Dictionary<string, string>
            {
                { "ProjectCode", "PROJ-" + random.Next(1000, 9999) },   // 对应：项目编号
                { "Material",    random.Next(0, 2) == 0 ? "铝合金" : "不锈钢" }, // 对应：原料
                { "AnodeType",   "Type-" + (char)random.Next('A', 'F') }, // 对应：阳极类型
                { "Color",       random.Next(0, 2) == 0 ? "黑色" : "银色" }  // 对应：颜色
            };

                    // 2. 推送数据
                    // 这里假设 上挂(Up) 和 下挂(Dn) 显示相同的信息进行测试
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 推送给上挂产品信息
                        HandleDataChanged("1", ModuleDataCategory.UpProductInfo, productData);

                        // 推送给下挂产品信息
                        HandleDataChanged("1", ModuleDataCategory.DnProductInfo, productData);
                    });
                }
            });
        }

        private void StartPieInfoSimulation()
        {
            // 开启后台任务：模拟饼图数据 (PieInfo)
            Task.Run(async () =>
            {
                var random = new Random();

                while (true)
                {
                    await Task.Delay(2500); // 2.5秒刷新一次，避免闪烁过快

                    // --- 1. 构造上挂饼图数据 ---
                    // Key = 扇区名称, Value = 数值
                    var upPieData = new Dictionary<string, int>
            {
                { "正常运行", random.Next(60, 100) },
                { "设备待机", random.Next(10, 30) },
                { "故障停机", random.Next(0, 15) },
                { "换料暂停", random.Next(5, 20) }
            };

                    // --- 2. 构造下挂饼图数据 ---
                    // 演示使用不同的分类名称
                    var dnPieData = new Dictionary<string, int>
            {
                { "型号A", random.Next(100, 200) },
                { "型号B", random.Next(50, 150) },
                { "型号C", random.Next(20, 80) },
                { "返工",   random.Next(0, 10) }
            };

                    // --- 3. 推送数据 ---
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 推送给上挂饼图 (对应 UpMyPieSeries)
                        HandleDataChanged("1", ModuleDataCategory.UpPieInfo, upPieData);

                        // 推送给下挂饼图 (对应 DnMyPieSeries)
                        HandleDataChanged("1", ModuleDataCategory.DnPieInfo, dnPieData);
                    });
                }
            });
        }

        private void StartColumnInfoSimulation()
        {
            // 开启后台任务：模拟柱状图数据 (ColumnInfo) 及 班次切换
            Task.Run(async () =>
            {
                var random = new Random();
                while (true)
                {
                    await Task.Delay(4000); // 4秒刷新一次，方便观察班次切换

                    // --- 1. 随机生成一个小时 (0-23) 用于模拟当前时间 ---
                    int simulatedHour = random.Next(0, 24);

                    // --- 2. 判断班次并生成 X 轴标签 (每班 12 小时) ---
                    // 白班定义：8:00 (含) ~ 20:00 (不含)
                    bool isDayShift = simulatedHour >= 8 && simulatedHour < 20;
                    string[] labels;

                    if (isDayShift)
                    {
                        // 白班: 8, 9, 10 ... 19
                        // 生成 8 到 19 的序列
                        labels = Enumerable.Range(8, 12).Select(h => h.ToString()).ToArray();
                    }
                    else
                    {
                        // 夜班: 20, 21 ... 23, 0, 1 ... 7
                        // 从 20 开始，循环 12 个小时
                        var nightLabels = new List<string>();
                        for (int i = 0; i < 12; i++)
                        {
                            int h = (20 + i) % 24; // 超过 24 取模
                            nightLabels.Add(h.ToString());
                        }
                        labels = nightLabels.ToArray();
                    }

                    // --- 3. 构造 12 个柱状图数据 (模拟产能) ---
                    var upValues = new double[12];
                    var dnValues = new double[12];

                    for (int i = 0; i < 12; i++)
                    {
                        // 模拟数据：随机生成 10~100 的产能
                        // (可选优化：可以根据模拟时间只填充当前时间之前的柱子，这里简单填满)
                        upValues[i] = random.Next(10, 100);
                        dnValues[i] = random.Next(10, 100);
                    }

                    // --- 4. 更新 UI ---
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // A. 推送柱状图数值 (通过 HandleDataChanged 标准流程)
                        HandleDataChanged("1", ModuleDataCategory.UpColumnSeries, upValues);
                        HandleDataChanged("1", ModuleDataCategory.DnColumnSeries, dnValues);

                        // B. 直接更新 X 轴标签
                        // 说明：这里直接操作 ViewModel 的属性来模拟 UpdateXLabelsByTime 的效果，
                        // 从而避开 ModuleModel 默认逻辑中生成 "MM-dd" 格式标签的问题，符合您要求的简单数字格式。
                        if (CurrentModule != null &&
                            CurrentModule.CurrentColumnInfo != null &&
                            CurrentModule.CurrentColumnInfo.XAxes != null &&
                            CurrentModule.CurrentColumnInfo.XAxes.Length > 0)
                        {
                            // LiveCharts 的 Axis.Labels 支持直接赋值更新
                            CurrentModule.CurrentColumnInfo.XAxes[0].Labels = labels;
                        }
                    });
                }
            });
        }

        private void StartWarningSimulation()
        {
            Task.Run(async () =>
            {
                var random = new Random();
                while (true)
                {
                    await Task.Delay(1000); // 3秒刷新一次

                    // 构造匿名对象，属性名必须与 _alarmConfig 的 Key 一致
                    var warningData = new
                    {
                        // 随机触发一些报警 (10% 概率)
                        FeederASensorFault = random.Next(0, 10) == 0,
                        FeederATraceCommFault = random.Next(0, 10) == 0,

                        FeederBSensorFault = random.Next(0, 10) == 0,
                        FeederBMasterCommFault = random.Next(0, 10) == 0,

                        FlipperDoorTriggered = random.Next(0, 10) == 0,
                        FlipperEmergencyStop = random.Next(0, 20) == 0, // 5% 概率急停
                        FlipperScannerCommFault = random.Next(0, 10) == 0
                    };

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 推送报警数据
                        HandleDataChanged("1", ModuleDataCategory.WarningInfo, warningData);
                    });
                }
            });
        }

        [RelayCommand]
        private async Task NavigateModule(string index)
        {
            SwitchModule(index);
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