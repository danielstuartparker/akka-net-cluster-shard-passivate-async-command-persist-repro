using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Persistence;

namespace AsyncCommandBug
{
    public class ReproActor : ReceivePersistentActor
    {
        public override string PersistenceId
        {
            get
            {
                return Self.Path.Name;
            }
        }

        public ReproActor()
        {
            var sharding = ClusterSharding.Get(Context.System);
            var shardRegion = sharding.ShardRegion(nameof(ReproActor));

            // Set a timer to passivate the shard while an async command is processing
            Context.SetReceiveTimeout(TimeSpan.FromSeconds(1));

            Command<ReceiveTimeout>(receiveTimeout =>
            {
                Console.WriteLine("Passivating Shard");
                Context.Parent.Tell(new Passivate(PoisonPill.Instance));
                this.SetReceiveTimeout(null);
            });

            CommandAsync<LongAsyncMessage>(async msg => {
                Console.WriteLine("Async Command Received");
                await Task.Delay(2000); // simulate doing work, like an external REST call
                Console.WriteLine("Async Command Processed");
                Persist("result", result => {
                    Console.WriteLine("Async Command Results Persisted");
                    shardRegion.Tell(new NonAsyncMessage());
                    shardRegion.Tell(new AsyncMessage());
                });
            });

            Command<NonAsyncMessage>(msg =>
            {
                Console.WriteLine("NonAsyncMessage Received");
                Persist("result", result => {
                    Console.WriteLine("NonAsyncMessage Persisted");
                });
            });

            CommandAsync<AsyncMessage>(async msg =>
            {
                Console.WriteLine("AsyncMessage Received");
                Persist("result", result => {
                    // BUG
                    // This should always succeed and print with the other 2
                    // However, if the parent gets a passivate, the upper 2 will execute
                    // and this will never run
                    // https://getakka.net/api/Akka.Persistence.ReceivePersistentActor.html#Akka_Persistence_ReceivePersistentActor_CommandAsync_System_Type_System_Predicate_System_Object__System_Func_System_Object_System_Threading_Tasks_Task__
                    Console.WriteLine("AsyncMessage Persisted");
                });
                await Task.Yield();
            });
        }

        protected override void PreStart() => Console.WriteLine("Actor Starting");

        protected override void PostStop() => Console.WriteLine("Actor Stopped");
    }
}

