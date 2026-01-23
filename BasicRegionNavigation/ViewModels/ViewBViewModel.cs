using BasicRegionNavigation;
using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Services;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using HandyControl.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Axis = LiveChartsCore.SkiaSharpView.Axis;

namespace BasicRegionNavigation.ViewModels
{
    // 1. Partial 类
    // 2. 继承 ObservableObject
    public partial class ViewBViewModel : ObservableObject, INavigationAware
    {
        private readonly IConfigService _configService;

        // -----------------------------------------------------------------------
        // 筛选条件属性 (Filter Properties)
        // -----------------------------------------------------------------------
        #region Filters

        [ObservableProperty] private string _modelNum = "1";
        [ObservableProperty] private DateTime _start;
        [ObservableProperty] private DateTime _end = DateTime.Now;
        [ObservableProperty] private List<string> _modeSelectGroup = new List<string> { "1", "2", "3" };

        // 模组数量（逻辑属性）
        [ObservableProperty] private int _modules = 3;

        // 钩子：当 Modules 改变时重新生成下拉列表
        partial void OnModulesChanged(int value)
        {
            ModeSelectGroup = Enumerable
                .Range(1, value)
                .Select(i => $"模组{i}")
                .ToList();
        }

        #endregion

        // -----------------------------------------------------------------------
        // 图表属性 (Chart Properties)
        // -----------------------------------------------------------------------
        #region Charts

        [ObservableProperty]
        private ISeries[] _series =
        {
            new ColumnSeries<double>
            {
                Fill = new SolidColorPaint(SKColors.Red),
                ScalesYAt = 0,
                Name = "Roger",
                Values = new double[] { 20, 10, 30, 50, 30, 40 },
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
            },
            new ColumnSeries<double>
            {
                Fill = new SolidColorPaint(SKColors.Aqua),
                ScalesYAt = 1,
                Name = "Susan",
                Values = new double[] { 1, 2, 3, 4, 5, 6 },
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
            }
        };

        [ObservableProperty]
        private Axis[] _xAxes =
        {
            new Axis
            {
                Labels = new[] { "上料机1", "上料机2", "上翻转台", "下料机1", "下料机2", "下翻转台" },
                LabelsPaint = new SolidColorPaint(SKColors.White),
                TextSize = 15,
                IsVisible = true
            }
        };

        [ObservableProperty]
        private Axis[] _yAxes =
        {
            new Axis
            {
                MinLimit = 0,
                LabelsPaint = new SolidColorPaint(SKColors.Red),
                TextSize = 15,
                Position = LiveChartsCore.Measure.AxisPosition.Start,
                SeparatorsPaint = new SolidColorPaint(SKColors.White)
            },
            new Axis
            {
                MinLimit = 0,
                Position = LiveChartsCore.Measure.AxisPosition.End,
                LabelsPaint = new SolidColorPaint(SKColors.Aqua),
                TextSize = 15,
                SeparatorsPaint = new SolidColorPaint(SKColors.White)
            }
        };

        // 饼图
        [ObservableProperty] private ObservableCollection<ISeries> _upMyPieSeries = new();
        [ObservableProperty] private ObservableCollection<ISeries> _dnMyPieSeries = new();

        #endregion

        // -----------------------------------------------------------------------
        // 表格数据属性 (Table Data Properties)
        // -----------------------------------------------------------------------
        #region Table Data

