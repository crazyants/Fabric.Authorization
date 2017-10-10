﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Fabric.Authorization.Domain.Exceptions
{
    public class InvalidPermissionException : Exception
    {
        public InvalidPermissionException()
        {
        }

        public InvalidPermissionException(string message) : base(message)
        {
        }

        public InvalidPermissionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
