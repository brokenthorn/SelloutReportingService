using System;
using Microsoft.Extensions.Configuration;

namespace SelloutReportingService
{
    /// <summary>Static class holding the default appsettings <see cref="IConfiguration" /> instance.</summary>
    /// <remarks>
    /// If the app is built in Debug mode, "appsettings.Development.json" is loaded,
    /// otherwise "appsettings.json" is loaded.
    /// </remarks>
    public static class Configuration
    {
        /// <summary>
        /// Full path to the directory where the current assembly was loaded from.
        /// </summary>
        public static readonly string BasePath = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// Our appsettings <see cref="IConfiguration" /> instance, which also loads the "appsettings.json"
        /// file or the "appsettings.Development.json" file, depending on the build target (Release or Debug).
        /// </summary>
        public static readonly IConfiguration Instance =
            new ConfigurationBuilder().SetBasePath(BasePath)
#if DEBUG
                .AddJsonFile("appsettings.Development.json", false, true)
#else
                .AddJsonFile("appsettings.json", false, true)
#endif
                .Build();
    }
}