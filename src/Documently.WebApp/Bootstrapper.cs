using System;
using System.Web;
using Documently.Infrastructure;
using Documently.Messages.DocMetaEvents;
using Documently.ReadModel;
using Documently.WebApp.Handlers.DocumentMetaData;
using EventStore;
using EventStore.Dispatcher;
using EventStore.Logging.NLog;
using EventStore.Persistence;
using EventStore.Serialization;
using Magnum.Extensions;
using Magnum.Policies;
using MassTransit;
using MassTransit.NLogIntegration;
using MassTransit.Pipeline.Inspectors;
using NLog;
using Raven.Client;
using Raven.Client.Document;
using StructureMap;
using StructureMap.Configuration.DSL;

namespace Documently.WebApp
{
	public class Bootstrapper
	{

		public static IContainer CreateContainer()
		{
			var container = new Container(
				x =>
				{
					x.AddRegistry<RavenDbServerRegistry>();
					x.AddRegistry<ReadRepositoryRegistry>();
					x.ForConcreteType<PostHandler>().Configure.Ctor<IServiceBus>();
					x.ForConcreteType<Handlers.DocumentList.GetHandler>().Configure.Ctor<IReadRepository>();
					x.AddRegistry<EventStoreRegistry>();
					x.AddRegistry(new BusRegistry("rabbitmq://localhost/Documently.WebApp"));
				});
			container.Inject(container);
//			PipelineViewer.Trace(container.GetInstance<IServiceBus>().InboundPipeline);
			return container;
		}
	}

	public class EventStoreRegistry : Registry
	{
		static readonly Logger _logger = LogManager.GetCurrentClassLogger();
		public EventStoreRegistry()
		{
			For<ExceptionPolicy>()
				.Transient()
				.Use(() => ExceptionPolicy.InCaseOf<StorageUnavailableException>()
				           	.CircuitBreak(4.Seconds(), 5))
				.Named("eventstore");
			For<IStoreEvents>().Use(ctx =>
			{
				var policy = ctx.GetInstance<ExceptionPolicy>("eventstore");
				var wup = Wireup.Init()
							.LogTo(t => new NLogLogger(t))
							.UsingAsynchronousDispatchScheduler(ctx.GetInstance<IDispatchCommits>())
							.UsingRavenPersistence(Keys.RavenDbConnectionStringName)
							.ConsistentQueries()
							.PageEvery(int.MaxValue)
							.MaxServerPageSizeConfiguration(1024)
							.UsingCustomSerialization(BuildSerializer());
				while (true)
					try { return policy.Do(() => wup.Build()); }
					catch (StorageUnavailableException) { _logger.Error("Event Store unavailable, retrying with '{0}'", policy); }
			});

		}
		private readonly byte[] _encryptionKey = new byte[]
		{
			0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9,
			0xa, 0xb, 0xc, 0xd, 0xe, 0xf
		};
		private ISerialize BuildSerializer()
		{
			var serializer = new JsonSerializer() as ISerialize;
			serializer = new GzipSerializer(serializer);
			return new RijndaelSerializer(serializer, _encryptionKey);
		}
	}
	public class BusRegistry : Registry
	{
		private readonly string _EndpointUri;

		public BusRegistry(string endpointUri)
		{
			_EndpointUri = endpointUri;

			//if (!container.Kernel.GetFacilities()
			//    .Any(x => x.GetType().Equals(typeof(TypedFactoryFacility))))
			//    container.AddFacility<TypedFactoryFacility>();

			// masstransit bus
			//var busAdapter = new MassTransitPublisher(bus);

			For<IServiceBus>()
				.Singleton()
				.Use(ctx => ServiceBusFactory.New(sbc =>
				                                  	{
				                                  		sbc.ReceiveFrom(_EndpointUri);
				                                  		sbc.UseRabbitMqRouting();
				                                  		sbc.UseNLog();
				                                  		sbc.Subscribe(c => c.LoadFrom(ctx.GetInstance<IContainer>()));
				                                  	}));
			For<IBus>().Singleton()
				.Use<MassTransitPublisher>().Ctor<IServiceBus>();
			ObjectFactory.Initialize(x => x.Forward<IBus, IDispatchCommits>());
		}
	}

	public class RavenDbServerRegistry : Registry
	{
		public RavenDbServerRegistry()
		{
			//use RavenDB Server as an event store and persists the read side (views) also to RavenDB Server
			IDocumentStore viewStore = new DocumentStore { ConnectionStringName = Keys.RavenDbConnectionStringName }.Initialize();
			For<IDocumentStore>().Singleton().Use(viewStore);
		}
	}

	public class ReadRepositoryRegistry : Registry
	{
		public ReadRepositoryRegistry()
		{
			Scan(a =>
			{
				a.AssemblyContainingType<IReadRepository>();
				a.AddAllTypesOf<IReadRepository>();
			});
			For<HandlesEvent<Created>>().Use<DocumentMetaListView>();
//			For(typeof(Consumes<>.All));
		}
	}


}