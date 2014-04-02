﻿using System;
using System.Collections.Generic;
using System.Reflection;
using R.MessageBus.Client.RabbitMQ;
using R.MessageBus.Container;
using R.MessageBus.Interfaces;
using R.MessageBus.Persistance.MongoDb;
using R.MessageBus.Settings;
using Queue = R.MessageBus.Interfaces.Queue;

namespace R.MessageBus
{
    /// <summary>
    /// Bus configuration.
    /// 
    /// Implicit initialization <see cref="Configuration"/>:
    /// Initialize from default values.
    /// 
    /// Explicit initialization <see cref="LoadSettings"/>:
    /// Initialize from BusSettings section of a custom configuration file,
    /// throw exception if the section is not found
    /// </summary>
    public class Configuration : IConfiguration
    {
        private const string DefaultDatabaseName = "RMessageBusPersistantStore";
        private const string DefaultConnectionString = "mongodb://localhost/";
        private const string DefaultHost= "localhost";

        #region Private Fields

        private string _configurationPath;
        private string _endPoint;

        #endregion

        #region Public Properties

        public Type ConsumerType { get; set; }
        public Type ProducerType { get; set; }
        public Type Container { get; set; }
        public Type ProcessManagerFinder { get; set; }
        public bool ScanForMesssageHandlers { get; set; }
        public string PersistenceStoreConnectionString { get; set; }
        public string PersistenceStoreDatabaseName { get; set; }
        public ITransportSettings TransportSettings { get; set; }
        public IDictionary<string, string> EndPointMappings { get; set; } 

        #endregion

        public Configuration()
        {
            var defaultQueueName = Assembly.GetEntryAssembly() != null ? Assembly.GetEntryAssembly().GetName().Name : System.Diagnostics.Process.GetCurrentProcess().ProcessName;

            TransportSettings = new TransportSettings { Queue = new Queue
            {
                Name = defaultQueueName
            }};

            _configurationPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            SetTransportSettings();
            SetPersistanceSettings();

            EndPointMappings = new Dictionary<string, string>();

            ConsumerType = typeof(Consumer);
            ProducerType = typeof(Producer);
            Container = typeof(StructuremapContainer);
            ProcessManagerFinder = typeof(MongoDbProcessManagerFinder);
        }

        /// <summary>
        /// Adds a message endpoint mapping. 
        /// </summary>
        /// <param name="messageType">Type of message</param>
        /// <param name="endPoint">Endpoint to send the message to</param>
        public void AddEndPointMapping(Type messageType, string endPoint)
        {
            EndPointMappings.Add(messageType.FullName, endPoint);
        }

        /// <summary>
        /// Load settings from configFilePath. 
        /// Use default App.config when configFilePath is not specified. 
        /// </summary>
        /// <param name="configFilePath">configuration file path</param>
        /// <param name="endPoint">RabbitMq settings endpoint name</param>
        public void LoadSettings(string configFilePath = null, string endPoint = null)
        {
            if (null != configFilePath)
            {
                _configurationPath = configFilePath;
            }

            _endPoint = endPoint;

            //todo: candidate for IoC
            var configurationManager = new ConfigurationManagerWrapper(_configurationPath);

            var section = configurationManager.GetSection<BusSettings.BusSettings>("BusSettings");

            if (section == null) throw new ArgumentException("BusSettings section not found in the configuration file.");

            SetTransportSettings(section);
            SetPersistanceSettings(section);
        }
        
        /// <summary>
        /// Sets the container.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void SetContainer<T>() where T : class, IBusContainer
        {
            Container = typeof(T);
        }

        /// <summary>
        /// Sets the process manager finder
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void SetProcessManagerFinder<T>() where T : class, IProcessManagerFinder
        {
            ProcessManagerFinder = typeof (T);
        }

        /// <summary>
        /// Sets consumer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void SetConsumer<T>() where T : class, IConsumer 
        {
            ConsumerType = typeof(T);
        }

        /// <summary>
        /// Sets publisher
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void SetProducer<T>() where T : class, IProducer
        {
            ProducerType = typeof(T);
        }

        /// <summary>
        /// Sets QueueName
        /// </summary>
        public void SetQueueName(string queueName)
        {
            TransportSettings.Queue.Name = queueName;
        }

        /// <summary>
        /// Gets QueueName
        /// </summary>
        public string GetQueueName()
        {
            return TransportSettings.Queue.Name;
        }

        /// <summary>
        /// Gets instance of IConsumer type
        /// </summary>
        /// <returns></returns>
        public IConsumer GetConsumer()
        {
            return (IConsumer)Activator.CreateInstance(ConsumerType, TransportSettings);
        }

