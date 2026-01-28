using My.Services;
using MyModbus;
using System;
using System.Collections.Generic;
using System.Linq;
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

    }
    public class MiddleFrameBusinessServices : IMiddleFrameBusinessServices
    {
        private readonly IFlipperHourlyCapacityService _flipperHourlyCapacityService;
        private readonly IUpDropHourlyCapacityService _upDropHourlyCapacityService;
        private readonly DataBus _bus;
        private readonly IProductionService _productionService;

        MiddleFrameBusinessServices(DataBus bus,IProductionService productionService, IFlipperHourlyCapacityService flipperHourlyCapacityService, IUpDropHourlyCapacityService upDropHourlyCapacityService)
        {
            //构造函数
            _flipperHourlyCapacityService = flipperHourlyCapacityService;
            _upDropHourlyCapacityService = upDropHourlyCapacityService;
            _bus = bus;
            _productionService = productionService;
        }

        public void FeedersHourlyDataCollectionMissionStart()
        {
            //小时数据采集任务只会在每小时的最后一分钟触发,可以直接从DataBus获取对应的点位数据
            _upDropHourlyCapacityService.ProcessUpDropHourlyDataAsync();



        }

        public void FlipperHourlyDataCollectionMissionStart()
        {
            //同样在最后一小时触发,直接从DataBus获取对应的点位数据
            _flipperHourlyCapacityService.ProcessFlipperHourlyDataAsync();
        }

        public void ProductCollectionMissionStart()
        {
            //
            //订阅触发点A_Trigger、B_Trigger、Flip_Trigger（当触发点值为int值11时，开始采集数据）

            //采集数据，直接从DataBus获取对应的点位数据，然后存入数据库,A_Trigger、B_Trigger工序只存入产品码字段，而Flip_Trigger工序根据产品码存入其他字段

            _productionService.ProcessProductDataAsync();



        }



    }
}
