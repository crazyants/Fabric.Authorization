﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fabric.Authorization.API.Models;
using Fabric.Authorization.Domain.Exceptions;
using Fabric.Authorization.Domain.Models;
using Fabric.Authorization.Domain.Stores.Services;
using Fabric.Authorization.Domain.Validators;
using Nancy;
using Nancy.ModelBinding;
using Serilog;

namespace Fabric.Authorization.API.Modules
{
    public class RolesModule : FabricModule<Role>
    {
        private readonly ClientService _clientService;
        private readonly RoleService _roleService;

        public RolesModule(RoleService roleService, ClientService clientService, RoleValidator validator,
            ILogger logger) : base("/v1/roles", logger, validator)
        {
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
            _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));

            Get("/{grain}/{securableItem}",
                async parameters => await this.GetRolesForSecurableItem(parameters).ConfigureAwait(false), null,
                "GetRolesBySecurableItem");

            Get("/{grain}/{securableItem}/{roleName}",
                async parameters => await this.GetRoleByName(parameters).ConfigureAwait(false), null, "GetRoleByName");

            Post("/", async parameters => await AddRole().ConfigureAwait(false), null, "AddRole");

            Delete("/{roleId}", async parameters => await this.DeleteRole(parameters).ConfigureAwait(false), null,
                "DeleteRole");

            Post("/{roleId}/permissions",
                async parameters => await this.AddPermissionsToRole(parameters).ConfigureAwait(false), null,
                "AddPermissionsToRole");

            Delete("/{roleId}/permissions",
                async parameters => await this.DeletePermissionsFromRole(parameters).ConfigureAwait(false), null,
                "DeletePermissionsFromRole");
        }

        private async Task<dynamic> GetRolesForSecurableItem(dynamic parameters)
        {
            await CheckAccess(_clientService, parameters.grain, parameters.securableItem, AuthorizationReadClaim);
            IEnumerable<Role> roles = await _roleService.GetRoles(parameters.grain, parameters.securableItem);
            return roles.Select(r => r.ToRoleApiModel());
        }

        private async Task<dynamic> GetRoleByName(dynamic parameters)
        {
            await CheckAccess(_clientService, parameters.grain, parameters.securableItem, AuthorizationReadClaim);
            IEnumerable<Role> roles =
                await _roleService.GetRoles(parameters.grain, parameters.securableItem, parameters.roleName);
            return roles.Select(r => r.ToRoleApiModel());
        }

        private async Task<dynamic> AddRole()
        {
            var roleApiModel = this.Bind<RoleApiModel>(binderIgnore => binderIgnore.Id,
                binderIgnore => binderIgnore.CreatedBy,
                binderIgnore => binderIgnore.CreatedDateTimeUtc,
                binderIgnore => binderIgnore.ModifiedDateTimeUtc,
                binderIgnore => binderIgnore.ModifiedBy);

            var incomingRole = roleApiModel.ToRoleDomainModel();
            Validate(incomingRole);
            await CheckAccess(_clientService, roleApiModel.Grain, roleApiModel.SecurableItem, AuthorizationWriteClaim);
            var role = await _roleService.AddRole(incomingRole);
            return CreateSuccessfulPostResponse(role.ToRoleApiModel());
        }

        private async Task<dynamic> DeleteRole(dynamic parameters)
        {
            try
            {
                if (!Guid.TryParse(parameters.roleId, out Guid roleId))
                    return CreateFailureResponse("roleId must be a guid.", HttpStatusCode.BadRequest);

                var roleToDelete = await _roleService.GetRole(roleId);
                await CheckAccess(_clientService, roleToDelete.Grain, roleToDelete.SecurableItem,
                    AuthorizationWriteClaim);
                await _roleService.DeleteRole(roleToDelete);
                return HttpStatusCode.NoContent;
            }
            catch (NotFoundException<Role> ex)
            {
                Logger.Error(ex, ex.Message, parameters.roleId);
                return CreateFailureResponse(ex.Message, HttpStatusCode.NotFound);
            }
        }

        private async Task<dynamic> AddPermissionsToRole(dynamic parameters)
        {
            try
            {
                var permissionApiModels = this.Bind<List<PermissionApiModel>>(new BindingConfig {BodyOnly = true});

                if (!Guid.TryParse(parameters.roleId, out Guid roleId))
                    return CreateFailureResponse("roleId must be a guid.", HttpStatusCode.BadRequest);

                if (permissionApiModels.Count == 0)
                    return CreateFailureResponse(
                        "No permissions specified to add, ensure an array of permissions is included in the request.",
                        HttpStatusCode.BadRequest);

                var roleToUpdate = await _roleService.GetRole(roleId);
                await CheckAccess(_clientService, roleToUpdate.Grain, roleToUpdate.SecurableItem,
                    AuthorizationWriteClaim);
                var updatedRole = await _roleService.AddPermissionsToRole(roleToUpdate,
                    permissionApiModels.Where(p => p.Id.HasValue).Select(p => p.Id.Value).ToArray());
                return CreateSuccessfulPostResponse(updatedRole.ToRoleApiModel());
            }
            catch (NotFoundException<Role> ex)
            {
                return CreateFailureResponse(ex.Message, HttpStatusCode.NotFound);
            }
            catch (NotFoundException<Permission> ex)
            {
                return CreateFailureResponse(ex.Message, HttpStatusCode.NotFound);
            }
            catch (IncompatiblePermissionException ex)
            {
                return CreateFailureResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        private async Task<dynamic> DeletePermissionsFromRole(dynamic parameters)
        {
            try
            {
                var permissionApiModels = this.Bind<List<PermissionApiModel>>(new BindingConfig {BodyOnly = true});

                if (!Guid.TryParse(parameters.roleId, out Guid roleId))
                    return CreateFailureResponse("roleId must be a guid.", HttpStatusCode.BadRequest);

                if (permissionApiModels.Count == 0)
                    return CreateFailureResponse(
                        "No permissions specified to add, ensure an array of permissions is included in the request.",
                        HttpStatusCode.BadRequest);

                var roleToUpdate = await _roleService.GetRole(roleId);
                await CheckAccess(_clientService, roleToUpdate.Grain, roleToUpdate.SecurableItem,
                    AuthorizationWriteClaim);
                var updatedRole = await _roleService.RemovePermissionsFromRole(roleToUpdate,
                    permissionApiModels.Where(p => p.Id.HasValue).Select(p => p.Id.Value).ToArray());
                return CreateSuccessfulPostResponse(updatedRole.ToRoleApiModel());
            }
            catch (NotFoundException<Role> ex)
            {
                return CreateFailureResponse(ex.Message, HttpStatusCode.NotFound);
            }
            catch (NotFoundException<Permission> ex)
            {
                return CreateFailureResponse(ex.Message, HttpStatusCode.NotFound);
            }
        }
    }
}