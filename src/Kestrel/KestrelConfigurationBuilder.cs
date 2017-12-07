﻿// Copyright (c) .NET Foundation. All rights reserved.
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Server.Kestrel
{
    public class KestrelConfigurationBuilder : IKestrelConfigurationBuilder
    {
        internal KestrelConfigurationBuilder(KestrelServerOptions options, IConfiguration configuration)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
        public KestrelConfigurationBuilder Endpoint(string name, Action<EndpointConfiguration> configureOptions)
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

            var configReader = new ConfigurationReader(Configuration);

            LoadDefaultCert(configReader);
            
            foreach (var endpoint in configReader.Endpoints)
            {
                var listenOptions = AddressBinder.ParseAddress(endpoint.Url, out var https);
                listenOptions.KestrelServerOptions = Options;
                Options.EndpointDefaults(listenOptions);

                var httpsOptions = new HttpsConnectionAdapterOptions();
                if (https)
                {
                    httpsOptions.ServerCertificate = listenOptions.KestrelServerOptions.GetOverriddenDefaultCertificate();
                    Options.GetHttpsDefaults()(httpsOptions);

                    var certInfo = new CertificateConfig(endpoint.CertConfig);
                    httpsOptions.ServerCertificate = LoadCertificate(certInfo, endpoint.Name);
                    if (httpsOptions.ServerCertificate == null)
                    {
                        var provider = Options.ApplicationServices.GetRequiredService<IDefaultHttpsProvider>();
                        httpsOptions.ServerCertificate = provider.Certificate; // May be null
                    }
                }

                var endpointConfig = new EndpointConfiguration(https, listenOptions, httpsOptions, endpoint.ConfigSection);

                if (EndpointConfigurations.TryGetValue(endpoint.Name, out var configureEndpoint))
                {
                    configureEndpoint(endpointConfig);
                }

                // EndpointDefaults or configureEndpoint may have specified an https adapter.
                if (https && !listenOptions.ConnectionAdapters.Any(f => f.IsHttps))
                {
                    // It's possible to get here with no cert configured if the default is missing. This will throw.
                    listenOptions.UseHttps(endpointConfig.Https);
                }

                Options.ListenOptions.Add(listenOptions);
            }
        private void LoadDefaultCert(ConfigurationReader configReader)
        {
            var defaultCertConfig = configReader.Certificates
                .Where(cert => string.Equals("Default", cert.Name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (defaultCertConfig != null)
            {
                var defaultCert = LoadCertificate(defaultCertConfig, "Default");
                if (defaultCert != null)
                {
                    Options.OverrideDefaultCertificate(defaultCert);
                }
            }
        }

        private X509Certificate2 LoadCertificate(CertificateConfig certInfo, string endpointName)
        {
            if (certInfo.IsFileCert && certInfo.IsStoreCert)
            {
                throw new InvalidOperationException(KestrelStrings.FormatMultipleCertificateSources(endpointName));
            }
            else if (certInfo.IsFileCert)
            {
                var env = Options.ApplicationServices.GetRequiredService<IHostingEnvironment>();
                return new X509Certificate2(Path.Combine(env.ContentRootPath, certInfo.Path), certInfo.Password);
            }
            else if (certInfo.IsStoreCert)
            {
                return LoadFromStoreCert(certInfo);
            }
            return null;
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
            var allowInvalid = certInfo.AllowInvalid ?? false;

            return CertificateLoader.LoadFromStoreCert(subject, storeName, storeLocation, allowInvalid);
        }
    }
}