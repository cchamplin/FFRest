using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common.Logging;

namespace TranscodeProcessor
{
    /// <summary>
    /// Handler to fetch transcoded video files
    /// </summary>
    public class VideoHandler : IHttpRequestHandler, IHttpExtendedRequestHandler
    {
        private ILog log = LogManager.GetCurrentClassLogger();
        public void HandleGet(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response)
        {
            try
            {
                string path = request.Url.AbsolutePath;
                if (path.Length < 9)
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Video not found");
                    return;
                }
                string file = path.Substring(8);
                if (string.IsNullOrEmpty(file))
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Video not found");
                    return;
                }
                var match = Regex.Match(file, "^([a-zA-Z0-9_-])+\\.[a-zA-Z0-9]+$");
                if (!match.Success)
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Video not found");
                    return;
                }

                var jobID = match.Groups[1].Value;
                string filePath = FFRest.config["workingdir"] + Path.DirectorySeparatorChar + jobID + Path.DirectorySeparatorChar + file;
                if (!File.Exists(filePath))
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Video not found");
                    return;
                }
                response.StatusCode = 200;

                FileInfo fi = new FileInfo(filePath);
                response.ContentType = Utility.GetMime(fi.Extension);
                response.ContentLength64 = fi.Length;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Utility.CopyStream(fs, response.OutputStream);
                }
                response.OutputStream.Flush();
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                log.Error("Failed to process video request", ex);
            }

        }
        
        public void HandlePost(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response, Server.RequestData data)
        {
            throw new NotImplementedException();
        }

        public void HandleHead(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response)
        {
            try {
            string path = request.Url.AbsolutePath;
            if (path.Length < 9)
            {
                response.StatusCode = 404;
                return;
            }
            string file = path.Substring(8);
            if (string.IsNullOrEmpty(file))
            {
                response.StatusCode = 404;
                return;
            }
            var match = Regex.Match(file, "^([a-zA-Z0-9_-])+\\.[a-zA-Z0-9]+$");
            if (!match.Success)
            {
                response.StatusCode = 404;
                response.WriteResponse("Video not found");
                return;
            }

            var jobID = match.Groups[1].Value;
            string filePath = FFRest.config["workingdir"] + Path.DirectorySeparatorChar + jobID + Path.DirectorySeparatorChar + file;
            if (!File.Exists(filePath))
            {
                response.StatusCode = 404;
                return;
            }
            response.StatusCode = 200;
            FileInfo fi = new FileInfo(filePath);
            response.ContentLength64 = fi.Length;
            response.ContentType = Utility.GetMime(fi.Extension);
            response.OutputStream.Close();
                        }
            catch (Exception ex)
            {
                log.Error("Failed to process video request", ex);
            }
        }

        public void HandlePut(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response, Server.RequestData data)
        {
            throw new NotImplementedException();
        }

        public void HandleDelete(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response)
        {
            throw new NotImplementedException();
        }
    }
}
