using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Services
{
    internal interface IUpDropHourlyCapacityService
    {
        /// <summary>
        /// 处理并存储产能相关数据
        /// </summary>
        /// <param name="plcName">PLC 枚举标识</param>
        /// <param name="data">PLC 传来的数据字典</param>
        Task ProcessUpDropHourlyDataAsync(string deviceName, Dictionary<string, object>? data);
    }
    internal class UpDropHourlyCapacityService : IUpDropHourlyCapacityService
    {
        private readonly ISqlSugarClient _db;

        public UpDropHourlyCapacityService(ISqlSugarClient db)
        {
            _db = db;
        }

        public async Task ProcessUpDropHourlyDataAsync(string deviceName, Dictionary<string, object> data)
        {
            if (data == null) return;

            var record = new UpDropHourlyCapacityRecord
            {
                // 直接赋值，不再需要 .ToString()
                DeviceName = deviceName,
                CreateTime = DateTime.Now
            };

            // 遍历字典，自动匹配属性赋值
            foreach (var item in data)
            {
                var prop = typeof(UpDropHourlyCapacityRecord).GetProperty(item.Key);
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

    [SugarTable("UpDropHourlyCapacity_Record")]
    public class UpDropHourlyCapacityRecord
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long Id { get; set; }

        public string? DeviceName { get; set; }

        // 字符串默认给横杠或空字符串，防止数据库出现 null 导致前端显示异常
        public string? HourlyProjectNumber { get; set; } = "-";

        // 数值类型默认给 -1，表示“未采集到有效数据”
        public int? HourlyCapacity { get; set; } = -1;
        public int? HourlyStandbyTime { get; set; } = -1;
        public int? HourlyFaultTime { get; set; } = -1;

        public short? HourlyFaultCount { get; set; } = -1;
        public short? HourlySystemNGCount { get; set; } = -1;
        public short? HourlyMaterialLoss { get; set; } = -1;

        public DateTime? CreateTime { get; set; } = DateTime.Now;
    }


}
