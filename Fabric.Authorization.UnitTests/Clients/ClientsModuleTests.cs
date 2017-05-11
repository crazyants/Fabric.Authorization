﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Fabric.Authorization.API.Constants;
using Fabric.Authorization.API.Models;
using Fabric.Authorization.API.Modules;
using Fabric.Authorization.Domain.Models;
using Fabric.Authorization.Domain.Services;
using Fabric.Authorization.Domain.Stores;
using Fabric.Authorization.UnitTests.Mocks;
using Moq;
using Nancy;
using Nancy.Testing;
using Serilog;
using Xunit;

namespace Fabric.Authorization.UnitTests.ClientsTests
{
    public class ClientsModuleTests
    {
        private readonly List<Client> _existingClients;
        private readonly Mock<IClientStore> _mockClientStore;
        private readonly Mock<ILogger> _mockLogger;

        public ClientsModuleTests()
        {
            _existingClients = new List<Client>
            {
                new Client
                {
                    Id = "sample-fabric-app",
                    Name = "Sample Fabric Client Application",
                    TopLevelSecurableItem = new SecurableItem
                    {
                        Id = Guid.NewGuid(),
                        Name = "sample-fabric-app"
                    }
                }
            };

            _mockClientStore = new Mock<IClientStore>()
                .SetupGetClient(_existingClients)
                .SetupAddClient();

            _mockLogger = new Mock<ILogger>();
        }
        
        [Fact]
        public void GetClients_ReturnsClients()
        {
            var existingClient = _existingClients.First();
            var clientsModule = CreateBrowser(new Claim(Claims.Scope, Scopes.ManageClientsScope),
                new Claim(Claims.Scope, Scopes.ReadScope));
            var result = clientsModule.Get("/clients").Result;
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var clients = result.Body.DeserializeJson<List<Client>>();
            Assert.Equal(1, clients.Count);
            Assert.Equal(existingClient.Id, clients.First().Id);
        }

        [Fact]
        public void GetClients_ReturnsForbidden()
        {
            var clientsModule = CreateBrowser();
            var result = clientsModule.Get("/clients").Result;
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        }
        
        [Fact]
        public void GetClient_ReturnsClient()
        {
            var existingClient = _existingClients.First();
            var clientsModule = CreateBrowser(new Claim(Claims.ClientId, existingClient.Id),
                new Claim(Claims.Scope, Scopes.ReadScope));
            var result = clientsModule.Get($"/clients/{existingClient.Id}").Result;
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var client = result.Body.DeserializeJson<Client>();
            Assert.Equal(existingClient.Id, client.Id);
        }

        [Fact]
        public void GetClient_ReturnsError()
        {
            var nonExistentClientId = "nonexistent";
            var clientsModule = CreateBrowser(new Claim(Claims.ClientId, nonExistentClientId),
                new Claim(Claims.Scope, Scopes.ReadScope));
            var result = clientsModule.Get($"/clients/{nonExistentClientId}").Result;
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            var error = result.Body.DeserializeJson<Error>();
            Assert.Equal(Enum.GetName(typeof(HttpStatusCode), HttpStatusCode.BadRequest), error.Code);
            Assert.Equal(typeof(Client).Name, error.Target);
        }

        [Fact]
        public void GetClient_ReturnsForbidden()
        {
            var existingClient = _existingClients.First();
            var clientsModule = CreateBrowser();
            var result = clientsModule.Get($"/clients/{existingClient.Id}").Result;
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        }

        [Fact]
        public void AddClient_Successful()
        {
            var clientsModule = CreateBrowser(new Claim(Claims.Scope, Scopes.ManageClientsScope),
                new Claim(Claims.Scope, Scopes.WriteScope));
            var clientToPost = new ClientApiModel
            {
                Id = "sample-fabric-app2",
                Name = "Sample Fabric App V2"
            };
            var result = clientsModule.Post("/clients", with => with.JsonBody(clientToPost)).Result;
            Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            var newClient = result.Body.DeserializeJson<ClientApiModel>();
            Assert.NotNull(newClient.TopLevelSecurableItem);
            Assert.Equal(clientToPost.Id, newClient.TopLevelSecurableItem.Name);
            Assert.NotEqual(DateTime.MinValue, newClient.CreatedDateTimeUtc);
        }

        [Theory, MemberData(nameof(BadRequestData))]
        public void AddClient_InvalidData(Client client, int errorCount)
        {
            var clientsModule = CreateBrowser(new Claim(Claims.Scope, Scopes.ManageClientsScope),
                new Claim(Claims.Scope, Scopes.WriteScope));
            var result = clientsModule.Post("/clients", with => with.JsonBody(client)).Result;
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            var error = result.Body.DeserializeJson<Error>();
            Assert.Equal(errorCount, error.Details.Length);
        }

        private Browser CreateBrowser(params Claim[] claims)
        {
            return new Browser(CreateBootstrapper(claims), withDefaults => withDefaults.Accept("application/json"));
        }

        private ConfigurableBootstrapper CreateBootstrapper(params Claim[] claims)
        {
            return new ConfigurableBootstrapper(with =>
            {
                with.Module<ClientsModule>()
                    .Dependency<IClientService>(typeof(ClientService))
                    .Dependency(_mockLogger.Object)
                    .Dependency(_mockClientStore.Object);

                with.RequestStartup((container, pipeline, context) =>
                {
                    context.CurrentUser = new TestPrincipal(claims);
                });
            });
        }

        public static IEnumerable<object[]> BadRequestData => new[]
        {
            new object[] { new Client{ Id = null, Name = null}, 2},
            new object[] { new Client{ Id = string.Empty, Name = string.Empty}, 2},
            new object[] { new Client{ Id = "newapp", Name = string.Empty}, 1},
            new object[] { new Client{ Id = "sample-fabric-app", Name = "sample-fabric-app" }, 1}
        };
    }
}
