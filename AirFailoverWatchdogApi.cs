using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace AirFailoverWatchdog
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class AirFailoverWatchdogApi : IAirFailoverWatchdogApi
    {
        private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

        public NetworkMetric NetworkMetric { get; set; }

        public RtpMetric RtpMetric { get; set; }

        public void GetGlobalOptions()
        {
            if (WebOperationContext.Current == null) return;

            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Headers", "content-type");
            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Methods",
                "GET,PUT,POST,DELETE,OPTIONS");
        }

        public Stream ServeEmbeddedStaticFile()
        {
            if (WebOperationContext.Current == null)
            {
                return null;
            }

            var wildcardSegments = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.WildcardPathSegments;
            var filename = wildcardSegments.LastOrDefault();

            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = "index.html";
                wildcardSegments = new KeyedByTypeCollection<string>(new List<string>() {"index.html"});
            }

            var fileExt = new FileInfo(filename).Extension.ToLower();

            switch (fileExt)
            {
                case (".js"):
                    WebOperationContext.Current.OutgoingResponse.ContentType = "text/javascript";
                    break;
                case (".png"):
                    WebOperationContext.Current.OutgoingResponse.ContentType = "image/png";
                    break;
                case (".htm"):
                case (".html"):
                case (".htmls"):
                    WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";
                    break;
                case (".css"):
                    WebOperationContext.Current.OutgoingResponse.ContentType = "text/css";
                    break;
                case (".jpeg"):
                case (".jpg"):
                    WebOperationContext.Current.OutgoingResponse.ContentType = "image/jpeg";
                    break;
                default:
                    WebOperationContext.Current.OutgoingResponse.ContentType = "application/octet-stream";
                    break;
            }

            try
            {
                var manifestAddress = wildcardSegments.Aggregate("TsAnalyser.embeddedWebResources",
                    (current, wildcardPathSegment) => current + ("." + wildcardPathSegment));

                return _assembly.GetManifestResourceStream(manifestAddress);
            }
            catch (Exception x)
            {
                Debug.WriteLine($"GetFile: {x.Message}", TraceEventType.Error);
                throw;
            }
        }

        public string GetCurrentStatus()
        {
            throw new NotImplementedException();
        }

        public delegate void CommandEventHandler(object sender, CommandEventArgs e);

        public event CommandEventHandler Command;

        protected virtual void OnStreamCommand(CommandType command)
        {
            var handler = Command;
            if (handler == null) return;
            var args = new CommandEventArgs { Command = command };
            handler(this, args);
        }

    }

    public class CommandEventArgs : EventArgs
    {
        public CommandType Command { get; set; }
    }

    public enum CommandType
    {
        ResetMetrics
    }

}
    


