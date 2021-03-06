﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Fabric.Authorization.API.Constants;
using Fabric.Authorization.API.Infrastructure.PipelineHooks;
using Fabric.Authorization.API.Models;
using Fabric.Authorization.API.Modules;
using Fabric.Authorization.Domain.Stores;
using Fabric.Authorization.Domain.Stores.CouchDB;
using Fabric.Authorization.Domain.Stores.InMemory;
using Fabric.Authorization.Domain.Stores.Services;
using IdentityModel;
using Nancy;
using Nancy.Testing;
using Xunit;

namespace Fabric.Authorization.IntegrationTests.Modules
{
    [Collection("InMemoryTests")]
    public class UserTests : IntegrationTestsFixture
    {
        private static readonly string Group1 = Guid.Parse("A9CA0300-1006-40B1-ABF1-E0C3B396F95F").ToString();
        private static readonly string Source1 = "Source1";

        private static readonly string Group2 = Guid.Parse("ad2cea96-c020-4014-9cf6-029147454adc").ToString();
        private static readonly string Source2 = "Source2";

        private static readonly string IdentityProvider = "idP1";
        private readonly IIdentifierFormatter _identifierFormatter = new IdpIdentifierFormatter();

        public UserTests(bool useInMemoryDB = true)
        {
            var roleStore = useInMemoryDB ? new InMemoryRoleStore() : (IRoleStore)new CouchDbRoleStore(this.DbService(), this.Logger, this.EventContextResolverService);
            var userStore = useInMemoryDB ? new InMemoryUserStore() : (IUserStore)new CouchDbUserStore(this.DbService(), this.Logger, this.EventContextResolverService, _identifierFormatter);
            var groupStore = useInMemoryDB ? new InMemoryGroupStore() : (IGroupStore)new CouchDbGroupStore(this.DbService(), this.Logger, this.EventContextResolverService, _identifierFormatter);
            var clientStore = useInMemoryDB ? new InMemoryClientStore() : (IClientStore)new CouchDbClientStore(this.DbService(), this.Logger, this.EventContextResolverService);
            var permissionStore = useInMemoryDB ? new InMemoryPermissionStore() : (IPermissionStore)new CouchDbPermissionStore(this.DbService(), this.Logger, this.EventContextResolverService);

            var clientService = new ClientService(clientStore);
            var roleService = new RoleService(roleStore, permissionStore, clientService);
            var groupService = new GroupService(groupStore, roleStore, userStore, roleService);
            var userService = new UserService(userStore);
            var permissionService = new PermissionService(permissionStore, roleService);

            this.Browser = new Browser(with =>
            {
                with.Module(new RolesModule(
                        roleService,
                        clientService,
                        new Domain.Validators.RoleValidator(roleService),
                        this.Logger));

                with.Module(new ClientsModule(
                        clientService,
                        new Domain.Validators.ClientValidator(clientService),
                        this.Logger));

                with.Module(new UsersModule(
                        clientService,
                        permissionService,
                        userService,
                        new Domain.Validators.UserValidator(),
                        this.Logger));

                with.Module(new GroupsModule(
                        groupService,
                        new Domain.Validators.GroupValidator(groupService),
                        this.Logger));

                with.Module(new PermissionsModule(
                        permissionService,
                        clientService,
                        new Domain.Validators.PermissionValidator(permissionService),
                        this.Logger));

                with.RequestStartup((_, pipelines, context) =>
                {
                    context.CurrentUser = new ClaimsPrincipal(
                        new ClaimsIdentity(new List<Claim>
                        {
                            new Claim(Claims.Scope, Scopes.ManageClientsScope),
                            new Claim(Claims.Scope, Scopes.ReadScope),
                            new Claim(Claims.Scope, Scopes.WriteScope),
                            new Claim(Claims.ClientId, "userprincipal"),
                            new Claim(Claims.Sub, "userprincipal"),
                            new Claim(JwtClaimTypes.Role, Group1),
                            new Claim(JwtClaimTypes.Role, Group2),
                            new Claim(JwtClaimTypes.IdentityProvider, IdentityProvider)
                        }, "userprincipal"));
                    pipelines.BeforeRequest += (ctx) => RequestHooks.SetDefaultVersionInUrl(ctx);
                });
            }, withDefaults => withDefaults.HostName("testhost"));

            this.Browser.Post("/clients", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", "userprincipal");
                with.FormValue("Name", "userprincipal");
                with.Header("Accept", "application/json");
            }).Wait();
        }

