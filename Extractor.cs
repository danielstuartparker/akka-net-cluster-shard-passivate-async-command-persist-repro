using Akka.Cluster.Sharding;

namespace AsyncCommandBug
{
    public class Extractor : IMessageExtractor
    {
        public string EntityId(object message)
        {
            return "routingId";
        }

        public object EntityMessage(object message)
        {
            // Just forward
            return message;
        }

        public string ShardId(object message)
        {
            return "routingId";
        }
    }
}
