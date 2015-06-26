using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common.Logging;

namespace TranscodeProcessor
{

    /// <summary>
    /// Handler to fetch generated thumbnails
    /// </summary>
    public class ThumbnailHandler : IHttpRequestHandler,IHttpExtendedRequestHandler
    {
        private ILog log = LogManager.GetCurrentClassLogger();

        public ThumbnailHandler()
        {
        }

        public void HandleGet(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string path = request.Url.AbsolutePath;
                if (path.Length < 9)
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Thumbnail not found");
                    return;
                }
                string file = path.Substring(8);
                if (string.IsNullOrEmpty(file))
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Thumbnail not found");
                    return;
                }
                // Ensure format matches thumbnail-identifier_x.jpg
                var match = Regex.Match(file, "^thumbnail-([a-zA-Z0-9_-]+)_[0-9]+\\.jpg$");
                if (!match.Success)
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Thumbnail not found");
                    return;
                }
                var jobID = match.Groups[1].Value;
                string filePath = FFRest.config["workingdir"] + Path.DirectorySeparatorChar + jobID + Path.DirectorySeparatorChar + file;
                if (!File.Exists(filePath))
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Thumbnail not found");
                    return;
                }
                response.StatusCode = 200;

                FileInfo fi = new FileInfo(filePath);
                response.ContentLength64 = fi.Length;
                response.ContentType = Utility.GetMime(fi.Extension);
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Utility.CopyStream(fs, response.OutputStream);
                }
                response.OutputStream.Flush();
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                log.Error("Failed to process thumb request", ex);
            }
        }
        public void HandlePost(HttpListenerRequest request, HttpListenerResponse response, Server.RequestData data)
        {
           response.StatusCode = 404;
            response.WriteResponse("404 Page Not Found");
        }

        public void HandleHead(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string path = request.Url.AbsolutePath;
                if (path.Length < 9)
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Thumbnail not found");
                    return;
                }
                string file = path.Substring(8);
                if (string.IsNullOrEmpty(file))
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Thumbnail not found");
                    return;
                }

                // Ensure format matches thumbnail-identifier_x.jpg
                var match = Regex.Match(file, "^thumbnail-([a-zA-Z0-9_-]+)_[0-9]+\\.jpg$");
                if (!match.Success)
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Thumbnail not found");
                    return;
                }
                var jobID = match.Groups[1].Value;
                string filePath = FFRest.config["workingdir"] + Path.DirectorySeparatorChar + jobID + Path.DirectorySeparatorChar + file;
                if (!File.Exists(filePath))
                {
                    response.StatusCode = 404;
                    response.WriteResponse("Thumbnail not found");
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
                log.Error("Failed to process thumb head request", ex);
            }
        }

        public void HandlePut(HttpListenerRequest request, HttpListenerResponse response, Server.RequestData data)
        {
            throw new NotImplementedException();
        }

        public void HandleDelete(HttpListenerRequest request, HttpListenerResponse response)
        {
            throw new NotImplementedException();
        }
    }
}