        /// <summary>
        /// Gets instance of IProducer type
        /// </summary>
        /// <returns></returns>
        public IProducer GetProducer()
        {
            return (IProducer)Activator.CreateInstance(ProducerType, TransportSettings, EndPointMappings);
        }

        /// <summary>
        /// Gets instance of IBusContainer type
        /// </summary>
        /// <returns></returns>
        public IBusContainer GetContainer()
        {
            return (IBusContainer)Activator.CreateInstance(Container);
        }

        /// <summary>
        /// Gets instance of IProcessManagerFinder type
        /// </summary>
        /// <returns></returns>
        public IProcessManagerFinder GetProcessManagerFinder()
        {
            return (IProcessManagerFinder)Activator.CreateInstance(ProcessManagerFinder, PersistenceStoreConnectionString, PersistenceStoreDatabaseName);
        }

        #region Private Methods

        private void SetTransportSettings(BusSettings.BusSettings section = null)
        {
            if (null != section)
            {
                var endPointSettings = !string.IsNullOrEmpty(_endPoint) ? section.EndpointSettings.GetItemByKey(_endPoint) : section.EndpointSettings.GetItemAt(0);
                var transportSettings = endPointSettings.TransportSettings;

                if (null != transportSettings)
                {
                    TransportSettings = GetTransportSettingsFromBusSettings(transportSettings);

                    return;
                }
            }

            // Set defaults
            TransportSettings = GetTransportSettingsFromDefaults();
        }

        private void SetPersistanceSettings(BusSettings.BusSettings section = null)
        {
            if (null != section)
            {
                var endPointSettings = !string.IsNullOrEmpty(_endPoint) ? section.EndpointSettings.GetItemByKey(_endPoint) : section.EndpointSettings.GetItemAt(0);
                var persistanceSettings = endPointSettings.PersistanceSettings;

                if (null != persistanceSettings)
                {
                    PersistenceStoreDatabaseName = !string.IsNullOrEmpty(persistanceSettings.Database) ? persistanceSettings.Database : DefaultDatabaseName;
                    PersistenceStoreConnectionString = PersistenceStoreConnectionString ?? persistanceSettings.ConnectionString;

                    return;
                }
            }

            // Set defaults
            PersistenceStoreDatabaseName = PersistenceStoreDatabaseName ?? DefaultDatabaseName;
            PersistenceStoreConnectionString = PersistenceStoreConnectionString ?? DefaultConnectionString;
        }

        private ITransportSettings GetTransportSettingsFromBusSettings(BusConfiguration.TransportSettings settings)
        {
            ITransportSettings transportSettings = new TransportSettings();
            transportSettings.Host = settings.Host;
            transportSettings.MaxRetries = settings.Retries.MaxRetries;
            transportSettings.RetryDelay = settings.Retries.RetryDelay;
            transportSettings.Username = settings.Username;
            transportSettings.Password = settings.Password;
            transportSettings.NoAck = settings.NoAck;
            transportSettings.Queue = new Queue
            {
                Name = string.IsNullOrEmpty(TransportSettings.Queue.Name) ? settings.Queue.Name : TransportSettings.Queue.Name,
                RoutingKey = settings.Queue.RoutingKey,
                Arguments = GetQueueArguments(settings),
                AutoDelete = settings.Queue.AutoDelete,
                Durable = settings.Queue.Durable,
                Exclusive = settings.Queue.Exclusive,
                IsReadOnly = settings.Queue.IsReadOnly()
            };

            return transportSettings;
        }

        private ITransportSettings GetTransportSettingsFromDefaults()
        {
            ITransportSettings transportSettings = new TransportSettings();
            transportSettings.Host = DefaultHost;
            transportSettings.MaxRetries = 3;
            transportSettings.RetryDelay = 3000;
            transportSettings.Username = null;
            transportSettings.Password = null;
            transportSettings.NoAck = false;
            transportSettings.Queue = new Queue
            {
                Name = TransportSettings.Queue.Name,
                RoutingKey = null,
                Arguments = null,
                AutoDelete = false,
                Durable = true,
                Exclusive = false,
                IsReadOnly = false
            };

            return transportSettings;
        }

        private static Dictionary<string, object> GetQueueArguments(BusConfiguration.TransportSettings settings)
        {
            var queueArguments = new Dictionary<string, object>();
            for (var i = 0; i < settings.Queue.Arguments.Count; i++)
            {
                queueArguments.Add(settings.Queue.Arguments[i].Name, settings.Queue.Arguments[i].Value);
            }
            return queueArguments;
        }

        #endregion
    }
}