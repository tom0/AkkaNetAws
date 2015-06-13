namespace AkkaNetAws
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Akka.Actor;
    using Akka.Cluster;

    public class BroadcastActor : ReceiveActor
    {
        private readonly Cluster _cluster = Cluster.Get(Context.System);
        private readonly ISet<Member> _members = new HashSet<Member>();

        protected override void PreStart()
        {
            _cluster.Subscribe(Self, ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents,
                new[]
                {
                    typeof (ClusterEvent.MemberUp),
                    typeof (ClusterEvent.MemberExited),
                    typeof (ClusterEvent.MemberRemoved),
                    typeof (ClusterEvent.UnreachableMember)
                });
        }

        public BroadcastActor()
        {
            Receive<string>(m =>
            {
                Console.WriteLine($"************ Message from [{Sender.Path.ToString()}] : [{m}]");
            });

            Receive<Message>(m =>
            {
                Console.WriteLine($"Message: {m.Text}");
                foreach (var path in _members.Select(PathOf))
                {
                    path.Tell(m.Text);
                }
            });

            Receive<ClusterEvent.MemberUp>(m =>
            {
                Console.WriteLine($"Seed nodes = {_cluster.Settings.SeedNodes.Select(sn => sn.ToString())}");
                Console.WriteLine($"MemberUp {m.Member.Address.ToString()}");
                _members.Add(m.Member);
            });

            Receive<ClusterEvent.MemberExited>(m =>
            {
                Console.WriteLine($"MemberExited {m.Member.Address.ToString()}");
                _members.Remove(m.Member);
            });

            Receive<ClusterEvent.MemberRemoved>(m =>
            {
                Console.WriteLine($"MemberRemoved {m.Member.Address.ToString()}");
                _members.Remove(m.Member);
            });

            Receive<ClusterEvent.UnreachableMember>(m => { });
        }

        private ActorSelection PathOf(Member member)
        {
            return Context.ActorSelection(new RootActorPath(member.Address)/"user"/Self.Path.Name);
        }
    }

    public sealed class Message
    {
        public string Text { get; }

        public Message(string message)
        {
            Text = message;
        }
    }
}