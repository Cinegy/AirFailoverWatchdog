using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AirFailoverWatchdog
{
    class PlayoutEngine
    {
        public string Name { get; set; }

        public string Playlist { get; set; }

        public string MulticastUrl { get; set; }

        public string multicastAddress
        {
            get
            {
                //should do all this with regex, but i can never remember how they work without google, and i'm at 38,000ft heading to Vegas...
                if (!string.IsNullOrEmpty(MulticastUrl))
                {
                    var parts = MulticastUrl.Split('@');

                    if (parts.Count() != 2) return null;

                    return parts[1].Split(':')[0];
                }
                else
                {
                    return null;
                }
            }
        }

        public int multicastGroupPort
        {
            get
            {
                //should do all this with regex, but i can never remember how they work without google, and i'm at 32,000ft heading to Vegas...
                if (!string.IsNullOrEmpty(MulticastUrl))
                {
                    var parts = MulticastUrl.Split('@');

                    if (parts.Count() != 2) return 0;

                    var subParts = parts[1].Split(':');

                    if ((subParts.Count() < 2) || (string.IsNullOrEmpty(subParts[1]))) return 0;

                    //a regex would be simpler, and could pluck just a numerical section before any ? or / symbols... but i'm still 32,000 feet above Earth...
                    //for now, let's assume that the URL has nothing after the port part (it should not for Air anyway)
                    //for now, let's assume that the URL has nothing after the port part (it should not for Air anyway)
                    return int.Parse(subParts[1]);
                }
                else
                {
                    return 0;
                }
            }
        }

        public DateTime lastMonitoredPacketTime { get; set; }

        public DateTime lastStallTime { get; set; }

        public DateTime lastFailoverTime { get; set; }

        public bool isStalled { get; set; }

        public int networkFailureCount { get; set; }

    }
}
