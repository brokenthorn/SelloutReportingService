using System;
using System.Runtime.InteropServices;
using SelloutReportingService.Services;
using Topshelf;
using Topshelf.Runtime.DotNetCore;

namespace SelloutReportingService
{
    public static class Program
    {
        public static int Main()
        {
            return (int) HostFactory.Run(c =>
            {
                // Change Topshelf's environment builder on non-Windows hosts to use DotNetCoreEnvironmentBuilder.
                // It defaults to WindowsHostEnvironmentBuilder, but that throws some errors when not running on Windows.
                if (
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                )
                    c.UseEnvironmentBuilder(hostConfigurator =>
                        new DotNetCoreEnvironmentBuilder(hostConfigurator));

                c.SetServiceName("SelloutReportingService");
                c.SetDisplayName("Sellout Reporting Service");
                c.SetDescription("A Windows service that generates and sends custom reports.");

                c.StartAutomatically();
                c.RunAsNetworkService();
                c.EnableServiceRecovery(
                    a => a.RestartService(TimeSpan.FromSeconds(60))
                );

                c.Service<ReportingServiceControl>(serviceConfigurator =>
                {
                    // Custom service construction.
                    serviceConfigurator.ConstructUsing(
                        () => new ReportingServiceControl());
                    serviceConfigurator.WhenStarted(
                        (service, control) => service.Start(control)
                    );
                    serviceConfigurator.WhenStopped(
                        (service, control) => service.Stop(control)
                    );
                });
            });
        }
    }
}