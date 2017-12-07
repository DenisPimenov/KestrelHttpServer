// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;

namespace Microsoft.AspNetCore.Server.Kestrel
{
    public class EndpointConfiguration
    {
        internal EndpointConfiguration(bool isHttps, ListenOptions listenOptions, HttpsConnectionAdapterOptions httpsOptions, IConfigurationSection configSection)
        {
            IsHttps = isHttps;
            Listener = listenOptions ?? throw new ArgumentNullException(nameof(listenOptions));
            Https = httpsOptions;
            ConfigSection = configSection ?? throw new ArgumentNullException(nameof(configSection));
        }

        public bool IsHttps { get; }
        public ListenOptions Listener { get; }
        public HttpsConnectionAdapterOptions Https { get; set; }
        public IConfigurationSection ConfigSection { get; }
    }
}
