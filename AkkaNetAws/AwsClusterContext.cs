namespace AkkaNetAws
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Amazon.AutoScaling;
    using Amazon.AutoScaling.Model;
    using Amazon.EC2;
    using Amazon.EC2.Model;
    using Amazon.Runtime;
    using NLog;
    using NLog.Fluent;
    using Properties;
    using Debug = System.Diagnostics.Debug;
    using Instance = Amazon.EC2.Model.Instance;

    public class AwsClusterContext : IClusterContext
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly AmazonAutoScalingClient _autoScalingClient;
        private readonly AmazonEC2Client _ec2Client;
        private readonly Lazy<Task<string>> _currentInstanceId;

        public AwsClusterContext()
        {
            var creds = new BasicAWSCredentials(Settings.Default.AwsAccessKeyId, Settings.Default.AwsSecretAccessKey);
            _autoScalingClient = new AmazonAutoScalingClient(creds);
            _ec2Client = new AmazonEC2Client(creds);

            _currentInstanceId = new Lazy<Task<string>>(
                async () => await GetCurrentInstanceIdAsync());
        }

        private static async Task<string> GetCurrentInstanceIdAsync()
        {
            var request = (HttpWebRequest)WebRequest.Create("http://169.254.169.254/latest/meta-data/instance-id");
            var response = (HttpWebResponse) (await request.GetResponseAsync());
            if (response.StatusCode != HttpStatusCode.OK)
            {
                const string msg = "Couldn't get current instance ID.";
                Log.Error(msg);
                throw new Exception(msg);
            }

            string currentInstanceId;

            Debug.Assert(response.GetResponseStream() != null); // 200 Response: The stream shouldn't be null at this point.
            using (var sr = new StreamReader(response.GetResponseStream())) 
            {
                currentInstanceId = sr.ReadToEnd();
            }

            Log.Debug().Message("Current instance ID: {0}", currentInstanceId);
            return currentInstanceId;
        }

        /// <summary>
        /// Gets the AWS Instances for the given instance IDs.
        /// </summary>
        /// <param name="instanceIds">The instance IDs.</param>
        /// <returns>The collection of AWS Instances.</returns>
        private async Task<IEnumerable<Instance>> GetInstancesForInstanceIds(params string[] instanceIds) // WHY didn't they put params IEnumerable<T> in C# 6?! https://github.com/dotnet/roslyn/issues/36
        {
            var response = await _ec2Client.DescribeInstancesAsync(
                new DescribeInstancesRequest
                {
                    InstanceIds = instanceIds.Distinct().ToList()
                });

            return response.Reservations.SelectMany(r => r.Instances.ToList());
        }

        private static IPAddress InstanceToIpAddress(Instance instance) =>
           !string.IsNullOrEmpty(instance?.PrivateIpAddress) ? IPAddress.Parse(instance.PrivateIpAddress) : null; 

        public async Task<IPAddress> GetCurrentInstanceIpAddress()
        {
            var instanceId = await _currentInstanceId.Value;
            var instance = (await GetInstancesForInstanceIds(instanceId)).FirstOrDefault();
            var currentInstanceIpAddress = InstanceToIpAddress(instance);
            Log.Debug().Message("Current instance IP address: {0}", currentInstanceIpAddress);
            return currentInstanceIpAddress;
        }

        public async Task<ISet<IPAddress>> GetSiblingInstancesIpAddresses()
        {
            var siblings = new HashSet<IPAddress>();
            var instanceId = await _currentInstanceId.Value;
            var instancesResponse = await _autoScalingClient.DescribeAutoScalingInstancesAsync(
                new DescribeAutoScalingInstancesRequest
                {
                    InstanceIds = new List<string> { instanceId }
                });
            var autoScalingGroupName = instancesResponse.AutoScalingInstances.FirstOrDefault()?.AutoScalingGroupName;
            if (!string.IsNullOrEmpty(autoScalingGroupName))
            {
                var groupsResponse = await _autoScalingClient.DescribeAutoScalingGroupsAsync(
                    new DescribeAutoScalingGroupsRequest
                    {
                        AutoScalingGroupNames = new List<string> { autoScalingGroupName }
                    });

                var instanceIds = 
                    (groupsResponse
                        .AutoScalingGroups
                        .FirstOrDefault()?
                        .Instances?
                        .Select(i => i.InstanceId)
                    ?? Enumerable.Empty<string>()).ToArray();

                if (instanceIds.Any())
                {
                    var instances = await GetInstancesForInstanceIds(instanceIds);
                    siblings = new HashSet<IPAddress>(instances.Select(InstanceToIpAddress));
                }
            }

            Log.Debug().Message("Sibling IP addresses: {0}", string.Join(", ", siblings));
            return siblings;
        }
    }
}