using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Services
{
    public interface IFlipperHourlyCapacityService
    {
        /// <summary>
        /// 处理并存储产能相关数据
        /// </summary>
        /// <param name="plcName">PLC 枚举标识</param>
        /// <param name="data">PLC 传来的数据字典</param>
        Task ProcessFlipperHourlyDataAsync(string plcName, Dictionary<string, object>? data);
        event Action<string, string, object> OnModuleDataChanged;
        Task QueryAndBroadcastAsync(string deviceName, DateTime start, DateTime end);
    }
    public class FlipperHourlyCapacityService : IFlipperHourlyCapacityService
    {
        private readonly ISqlSugarClient _db;
        public event Action<string, string, object> OnModuleDataChanged;

        public FlipperHourlyCapacityService(ISqlSugarClient db)
        {
            _db = db;
        }
        public async Task QueryAndBroadcastAsync(string deviceName, DateTime start, DateTime end)
        {
            // 1. 从数据库查数据 (SqlSugar)
            // 假设我们要查 "按小时" 的产能统计
            var list = await _db.Queryable<FlipperHourlyCapacityRecord>()
                .Where(x => x.DeviceName == deviceName && x.CreateTime >= start && x.CreateTime <= end)
                .OrderBy(x => x.CreateTime)
                .ToListAsync();

            // 2. 数据处理：我们需要填满时间轴，防止中间有空缺
            // 假设我们要生成最近 24 小时的数据
            var fullData = new double[24]; // 假设显示24个点
                                           // 这里需要根据你的业务逻辑，把 list 映射到 fullData 数组里
                                           // 简单示例：直接取前24个，或者根据时间对齐

            // 示例：简单映射 (实际项目建议用时间对齐算法)
            var values = list.Select(x => (double)(x.HourlyCapacity ?? 0)).ToArray();

            // 3. 打包 DTO
            var dto = new ColumnChartDto
            {
                IsUp = deviceName.Contains("Up"), // 简单判断是上还是下
                Values = values,
                StartTime = start,
                EndTime = end,
                TimeUnit = Unit.时
            };

            // 4. 发送广播 (复用之前的事件机制)
            // 参数1: 模组ID (需要从 deviceName 解析，比如 "Module_01")
            // 参数2: 数据类型 "Column"
            // 参数3: 数据包 dto
            string moduleId = ParseModuleId(deviceName);
            OnModuleDataChanged?.Invoke(moduleId, "Column", dto);
        }

        private string ParseModuleId(string deviceName)
        {
            // 根据你的命名规则解析，例如 "Module_01_Up" -> "Module_01"
            // 这里简单返回
            return deviceName.Split('_')[0] + "_" + deviceName.Split('_')[1];
        }
        public async Task ProcessFlipperHourlyDataAsync(string plcName, Dictionary<string, object> data)
        {
            if (data == null) return;

            // 1. 将 Dictionary 转为实体类 (假设 Key 名 = 属性名)
            // SqlSugar 并不直接提供 Dict To Entity，但我们可以用简单的反射
            var record = new FlipperHourlyCapacityRecord
            {
                DeviceName = plcName.ToString(),
                CreateTime = DateTime.Now
            };

            // 遍历字典，自动匹配属性赋值
            foreach (var item in data)
            {
                var prop = typeof(FlipperHourlyCapacityRecord).GetProperty(item.Key);
                if (prop != null && item.Value != null)
                {
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    var val = Convert.ChangeType(item.Value, targetType);
                    prop.SetValue(record, val);
                }
            }

            await _db.Insertable(record).ExecuteCommandAsync();
        }
    }

    [SugarTable("FlipperHourlyCapacity_Record")]
    public class FlipperHourlyCapacityRecord
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long Id { get; set; }

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
