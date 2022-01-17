using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AKS.Shared.Settings
{
    public class KubernetesClusterOptions
    {
	    public const string Key = "KubernetesCluster";
        public int WatchTimeoutSeconds { get; set; }
	    public bool DeveloperLogging { get; set; }
    }
}
