using BasicRegionNavigation.Services;
using Microsoft.Extensions.DependencyInjection;
using My.Services;
using MyDatabase;
using MyModbus;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Helper
{
    public static class PrismServiceExtensions
    {
        /// <summary>
        /// 集中注册所有业务服务
        /// </summary>
        public static void AddBusinessServices(this IServiceCollection services)
        {
            // 1. 注册 Modbus 核心 (包含克隆逻辑)
            RegisterModbus(services);

            // 2. 注册 SqlSugar 数据库
            RegisterDatabase(services);

            // 3. 注册其他服务
            services.AddSingleton<IConfigService>(new ConfigService(new string[] { "Configs/config.json", "Configs/product_setting.json" }));

            // 4. 【关键】注册后台任务 (BackgroundService)
            // 注意：在 Prism 中，这里只是注册了类型，不会自动运行，需要后续手动 Start
            services.AddSingleton<HourlyDataCollectionService>();


            services.AddSingleton<IModbusService, ModbusService>();

        }

        private static void RegisterModbus(IServiceCollection services)
        {
            string modbusConfigPath = "Configs/config.csv";

            // 注意：这里只需要调用一次 AddMyModbusCore，不要重复调用
            services.AddMyModbusCore(modbusConfigPath, devices =>
            {
                // 定义克隆清单
                // Template: 原始设备名 (csv里配的)
                // ModuleId: 模组编号 (1, 2...)
                // Ip: 该模组该设备的实际IP
                var cloneList = new[]
                {
            (Template: "PLC_Peripheral", ModuleId: "1", Ip: "127.0.0.1"),
            (Template: "PLC_Robot",      ModuleId: "1", Ip: "127.0.0.1"),
            (Template: "PLC_Feeder_A",   ModuleId: "1", Ip: "127.0.0.2"),
            (Template: "PLC_Feeder_B",   ModuleId: "1", Ip: "127.0.0.3"),
            (Template: "PLC_Flipper",    ModuleId: "1", Ip: "127.0.0.1"),

            (Template: "PLC_Peripheral", ModuleId: "2", Ip: "127.0.0.1"),
            (Template: "PLC_Robot",      ModuleId: "2", Ip: "127.0.0.1"),
            (Template: "PLC_Feeder_A",   ModuleId: "2", Ip: "127.0.0.2"),
            (Template: "PLC_Feeder_B",   ModuleId: "2", Ip: "127.0.0.3"),
            (Template: "PLC_Flipper",    ModuleId: "2", Ip: "127.0.0.1"),
        };

                var templatesToRemove = new HashSet<Device>();

                foreach (var item in cloneList)
                {
                    var template = devices.FirstOrDefault(d => d.DeviceId == item.Template);
                    if (template != null)
                    {
                        templatesToRemove.Add(template);

                        // --- 关键修改 ---
                        // 使用 CloneToModule，而不是 CloneAsNew
                        // 这样生成的 DeviceId 将是 "1_PLC_Peripheral" 而不是 "1"
                        var newDevice = template.CloneToModule(item.ModuleId, item.Ip);

                        devices.Add(newDevice);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"警告：找不到模板设备 {item.Template}");
                    }
                }

                // 移除原始模板，防止它占用资源
                foreach (var t in templatesToRemove)
                {
                    devices.Remove(t);
                }
            });
        }

        private static void RegisterDatabase(IServiceCollection services)
        {
            var dbConfig = new ConnectionConfig
            {
                ConnectionString = "DataSource=IndustrialData.db",
                DbType = SqlSugar.DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
                MoreSettings = new ConnMoreSettings { IsAutoRemoveDataCache = true }
            };

            // 注册 Store 和相关 Service
            services.AddMySqlSugarStore(dbConfig);

            //上下料机小时产能入库
            services.AddTransient<IUpDropHourlyCapacityService, UpDropHourlyCapacityService>();
        }
    }
}
