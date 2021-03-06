﻿using Fabric.Platform.Shared.Configuration;

namespace Fabric.Authorization.API.Configuration
{
    public class AppConfiguration : IAppConfiguration
    {
        public string ClientName { get; set; }
        public bool UseInMemoryStores { get; set; }
        public ElasticSearchSettings ElasticSearchSettings { get; set; }
        public IdentityServerConfidentialClientSettings IdentityServerConfidentialClientSettings { get; set; }
        public CouchDbSettings CouchDbSettings { get; set; }
        public ApplicationInsights ApplicationInsights { get; set; }
        public HostingOptions HostingOptions { get; set; }
        public EncryptionCertificateSettings EncryptionCertificateSettings { get; set; }
        public DefaultPropertySettings DefaultPropertySettings{ get; set; }
    }
}
