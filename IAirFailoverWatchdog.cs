using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace AirFailoverWatchdog
{

    [ServiceContract]
    public interface IAirFailoverWatchdogApi
    {
        [OperationContract]
        [WebInvoke(Method = "OPTIONS", UriTemplate = "/*")]
        void GetGlobalOptions();

        [OperationContract]
        [WebGet(UriTemplate = "/*")]
        Stream ServeEmbeddedStaticFile();

        [OperationContract]
        [WebGet(UriTemplate = "/V1/Status")]
        string GetCurrentStatus();

        //[OperationContract]
        //[WebInvoke(Method = "POST", UriTemplate = "/V1/ResetMetrics")]
        //void ResetMetrics();
        
        //[OperationContract]
        //[WebInvoke(Method = "POST", UriTemplate = "/V1/Start")]
        //void StartStream();

        //[OperationContract]
        //[WebInvoke(Method = "POST", UriTemplate = "/V1/Stop")]
        //void StopStream();

    }


}
