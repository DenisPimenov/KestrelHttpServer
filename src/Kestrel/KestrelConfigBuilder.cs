// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Server.Kestrel.Https.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Server.Kestrel
{
    public class KestrelConfigBuilder : IKestrelConfigBuilder
    {
        internal KestrelConfigBuilder(KestrelServerOptions options, IConfiguration configuration)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            Options.ConfigurationBuilder = this;
        }

        public KestrelServerOptions Options { get; }
        public IConfiguration Configuration { get; }
        private IDictionary<string, Action<EndpointConfiguration>> EndpointConfigurations { get; }
            = new Dictionary<string, Action<EndpointConfiguration>>(0);

        /// <summary>
        /// Specifies a configuration Action to run when an endpoint with the given name is loaded from configuration.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="configureOptions"></param>
        public KestrelConfigBuilder Endpoint(string name, Action<EndpointConfiguration> configureOptions)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            EndpointConfigurations[name] = configureOptions ?? throw new ArgumentNullException(nameof(configureOptions));
            return this;
        }

        public void Build()
        {
            if (Options.ConfigurationBuilder == null)
            {
                // The builder has already been built.
                return;
            }
            Options.ConfigurationBuilder = null;

            var configReader = new ConfigReader(Configuration);
            
            foreach (var endpoint in configReader.Endpoints)
            {
                var listenOptions = AddressBinder.ParseAddress(endpoint.Url, out var https);
                listenOptions.KestrelServerOptions = Options;
                Options.EndpointDefaults(listenOptions);

                HttpsConnectionAdapterOptions httpsOptions = null;
                if (https)
                {
                    httpsOptions = new HttpsConnectionAdapterOptions();
                    Options.GetHttpsDefaults()(httpsOptions);

                    var certInfo = new CertificateConfig(endpoint.CertConfig);
                    if (certInfo.IsFileCert)
                    {
                        var env = Options.ApplicationServices.GetRequiredService<IHostingEnvironment>();
                        httpsOptions.ServerCertificate = new X509Certificate2(Path.Combine(env.ContentRootPath, certInfo.Path), certInfo.Password);
                    }
                    else if (certInfo.IsStoreCert)
                    {
                        // TODO: Throw if the cert cannot be loaded, FileCert does.
                        httpsOptions.ServerCertificate = LoadFromStoreCert(certInfo);
                    }
                    else if (httpsOptions.ServerCertificate == null)
                    {
                        var provider = Options.ApplicationServices.GetRequiredService<IDefaultHttpsProvider>();
                        httpsOptions.ServerCertificate = provider.Certificate; // May be null
                    }
                }

                var endpointConfig = new EndpointConfiguration(listenOptions, httpsOptions, endpoint.ConfigSection);

                if (EndpointConfigurations.TryGetValue(endpoint.Name, out var configureEndpoint))
                {
                    configureEndpoint(endpointConfig);
                }

                if (endpointConfig.Https != null && !listenOptions.ConnectionAdapters.Any(f => f.IsHttps))
                {
                    // It's possible to get here with no cert configured. This will throw.
                    listenOptions.UseHttps(endpointConfig.Https);
                }

                Options.ListenOptions.Add(listenOptions);
            }
        }

        private static X509Certificate2 LoadFromStoreCert(CertificateConfig certInfo)
        {
            var subject = certInfo.Subject;
            var storeName = certInfo.Store;
            var location = certInfo.Location;
            var storeLocation = StoreLocation.CurrentUser;
            if (!string.IsNullOrEmpty(location))
            {
                storeLocation = (StoreLocation)Enum.Parse(typeof(StoreLocation), location, ignoreCase: true);
            }
            var validOnly = !certInfo.AllowInvalid ?? false;

            return CertificateLoader.LoadFromStoreCert(subject, storeName, storeLocation, validOnly);
        }
    }
}
