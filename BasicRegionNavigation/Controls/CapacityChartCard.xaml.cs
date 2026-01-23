using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Drawing.Charts;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq; // 必须引用 Linq 以使用 ToList()
using System.Windows;
using System.Windows.Controls;

namespace BasicRegionNavigation.Controls
{
    public partial class CapacityChartCard : UserControl
    {
        public CapacityChartCard()
        {
            InitializeComponent();
            ChartYAxes = new Axis[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                    SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50))
                }
            };
        }

        #region 公共输入属性

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(CapacityChartCard), new PropertyMetadata("图表标题"));

        public IList<string> Labels
        {
            get => (IList<string>)GetValue(LabelsProperty);
            set => SetValue(LabelsProperty, value);
        }
        public static readonly DependencyProperty LabelsProperty =
            DependencyProperty.Register(nameof(Labels), typeof(IList<string>), typeof(CapacityChartCard),
                new PropertyMetadata(null, OnDataOrLabelsChanged));

        // 保持 IList<int> 以便 XAML 绑定数组
        public IList<int> Values
        {
            get => (IList<int>)GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }
        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register(nameof(Values), typeof(IList<int>), typeof(CapacityChartCard),
                new PropertyMetadata(null, OnDataOrLabelsChanged));

        #endregion

        #region 内部绑定属性

        public ISeries[] ChartSeries
        {
            get => (ISeries[])GetValue(ChartSeriesProperty);
            private set => SetValue(ChartSeriesProperty, value);
        }
        public static readonly DependencyProperty ChartSeriesProperty =
            DependencyProperty.Register(nameof(ChartSeries), typeof(ISeries[]), typeof(CapacityChartCard), new PropertyMetadata(null));

        public Axis[] ChartXAxes
        {
            get => (Axis[])GetValue(ChartXAxesProperty);
            private set => SetValue(ChartXAxesProperty, value);
        }
        public static readonly DependencyProperty ChartXAxesProperty =
            DependencyProperty.Register(nameof(ChartXAxes), typeof(Axis[]), typeof(CapacityChartCard), new PropertyMetadata(null));

        public Axis[] ChartYAxes
        {
            get => (Axis[])GetValue(ChartYAxesProperty);
            private set => SetValue(ChartYAxesProperty, value);
        }
        public static readonly DependencyProperty ChartYAxesProperty =
            DependencyProperty.Register(nameof(ChartYAxes), typeof(Axis[]), typeof(CapacityChartCard), new PropertyMetadata(null));

        #endregion

        #region 逻辑处理

        private static void OnDataOrLabelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CapacityChartCard control)
            {
                control.UpdateChart();
            }
        }

        private void UpdateChart()
        {
            if (Values == null) return;

            var chartValues = Values as IReadOnlyCollection<int> ?? Values.ToList();

            ChartSeries = new ISeries[]
            {
        new ColumnSeries<int>
        {
            Values = chartValues,
            Name = "产能",
            Fill = new SolidColorPaint(SKColors.CornflowerBlue),
            Stroke = null,
            Padding = 2,

            // --- 新增功能：在柱子上显示数值 ---
            // 1. 定义绘制文本的画笔 (颜色设为白色或适合背景的颜色)
            DataLabelsPaint = new SolidColorPaint(SKColors.White), 
            // 2. 设置位置 (Top 表示在柱子顶部，Middle 在中间，Bottom 在底部)
            DataLabelsPosition =LiveChartsCore.Measure.DataLabelsPosition.Top,
            // 3. (可选) 设置字体大小
            DataLabelsSize = 12,
            // 4. (可选) 格式化文本 (例如 "{point.y}" 是默认值)
            DataLabelsFormatter = point => point.Model.ToString()
        }
            };

            ChartXAxes = new Axis[]
            {
        new Axis
        {
            Labels = Labels,
            LabelsPaint = new SolidColorPaint(SKColors.White),
            LabelsRotation = 0,
            SeparatorsPaint = null,

            // --- 新增功能：强制显示所有 X 轴标签 ---
            // 1. 设置最小步长为 1，代表每个数据点之间间隔 1
            MinStep = 1, 
            // 2. 强制使用最小步长 (防止图表自动隐藏拥挤的标签)
            ForceStepToMin = true
        }
            };
        }
        #endregion
    }
}