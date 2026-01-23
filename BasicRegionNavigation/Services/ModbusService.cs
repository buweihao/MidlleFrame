using MyModbus; // 引用包含 ModbusKeyHelper 的命名空间
using System;
using System.Collections.Generic;
using System.Linq;

namespace BasicRegionNavigation.Services
{
    public interface IModbusService
    {
        event Action<string, ModuleDataCategory, object> OnModuleDataChanged;

        void SubscribeDynamicGroup(string moduleId, ModuleDataCategory category, string locationPrefix, string[] fields);

        // 产能订阅也可以保留，用于简单的连续地址读取
        void SubscribeCapacity(string moduleId, ModuleDataCategory category, string tagInfix, int startIndex, int count);
    }

    public class ModbusService : IModbusService
    {
        private readonly DataCollectionEngine _engine;
        private readonly DataBus _bus;
        private readonly List<Device> _devices;

        public event Action<bool> OnError;
        public event Action<string, ModuleDataCategory, object> OnModuleDataChanged;

        public ModbusService(DataCollectionEngine engine, DataBus bus, List<Device> devices)
        {
            _engine = engine;
            _bus = bus;
            _devices = devices;

            _bus.OnDataChanged += data =>
            {
                if (!data.IsQualityGood) OnError?.Invoke(true);
            };
        }

        /// <summary>
        /// 通用订阅方法：自动映射点位名，并返回字典数据
        /// </summary>
        public void SubscribeDynamicGroup(string moduleId, ModuleDataCategory category, string locationPrefix, string[] fields)
        {
            // 1. 生成点位名
            var tags = fields.Select(field =>
            {
                // [修改点] 使用 ModbusKeyHelper.Build 统一生成
                // 自动处理 moduleId(设备ID) + locationPrefix(分组) + field(字段名) 的拼接
                // 例如: Build("1", "IO", "FeedLift") -> "1_IO_FeedLift"
                // 例如: Build("1", null, "Status")   -> "1_Status"
                return ModbusKeyHelper.Build(moduleId, locationPrefix, field);

            }).ToArray();

            // 2. 订阅
            _bus.Subscribe<int>(tags, (values, isGood) =>
            {
                if (isGood && values != null && values.Length == fields.Length)
                {
                    var dataPayload = new Dictionary<string, int>();

                    for (int i = 0; i < fields.Length; i++)
                    {
                        // 字典的 Key 依然保持纯净的 field 名，方便 UI 绑定
                        dataPayload[fields[i]] = values[i];
                    }

                    OnModuleDataChanged?.Invoke(moduleId, category, dataPayload);
                }
            });
        }

        public void SubscribeCapacity(string moduleId, ModuleDataCategory category, string tagInfix, int startIndex, int count)
        {
            var tags = Enumerable.Range(startIndex, count)
                .Select(i =>
                {
                    // [修改点] 业务逻辑: 生成具体名称，如 "Counter01" 或 "Counter_01" (取决于 tagInfix 是否带下划线)
                    // 原逻辑直接拼接，这里保持一致
                    string specificName = $"{tagInfix}{i:D2}";

                    // [修改点] 库逻辑: 调用 ModbusKeyHelper 加上设备前缀
                    // 因为 specificName 已经是具体的变量名了，所以 group 传 null
                    // 结果: "1" + "_" + "Counter_01" -> "1_Counter_01"
                    return ModbusKeyHelper.Build(moduleId, null, specificName);
                })
                .ToArray();

            _bus.Subscribe<int>(tags, (values, isGood) =>
            {
                if (isGood)
                {
                    OnModuleDataChanged?.Invoke(moduleId, category, values);
                }
            });
        }
    }

    public enum ModuleDataCategory
    {
        Status,      // 基础IO/气缸状态 (Brush)
        Capacity,    // 产量数据 (int[])
        ProductInfo, // 产品详细信息 (ExpandoObject)
        Warning,     // 报警信息
        PieInfo,     // 饼图统计数据
        ColumnInfo,    // 柱状图统计数据
        UpProductInfo,
        DnProductInfo,
        UpPieInfo,
        DnPieInfo,
        UpColumnSeries,
        DnColumnSeries,
        ChartAxis,
        WarningInfo
    }
}