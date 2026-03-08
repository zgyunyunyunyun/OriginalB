using OriginalB.Platform.Core;
using OriginalB.Platform.Interfaces;
using OriginalB.Platform.Services.Common;
using OriginalB.Platform.Services.Douyin;
using OriginalB.Platform.Services.WeChat;

namespace OriginalB.Platform.Bootstrap
{
    public static class PlatformServiceRegistrar
    {
        public static void RegisterCoreServices(PlatformType platformType)
        {
            switch (platformType)
            {
                case PlatformType.WeChat:
                    ServiceLocator.Register<IPlatformContext>(new WeChatPlatformContext());
                    ServiceLocator.Register<IInputService>(new WeChatInputService());
                    ServiceLocator.Register<ILogService>(new WeChatLogService());
                    ServiceLocator.Register<IStorageService>(new WeChatStorageService());
                    ServiceLocator.Register<IAuthService>(new WeChatAuthService());
                    ServiceLocator.Register<IAdService>(new WeChatAdService());
                    ServiceLocator.Register<IShareService>(new WeChatShareService());
                    ServiceLocator.Register<IAnalyticsService>(new WeChatAnalyticsService());
                    break;
                case PlatformType.Douyin:
                    ServiceLocator.Register<IPlatformContext>(new DouyinPlatformContext());
                    ServiceLocator.Register<IInputService>(new DouyinInputService());
                    ServiceLocator.Register<ILogService>(new DouyinLogService());
                    ServiceLocator.Register<IStorageService>(new DouyinStorageService());
                    ServiceLocator.Register<IAuthService>(new DouyinAuthService());
                    ServiceLocator.Register<IAdService>(new DouyinAdService());
                    ServiceLocator.Register<IShareService>(new DouyinShareService());
                    ServiceLocator.Register<IAnalyticsService>(new DouyinAnalyticsService());
                    break;
                default:
                    ServiceLocator.Register<IPlatformContext>(new CommonPlatformContext());
                    ServiceLocator.Register<IInputService>(new CommonInputService());
                    ServiceLocator.Register<ILogService>(new CommonLogService());
                    ServiceLocator.Register<IStorageService>(new CommonStorageService());
                    ServiceLocator.Register<IAuthService>(new CommonAuthService());
                    ServiceLocator.Register<IAdService>(new CommonAdService());
                    ServiceLocator.Register<IShareService>(new CommonShareService());
                    ServiceLocator.Register<IAnalyticsService>(new CommonAnalyticsService());
                    break;
            }
        }
    }
}
