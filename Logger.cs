using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SelloutReportingService.Services;

namespace SelloutReportingService
{
    /// <summary>
    /// Static class holding the default logger instance.
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// Our default <see cref="ILogger" /> instance.
        /// </summary>
        /// <remarks>
        /// This instance logs to console and Windows Event Log if running under Windows.
        /// </remarks>
        public static readonly ILogger Instance = LoggerFactory
            .Create(builder =>
            {
                builder
                    .AddConsole(o => { o.TimestampFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK"; })
                    .AddConfiguration(Configuration.Instance);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    builder.AddEventLog();
            })
            .CreateLogger<ReportingServiceControl>();
    }
}