        [Fact]
        [DisplayTestMethodName]
        public void TestInheritance_Success()
        {
            var group = Group1;

            // Adding permissions
            var post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "greatgrandfatherpermissions");
            }).Result;

            var ggfperm = post.Body.DeserializeJson<PermissionApiModel>();

            post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "grandfatherpermissions");
            }).Result;

            var gfperm = post.Body.DeserializeJson<PermissionApiModel>();

            post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "fatherpermissions");
            }).Result;

            var fperm = post.Body.DeserializeJson<PermissionApiModel>();

            post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "himselfpermissions");
            }).Result;

            var hsperm = post.Body.DeserializeJson<PermissionApiModel>();

            post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "sonpermissions");
            }).Result;

            var sonperm = post.Body.DeserializeJson<PermissionApiModel>();

            // Adding Roles
            var role = new RoleApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "greatgrandfather",
                Permissions = new List<PermissionApiModel> { ggfperm }
            };

            post = this.Browser.Post("/roles", with => // -3
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.JsonBody(role);
            }).Result;

            var ggf = post.Body.DeserializeJson<RoleApiModel>();

            post = this.Browser.Post("/roles", with => // -2
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                role.Name = "grandfather";
                role.ParentRole = ggf.Id;
                role.Permissions = new List<PermissionApiModel> { gfperm };
                with.JsonBody(role);
            }).Result;

            var gf = post.Body.DeserializeJson<RoleApiModel>();

            post = this.Browser.Post("/roles", with => // -1
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                role.Name = "father";
                role.ParentRole = gf.Id;
                role.Permissions = new List<PermissionApiModel> { fperm };
                with.JsonBody(role);
            }).Result;

            var f = post.Body.DeserializeJson<RoleApiModel>();

            post = this.Browser.Post("/roles", with => // 0
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                role.Name = "himself";
                role.ParentRole = f.Id;
                role.Permissions = new List<PermissionApiModel> { hsperm };
                with.JsonBody(role);
            }).Result;

            var hs = post.Body.DeserializeJson<RoleApiModel>();

            post = this.Browser.Post("/roles", with => // 1
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                role.Name = "son";
                role.ParentRole = hs.Id;
                role.Permissions = new List<PermissionApiModel> { sonperm };
                with.JsonBody(role);
            }).Result;

            post.Body.DeserializeJson<RoleApiModel>();

            // Adding groups
            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", group);
                with.FormValue("GroupName", group);
                with.FormValue("GroupSource", Source1);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post($"/groups/{group}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", hs.Identifier);
                with.Header("Accept", "application/json");
            }).Wait();

            // Get the permissions
            var get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            Assert.True(get.Body.AsString().Contains("greatgrandfatherpermissions"));
            Assert.True(get.Body.AsString().Contains("grandfatherpermissions"));
            Assert.True(get.Body.AsString().Contains("fatherpermissions"));
            Assert.True(get.Body.AsString().Contains("himselfpermissions"));
            Assert.False(get.Body.AsString().Contains("sonpermissions"));
        }

        [Fact]
        [DisplayTestMethodName]
        public void TestGetPermissions_Success()
        {
            // Adding permissions
            var post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "viewpatient");
            }).Result;

            var viewPatientPermission = post.Body.DeserializeJson<PermissionApiModel>();

            post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "editpatient");
            }).Result;

            var editPatientPermission = post.Body.DeserializeJson<PermissionApiModel>();

            var role = new RoleApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "viewer",
                Permissions = new List<PermissionApiModel> { viewPatientPermission }
            };

            post = this.Browser.Post("/roles", with => // -3
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.JsonBody(role);
            }).Result;

            var viewerRole = post.Body.DeserializeJson<RoleApiModel>();

            post = this.Browser.Post("/roles", with => // -2
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                role.Name = "editor";
                role.Permissions = new List<PermissionApiModel> { editPatientPermission };
                with.JsonBody(role);
            }).Result;

            var editorRole = post.Body.DeserializeJson<RoleApiModel>();

            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", Group1);
                with.FormValue("GroupName", Group1);
                with.FormValue("GroupSource", Source1);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", Group2);
                with.FormValue("GroupName", Group2);
                with.FormValue("GroupSource", Source2);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post($"/groups/{Group1}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", viewerRole.Identifier);
            }).Wait();

            this.Browser.Post($"/groups/{Group2}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", editorRole.Identifier);
            }).Wait();

            // Get the permissions
            var get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.Contains("app/userprincipal.editpatient", permissions.Permissions);
            Assert.Contains("app/userprincipal.viewpatient", permissions.Permissions);
            Assert.Equal(2, permissions.Permissions.Count());
        }

        [Fact]
        [DisplayTestMethodName]
        public void TestUserBlacklist_Success()
        {
            // Adding permissions
            var post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "viewpatient");
            }).Result;

            var viewPatientPermission = post.Body.DeserializeJson<PermissionApiModel>();

            post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "editpatient");
            }).Result;

            var editPatientPermission = post.Body.DeserializeJson<PermissionApiModel>();


            // Adding roles
            var role = new RoleApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "viewer",
                Permissions = new List<PermissionApiModel> { viewPatientPermission }
            };

            post = this.Browser.Post("/roles", with => // -3
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.JsonBody(role);
            }).Result;

            var viewerRole = post.Body.DeserializeJson<RoleApiModel>();

            post = this.Browser.Post("/roles", with => // -2
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                role.Name = "editor";
                role.Permissions = new List<PermissionApiModel> { editPatientPermission };
                with.JsonBody(role);
            }).Result;

            var editorRole = post.Body.DeserializeJson<RoleApiModel>();

            // Adding groups
            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", Group1);
                with.FormValue("GroupName", Group1);
                with.FormValue("GroupSource", Source1);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", Group2);
                with.FormValue("GroupName", Group2);
                with.FormValue("GroupSource", Source2);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post($"/groups/{Group1}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", viewerRole.Identifier);
            }).Wait();

            this.Browser.Post($"/groups/{Group2}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", editorRole.Identifier);
            }).Wait();

            // Adding blacklist (user cannot edit patient, even though role allows)
            string subjectId = "userprincipal";

            editPatientPermission.PermissionAction = PermissionAction.Deny;

            this.Browser.Post($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { editPatientPermission };
                with.JsonBody(perms);
            }).Wait();

            // Get the permissions
            var get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.DoesNotContain("app/userprincipal.editpatient", permissions.Permissions);
            Assert.Contains("app/userprincipal.viewpatient", permissions.Permissions);
            Assert.Equal(1, permissions.Permissions.Count());
        }

        [Fact]
        [DisplayTestMethodName]
        public void TestUserWhitelist_Success()
        {
            // Adding permissions
            var post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "viewpatient");
            }).Result;

            var viewPatientPermission = post.Body.DeserializeJson<PermissionApiModel>();

            post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "editpatient");
            }).Result;

            var editPatientPermission = post.Body.DeserializeJson<PermissionApiModel>();

            // Adding roles
            var role = new RoleApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "viewer",
                Permissions = new List<PermissionApiModel> { viewPatientPermission }
            };

            post = this.Browser.Post("/roles", with => // -3
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.JsonBody(role);
            }).Result;

            var viewerRole = post.Body.DeserializeJson<RoleApiModel>();

            post = this.Browser.Post("/roles", with => // -2
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                role.Name = "editor";
                role.Permissions = new List<PermissionApiModel> { editPatientPermission };
                with.JsonBody(role);
            }).Result;

            var editorRole = post.Body.DeserializeJson<RoleApiModel>();

            // Adding groups
            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", Group1);
                with.FormValue("GroupName", Group1);
                with.FormValue("GroupSource", Source1);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", Group2);
                with.FormValue("GroupName", Group2);
                with.FormValue("GroupSource", Source2);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post($"/groups/{Group1}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", viewerRole.Identifier);
            }).Wait();

            this.Browser.Post($"/groups/{Group2}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", editorRole.Identifier);
            }).Wait();

            // Adding permission (user also can modify patient, even though role doesn't)
            var modifyPatientPermission = new PermissionApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "modifypatient",
                PermissionAction = PermissionAction.Allow
            };

            string subjectId = "userprincipal";

            this.Browser.Post($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { modifyPatientPermission };
                with.JsonBody(perms);
            }).Wait();

            // Get the permissions
            var get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.Contains("app/userprincipal.editpatient", permissions.Permissions);
            Assert.Contains("app/userprincipal.viewpatient", permissions.Permissions);
            Assert.Contains("app/userprincipal.modifypatient", permissions.Permissions);
            Assert.Equal(3, permissions.Permissions.Count());
        }

        [Fact]
        [DisplayTestMethodName]
        public void TestRoleBlacklist_Success()
        {
            // Adding permissions
            var post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "viewpatient");
            }).Result;

            var viewPatientPermission = post.Body.DeserializeJson<PermissionApiModel>();

            post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "editpatient");
            }).Result;

            var editPatientPermission = post.Body.DeserializeJson<PermissionApiModel>();


            // Adding roles
            var role = new RoleApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "viewer",
                Permissions = new List<PermissionApiModel> { viewPatientPermission }
            };

            post = this.Browser.Post("/roles", with => // -3
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.JsonBody(role);
            }).Result;

            var viewerRole = post.Body.DeserializeJson<RoleApiModel>();

            post = this.Browser.Post("/roles", with => // -2
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                role.Name = "editor";
                role.Permissions = new List<PermissionApiModel>() { editPatientPermission };

                // Role denies viewPatient permission
                role.DeniedPermissions = new List<PermissionApiModel> { viewPatientPermission };
                with.JsonBody(role);
            }).Result;

            var editorRole = post.Body.DeserializeJson<RoleApiModel>();

            // Adding groups
            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", Group1);
                with.FormValue("GroupName", Group1);
                with.FormValue("GroupSource", Source1);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", Group2);
                with.FormValue("GroupName", Group2);
                with.FormValue("GroupSource", Source2);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post($"/groups/{Group1}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", viewerRole.Identifier);
            }).Wait();

            this.Browser.Post($"/groups/{Group2}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", editorRole.Identifier);
            }).Wait();

            // Get the permissions
            var get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.Contains("app/userprincipal.editpatient", permissions.Permissions);
            Assert.DoesNotContain("app/userprincipal.viewpatient", permissions.Permissions); // Denied by role
            Assert.Equal(1, permissions.Permissions.Count());
        }

        [Fact]
        [DisplayTestMethodName]
        public void TestUserDeniedPermissionHasPrecedence_Success()
        {
            // Adding permissions
            var post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "viewpatient");
            }).Result;

            var viewPatientPermission = post.Body.DeserializeJson<PermissionApiModel>();

            post = this.Browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Grain", "app");
                with.FormValue("SecurableItem", "userprincipal");
                with.FormValue("Name", "editpatient");
            }).Result;

            var editPatientPermission = post.Body.DeserializeJson<PermissionApiModel>();

            // Adding roles
            var role = new RoleApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "viewer",
                Permissions = new List<PermissionApiModel> { viewPatientPermission }
            };

            post = this.Browser.Post("/roles", with => // -3
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.JsonBody(role);
            }).Result;

            var viewerRole = post.Body.DeserializeJson<RoleApiModel>();

            post = this.Browser.Post("/roles", with => // -2
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                role.Name = "editor";
                role.Permissions = new List<PermissionApiModel> { editPatientPermission };
                with.JsonBody(role);
            }).Result;

            var editorRole = post.Body.DeserializeJson<RoleApiModel>();

            // Adding groups
            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", Group1);
                with.FormValue("GroupName", Group1);
                with.FormValue("GroupSource", Source1);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post("/groups", with =>
            {
                with.HttpRequest();
                with.FormValue("Id", Group2);
                with.FormValue("GroupName", Group2);
                with.FormValue("GroupSource", Source2);
                with.Header("Accept", "application/json");
            }).Wait();

            this.Browser.Post($"/groups/{Group1}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", viewerRole.Identifier);
            }).Wait();

            this.Browser.Post($"/groups/{Group2}/roles", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.FormValue("Id", editorRole.Identifier);
            }).Wait();

            // Adding same permission to blacklist and whitellist
            string subjectId = "userprincipal";
            editPatientPermission.PermissionAction = PermissionAction.Deny;

            this.Browser.Post($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { editPatientPermission };
                with.JsonBody(perms);
            }).Wait();

            var modifyPatientPermission = new PermissionApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "modifypatient"
            };

            modifyPatientPermission.PermissionAction = PermissionAction.Allow;

            this.Browser.Post($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { editPatientPermission, modifyPatientPermission };
                with.JsonBody(perms);
            }).Wait();

            // Get the permissions
            var get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.Contains("app/userprincipal.viewpatient", permissions.Permissions); // from role
            Assert.Contains("app/userprincipal.modifypatient", permissions.Permissions); // only whitelisted
            Assert.DoesNotContain("app/userprincipal.editpatient", permissions.Permissions); // from role & backlisted & whitelisted
            Assert.Equal(2, permissions.Permissions.Count());

            // Deny modifypatient and try again            
            modifyPatientPermission.PermissionAction = PermissionAction.Deny;

            this.Browser.Post($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel>() { editPatientPermission, modifyPatientPermission };
                with.JsonBody(perms);
            }).Wait();

            get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.Contains("app/userprincipal.viewpatient", permissions.Permissions);// from role
            Assert.DoesNotContain("app/userprincipal.modifypatient", permissions.Permissions);// backlisted & whitelisted
            Assert.DoesNotContain("app/userprincipal.editpatient", permissions.Permissions);// from role & backlisted & whitelisted
            Assert.Equal(1, permissions.Permissions.Count());
        }

        [Fact]
        [DisplayTestMethodName]
        public void Test_Delete_Success()
        {
            // Adding permission
            var modifyPatientPermission = new PermissionApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "modifypatient",
                PermissionAction = PermissionAction.Allow
            };

            string subjectId = "userprincipal";
            this.Browser.Post($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { modifyPatientPermission };
                with.JsonBody(perms);
            }).Wait();

            // Get the permissions
            var get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.Contains("app/userprincipal.modifypatient", permissions.Permissions);

            //delete the permission
            this.Browser.Delete($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { modifyPatientPermission };
                with.JsonBody(perms);
            }).Wait();

            // Get the permissions
            get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.DoesNotContain("app/userprincipal.modifypatient", permissions.Permissions);
        }

        [Fact]
        [DisplayTestMethodName]
        public void Test_Delete_UserHasNoGranularPermissions()
        {
            // Get the permissions
            var get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.Equal(0, permissions.Permissions.Count());

            var modifyPatientPermission = new PermissionApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "modifypatient",
                PermissionAction = PermissionAction.Allow
            };

            string subjectId = "userprincipal";

            //attempt to delete a permission the user does not have 
            var deleteRequest = this.Browser.Delete($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { modifyPatientPermission };
                with.JsonBody(perms);
            }).Result;

            Assert.Equal(HttpStatusCode.BadRequest, deleteRequest.StatusCode);
            Assert.Contains("Invalid allow permissions: app/userprincipal.modifypatient", deleteRequest.Body.AsString());
            Assert.DoesNotContain("Invalid allow permission actions", deleteRequest.Body.AsString());

        }

        [Fact]
        [DisplayTestMethodName]
        public void Test_Delete_WrongPermissionAction()
        {
            var modifyPatientPermission = new PermissionApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "modifypatient",
                PermissionAction = PermissionAction.Allow
            };

            string subjectId = "userprincipal";

            this.Browser.Post($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { modifyPatientPermission };
                with.JsonBody(perms);
            }).Wait();

            // Get the permissions
            var get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.Contains("app/userprincipal.modifypatient", permissions.Permissions);

            //attempt to delete modifyPatientPermission with permission action Deny
            modifyPatientPermission.PermissionAction = PermissionAction.Deny;

            var deleteRequest = this.Browser.Delete($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { modifyPatientPermission };
                with.JsonBody(perms);
            }).Result;

            Assert.Equal(HttpStatusCode.BadRequest, deleteRequest.StatusCode);
            Assert.Contains("Invalid deny permission actions: app/userprincipal.modifypatient", deleteRequest.Body.AsString());
            Assert.DoesNotContain("Invalid deny permissions", deleteRequest.Body.AsString());
        }

        [Fact]
        [DisplayTestMethodName]
        public void Test_Delete_WrongPermissionAction_InvalidPermission()
        {
            var modifyPatientPermission = new PermissionApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "modifypatient",
                PermissionAction = PermissionAction.Allow
            };

            string subjectId = "userprincipal";

            this.Browser.Post($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { modifyPatientPermission };
                with.JsonBody(perms);
            }).Wait();

            // Get the permissions
            var get = this.Browser.Get("/user/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var permissions = get.Body.DeserializeJson<UserPermissionsApiModel>();
            Assert.Contains("app/userprincipal.modifypatient", permissions.Permissions);

            //attempt to delete modifyPatientPermission with permission action Deny and include an invalid permission
            modifyPatientPermission.PermissionAction = PermissionAction.Deny;

            var deletePatientPermission = new PermissionApiModel()
            {
                Grain = "app",
                SecurableItem = "userprincipal",
                Name = "deletepatient",
                PermissionAction = PermissionAction.Allow
            };

            var deleteRequest = this.Browser.Delete($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel> { modifyPatientPermission, deletePatientPermission };
                with.JsonBody(perms);
            }).Result;

            Assert.Equal(HttpStatusCode.BadRequest, deleteRequest.StatusCode);
            Assert.Contains("Invalid deny permission actions: app/userprincipal.modifypatient", deleteRequest.Body.AsString());
            Assert.DoesNotContain("Invalid deny permissions", deleteRequest.Body.AsString());
            Assert.Contains("Invalid allow permissions: app/userprincipal.deletepatient", deleteRequest.Body.AsString());
            Assert.DoesNotContain("Invalid allow permission actions", deleteRequest.Body.AsString());

        }

        [Fact]
        [DisplayTestMethodName]
        public void Test_AddGranularPermissions_NoPermissionsInBody()
        {
            string subjectId = "userprincipal";

            var postRequest = this.Browser.Post($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel>();
                with.JsonBody(perms);
            }).Result;

            Assert.Equal(HttpStatusCode.BadRequest, postRequest.StatusCode);
        }

        [Fact]
        [DisplayTestMethodName]
        public void Test_DeleteGranularPermissions_NoPermissionsInBody()
        {
            string subjectId = "userprincipal";

            var deleteRequest = this.Browser.Delete($"/user/{IdentityProvider}/{subjectId}/permissions", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                var perms = new List<PermissionApiModel>();
                with.JsonBody(perms);
            }).Result;

            Assert.Equal(HttpStatusCode.BadRequest, deleteRequest.StatusCode);
        }
    }
}