using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Persistence;

namespace AsyncCommandBug
{
    public class ReproActor : ReceivePersistentActor
    {
        private int _asyncOperations = 0;
        private bool _stopping = false;
        public override string PersistenceId => Self.Path.Name;

        public ReproActor()
        {
            var sharding = ClusterSharding.Get(Context.System);
            var shardRegion = sharding.ShardRegion(nameof(ReproActor));

            // Set a timer to passivate the shard while an async command is processing
            Context.SetReceiveTimeout(TimeSpan.FromSeconds(1));

            Command<ReceiveTimeout>(receiveTimeout =>
            {
                Console.WriteLine("Passivating Shard");
                Context.Parent.Tell(new Passivate(new StopEntity()));
                SetReceiveTimeout(null);
            });

            Command<StopEntity>(_ =>
            {
                _stopping = true;
                if(_asyncOperations == 0)
                    Context.Stop(Self);
            });

            CommandAsync<LongAsyncMessage>(async msg => {
                _asyncOperations++;
                Console.WriteLine("Async Command Received");
                await Task.Delay(2000); // simulate doing work, like an external REST call
                Console.WriteLine("Async Command Processed");
                Persist("result", result =>
                {
                    Console.WriteLine("Async Command Results Persisted");
                    shardRegion.Tell(new NonAsyncMessage());
                    shardRegion.Tell(new AsyncMessage());
                    _asyncOperations--;
                });
            });

            Command<NonAsyncMessage>(msg =>
            {
                Console.WriteLine("NonAsyncMessage Received");
                Persist("result", result => {
                    Console.WriteLine("NonAsyncMessage Persisted");
                });
            });

            CommandAsync<AsyncMessage>(msg =>
            {
                _asyncOperations++;
                var self = Self;
                Console.WriteLine("AsyncMessage Received");
                Persist("result", result => {
                    // BUG
                    // This should always succeed and print with the other 2
                    // However, if the parent gets a passivate, the upper 2 will execute
                    // and this will never run
                    // https://getakka.net/api/Akka.Persistence.ReceivePersistentActor.html#Akka_Persistence_ReceivePersistentActor_CommandAsync_System_Type_System_Predicate_System_Object__System_Func_System_Object_System_Threading_Tasks_Task__
                    Console.WriteLine("AsyncMessage Persisted");
                    
                    // This test should only be done at the leaf operations
                    _asyncOperations--;
                    if (_stopping && _asyncOperations == 0)
                    {
                        Context.Stop(self);
                    }
                });
                return Task.CompletedTask;
            });
        }

        protected override void PreStart() => Console.WriteLine("Actor Starting");

        protected override void PostStop() => Console.WriteLine("Actor Stopped");
    }
}

