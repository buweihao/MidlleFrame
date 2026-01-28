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

        public async Task ProcessProductDataAsync(string plcName, string identityKey, Dictionary<string, object>? data)
        {
            using var db = _factory.GetClient();

            // ---------------------------------------------------------
            // 1. 记录原始日志 (Raw Log) - 这一步不变，用于追溯
            // ---------------------------------------------------------
            string jsonPayload = System.Text.Json.JsonSerializer.Serialize(data);
            await db.Insertable(new DeviceLog
            {
                Module = plcName.ToString(),
                Message = $"Identity: {identityKey} | Data: {jsonPayload}", // 记录传入的 Key
                CreateTime = DateTime.Now
            }).ExecuteCommandAsync();

            // ---------------------------------------------------------
            // 2. 业务逻辑处理 (根据 PLC 不同，采取不同策略)
            // ---------------------------------------------------------

            // === 场景 A：第一道工序（上料） ===
            // 特点：系统里可能没数据，需要 Insert；如果有数据则是 Update。
            // 标识：identityKey 是 ProductCode
            if (plcName.ToString().Contains("UpLoad"))
            {
                var record = new ProductionRecord
                {
                    ProductCode = identityKey, // 这里 Key 就是 SN
                    UpLoadDeivceName = plcName.ToString(),
                    UpLoad_Time = DateTime.Now,
                };

                // 使用 Storageable 实现 "有则更新，无则插入" (Upsert)
                await db.Storageable(record)
                    .SplitInsert(it => !it.Any())
                    .SplitUpdate(it => true)
                    .ExecuteCommandAsync();
            }

            // === 场景 B：中间工序（上翻转台） ===
            // 特点：记录必然存在，只更新字段。
            // 标识：identityKey 是 ProductCode
            else if (plcName.ToString().Contains("UpperHangFlip"))
            {
                // 直接根据主键 SN 更新特定列
                await db.Updateable<ProductionRecord>()
                    .SetColumns(it => new ProductionRecord
                    {
                        UpperHangFlipDeivceName = plcName.ToString(),
                        UpperHangFlip_Time = DateTime.Now,
                        FixtureCode = data.ContainsKey("FixtureCode") ? data["FixtureCode"].ToString() : null // 这里可能绑定挂具
                    })
                    .Where(it => it.ProductCode == identityKey) // Key 是 SN
                    .ExecuteCommandAsync();
            }

            // === 场景 C：特殊工序（下翻转台 - 你的痛点） ===
            // 特点：PLC 只知道挂具号，不知道 SN。需要先反查，再更新。
            // 标识：identityKey 是 FixtureCode
            //else if (plcName.ToString().Contains("LowerHangFlip"))
            //{
            //    // 1. [关键步骤] 反查 SN
            //    // 查找条件：FixtureCode 匹配 且 流程未结束 (IsCompleted == false)
            //    // 排序：按时间倒序，防止极低概率的历史重复数据干扰
            //    var record = await db.Queryable<ProductionRecord>()
            //        .OrderByDescending(it => it.CreateTime)
            //        .FirstAsync(it => it.FixtureCode == identityKey && !it.IsCompleted);

            //    if (record == null)
            //    {
            //        // 严重警告：找不到对应的在线产品（可能是挂具号读错，或者步骤2没写入）
            //        Console.WriteLine($"[Error] 挂具 {identityKey} 上未找到未完成的产品记录！");
            //        return;
            //    }

            //    // 2. 更新状态
            //    // 既然拿到了 record 对象，直接修改它的属性并 Update 即可
            //    record.LowerHangFlipDeivceName = plcName.ToString();
            //    record.LowerHangFlip_Time = DateTime.Now;
            //    record.IsCompleted = true; // 假设这是最后一步
            //    record.FinishTime = DateTime.Now;

            //    // 3. 执行更新 (SqlSugar 会自动根据 record.Id 主键去更新)
            //    await db.Updateable(record)
            //        .UpdateColumns(it => new
            //        {
            //            it.LowerHangFlipDeivceName,
            //            it.LowerHangFlip_Time,
            //            it.IsCompleted,
            //            it.FinishTime
            //        })
            //        .ExecuteCommandAsync();
            //}
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
        Task ProcessProductDataAsync(string plcName, string sn, Dictionary<string, object> data);
        Task<ObservableCollection<ProductionRecord>> GetProductionRecordsAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        Dictionary<string, object>? filters = null);
    }
}
