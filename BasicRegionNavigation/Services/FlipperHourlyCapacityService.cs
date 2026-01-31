using MyDatabase;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Services
{
    /// <summary>
    /// 翻转台小时产能存储的服务接口
    /// </summary>
    public interface IFlipperHourlyCapacityService
    {
        Task ProcessFlipperHourlyDataAsync(string plcName, Dictionary<string, object>? data);
        event Action<string, string, object> OnModuleDataChanged;
        Task QueryAndBroadcastAsync(string deviceName, DateTime start, DateTime end);
    }

    /// <summary>
    /// 服务实现类
    /// </summary>
    public class FlipperHourlyCapacityService : IFlipperHourlyCapacityService
    {
        // 使用你定义的泛型仓储接口
        private readonly IRepository<FlipperHourlyCapacityRecord> _db;

        public event Action<string, string, object> OnModuleDataChanged;

        // 构造函数注入泛型仓储
        public FlipperHourlyCapacityService(IRepository<FlipperHourlyCapacityRecord> db)
        {
            _db = db;
        }

        public async Task QueryAndBroadcastAsync(string deviceName, DateTime start, DateTime end)
        {
            // 1. 从数据库查数据
            // 修改点：因为 IRepository 封装了 Queryable，我们使用 GetListAsync 获取符合条件的数据
            // 注意：你的仓储 GetListAsync 不直接支持 OrderBy，我们取回数据后在内存中排序
            var rawList = await _db.GetListAsync(x =>
                x.DeviceName == deviceName &&
                x.CreateTime >= start &&
                x.CreateTime <= end
            );

            // 内存排序 (数据量小，性能无影响)
            var list = rawList.OrderBy(x => x.CreateTime).ToList();

            // 2. 数据处理：填满时间轴 (示例逻辑保持不变)
            // 假设我们要生成最近 24 小时的数据，这里做简单映射
            var values = list.Select(x => (double)(x.HourlyCapacity ?? 0)).ToArray();

            // 3. 打包 DTO
            var dto = new ColumnChartDto
            {
                IsUp = deviceName.Contains("Up"),
                Values = values,
                StartTime = start,
                EndTime = end,
                TimeUnit = Unit.时
            };

            // 4. 发送广播
            string moduleId = ParseModuleId(deviceName);
            OnModuleDataChanged?.Invoke(moduleId, "Column", dto);
        }

        private string ParseModuleId(string deviceName)
        {
            // 简单解析逻辑
            if (string.IsNullOrEmpty(deviceName) || !deviceName.Contains("_")) return deviceName;
            var parts = deviceName.Split('_');
            if (parts.Length >= 2) return parts[0] + "_" + parts[1];
            return deviceName;
        }

        public async Task ProcessFlipperHourlyDataAsync(string plcName, Dictionary<string, object> data)
        {
            if (data == null) return;

            // 1. 将 Dictionary 转为实体类
            var record = new FlipperHourlyCapacityRecord
            {
                DeviceName = plcName, // 不需要 .ToString()，本身就是 string
                CreateTime = DateTime.Now
            };

            // 反射赋值逻辑保持不变
            foreach (var item in data)
            {
                var prop = typeof(FlipperHourlyCapacityRecord).GetProperty(item.Key);
                // 增加判断：属性存在且可写
                if (prop != null && prop.CanWrite && item.Value != null)
                {
                    try
                    {
                        // 处理 Nullable 类型转换
                        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        var val = Convert.ChangeType(item.Value, targetType);
                        prop.SetValue(record, val);
                    }
                    catch
                    {
                        // 忽略类型转换失败，防止单个字段错误导致整个记录丢失
                    }
                }
            }

            // 2. 插入数据库
            // 修改点：使用 IRepository 提供的 InsertAsync 方法
            await _db.InsertAsync(record);
        }
    }

    [SugarTable("FlipperHourlyCapacity_Record")]
    public class FlipperHourlyCapacityRecord
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }

        // --- 工序 1 (上翻转台、下翻转台) ---

        public string? DeviceName { get; set; } = "-";

        // 字符串类型初始值：设为 "-" 避免 UI 显示空白或数据库 Null 引起异常
        public string? HourlyProductTypeFlag { get; set; } = "-";
        public string? HourlyProjectNumber { get; set; } = "-";
        public string? HourlyBatch { get; set; } = "-";
        public string? HourlyAnodeType { get; set; } = "-";
        public string? HourlyMaterialCategory { get; set; } = "-";

        // 数值类型初始值：设为 -1，作为“数据未就绪”的标记
        public int? HourlyCapacity { get; set; } = -1;
        public int? HourlyStandbyTime { get; set; } = -1;
        public int? HourlyFaultTime { get; set; } = -1;

        public short? HourlyFaultCount { get; set; } = -1;
        public short? HourlyMixCount { get; set; } = -1;
        public short? HourlyScanNGCount { get; set; } = -1;
        public short? HourlySystemFeedbackCount { get; set; } = -1;

        // 时间初始值：直接给当前系统时间
        public DateTime? CreateTime { get; set; } = DateTime.Now;
    }

    public class ColumnChartDto
    {
        public bool IsUp { get; set; }          // true=上翻转, false=下翻转
        public double[] Values { get; set; }    // Y轴数值 (产能)
        public DateTime StartTime { get; set; } // 起始时间 (用于生成 X轴)
        public DateTime EndTime { get; set; }   // 结束时间
        public Unit TimeUnit { get; set; }      // 时间粒度 (时/日/月)
    }

    public enum Unit { 年, 月, 日, 时 }
}
