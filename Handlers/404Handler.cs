using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TranscodeProcessor
{
    /// <summary>
    /// Generic request handler to return 404 errors.
    /// </summary>
    public class _404Handler : IHttpRequestHandler
    {
        private static _404Handler instance;
        public void HandleGet(HttpListenerRequest request, HttpListenerResponse response)
        {
            response.StatusCode = 404;
            response.WriteResponse("404 Page Not Found");
        }
        public void HandlePost(HttpListenerRequest request, HttpListenerResponse response, Server.RequestData data)
        {
            response.StatusCode = 404;
            response.WriteResponse("404 Page Not Found");
        }
        public static _404Handler Instance()
        {
            if (instance == null)
            {
                instance = new _404Handler();
            }
            return instance;
        }
    }
}
