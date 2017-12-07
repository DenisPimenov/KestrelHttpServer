// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.AspNetCore.Server.Kestrel.Core
{
    public static class KestrelServerOptionsConfigurationExtensions
    {
        /// <summary>
        /// Creates a configuration builder for setting up Kestrel.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static KestrelConfigurationBuilder Configure(this KestrelServerOptions options)
        {
            var builder = new KestrelConfigurationBuilder(options, new ConfigurationBuilder().Build());
            options.ConfigurationBuilder = builder;
            return builder;
        }

        /// <summary>
        /// Creates a configuration builder for setting up Kestrel that takes an IConfiguration as input.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public static KestrelConfigurationBuilder Configure(this KestrelServerOptions options, IConfiguration config)
        {
            var builder = new KestrelConfigurationBuilder(options, config);
            options.ConfigurationBuilder = builder;
            return builder;
        }
    }
}
