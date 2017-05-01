﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fabric.Authorization.Domain;

namespace Fabric.Authorization.API.Models
{
    public class GroupRoleApiModel : ApiModelBase
    {
        public string GroupName { get; set; }
        public IEnumerable<RoleApiModel> Roles { get; set; }
    }
}