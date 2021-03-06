﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fabric.Authorization.Domain.Models;

namespace Fabric.Authorization.Domain.Stores.InMemory
{
    public class InMemoryRoleStore : InMemoryGenericStore<Role>, IRoleStore
    {
        public override async Task<Role> Add(Role model)
        {
            model.Id = Guid.NewGuid();
            return await base.Add(model);
        }

        public async Task<bool> Exists(Guid id)
        {
            return await Exists(id.ToString());
        }

        public async Task<Role> Get(Guid id)
        {
            return await Get(id.ToString());
        }

        public Task<IEnumerable<Role>> GetRoles(string grain = null, string securableItem = null,
            string roleName = null)
        {
            var roles = Dictionary.Select(kvp => kvp.Value);
            if (!string.IsNullOrEmpty(grain))
            {
                roles = roles.Where(r => r.Grain == grain);
            }
            if (!string.IsNullOrEmpty(securableItem))
            {
                roles = roles.Where(r => r.SecurableItem == securableItem);
            }
            if (!string.IsNullOrEmpty(roleName))
            {
                roles = roles.Where(r => r.Name == roleName);
            }
            return Task.FromResult(roles.Where(r => !r.IsDeleted));
        }
    }
}