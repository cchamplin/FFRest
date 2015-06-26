using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TranscodeProcessor
{
    /// <summary>
    /// Handler for FFMpeg meta data
    /// </summary>
    public class MetaHandler : IHttpRequestHandler
    {
       
        public void HandleGet(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response)
        {
            string queryString = request.Url.Query;
            var queryParts = Server.ParseQueryString(queryString);

            string type = queryParts.GetFirstValue("type");

            if (string.IsNullOrEmpty(type))
            {
                response.WriteResponse(400, "Missing parameter type");
                return;
            }
            string data = "";
            int exitCode;
            switch (type.ToLower())
            {
                case "formats":
                    data = FFmpeg.Exec("-formats", out exitCode);
                    break;
                case "codecs":
                    data = FFmpeg.Exec("-codecs", out exitCode);
                    break;
                case "bsfs":
                    data = FFmpeg.Exec("-bsfs", out exitCode);
                    break;
                case "protocols":
                    data = FFmpeg.Exec("-protocols", out exitCode);
                    break;
                case "pix_fmts":
                    data = FFmpeg.Exec("-pix_fmts", out exitCode);
                    break;
                case "help":
                    data = FFmpeg.Exec("-help", out exitCode);
                    break;
            }
          
            response.WriteResponse(200, data);

        }

        public void HandlePost(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response, Server.RequestData data)
        {
            throw new NotImplementedException();
        }
    }
}
