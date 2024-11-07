/********************************************************************************
* RequireRedisAttribute.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Diagnostics;

using StackExchange.Redis;

namespace Solti.Utils.Eventing.Tests
{
    using Abstractions.Tests;

    public sealed class RequireRedisAttribute() : RequireExternalServiceAttribute("redis:7.4.1", 6379)
    {
        private ConnectionMultiplexer FConnection = null!;

        public override bool TryConnect()
        {
            try
            {
                FConnection = ConnectionMultiplexer.Connect("localhost,allowAdmin=true"); ;
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Cannot connect to redis instance: {e.Message}");
                return false;
            }
        }

        public override void CloseConnection()
        {
            FConnection?.Dispose();
            FConnection = null!;
        }

        public override void TearDownTest()
        {
            foreach (IServer server in FConnection.GetServers())
            {
                server.FlushAllDatabases();
            }
        }
    }
}
