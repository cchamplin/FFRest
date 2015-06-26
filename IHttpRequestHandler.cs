using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TranscodeProcessor
{
    public interface IHttpRequestHandler
    {
        void HandleGet(HttpListenerRequest request, HttpListenerResponse response);
        void HandlePost(HttpListenerRequest request, HttpListenerResponse response, Server.RequestData data);
    }
    public interface IHttpExtendedRequestHandler
    {
        void HandleHead(HttpListenerRequest request, HttpListenerResponse response);
        void HandlePut(HttpListenerRequest request, HttpListenerResponse response, Server.RequestData data);
        void HandleDelete(HttpListenerRequest request, HttpListenerResponse response);
    }
}
