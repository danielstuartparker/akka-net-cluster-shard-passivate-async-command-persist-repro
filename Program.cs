using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Configuration;
using Akka.Configuration.Hocon;

namespace AsyncCommandBug
{
    class Program
    {
        static void Main(string[] args)
        {
            Start();
            Thread.Sleep(20000);      
            Console.ReadLine();
        }

        static async void Start()
        {
            var section = (AkkaConfigurationSection)ConfigurationManager.GetSection("akka");
            //Override the configuration of the port
            var config =
                ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.port=2551")
                    .WithFallback(section.AkkaConfig);

            var system = ActorSystem.Create("ReproSystem", config);

            var sharding = ClusterSharding.Get(system);
            var shardRegion = await sharding.StartAsync(
                typeName: nameof(ReproActor),
                entityPropsFactory: e => Props.Create(() => new ReproActor()),
                settings: ClusterShardingSettings.Create(system),
                messageExtractor: new Extractor());

            shardRegion.Tell(new LongAsyncMessage());

            await Task.Delay(15000);
            await system.Terminate();
        }
    }
}
