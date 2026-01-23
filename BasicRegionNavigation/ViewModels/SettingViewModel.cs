using BasicRegionNavigation;
using CommunityToolkit.Mvvm.ComponentModel; // 核心
using CommunityToolkit.Mvvm.Input;        // 核心
using Core;
using DocumentFormat.OpenXml.Spreadsheet;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Prism.Events; // 假设 IEventAggregator 来自 Prism
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = HandyControl.Controls.MessageBox;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace BasicRegionNavigation.ViewModels
{
    // 修改 1: partial + ObservableObject
    public partial class SettingViewModel : ObservableObject
    {
        // 静态资源列表保持不变 (建议设为 readonly)
        private static readonly List<string> projectCodes = new List<string> { "CY50132", "CY50146", "CY50168", "CY50375", "-" };
        private static readonly List<string> productTypes = new List<string> { "T处理", "-" };
        private static readonly List<string> materialTypes = new List<string> { "金桥", "福蓉", "-" };

        private string TurnProductNormal = "10";
        private string TurnProductAtivate = "11";

        private readonly IEventAggregator _ea;
        private int Modules = Global.Modules;

        // =================================================================================
        // 重要修改建议：
        // 原代码使用了 static TableRowViewModel，这意味着所有 SettingViewModel 实例共享同一组数据。
        // 如果这不符合你的单例设计意图，应该去掉 static。这里为了稳妥起见，我将其改为实例字段。
        // =================================================================================

        private readonly TableRowViewModel tableRowViewModel1 = CreateDefaultRow("模组1上挂");
        private readonly TableRowViewModel tableRowViewModel1_ = CreateDefaultRow("模组1下挂");
        private readonly TableRowViewModel tableRowViewModel2 = CreateDefaultRow("模组2上挂");
        private readonly TableRowViewModel tableRowViewModel2_ = CreateDefaultRow("模组2下挂");
        private readonly TableRowViewModel tableRowViewModel3 = CreateDefaultRow("模组3上挂");
        private readonly TableRowViewModel tableRowViewModel3_ = CreateDefaultRow("模组3下挂");
        private readonly TableRowViewModel tableRowViewModel4 = CreateDefaultRow("模组4上挂");
        private readonly TableRowViewModel tableRowViewModel4_ = CreateDefaultRow("模组4下挂");
        private readonly TableRowViewModel tableRowViewModel5 = CreateDefaultRow("模组5上挂");
        private readonly TableRowViewModel tableRowViewModel5_ = CreateDefaultRow("模组5下挂");
        private readonly TableRowViewModel tableRowViewModel6 = CreateDefaultRow("模组6上挂");
        private readonly TableRowViewModel tableRowViewModel6_ = CreateDefaultRow("模组6下挂");

        // 辅助方法：统一创建行对象，避免大量重复代码
        private static TableRowViewModel CreateDefaultRow(string moduleName)
        {
            return new TableRowViewModel
            {
                ModuleName = moduleName,
                ProjectCodes = projectCodes,
                SelectedProject = "CY50132",
                ProductTypes = productTypes,
                SelectedProductType = "DH",
                SelectedAnodeType = "一阳",
                SelectedProductColor = "银色",
                MaterialTypes = materialTypes,
                SelectedMaterialType = "UACJ",
                SelectedTimes = "-",
                SelectBatchNumber = "1"
            };
        }

        [ObservableProperty]
        private ObservableCollection<TableRowViewModel> _rowItems = new ObservableCollection<TableRowViewModel>();

        public SettingViewModel(IEventAggregator ea)
        {
            _ea = ea;

            // 初始化命令绑定
            // 注意：这里使用 RelayCommand 是为了绑定到具体的行对象上
            BindCommands(tableRowViewModel1, SettingModoulNum.上, 1);
            BindCommands(tableRowViewModel1_, SettingModoulNum.下, 1);
            BindCommands(tableRowViewModel2, SettingModoulNum.上, 2);
            BindCommands(tableRowViewModel2_, SettingModoulNum.下, 2);
            BindCommands(tableRowViewModel3, SettingModoulNum.上, 3);
            BindCommands(tableRowViewModel3_, SettingModoulNum.下, 3);
            BindCommands(tableRowViewModel4, SettingModoulNum.上, 4);
            BindCommands(tableRowViewModel4_, SettingModoulNum.下, 4);
            BindCommands(tableRowViewModel5, SettingModoulNum.上, 5);
            BindCommands(tableRowViewModel5_, SettingModoulNum.下, 5);
            BindCommands(tableRowViewModel6, SettingModoulNum.上, 6);
            BindCommands(tableRowViewModel6_, SettingModoulNum.下, 6);

            InitRowItems();

            // 监控模块数量变化
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);
                    if (Global.Modules != Modules)
                    {
                        Modules = Global.Modules;
                        InitRowItems();
                    }
                }
            });
        }

        private void BindCommands(TableRowViewModel vm, SettingModoulNum upOrDown, int moduleNum)
        {
            // 使用 RelayCommand 包装异步方法
            vm.ConfirmCommand = new RelayCommand(async () =>
                await ExecuteConfirmAsync(vm, upOrDown, moduleNum));

            vm.SettingCommand = new RelayCommand(() =>
                TableRowViewModelDefault(vm));
        }

        public void NotifyChanges(TableRowViewModel newValue, SettingModoulNum settingModoulNum, int i)
        {
            Core.TableRowViewModel tableRowViewModel = new Core.TableRowViewModel
            {
                ModuleNum = i,
                UporDn = settingModoulNum,
                ProjectCodes = newValue.SelectedProject,
                AnodeTypes = newValue.SelectedAnodeType,
                ProductColors = newValue.SelectedProductColor,
                MaterialTypes = newValue.SelectedMaterialType,
            };

            _ea.GetEvent<MyDataUpdatedSettingEvent>().Publish(tableRowViewModel);
        }

        [RequireRole(Role.Admin)]
        private async Task ExecuteConfirmAsync(
            TableRowViewModel vm,
            SettingModoulNum upOrDown,
            int moduleNum,
            int delayMs = 200)
        {
            SendSetting2PLC(moduleNum,vm, upOrDown.ToString());


            // 这里保留你的业务逻辑注释...
            // ConfirmRowSettings(vm)...
            // SendSetting2PLC...
            // TurnProduct...
        }

        public static bool ConfirmRowSettings(TableRowViewModel vm)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(vm.ModuleName)) missing.Add("模块");
            if (string.IsNullOrWhiteSpace(vm.SelectedProject)) missing.Add("项目");
            // ... 其他校验

            if (missing.Count > 0)
            {
                MessageBox.Show("以下必填项未设置：\n- " + string.Join("\n- ", missing), "缺少设定", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var sb = new StringBuilder();
            sb.AppendLine("请确认以下设定：");
            sb.AppendLine($"{"模块",-19}：{vm.ModuleName}");
            // ...

            return MessageBox.Show(sb.ToString(), "确认设定", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        }

        public void TableRowViewModelDefault(TableRowViewModel tableRowViewModel)
        {
            tableRowViewModel.SelectedProject = "CY50132";
            tableRowViewModel.SelectedProductType = "DH";
            tableRowViewModel.SelectedAnodeType = "一阳";
            tableRowViewModel.SelectedProductColor = "银色";
            tableRowViewModel.SelectedMaterialType = "UACJ";
            tableRowViewModel.SelectBatchNumber = "1";
            tableRowViewModel.SelectedTimes = "-";
        }

        public async Task SendSetting2PLC(int num, TableRowViewModel tableRowViewModel, string UpDn)
        {
            //这里需要通过反射取得变量名，暂时都给模组1的PLC
            var projectNum = tableRowViewModel.SelectedProject;
            try
            {
               await Task.Delay(1000);

                Application.Current.Dispatcher.Invoke(() =>
                {

                    MessageBox.Show($"已设置项目号{projectNum}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);


                });

                }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<bool> TurnProduct(int num, string upDn, TimeSpan timeout, int pollInterval = 200)
        {
            return await Task.FromResult(true); // 占位符
        }

        private void InitRowItems()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RowItems.Clear();
                // 简化逻辑：直接用循环
                var allRows = new[]
                {
                    (tableRowViewModel1, tableRowViewModel1_),
                    (tableRowViewModel2, tableRowViewModel2_),
                    (tableRowViewModel3, tableRowViewModel3_),
                    (tableRowViewModel4, tableRowViewModel4_),
                    (tableRowViewModel5, tableRowViewModel5_),
                    (tableRowViewModel6, tableRowViewModel6_)
                };

                for (int i = 0; i < Global.Modules && i < allRows.Length; i++)
                {
                    RowItems.Add(allRows[i].Item1);
                    RowItems.Add(allRows[i].Item2);
                }
            });
        }
    }

    // 修改 2: TableRowViewModel 重构
    public partial class TableRowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _moduleName;

        [ObservableProperty]
        private List<string> _projectCodes;

        [ObservableProperty]
        private string _selectedProject;

        [ObservableProperty]
        private List<string> _productTypes;

        [ObservableProperty]
        private string _selectedProductType;

        [ObservableProperty]
        private List<string> _anodeTypes;

        [ObservableProperty]
        private string _selectedAnodeType;

        [ObservableProperty]
        private List<string> _productColors;

        [ObservableProperty]
        private string _selectedProductColor;

        [ObservableProperty]
        private List<string> _materialTypes;

        [ObservableProperty]
        private string _selectedMaterialType;

        [ObservableProperty]
        private List<string> _batchNumber;

        [ObservableProperty]
        private string _selectBatchNumber;

        [ObservableProperty]
        private List<string> _times;

        [ObservableProperty]
        private string _selectedTimes;

        // Command 依然作为属性暴露，因为它们是在 SettingViewModel 里被动态赋值的
        // 这种模式下，Source Generator 的 [RelayCommand] 不太好用，因为逻辑在外部
        // 所以这里保留 ICommand 属性定义
        private ICommand _confirmCommand;
        public ICommand ConfirmCommand
        {
            get => _confirmCommand;
            set => SetProperty(ref _confirmCommand, value);
        }

        private ICommand _settingCommand;
        public ICommand SettingCommand
        {
            get => _settingCommand;
            set => SetProperty(ref _settingCommand, value);
        }
    }
}