namespace AkkaNetAws
{
    using System;
    using Akka.Actor;

    class Program
    {
        private static ActorSystem _actorSystem;

        static void Main()
        {
            var random = new Random();

            var akkaConfig = new AkkaConfigProvider(new AwsClusterContext()).GetConfigAsync().Result;

            _actorSystem = ActorSystem.Create(Properties.Settings.Default.AkkaActorSystem, akkaConfig);
            var broadcaster = _actorSystem.ActorOf(Props.Create<BroadcastActor>(), "broadcast");
            var t = new System.Threading.Timer(_ => broadcaster.Tell(new Message($"Yo: {random.Next()}")), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            Console.ReadKey();
        }
    }
}
