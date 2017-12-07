// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
        // Actions that will be delayed until Build so that they aren't applied if the configuration builder is replaced.
        private IList<Action> EndpointsToAdd { get; } = new List<Action>();

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

        /// <summary>
        /// Bind to given IP address and port.
        /// </summary>
        public KestrelConfigurationBuilder Endpoint(IPAddress address, int port) => Endpoint(address, port, _ => { });

        /// <summary>
        /// Bind to given IP address and port.
        /// The callback configures endpoint-specific settings.
        /// </summary>
        public KestrelConfigurationBuilder Endpoint(IPAddress address, int port, Action<ListenOptions> configure)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            return Endpoint(new IPEndPoint(address, port), configure);
        }

        /// <summary>
        /// Bind to given IP endpoint.
        /// </summary>
        public KestrelConfigurationBuilder Endpoint(IPEndPoint endPoint) => Endpoint(endPoint, _ => { });

        /// <summary>
        /// Bind to given IP address and port.
        /// The callback configures endpoint-specific settings.
        /// </summary>
        public KestrelConfigurationBuilder Endpoint(IPEndPoint endPoint, Action<ListenOptions> configure)
        {
            if (endPoint == null)
            {
                throw new ArgumentNullException(nameof(endPoint));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            EndpointsToAdd.Add(() =>
            {
                Options.Listen(endPoint, configure);
            });

            return this;
        }

        /// <summary>
        /// Listens on ::1 and 127.0.0.1 with the given port. Requesting a dynamic port by specifying 0 is not supported
        /// for this type of endpoint.
        /// </summary>
        public KestrelConfigurationBuilder LocalhostEndpoint(int port) => LocalhostEndpoint(port, options => { });

        /// <summary>
        /// Listens on ::1 and 127.0.0.1 with the given port. Requesting a dynamic port by specifying 0 is not supported
        /// for this type of endpoint.
        /// </summary>
        public KestrelConfigurationBuilder LocalhostEndpoint(int port, Action<ListenOptions> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            EndpointsToAdd.Add(() =>
            {
                Options.ListenLocalhost(port, configure);
            });

            return this;
        }

        /// <summary>
        /// Listens on all IPs using IPv6 [::], or IPv4 0.0.0.0 if IPv6 is not supported.
        /// </summary>
        public KestrelConfigurationBuilder AnyIPEndpoint(int port) => AnyIPEndpoint(port, options => { });

        /// <summary>
        /// Listens on all IPs using IPv6 [::], or IPv4 0.0.0.0 if IPv6 is not supported.
        /// </summary>
        public KestrelConfigurationBuilder AnyIPEndpoint(int port, Action<ListenOptions> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            EndpointsToAdd.Add(() =>
            {
                Options.ListenAnyIP(port, configure);
            });

            return this;
        }

        /// <summary>
        /// Bind to given Unix domain socket path.
        /// </summary>
        public KestrelConfigurationBuilder UnixSocketEndpoint(string socketPath) => UnixSocketEndpoint(socketPath, _ => { });

        /// <summary>
        /// Bind to given Unix domain socket path.
        /// Specify callback to configure endpoint-specific settings.
        /// </summary>
        public KestrelConfigurationBuilder UnixSocketEndpoint(string socketPath, Action<ListenOptions> configure)
        {
            if (socketPath == null)
            {
                throw new ArgumentNullException(nameof(socketPath));
            }
            if (socketPath.Length == 0 || socketPath[0] != '/')
            {
                throw new ArgumentException(CoreStrings.UnixSocketPathMustBeAbsolute, nameof(socketPath));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            EndpointsToAdd.Add(() =>
            {
                Options.ListenUnixSocket(socketPath, configure);
            });

            return this;
        }

        /// <summary>
        /// Open a socket file descriptor.
        /// </summary>
        public KestrelConfigurationBuilder HandleEndpoint(ulong handle) => HandleEndpoint(handle, _ => { });

        /// <summary>
        /// Open a socket file descriptor.
        /// The callback configures endpoint-specific settings.
        /// </summary>
        public KestrelConfigurationBuilder HandleEndpoint(ulong handle, Action<ListenOptions> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            EndpointsToAdd.Add(() =>
            {
                Options.ListenHandle(handle, configure);
            });

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

            foreach (var action in EndpointsToAdd)
            {
                action();
            }
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
