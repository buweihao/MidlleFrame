using BasicRegionNavigation.Helper;
using BasicRegionNavigation.ViewModels;
using DocumentFormat.OpenXml.Spreadsheet;
using My.Services;
using MyModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Services
{
    public interface IMiddleFrameBusinessServices
    {
        //中框阳极上下挂的业务内容
        //中框每个模组拥有3个PLC:上料机A、上料机B、翻转台

        //一、上料信息采集,这个采集是根据某个触发点从而触发的一个任务，然后将数据存入数据库
        void ProductCollectionMissionStart();

        //二、两个上料机的小时数据采集，需要在每个整点的最后时刻将某个寄存器的数据作为小时产能数据存入数据库,并且伴随部分其他的小时数据
        void FeedersHourlyDataCollectionMissionStart();

        //三、翻转台的小时数据采集，需要在每个整点的最后时刻将某个寄存器的数据作为小时产能数据存入数据库,并且伴随部分其他的小时数据
        void FlipperHourlyDataCollectionMissionStart();

        //四、下发设置
        void SendSetting(int num, TableRowViewModel tableRowViewModel);

        //五、主动触发转产
        public void ChangeoverTrigger(int num);


    }
    public class MiddleFrameBusinessServices : IMiddleFrameBusinessServices
    {
        private readonly IFlipperHourlyCapacityService _flipperHourlyCapacityService;
        private readonly IUpDropHourlyCapacityService _upDropHourlyCapacityService;
        private readonly DataBus _bus;
        private readonly IProductionService _productionService;
        private readonly DataCollectionEngine _engine;

        public MiddleFrameBusinessServices(DataCollectionEngine engine, DataBus bus, IProductionService productionService, IFlipperHourlyCapacityService flipperHourlyCapacityService, IUpDropHourlyCapacityService upDropHourlyCapacityService)
        {
            //构造函数
            _flipperHourlyCapacityService = flipperHourlyCapacityService;
            _upDropHourlyCapacityService = upDropHourlyCapacityService;
            _bus = bus;
            _productionService = productionService;
            _engine = engine;
        }


        #region  业务一、产品信息采集
        private const string ModuleId = "1"; // 建议放入配置或作为类属性
        private const string TriggerSuffix = "ReadTrigger"; // 统一的后缀，防止手写错误
        public void ProductCollectionMissionStart()
        {
            // --- 供料机 (Feeders) ---
            // 对应 CSV: PLC_Feeder_A_ReadTrigger -> 运行时: 1_PLC_Feeder_A_ReadTrigger
            SubscribeToDevice("PLC_Feeder_A", TriggerSuffix, HandleUpLoad_Trigger);
            SubscribeToDevice("PLC_Feeder_B", TriggerSuffix, HandleUpLoad_Trigger);

            // --- 翻转台 (Flipper) ---
            // 翻转台比较特殊，CSV 中设备ID是 PLC_Flipper，但点位区分了 A/B 面
            // 对应 CSV: PLC_Flipper_A_ReadTrigger -> 运行时: 1_PLC_Flipper_A_ReadTrigger
            // 因此这里的后缀需要补上 "A_" 或 "B_"
            SubscribeToDevice("PLC_Flipper", $"A_{TriggerSuffix}", HandleFlipper_Trigger);
            SubscribeToDevice("PLC_Flipper", $"B_{TriggerSuffix}", HandleFlipper_Trigger);

        }
        /// <summary>
        /// 通用订阅辅助方法
        /// </summary>
        /// <param name="templateDeviceId">CSV中的原始设备ID (如 PLC_Feeder_A)</param>
        /// <param name="pointSuffix">点位后缀 (如 ReadTrigger)</param>
        /// <param name="handler">回调函数</param>
        private void SubscribeToDevice(string templateDeviceId, string pointSuffix, Action<TagData> handler)
        {
            // 1. 构造运行时的设备 ID (自动加上模组前缀)
            // 结果: "1_PLC_Feeder_A"
            string realDeviceId = ModbusKeyHelper.BuildDeviceId(ModuleId, templateDeviceId);

            // 2. 构造完整的点位名 (自动加上分隔符)
            // 结果: "1_PLC_Feeder_A_ReadTrigger"
            string finalTagName = ModbusKeyHelper.Build(realDeviceId, null, pointSuffix);

            // 3. 注册订阅
            _bus.Subscribe(finalTagName, handler);
        }


        private void HandleUpLoad_Trigger(TagData data)
        {
            //上料机这边是触发点为1时表示触发，读完回写0即可
            if (data.IsQualityGood && data.Value is System.Int16 speed && speed == 1)
            {
                //触发成功
                //去缓冲区读产品码,先要知道那个点位名
                //使用代理，传入触发点TagData，可以直接代理获取对应数据
                var flipper = new UpLoadProxy(_bus, data);

                //通过代理获取产品码
                var ProductCode = flipper.ProductCode;
                //通过代理获取所属机器名
                var BelongMechine = flipper.DeviceName;

                if (ProductCode is string)
                {
                    var contextA = StationProcessContext.Create(
                        deviceId: BelongMechine,
                        identity: (string)ProductCode,        // 传 SN
                        type: StationProcessType.Entry_Upload, // 明确指明是上料
                        data: null
                    );
                    _productionService.ProcessProductDataAsync(contextA);
                }

                //回写
                _engine.WriteTag(data.TagName, 0);
            }



        }


        private void HandleFlipper_Trigger(TagData data)
        {
            // 1. 校验触发信号：必须是 Good 且值为 11
            if (data.IsQualityGood && data.Value is short speed && speed == 11)
            {
                // 2. 创建智能代理 (自动识别是 A 面还是 B 面触发)
                var flipper = new FlipperProductProxy(_bus, data.TagName);

                // 3. 通过代理一次性获取所有上下文信息
                var fixture = flipper.FixtureCode;
                var belongMachine = flipper.DeviceName; // 例如 PLC_Flipper_A
                var projectNo = flipper.ProductProjectNo;
                var category = flipper.ProductCategory;
                var productCodes = flipper.CurrentProductCodes; // 获取列表

                // 4. 遍历所有产品码，逐个生成生产数据
                // (翻转台一次可能翻转多个产品，CSV中长度168也暗示了这一点)
                foreach (var sn in productCodes)
                {
                    if (string.IsNullOrWhiteSpace(sn)) continue;

                    // 4.1 组装扩展数据 (这里可以放想要存入数据库Json列的任何额外信息)
                    var plcData = new Dictionary<string, object>
                    {
                        { "FixtureCode", fixture },
                        { "ProjectNo", projectNo },
                        { "Category", category },
                        { "Side", flipper.IsSideA ? "A" : "B" } // 记录是哪一面
                    };

                    // 4.2 构造调用上下文
                    var context = StationProcessContext.Create(
                        deviceId: belongMachine,              // 哪个逻辑设备 (PLC_Flipper_A)
                        identity: sn,                         // 产品的 SN 码 (循环变量)
                        type: StationProcessType.Process_Flip,// 工序类型
                        data: plcData                         // 原始数据包
                    );

                    // 4.3 执行异步调用 (Fire and Forget 或 await 取决于上层调用)
                    // 注意：如果在 void 方法中调用 async，建议使用 Task.Run 或确保内部处理了异常
                    _productionService.ProcessProductDataAsync(context);
                }

                // 可选：打印日志
                //_logger.Info($"翻转台触发处理完成: 设备={belongMachine}, 数量={productCodes.Count}");
            }
        }

        #endregion

        #region 业务二、供料机小时数据采集
        public void FeedersHourlyDataCollectionMissionStart()
        {
            //小时数据采集任务只会在每小时的最后一分钟触发,可以直接从DataBus获取对应的点位数据
            //_upDropHourlyCapacityService.ProcessUpDropHourlyDataAsync();
        }

        #endregion

        #region 业务三、翻转台小时数据采集
        public void FlipperHourlyDataCollectionMissionStart()
        {
            //同样在最后一小时触发,直接从DataBus获取对应的点位数据
            //_flipperHourlyCapacityService.ProcessFlipperHourlyDataAsync();
        }
        #endregion

        #region 业务四、向供料机、翻转台下发数据
        public void SendSetting(int num, TableRowViewModel tableRowViewModel)
        {
            // num表示几号模组 (例如 1)
            string moduleId = num.ToString();

            // 1. 构造设备ID
            // 结果: 1_PLC_Feeder_A, 1_PLC_Feeder_B, 1_PLC_Flipper
            string deviceFeederA = ModbusKeyHelper.BuildDeviceId(moduleId, "PLC_Feeder_A");
            string deviceFeederB = ModbusKeyHelper.BuildDeviceId(moduleId, "PLC_Feeder_B");
            string deviceFlipper = ModbusKeyHelper.BuildDeviceId(moduleId, "PLC_Flipper");

            // 2. 下发给 供料机 A
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFeederA, null, "ProjectNo"), tableRowViewModel.ProjectCodes);
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFeederA, null, "ProductType"), tableRowViewModel.ProductTypes);
            //_engine.WriteTag(ModbusKeyHelper.Build(deviceFeederA, null, "MaterialCategory"), tableRowViewModel.MaterialCategorys);

            // 3. 下发给 供料机 B
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFeederB, null, "ProjectNo"), tableRowViewModel.ProjectCodes);
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFeederB, null, "ProductType"), tableRowViewModel.ProductTypes);
            //_engine.WriteTag(ModbusKeyHelper.Build(deviceFeederB, null, "MaterialCategory"), tableRowViewModel.MaterialCategorys);

            // 4. 下发给 翻转台 (同时写入 A 面和 B 面，确保参数同步)
            // 根据 FlipperProductProxy 逻辑，翻转台点位带有 A_ / B_ 前缀

            // Side A
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFlipper, null, "A_ProjectNo"), tableRowViewModel.ProjectCodes);
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFlipper, null, "A_ProductType"), tableRowViewModel.ProductTypes);
            //_engine.WriteTag(ModbusKeyHelper.Build(deviceFlipper, null, "A_MaterialCategory"), tableRowViewModel.MaterialCategorys);

            // Side B
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFlipper, null, "B_ProjectNo"), tableRowViewModel.ProjectCodes);
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFlipper, null, "B_ProductType"), tableRowViewModel.ProductTypes);
            //_engine.WriteTag(ModbusKeyHelper.Build(deviceFlipper, null, "B_MaterialCategory"), tableRowViewModel.MaterialCategorys);
        }

        /// <summary>
        /// 上位机主动触发转产
        /// </summary>
        /// <param name="num"></param>
        public void ChangeoverTrigger(int num)
        {
            string moduleId = num.ToString();
            string suffix = "Hourly_ChangeoverTrigger";
            short triggerVal = 1; // 写入 1 触发
            short triggerVal_ = 11; // 写入 11 触发

            // 1. 构造设备ID
            string deviceFeederA = ModbusKeyHelper.BuildDeviceId(moduleId, "PLC_Feeder_A");
            string deviceFeederB = ModbusKeyHelper.BuildDeviceId(moduleId, "PLC_Feeder_B");
            string deviceFlipper = ModbusKeyHelper.BuildDeviceId(moduleId, "PLC_Flipper");

            // 2. 写入转产信号
            // 结果示例: 1_PLC_Feeder_A_Hourly_ChangeoverTrigger
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFeederA, null, suffix), triggerVal);
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFeederB, null, suffix), triggerVal);

            // 翻转台的转产信号通常是全局的，根据注释直接拼接后缀即可
            // 结果示例: 1_PLC_Flipper_Hourly_ChangeoverTrigger
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFlipper, null, suffix), triggerVal_);//翻转台是写入11
        }
        #endregion

    }
    public class UpLoadProxy
    {
        private readonly DataBus _bus;
        private readonly string _triggerTagName;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="bus">全局数据总线，用于读取兄弟点位的值</param>
        /// <param name="triggerData">触发信号的数据包</param>
        public UpLoadProxy(DataBus bus, TagData triggerData)
        {
            _bus = bus;
            _triggerTagName = triggerData.TagName;
        }

        /// <summary>
        /// 动态获取所属机器名
        /// 逻辑：从 "PLC_Feeder_A_ReadTrigger" 解析出 "PLC_Feeder_A"
        /// </summary>
        public string DeviceName
        {
            get
            {
                // 使用 ModbusKeyHelper.GetDeviceNameFromTag (截取最后一个 '_' 之前的内容)
                // 结果示例: "PLC_Feeder_A" 或 "PLC_Flipper_A"
                return ModbusKeyHelper.GetDeviceNameFromTag(_triggerTagName);
            }
        }

        /// <summary>
        /// 动态获取同组的产品码
        /// 逻辑：将 "ReadTrigger" 替换为 "ProductCode"
        /// </summary>
        public object ProductCode
        {
            get
            {
                // 1. 计算目标点位名
                // 你的 ModbusKeyHelper.GetSibling 完美适用于此场景
                // 它会将 "PLC_Feeder_A_ReadTrigger" 变成 "PLC_Feeder_A_ProductCode"
                string targetTagName = ModbusKeyHelper.GetSibling(_triggerTagName, "ProductCode");

                // 2. 从 DataBus 缓存中直接读取该点位的最新值
                return _bus.GetValue(targetTagName);
            }
        }

    }


    public class FlipperProductProxy
    {
        private readonly DataBus _bus;
        private readonly string _prefix; // 例如: "PLC_Flipper_A" 或 "PLC_Flipper_B"

        public FlipperProductProxy(DataBus bus, string triggerTagName)
        {
            _bus = bus;
            // 核心逻辑：利用 Helper 截取触发信号的前缀
            // 输入: "PLC_Flipper_A_ReadTrigger" -> 得到: "PLC_Flipper_A"
            // 输入: "PLC_Flipper_B_ReadTrigger" -> 得到: "PLC_Flipper_B"
            _prefix = ModbusKeyHelper.GetDeviceNameFromTag(triggerTagName);
        }

        /// <summary>
        /// 获取当前逻辑设备名 (如 "PLC_Flipper_A")
        /// </summary>
        public string DeviceName => _prefix;

        /// <summary>
        /// 动态获取当前触发面的所有产品码
        /// 自动适配 A 面或 B 面
        /// </summary>
        public List<string> CurrentProductCodes
        {
            get
            {
                // 1. 拼接目标点位名: PLC_Flipper_A_ProductCode
                string tagName = $"{_prefix}_ProductCode";

                // 2. 获取数据
                object val = _bus.GetValue(tagName);

                // 3. 解析字符串列表
                // 假设 PLC 传上来的是 "SN001,SN002" 这种格式，或者就是一个长字符串
                if (val is string s && !string.IsNullOrWhiteSpace(s))
                {
                    // 兼容逗号、分号分隔，去除空白项
                    return s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                return new List<string>();
            }
        }

        // --- 以下属性利用 Expression Body 动态获取对应点位值 ---

        // 挂具码: PLC_Flipper_A_FixtureCode
        public string FixtureCode => _bus.GetValue($"{_prefix}_FixtureCode")?.ToString() ?? string.Empty;

        // 项目号: PLC_Flipper_A_ProjectNo
        public string ProductProjectNo => _bus.GetValue($"{_prefix}_ProjectNo")?.ToString() ?? string.Empty;

        // 产品类型/原料类别: PLC_Flipper_A_ProductType (或 MaterialCategory，视你具体需求而定)
        // 这里映射到 ProductType，如需 MaterialCategory 请修改后缀
        public string ProductCategory => _bus.GetValue($"{_prefix}_MaterialCategory")?.ToString() ?? string.Empty;

        // 辅助属性：获取当前是 A 面还是 B 面
        public bool IsSideA => _prefix.EndsWith("A");
    }
}
