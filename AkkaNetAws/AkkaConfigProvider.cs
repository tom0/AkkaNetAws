namespace AkkaNetAws
{
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Akka.Configuration;
    using Properties;

    internal class AkkaConfigProvider
    {
        private readonly IClusterContext _clusterContext;
        private readonly ushort _akkaPort;
        private readonly string _akkaActorSystem;

        public AkkaConfigProvider(IClusterContext clusterContext)
        {
            _clusterContext = clusterContext;
            _akkaPort = Settings.Default.AkkaPort;
            _akkaActorSystem = Settings.Default.AkkaActorSystem;
        }

        public async Task<Config> GetConfigAsync()
        {
            var defaultConfig = ConfigurationFactory.Load();

            var ipAddress = await _clusterContext.GetCurrentInstanceIpAddress();
            var siblingIps = await _clusterContext.GetSiblingInstancesIpAddresses();

            if (siblingIps.Count > 1)
            {
                // It is only valid to have your own IP in the seed-nodes when
                // you are the first instance running.
                siblingIps.Remove(ipAddress);                
            }
            var seedUrls = siblingIps.Select(ip => $"\"akka.tcp://{_akkaActorSystem}@{ip}:{_akkaPort}\"");
            var hoconArraySeedUrls = $"[{string.Join(",", seedUrls)}]";

            var finalConfig = 
                ConfigurationFactory.ParseString(
$@"akka.remote.helios.tcp.public-hostname = ""{ipAddress}""
akka.remote.helios.tcp.port = {_akkaPort}
akka.cluster.seed-nodes = {hoconArraySeedUrls}")
                .WithFallback(defaultConfig);

            return finalConfig;
        }
    }
}