        // 产品生产信息表 (Columns 这种通常只读，也可以不做 ObservableProperty，保留原样即可)
        public ObservableCollection<DataGridColumn> ProductInfoColumns { get; } = new ObservableCollection<DataGridColumn>
        {
            new DataGridTextColumn { Header = "项目号", Binding = new Binding("ProjectId"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "原料类别", Binding = new Binding("MaterialType"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "产品类别", Binding = new Binding("AnodeType"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "上料机A", Binding = new Binding("UpFeeder1"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "上料机B", Binding = new Binding("UpFeeder2"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "上料合计", Binding = new Binding("UpTotalFeederOutput"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "上翻转台", Binding = new Binding("UpTurnTable"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
        };

        [ObservableProperty]
        private string _twoDataTableWithHeaderTitle = "产品生产信息表";

        public ObservableCollection<DataGridColumn> ProductInfoColumns_Down { get; } = new ObservableCollection<DataGridColumn>
        {
            new DataGridTextColumn { Header = "项目号", Binding = new Binding("ProjectId"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "原料类别", Binding = new Binding("MaterialType"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "阳极类型", Binding = new Binding("AnodeType"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "下料机A", Binding = new Binding("DnFeeder1"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "下料机B", Binding = new Binding("DnFeeder2"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "下料合计", Binding = new Binding("DnTotalFeederOutput"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "下翻转台", Binding = new Binding("DnTurnTable"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
        };

        [ObservableProperty]
        private ObservableCollection<ProductInfoTable> _productInfoData = new ObservableCollection<ProductInfoTable>
        {
            new ProductInfoTable { ProjectId = "CY50132", MaterialType = "UACJ", AnodeType = "一阳", UpFeeder1 = 120, UpFeeder2 = 110, UpTotalFeederOutput = 230, UpTurnTable = 5 },
        };

        [ObservableProperty]
        private ObservableCollection<ProductInfoTable> _productInfoData_Down = new ObservableCollection<ProductInfoTable>
        {
            new ProductInfoTable { ProjectId = "CY50132", MaterialType = "UACJ", AnodeType = "一阳", UpFeeder1 = 120, UpFeeder2 = 110, UpTotalFeederOutput = 230, UpTurnTable = 5 },
        };

        // 设备效能表
        public ObservableCollection<DataGridColumn> ProductEfficiencyColumns { get; } = new ObservableCollection<DataGridColumn>
        {
            new DataGridTextColumn { Header = "设备名称", Binding = new Binding("DeviceName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "扫码NG", Binding = new Binding("ScanNG"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "系统反馈NG", Binding = new Binding("SystemNG"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "故障次数", Binding = new Binding("FailureCount"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "故障时间(min)", Binding = new Binding("FailureTime"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "待机时间(min)", Binding = new Binding("IdleTime"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "上挂率", Binding = new Binding("MountRate"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "稼动率", Binding = new Binding("UtilizationRate"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) }
        };

        [ObservableProperty]
        private ObservableCollection<ProductionEfficiencyTable> _productEfficiencyData = new ObservableCollection<ProductionEfficiencyTable>
        {
            new ProductionEfficiencyTable { DeviceName = "上料机1", ScanNG = 0, SystemNG = 0, FailureCount = 0, FailureTime = 0, IdleTime = 0, MountRate = "100.00%", UtilizationRate = "0.00%" },
            new ProductionEfficiencyTable { DeviceName = "上料机2", ScanNG = 0, SystemNG = 0, FailureCount = 0, FailureTime = 0, IdleTime = 0, MountRate = "100.00%", UtilizationRate = "0.00%" },
            new ProductionEfficiencyTable { DeviceName = "上挂翻转台", ScanNG = 0, SystemNG = 34, FailureCount = 0, FailureTime = 38, IdleTime = 7, MountRate = "100.00%", UtilizationRate = "98.91%" },
            new ProductionEfficiencyTable { DeviceName = "下挂翻转台", ScanNG = 0, SystemNG = 20, FailureCount = 28, FailureTime = 19, IdleTime = 0, MountRate = "100.00%", UtilizationRate = "99.54%" },
            new ProductionEfficiencyTable { DeviceName = "下料机1", ScanNG = 0, SystemNG = 82, FailureCount = 0, FailureTime = 0, IdleTime = 0, MountRate = "100.00%", UtilizationRate = "100.00%" },
            new ProductionEfficiencyTable { DeviceName = "下料机2", ScanNG = 0, SystemNG = 0, FailureCount = 0, FailureTime = 0, IdleTime = 0, MountRate = "100.00%", UtilizationRate = "100.00%" },
        };

        #endregion

        // -----------------------------------------------------------------------
        // 构造函数
        // -----------------------------------------------------------------------
        public ViewBViewModel(IConfigService configService)
        {
            _configService = configService;
            InitPieData();
            // 启动后台任务监控全局 Modules 数量
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);
                    if (Global.Modules != Modules)
                    {
                        // 触发 OnModulesChanged
                        Modules = Global.Modules;
                    }
                }
            });
        }

        // -----------------------------------------------------------------------
        // 导航 (Navigation)
        // -----------------------------------------------------------------------
        public void OnNavigatedTo(NavigationContext context)
        {
            Start = Global.GetCurrentClassTime().Start;
            if (QueryCommand.CanExecute(null))
            {
                QueryCommand.Execute(null);
            }
        }

        public void OnNavigatedFrom(NavigationContext context) { }
        public bool IsNavigationTarget(NavigationContext context) => true;

        // -----------------------------------------------------------------------
        // 命令 (Commands)
        // -----------------------------------------------------------------------

        [RelayCommand]
        private async Task ExportAsync()
        {
            try
            {
                Global.LoadingManager.StartLoading();
                ExportToExcelWithDialog(ProductInfoData, ProductInfoData_Down, ProductEfficiencyData);
            }
            finally
            {
                await Task.Delay(200);
                Global.LoadingManager.StopLoading();
            }
        }

        [RelayCommand]
        private async Task QueryAsync()
        {
        }

        // -----------------------------------------------------------------------
        // 业务逻辑与辅助方法 (Logic & Helpers)
        // -----------------------------------------------------------------------

        public void InitPieData()
        {
            Update.UpdatePieData(UpMyPieSeries, new[] { 1, 1, 1, 1, 1 }, new[] { "Maria", "Susan", "Charles", "Fiona", "George" });
            Update.UpdatePieData(DnMyPieSeries, new[] { 1, 1, 1, 1, 2 }, new[] { "Maria", "Susan", "Charles", "Fiona", "George" });
        }

        public void GetNewColumTableData(ObservableCollection<ProductionEfficiencyTable> newEfficiencyData, out ColumnSeries<double> columnSeries1, out ColumnSeries<double> columnSeries2)
        {
            var deviceOrder = new[] { "上料机1", "上料机2", "上翻转台", "下料机1", "下料机2", "下翻转台" };
            var list = newEfficiencyData.OrderBy(e => Array.IndexOf(deviceOrder, e.DeviceName)).Take(6).ToList();

            var values1 = list.Select(e => (double)e.FailureTime).ToArray();
            columnSeries1 = new ColumnSeries<double>
            {
                Fill = new SolidColorPaint(SKColors.Red),
                Name = "故障时间 (分钟)",
                ScalesYAt = 0,
                Values = values1,
                DataLabelsPaint = new SolidColorPaint(SKColors.Red),
            };

            var values2 = list.Select(e => (double)e.FailureCount).ToArray();
            columnSeries2 = new ColumnSeries<double>
            {
                Fill = new SolidColorPaint(SKColors.Aqua),
                Name = "故障次数",
                ScalesYAt = 1,
                Values = values2,
                DataLabelsPaint = new SolidColorPaint(SKColors.Aqua),
            };
        }

            public void UpdateSeries(ColumnSeries<double> seriesLeft, ColumnSeries<double> seriesRight)
        {
            if (seriesLeft == null || seriesRight == null)
                throw new ArgumentNullException("seriesLeft / seriesRight 不能为 null");
            Series = new ISeries[] { seriesLeft, seriesRight };
        }

        public void UpdateXLabels(DateTime start, DateTime end)
        {
            if (XAxes == null || XAxes.Length == 0) return;

            int startHour = start.Hour;
            int endHour = end.Hour;
            if (end < start) endHour += 24;

            var labels = new List<string>();
            for (int hour = startHour; hour <= endHour; hour++)
            {
                int normalizedHour = hour % 24;
                labels.Add(normalizedHour.ToString());
            }

            XAxes[0].Labels = labels.ToArray();
        }

        [RequireRole(Role.Admin)]
        public static void ExportToExcelWithDialog(
            ObservableCollection<ProductInfoTable> productInfoDataUp,
            ObservableCollection<ProductInfoTable> productInfoDataDn,
            ObservableCollection<ProductionEfficiencyTable> efficiencyData)
        {
            var dialog = new SaveFileDialog
            {
                Title = "选择导出路径",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = "生产数据导出.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    // (导出逻辑保持不变，略)
                    var sheetUp = workbook.Worksheets.Add("上料生产信息表");
                    // ... Header & Loop
                    sheetUp.Columns().AdjustToContents();

                    var sheetDn = workbook.Worksheets.Add("下料生产信息表");
                    // ... Header & Loop
                    sheetDn.Columns().AdjustToContents();

                    var sheetEff = workbook.Worksheets.Add("设备效能数据表");
                    // ... Header & Loop
                    sheetEff.Columns().AdjustToContents();

                    workbook.SaveAs(dialog.FileName);
                }
            }
        }
    }

    // Model 也建议改造，虽非强制
    public partial class ProductInfoTable : ObservableObject
    {
        [ObservableProperty] private string _projectId;
        [ObservableProperty] private string _materialType;
        [ObservableProperty] private string _anodeType;
        [ObservableProperty] private int _upFeeder1;
        [ObservableProperty] private int _upFeeder2;
        [ObservableProperty] private int _upTotalFeederOutput;
        [ObservableProperty] private int _upTurnTable;
        [ObservableProperty] private int _dnFeeder1;
        [ObservableProperty] private int _dnFeeder2;
        [ObservableProperty] private int _dnTotalFeederOutput;
        [ObservableProperty] private int _dnTurnTable;
    }

    public partial class ProductionEfficiencyTable : ObservableObject
    {
        [ObservableProperty] private string _deviceName;
        [ObservableProperty] private int _scanNG;
        [ObservableProperty] private int _systemNG;
        [ObservableProperty] private int _failureCount;
        [ObservableProperty] private int _failureTime;
        [ObservableProperty] private int _idleTime;
        [ObservableProperty] private string _mountRate;
        [ObservableProperty] private string _utilizationRate;
    }
}