﻿namespace Fabric.Authorization.API.Configuration
{
    public interface ICouchDbSettings
    {
        string DatabaseName { get; set; }
        string Server { get; set; }
        string Username { get; set; }
        string Password { get; set; }
    }
}