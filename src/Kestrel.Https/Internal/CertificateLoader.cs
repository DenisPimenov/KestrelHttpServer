// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Microsoft.AspNetCore.Server.Kestrel.Https.Internal
{
    public static class CertificateLoader
    {
        public static X509Certificate2 LoadFromStoreCert(string subject, string storeName, StoreLocation storeLocation, bool validOnly)
        {
            using (var store = new X509Store(storeName, storeLocation))
            {
                X509Certificate2Collection storeCertificates = null;
                X509Certificate2Collection foundCertificates = null;
                X509Certificate2 foundCertificate = null;

                try
                {
                    store.Open(OpenFlags.ReadOnly);
                    storeCertificates = store.Certificates;
                    foundCertificates = storeCertificates.Find(X509FindType.FindBySubjectName, subject, validOnly);
                    foundCertificate = foundCertificates
                        .OfType<X509Certificate2>()
                        // TODO: EKU check
                        .OrderByDescending(certificate => certificate.NotAfter)
                        .FirstOrDefault();

                    if (foundCertificate == null)
                    {
                        throw new InvalidOperationException($"The requested certificate {subject} could not be found in {storeLocation}/{storeName}.");
                    }

                    return foundCertificate;
                }
                finally
                {
                    if (foundCertificate != null)
                    {
                        storeCertificates.Remove(foundCertificate);
                        foundCertificates.Remove(foundCertificate);
                    }

                    DisposeCertificates(storeCertificates);
                    DisposeCertificates(foundCertificates);
                }
            }
        }

        private static void DisposeCertificates(X509Certificate2Collection certificates)
        {
            if (certificates != null)
            {
                foreach (var certificate in certificates)
                {
                    certificate.Dispose();
                }
            }
        }
    }
}
