﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Fabric.Authorization.API.Constants
{
    public static class Scopes
    {
        public static readonly string ReadScope = "fabric/authorization.read";
        public static readonly string WriteScope = "fabric/authorization.write";
        public static readonly string ManageClientsScope = "fabric/authorization.manageclients";
    }
}
