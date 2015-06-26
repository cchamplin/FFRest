using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TranscodeProcessor
{
    /// <summary>
    /// Handle request for adaptive (m3u8 files)
    /// </summary>
    public class AdaptiveHandler : IHttpRequestHandler
    {
        private ConcurrentDictionary<string, TranscodeJob> jobs;
        public AdaptiveHandler(ConcurrentDictionary<string, TranscodeJob> jobs)
        {
            this.jobs = jobs;
        }
        public void HandleGet(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response)
        {
            string queryString = request.Url.Query;
            var queryParts = Server.ParseQueryString(queryString);

            string jobID = queryParts.GetFirstValue("jobid");

            if (string.IsNullOrEmpty(jobID))
            {
                response.WriteResponse(400, "Missing parameter jobID");
                return;
            }

            TranscodeJob job;
            if (!jobs.TryGetValue(jobID, out job))
            {
                response.WriteResponse(404, "Job Not Found");
                return;
            }


            // Build m3u8 file data
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("#EXTM3U");
            foreach (var task in job.Results.OrderBy(x => x.BitRate))
            {
                if (task.HasSegments)
                {
                    sb.AppendLine("#EXT-X-STREAM-INF:PROGRAM-ID=1, BANDWIDTH=" + (task.BitRate));
                    sb.AppendLine(task.Playlist);
                }
                
            }
            response.WriteResponse(200,sb.ToString());

        }

        public void HandlePost(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response, Server.RequestData data)
        {
            throw new NotImplementedException();
        }
    }
}
