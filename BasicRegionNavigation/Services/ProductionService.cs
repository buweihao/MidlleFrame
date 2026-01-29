using MyDatabase;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Services
{

    public class ProductionService : IProductionService
    {
        private readonly ISqlSugarClientFactory _factory;




        /// <summary>
        /// 处理 PLC 数据（统一入口）
        /// </summary>
        /// <param name="plcName">PLC名称，用于区分工序逻辑</param>
        /// <param name="identityKey">标识Key：工序1/2传SN，工序3传FixtureCode</param>
        /// <param name="data">PLC读取的数据字典</param>

        /// <summary>
        /// 处理 PLC 数据（重构版）
        /// </summary>
        public async Task ProcessProductDataAsync(StationProcessContext context)
        {
            using var db = _factory.GetClient();

            // 1. 记录原始日志 (Raw Log)
            string jsonPayload = System.Text.Json.JsonSerializer.Serialize(context.PlcData);
            await db.Insertable(new DeviceLog
            {
                Module = context.DeviceId,
                // 清晰记录：当前是[什么工序]，标识是[什么]
                Message = $"[{context.ProcessType}] Key: {context.IdentityValue} | Data: {jsonPayload}",
                CreateTime = DateTime.Now
            }).ExecuteCommandAsync();

            // 2. 根据枚举分发业务逻辑
            switch (context.ProcessType)
            {
                // === 场景 A：第一道工序（上料） ===
                case StationProcessType.Entry_Upload:
                    {
                        var record = new ProductionRecord
                        {
                            ProductCode = context.IdentityValue, // 这里明确是 SN
                            UpLoadDeivceName = context.DeviceId,
                            UpLoad_Time = DateTime.Now,
                        };

                        await db.Storageable(record)
                            .SplitInsert(it => !it.Any())
                            .SplitUpdate(it => true)
                            .ExecuteCommandAsync();
                    }
                    break;

                // === 场景 B：中间工序（上翻转台） ===
                case StationProcessType.Process_Flip:
                    {
                        // 尝试从数据字典里拿挂具号，拿不到就空
                        string fixtureCode = context.PlcData.ContainsKey("FixtureCode")
                                             ? context.PlcData["FixtureCode"]?.ToString()
                                             : null;
                        string projectNumber = context.PlcData.ContainsKey("ProjectNumber")
                                             ? context.PlcData["ProjectNumber"]?.ToString()
                                             : null;
                        string productCategory = context.PlcData.ContainsKey("ProductCategory")
                                             ? context.PlcData["ProductCategory"]?.ToString()
                                             : null;
                        await db.Updateable<ProductionRecord>()
                            .SetColumns(it => new ProductionRecord
                            {
                                UpperHangFlipDeivceName = context.DeviceId,
                                UpperHangFlip_Time = DateTime.Now,
                                FixtureCode = fixtureCode,
                                ProjectNumber = projectNumber,
                                ProductCategory = productCategory

    })
                            .Where(it => it.ProductCode == context.IdentityValue) // Where SN = Key
                            .ExecuteCommandAsync();
                    }
                    break;

                // === 场景 C：特殊工序（下翻转台/解绑） ===
                case StationProcessType.Exit_Unload:
                    {
                        // 1. 反查 SN (Key 是 FixtureCode)
                        var record = await db.Queryable<ProductionRecord>()
                            .OrderByDescending(it => it.CreateTime)
                            .FirstAsync(it => it.FixtureCode == context.IdentityValue && !it.IsCompleted);

                        if (record == null)
                        {
                            // 记录错误日志，或者触发报警
                            Console.WriteLine($"[Error] 挂具 {context.IdentityValue} 未找到对应产品！设备: {context.DeviceId}");
                            return;
                        }

                        // 2. 更新状态
                        record.LowerHangFlipDeivceName = context.DeviceId;
                        record.LowerHangFlip_Time = DateTime.Now;
                        record.IsCompleted = true;
                        record.FinishTime = DateTime.Now;

                        // 3. 执行更新
                        await db.Updateable(record)
                            .UpdateColumns(it => new
                            {
                                it.LowerHangFlipDeivceName,
                                it.LowerHangFlip_Time,
                                it.IsCompleted,
                                it.FinishTime
                            })
                            .ExecuteCommandAsync();
                    }
                    break;
            }
        }

        /// <summary>
        /// 灵活查询生产记录
        /// </summary>
        /// <param name="startTime">开始时间（针对 CreateTime）</param>
        /// <param name="endTime">结束时间</param>
        /// <param name="filters">任意字段过滤字典，例如 Key="ProductCode", Value="SN123"</param>
        public async Task<ObservableCollection<ProductionRecord>> GetProductionRecordsAsync(
            DateTime? startTime = null,
            DateTime? endTime = null,
            Dictionary<string, object>? filters = null)
        {
            using var db = _factory.GetClient();

            var query = db.Queryable<ProductionRecord>();

            // 1. 时间范围过滤
            query.WhereIF(startTime.HasValue, it => it.CreateTime >= startTime.Value)
                 .WhereIF(endTime.HasValue, it => it.CreateTime <= endTime.Value);

            // 2. 动态字典过滤 (支持任意字段)
            if (filters != null && filters.Count > 0)
            {
                foreach (var filter in filters)
                {
                    // 检查字段是否存在，防止非法 Key 导致报错
                    var property = typeof(ProductionRecord).GetProperty(filter.Key);
                    if (property != null && filter.Value != null)
                    {
                        string fieldName = filter.Key;
                        object fieldValue = filter.Value;

                        // 使用动态字符串拼接 Where，支持多种类型转换
                        // SqlSugar 会自动处理 SQL 注入风险
                        query.Where($"{fieldName} = @val", new { val = fieldValue });
                    }
                }
            }

            // 3. 执行查询并排序
            var list = await query.OrderByDescending(it => it.CreateTime).ToListAsync();

            // 4. 转换为 ObservableCollection 返回给 UI
            return new ObservableCollection<ProductionRecord>(list);
        }

    }
    [SugarTable("Production_Records")]
    public class ProductionRecord
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long Id { get; set; }

        // --- 工序 1 (上料机A/B) --        //产品码-
        public string? ProductCode { get; set; }
        public string? UpLoadDeivceName { get; set; }
        public DateTime? UpLoad_Time { get; set; } // 记录这一步具体发生的时间

        // --- 工序 2 (上翻转台) --        //挂具码、项目编号、产品类别
        public string? FixtureCode { get; set; }
        public string? ProjectNumber { get; set; }
        public string? ProductCategory { get; set; }
        public string? UpperHangFlipDeivceName { get; set; }
        public DateTime? UpperHangFlip_Time { get; set; }

        // --- 工序 3 (下翻转台) ---      //下翻转
        public string? LowerHangFlipDeivceName { get; set; }
        public DateTime? LowerHangFlip_Time { get; set; }

        // 最终状态
        public DateTime CreateTime { get; set; } // 也就是上线时间
        public DateTime? FinishTime { get; set; } // 下线时间
        public bool IsCompleted { get; set; } // 是否所有工序都跑完了
    }
    // 设备日志表
    [SugarTable("Device_Logs")]
    public class DeviceLog
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }
        public string Module { get; set; } // 来源模块 (例如: PLC, Vision)
        public string Message { get; set; }
        public DateTime CreateTime { get; set; }
    }
    public interface IProductionService
    {
        Task ProcessProductDataAsync(StationProcessContext context);
        Task<ObservableCollection<ProductionRecord>> GetProductionRecordsAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        Dictionary<string, object>? filters = null);
    }
    // 1. 定义工序类型（明确告诉程序当前是哪一步）
    public enum StationProcessType
    {
        /// <summary>
        /// 进站/上料 (Identity = SN)
        /// 行为：新建或更新记录
        /// </summary>
        Entry_Upload,

        /// <summary>
        /// 中间工序 (Identity = SN)
        /// 行为：只更新数据
        /// </summary>
        Process_Flip,

        /// <summary>
        /// 出站/下料 (Identity = FixtureCode)
        /// 行为：反查挂具 -> 完结记录
        /// </summary>
        Exit_Unload
    }

    // 2. 定义统一的参数对象 (Context)
    public class StationProcessContext
    {
        /// <summary>
        /// 触发的设备全名 (如 "1_PLC_UpLoad")
        /// 用于写入数据库的 DeviceName 字段
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 关键标识值 (可能是 SN，也可能是 挂具号)
        /// </summary>
        public string IdentityValue { get; set; }

        /// <summary>
        /// 当前工序类型 (核心逻辑开关)
        /// </summary>
        public StationProcessType ProcessType { get; set; }

        /// <summary>
        /// PLC 原始数据包
        /// </summary>
        public Dictionary<string, object> PlcData { get; set; }

        /// <summary>
        /// 辅助方法：快速创建
        /// </summary>
        public static StationProcessContext Create(string deviceId, string identity, StationProcessType type, Dictionary<string, object> data)
        {
            return new StationProcessContext
            {
                DeviceId = deviceId,
                IdentityValue = identity,
                ProcessType = type,
                PlcData = data
            };
        }
    }
}
