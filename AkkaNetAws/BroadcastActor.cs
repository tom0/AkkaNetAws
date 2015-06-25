namespace AkkaNetAws
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Akka.Actor;
    using Akka.Cluster;
    using NLog;

    public class BroadcastActor : ReceiveActor
    {
        private readonly ILogger _log = LogManager.GetCurrentClassLogger();
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

        protected override void PostStop()
        {
            _cluster.Unsubscribe(Self);
        }

        public BroadcastActor()
        {
            Receive<string>(m =>
            {
                _log.Info($"************ Message from [{Sender.Path.ToString()}] : [{m}]");
            });

            Receive<Message>(m =>
            {
                _log.Info($"Message: {m.Text}");
                foreach (var path in _members.Select(PathOf))
                {
                    path.Tell(m.Text);
                }
            });

            Receive<ClusterEvent.MemberUp>(m =>
            {
                _log.Info($"Seed nodes = {string.Join(", ", _cluster.Settings.SeedNodes.Select(sn => sn.ToString()))}");
                _log.Info($"MemberUp {m.Member.Address.ToString()}");
                _members.Add(m.Member);
            });

            Receive<ClusterEvent.MemberExited>(m =>
            {
                _log.Info($"MemberExited {m.Member.Address.ToString()}");
                _members.Remove(m.Member);
            });

            Receive<ClusterEvent.MemberRemoved>(m =>
            {
                _log.Info($"MemberRemoved {m.Member.Address.ToString()}");
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