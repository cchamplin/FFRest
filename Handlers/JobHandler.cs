using System;
using System.Collections.Concurrent;
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
    /// Handle lookup and creation of transcoding jobs
    /// </summary>
    public class JobHandler : IHttpRequestHandler, IHttpExtendedRequestHandler
    {
        ConcurrentDictionary<string, TranscodeJob> jobs;
        Sprite.ThreadPool jobPool;
        Random rGenerator;
        private ILog log = LogManager.GetCurrentClassLogger();
        public JobHandler(ConcurrentDictionary<string, TranscodeJob> jobs, Sprite.ThreadPool pool)
        {
            this.jobs = jobs;
            this.jobPool = pool;
            rGenerator = new Random();
        }
        public void HandlePost(HttpListenerRequest request, HttpListenerResponse response, Server.RequestData data)
        {
            try
            {

                string jobID = data.PostParameters.GetFirstValue("jobid");
                string taskID = data.PostParameters.GetFirstValue("taskid");
                string complete = data.PostParameters.GetFirstValue("complete");
                string videoTag = data.PostParameters.GetFirstValue("tag");
                string videoUrl = data.PostParameters.GetFirstValue("video");
                string preset = data.PostParameters.GetFirstValue("preset");
                string options = data.PostParameters.GetFirstValue("encoding-options");
                string callbackUrl = data.PostParameters.GetFirstValue("callback");
                string outputExtension = data.PostParameters.GetFirstValue("extension");
                string multiPass = data.PostParameters.GetFirstValue("multipass");
                string multiPassExtension = data.PostParameters.GetFirstValue("multipass-extension");
                string multiPassOptions = data.PostParameters.GetFirstValue("multipass-options");
                string segment = data.PostParameters.GetFirstValue("segment");

                if (string.IsNullOrEmpty(jobID))
                {
                    response.WriteResponse(400, "Missing parameter jobid");
                    return;
                }
                if (!Regex.Match(jobID, "^[a-zA-Z0-9_-]+$").Success)
                {
                    response.WriteResponse(400, "Job parameter container invalid characters [allowed characters a-Z, 0-9, _ -]");
                    return;
                }
                if (!string.IsNullOrEmpty(complete))
                {
                    if (jobs.ContainsKey(jobID))
                    {
                        jobs[jobID].Complete();
                        response.WriteResponse(200, jobs[jobID]);
                        return;
                    }
                    response.WriteResponse(400, "No such job");
                    return;
                }


                if (!string.IsNullOrEmpty(taskID))
                {


                    if (!Regex.Match(taskID, "^[a-zA-Z0-9_-]+$").Success)
                    {
                        response.WriteResponse(400, "TaskID parameter container invalid characters [allowed characters a-Z, 0-9, _ -]");
                        return;
                    }
                }
                else
                {
                    taskID = rGenerator.Next().ToString();
                }


                if (string.IsNullOrEmpty(outputExtension))
                {
                    response.WriteResponse(400, "Missing parameter extension");
                    return;
                }
                if (!Regex.Match(outputExtension, "^[a-zA-Z0-9]+$").Success)
                {
                    response.WriteResponse(400, "Extension contains invalid characters [allowed characters a-Z, 0-9]");
                    return;
                }
                bool multiPassVal = false;
                if (!string.IsNullOrEmpty(multiPass))
                {
                    if (string.IsNullOrEmpty(multiPassOptions))
                    {
                        response.WriteResponse(400, "MultiPass enabled but multipass-options not provided");
                        return;
                    }
                    if (string.IsNullOrEmpty(multiPassExtension))
                    {
                        response.WriteResponse(400, "MultiPass enabled but multipass-extension not provided");
                        return;
                    }
                    if (!Regex.Match(multiPassExtension, "^[a-zA-Z0-9]+$").Success)
                    {
                        response.WriteResponse(400, "Multipass extension contains invalid characters [allowed characters a-Z, 0-9]");
                        return;
                    }
                    multiPassVal = true;
                    multiPassOptions = Uri.UnescapeDataString(multiPassOptions.Replace("+", " "));
                }
                else
                {
                    multiPassOptions = null;
                    multiPassExtension = null;
                }
                if (!string.IsNullOrEmpty(segment))
                {
                    switch (segment.ToLower())
                    {
                        case "hls":
                            segment = "hls";
                            break;
                        default:
                            response.WriteResponse(400, "Invalid segment type valid types are [hls]");
                            return;

                    }
                }

                /*if (string.IsNullOrEmpty(callbackUrl))
                {
                    response.WriteResponse(400, "Missing parameter callbackUrl");
                    return;
                }*/
                if (string.IsNullOrEmpty(preset) && string.IsNullOrEmpty(options))
                {
                    response.WriteResponse(400, "Job must specify ffmpeg parameters or a preset");
                    return;
                }
                if (!string.IsNullOrEmpty(options))
                {
                    options = Uri.UnescapeDataString(options.Replace("+", " "));
                }
                if (data.Files.Count == 0)
                {
                    if (string.IsNullOrEmpty(videoUrl))
                    {
                        response.WriteResponse(400, "Missing parameter video");
                        return;
                    }
                    videoUrl = Uri.UnescapeDataString(videoUrl);
                }
                else
                {
                    if (!string.IsNullOrEmpty(videoUrl))
                    {
                        response.WriteResponse(400, "A video was uploaded but a video url parameter was also provided");
                        return;
                    }
                }
                if (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32S || Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.WinCE)
                {
                    if (options != null)
                    {
                        options = Regex.Replace(options, " '([a-zA-Z0-9{}:=_-]+)'", " \"$1\"");
                    }
                    if (multiPassOptions != null)
                    {
                        multiPassOptions = Regex.Replace(multiPassOptions, " '([a-zA-Z0-9{}:=_-]+)'", " \"$1\"");
                    }
                }
                if (!string.IsNullOrEmpty(callbackUrl))
                {
                    callbackUrl = Uri.UnescapeDataString(callbackUrl);
                }
                TranscodeJob tj = null;
                bool added = jobs.TryAdd(jobID, tj);
                if (added)
                {
                    tj = new TranscodeJob(this.jobPool, jobID, callbackUrl);
                    if (!string.IsNullOrEmpty(videoTag))
                    {
                        tj.Tag = Uri.UnescapeDataString(videoTag);
                    }
                    jobs[jobID] = tj;
                    FFRest.stats.AddJob();
                    try
                    {
                        if (data.Files.Count > 0)
                        {

                            var download = new FileDownload(jobID, tj.WorkingDir, data.Files[0].Name);
                           
                            tj.AddDownload(download);
                        }
                        else
                        {
                            
                            var download = new FileDownload(jobID, videoUrl, tj.WorkingDir, jobID + ".vod");
                           
                            tj.AddDownload(download);

                        }
                    }
                    catch (Exception ex)
                    {
                        tj.Fail("Unable to perform transcoding, are your sure your video file is a valid format?");
                    }
                    tj.GenerateThumbnails();


                }
                else
                {
                    tj = jobs[jobID];
                    if (!string.IsNullOrEmpty(videoTag))
                    {
                        tj.Tag = Uri.UnescapeDataString(videoTag);
                    }
                    // We should already have a file for this job on hand
                    if (tj.IsComplete && (tj.CanExpire || tj.IsFailed))
                    {
                        tj = new TranscodeJob(this.jobPool, jobID, callbackUrl);
                        jobs[jobID] = tj;
                        FFRest.stats.AddJob();
                        try
                        {
                            if (data.Files.Count > 0)
                            {

                                var download = new FileDownload(jobID, tj.WorkingDir, data.Files[0].Name);
                                tj.AddDownload(download);
                            }
                            else
                            {
                                var download = new FileDownload(jobID, videoUrl, tj.WorkingDir, jobID + ".vod");
                                tj.AddDownload(download);

                            }
                        }
                        catch (Exception ex)
                        {
                            tj.Fail("Unable to perform transcoding, are your sure your video file is a valid format?");
                        }
                        tj.GenerateThumbnails();
                    }
                    else
                    {
                        if (data.Files.Count > 0)
                        {
                            foreach (var file in data.Files)
                                File.Delete(file.Path);
                        }
                    }
                }

                if (multiPassVal)
                {
                    var mpass = new Multipass(tj, jobID, tj.WorkingDir, multiPassExtension, multiPassOptions);
                    tj.AddFirstPass(mpass);
                }


                TranscoderResult tr = new TranscoderResult(tj.JobToken, taskID, tj.WorkingDir);
                TranscodeTask tt = new TranscodeTask(tj, tr, taskID, segment, options, outputExtension,multiPassVal);
                tj.AddTask(tt, tr);
                response.WriteResponse(200, new TaskSubmittedResponse() { Job = tj, Task = tt });
            }
            catch (Exception ex)
            {
                log.Debug("Failed to process request",ex);
                response.WriteResponse(500, "Server Error");
            }

        }
        public class TaskSubmittedResponse
        {
            public TranscodeJob Job;
            public TranscodeTask Task;
        }
        public ConcurrentDictionary<string, TranscodeJob> Jobs
        {
            get
            {
                return this.jobs;
            }
        }
        public void HandleGet(HttpListenerRequest request, HttpListenerResponse response)
        {
            string queryString = request.Url.Query;
            var queryParts = Server.ParseQueryString(queryString);

            string jobID = queryParts.GetFirstValue("jobid");


            if (string.IsNullOrEmpty(jobID))
            {
                var jobs = this.jobs.Values.ToList();
                response.WriteResponse(200, jobs);
            }
            else
            {

                TranscodeJob job;
                if (!jobs.TryGetValue(jobID, out job))
                {
                    response.WriteResponse(404, "Job Not Found");
                    return;
                }
                response.WriteResponse(200, job);
            }

        }

        public void HandleHead(HttpListenerRequest request, HttpListenerResponse response)
        {
            throw new NotImplementedException();
        }

        public void HandlePut(HttpListenerRequest request, HttpListenerResponse response, Server.RequestData data)
        {
            throw new NotImplementedException();
        }

        public void HandleDelete(HttpListenerRequest request, HttpListenerResponse response)
        {
            string queryString = request.Url.Query;
            var queryParts = Server.ParseQueryString(queryString);

            string jobID = queryParts.GetFirstValue("jobid");


            if (string.IsNullOrEmpty(jobID))
            {
                response.WriteResponse(404, "Not Found");
            }
            else
            {
                try
                {
                    log.Info("Deleting job: " + jobID);
                    TranscodeJob job;
                    if (!jobs.TryGetValue(jobID, out job))
                    {
                        job.CleanUp();
                        jobs.TryRemove(job.JobToken, out job);
                    }

                   
                    if (FFRest.config["mode"] == "move")
                    {
                        if (Directory.Exists(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["thumb-destination"] + Path.DirectorySeparatorChar + jobID))
                        {
                            DirectoryInfo di = new DirectoryInfo(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["thumb-destination"] + Path.DirectorySeparatorChar + jobID);
                            foreach (var file in di.GetFiles())
                            {
                                file.Delete();
                            }
                            di.Delete();
                        }
                        if (Directory.Exists(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + jobID))
                        {
                            DirectoryInfo di = new DirectoryInfo(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + jobID);
                            foreach (var file in di.GetFiles())
                            {
                                file.Delete();
                            }
                            di.Delete();
                        }
                    }
                    response.WriteResponse(200, "Deleted");
                }
                catch (Exception ex)
                {
                    log.Debug("Failed to process request", ex);
                    response.WriteResponse(500, "Server Error");
                }
            }
        }
    }
}
