using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Helper
{
    public static class MachineNames
    {
        // 1. 定义模组 ID 生成器
        public static string GetUpLoadModuleAId(int index) => $"{index:D2}_UpLoadModuleA";
        public static string GetUpLoadModuleBId(int index) => $"{index:D2}_UpLoadModuleB";
        public static string GetPierModuleId(int index) => $"{index:D2}_PierModule";
        public static string GetDropModuleAId(int index) => $"{index:D2}_DropModuleA";
        public static string GetDropModuleBId(int index) => $"{index:D2}_DropModuleB";
        public static string GetUpperFlipperId(int index) => $"{index:D2}_UpperFlipper";
        public static string GetLowerFlipperId(int index) => $"{index:D2}_LowerFlipper";
        public static readonly string[] CapacityModules = new[]
            {
        "UpperFlipper",
        "LowerFlipper"
    };

        /// <summary>
        /// 判断这个 DeviceId 是否属于需要记录产能的模组
        /// </summary>
        public static bool IsCapacityModule(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;

            // 检查 ID 是否包含定义的后缀 (例如 "01_UpLoadModuleA" 包含 "UpLoadModuleA")
            return CapacityModules.Any(type => deviceId.EndsWith(type));
        }
    }
}
