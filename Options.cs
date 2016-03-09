using CommandLine;
using CommandLine.Text;

namespace AirFailoverWatchdog
{
    // Define a class to receive parsed values
    internal class Options 
    {
        [Option('q', "quiet", Required = false, DefaultValue = false,
        HelpText = "Don't print anything to the console")]
        public bool SuppressOutput { get; set; }
    
        [Option('l', "logfile", Required = false,
        HelpText = "Optional file to record events to.")]
        public string LogFile { get; set; }

        [Option('a', "adapter", Required = false,
        HelpText = "IP address of the adapter to listen for multicasts (if not set, tries first binding adapter).")]
        public string AdapterAddress { get; set; }

        [Option('w', "webservices", Required = false, DefaultValue = false,
        HelpText = "Enable Web Services (available on http://localhost:8124/failover by default).")]
        public bool EnableWebServices { get; set; }
        
        [Option('u', "serviceurl", Required = false, DefaultValue = "http://localhost:8124/failover",
        HelpText = "Optional service URL for REST web services (must change if running multiple instances with web services enabled.")]
        public string ServiceUrl { get; set; }


        [Option('c', "configpath", Required = false,
        HelpText = "Path to the config file to load")]
        public string ConfigPath { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
                current => HelpText.DefaultParsingErrorsHandler(this, current));
        }

    }
}
