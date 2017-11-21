// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Microsoft.AspNetCore.Server.Kestrel.Https
{
    public static class KestrelServerOptionsHttpsExtensions
    {
        public static void ConfigureHttpsDefaults(this KestrelServerOptions serverOptions, Action<HttpsConnectionAdapterOptions> configureOptions)
        {
            serverOptions.AdapterData[nameof(ConfigureHttpsDefaults)] = configureOptions;
        }

        public static Action<HttpsConnectionAdapterOptions> GetHttpsDefaults(this KestrelServerOptions serverOptions)
        {
            if (serverOptions.AdapterData.TryGetValue(nameof(ConfigureHttpsDefaults), out var action))
            {
                return (Action<HttpsConnectionAdapterOptions>)action;
            }
            return _ => { };
        }
    }
}
