﻿using System;
using ServiceConnect;
using Ssl.Messages;

namespace Ssl.Producer
{
    class Program
    {
        static void Main(string[] args)
        {
            var bus = Bus.Initialize(config =>
            {
                config.TransportSettings.SslEnabled = true;
                config.TransportSettings.ServerName = "SslTest";
                config.SetQueueName("Ssl.Producer");
            });

            bus.Send("Ssl.Consumer", new SslMessage(Guid.NewGuid()));
        }
    }
}
