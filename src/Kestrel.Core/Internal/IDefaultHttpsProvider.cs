// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal
{
    public interface IDefaultHttpsProvider
    {
        /// <summary>
        /// Returns the default certificate, if available, otherwise null.
        /// </summary>
        X509Certificate2 Certificate { get; }

        /// <summary>
        /// Adds the https connection adapter using the default certificate. This throws if the certificate is not available.
        /// </summary>
        /// <param name="listenOptions"></param>
        void ConfigureHttps(ListenOptions listenOptions);
    }
}
