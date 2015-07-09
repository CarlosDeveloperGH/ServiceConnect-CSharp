﻿using System;
using PointToPoint.ZeroMQ.Messages;
using R.MessageBus.Interfaces;

namespace PointToPoint.ZeroMQ.Consumer2
{
    public class PointToPointMessageHandler : IMessageHandler<PointToPointMessage>
    {
        public void Execute(PointToPointMessage command)
        {
            Console.WriteLine("Received message - {0} {1}", command.Count, DateTime.Now);
        }

        public IConsumeContext Context { get; set; }
    }
}
