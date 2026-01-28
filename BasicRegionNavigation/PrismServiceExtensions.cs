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
            services.AddMyModbusCore(modbusConfigPath); // 假设这是你的扩展方法
            //services.AddMyModbusCore(modbusConfigPath, devices =>
            //{
            //    // 定义克隆清单
            //    var cloneList = new[]
            //    {
            //        (Template: "PLC_Peripheral", NewId: MachineNames.GetLowerFlipperId(1), Ip: "127.0.0.1"),
            //        (Template: "PLC_Robot", NewId: MachineNames.GetLowerFlipperId(1), Ip: "127.0.0.1"),
            //        (Template: "PLC_Feeder_A",    NewId: MachineNames.GetUpLoadModuleAId(1),    Ip: "127.0.0.2"),
            //        (Template: "PLC_Feeder_B",  NewId: MachineNames.GetUpLoadModuleBId(1),  Ip: "127.0.0.3"),
            //        (Template: "PLC_Flipper",  NewId: MachineNames.GetLowerFlipperId(1),  Ip: "127.0.0.1"),

            //        (Template: "PLC_Peripheral", NewId: MachineNames.GetLowerFlipperId(2), Ip: "127.0.0.1"),
            //        (Template: "PLC_Robot", NewId: MachineNames.GetLowerFlipperId(2), Ip: "127.0.0.1"),
            //        (Template: "PLC_Feeder_A",    NewId: MachineNames.GetUpLoadModuleAId(2),    Ip: "127.0.0.2"),
            //        (Template: "PLC_Feeder_B",  NewId: MachineNames.GetUpLoadModuleBId(2),  Ip: "127.0.0.3"),
            //        (Template: "PLC_Flipper",  NewId: MachineNames.GetLowerFlipperId(2),  Ip: "127.0.0.1"),
            //        };

            //    var templatesToRemove = new HashSet<Device>();

            //    foreach (var item in cloneList)
            //    {
            //        var template = devices.FirstOrDefault(d => d.DeviceId == item.Template);
            //        if (template != null)
            //        {
            //            templatesToRemove.Add(template);
            //            devices.Add(template.CloneAsNew(item.NewId, item.Ip));
            //        }
            //        else
            //        {
            //            System.Diagnostics.Debug.WriteLine($"警告：找不到模板设备 {item.Template}");
            //        }
            //    }

            //    foreach (var t in templatesToRemove)
            //    {
            //        devices.Remove(t);
            //    }
            //});
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
