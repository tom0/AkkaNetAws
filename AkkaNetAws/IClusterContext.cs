namespace AkkaNetAws
{
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    public interface IClusterContext
    {
        /// <summary>
        /// Get the private IP address of the AWS instance this application is currently running on.
        /// </summary>
        /// <returns>This instance's private IP address.</returns>
        Task<IPAddress> GetCurrentInstanceIpAddress();

        /// <summary>
        /// Get the private IP addresses of the AWS instances that are in the same autoscaling group as this instance.
        /// </summary>
        /// <returns>The IP addresses of instances in the same autoscaling group.</returns>
        Task<ISet<IPAddress>> GetSiblingInstancesIpAddresses();
    }
}