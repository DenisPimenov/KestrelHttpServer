// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.AspNetCore.Server.Kestrel.Core
{
    public static class KestrelServerOptionsConfigurationExtensions
    {
        public static KestrelConfigBuilder Configure(this KestrelServerOptions options, IConfiguration config)
        {
            return new KestrelConfigBuilder(options, config); // Assigns itself to options.ConfigurationBuilder
        }
    }
}
