using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sprite;
using System.IO;
using System.Net;
using Common.Logging;

namespace TranscodeProcessor
{
    /// <summary>
    /// Handle the delegation of tasks around a transcoding job
    /// </summary>
    public class TranscodeJob : Sprite.INotifiable
    {
        private ILog log = LogManager.GetCurrentClassLogger();
        protected string jobToken;
        protected TaskSet taskSet;
        protected FileDownload download;
        protected ThumbnailGenerator thumbGenerator;
        protected List<TranscoderResult> results;
        protected ConcurrentDictionary<string, Multipass> passes;
        protected bool flaggedComplete;
        protected bool failed;
        protected int totalTasks;
        protected DateTime startTime;
        protected DateTime? endTime;
        protected TimeSpan? jobTime;
        protected string status = null;
        protected string message = null;
        protected string tag = null;
        protected string callbackUrl;
        protected bool cleaned;
        protected bool adaptive = false;
        protected volatile int markedTasks;
        protected string workingDirectory;
        public TranscodeJob(ThreadPool pool, string jobToken, string callbackUrl)
        {
            this.taskSet = new TaskSet(pool,this, WaitStrategy.MODERATE);
            this.jobToken = jobToken;
            this.flaggedComplete = false;
            this.download = null;
            this.failed = false;
            this.cleaned = false;
            this.startTime = DateTime.Now;
            this.callbackUrl = callbackUrl;
            this.markedTasks = 0;
            this.tag = null;
            this.passes = new ConcurrentDictionary<string, Multipass>();
            this.workingDirectory =  FFRest.config["workingdir"] + Path.DirectorySeparatorChar + this.jobToken;
            if (this.workingDirectory[this.workingDirectory.Length-1] != Path.DirectorySeparatorChar)
            {
                this.workingDirectory += Path.DirectorySeparatorChar;
            }
        }

        internal bool DownloadComplete
        {
            get {
                return this.download != null && this.download.IsComplete;
            }
        }
        internal ConcurrentDictionary<string, Multipass> FirstPasses
        {
            get
            {
                return this.passes;
            }
        }
        public string Tag
        {
            get
            {
                return this.tag;
            }
            set
            {
                this.tag = value;
            }
        }
        internal bool isCleaned
        {
            get
            {
                return this.cleaned;
            }
        }
        public void Fail(string message)
        {
            log.Debug("Marking job as failed" + this.jobToken + " " + message);
            this.failed = true;
            this.status = "Failed";
            this.message = message;
            this.endTime = DateTime.Now;
            this.jobTime = this.endTime - this.startTime;
            CleanUp();
        }
        public DateTime StartTime
        {
            get
            {
                return startTime;
            }
        }
        public DateTime? EndTime
        {
            get
            {
                if (endTime == null)
                    return null;
                return endTime.Value;
            }
        }
        public TimeSpan JobTime
        {
            get
            {
                if (endTime == null)
                    return DateTime.Now - this.startTime;
                return jobTime.Value; 
            }
        }
        internal string WorkingDir
        {
            get
            {
                return this.workingDirectory;
            }
        }
        public void CleanUp()
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(this.workingDirectory);
                foreach (var file in di.GetFiles())
                {
                    file.Delete();
                }
                di.Delete();
                this.passes.Clear();
                this.taskSet = null;
                this.download = null;
                this.thumbGenerator = null;
                this.callbackUrl = null;
                this.cleaned = true;
            }
            catch (Exception ex)
            {
                log.Error("Failed to cleanup working directory", ex);
            }
        }
        public void Complete()
        {
            if (this.flaggedComplete)
                return;
            this.flaggedComplete = true;
            if (thumbGenerator == null)
                return;
            if (taskSet.IsComplete && thumbGenerator.IsComplete)
            {
                if (!failed)
                {
                    this.status = "Complete";
                    this.endTime = DateTime.Now;
                    this.jobTime = this.endTime - this.startTime;
                }
            }
        }
        internal FileDownload Download
        {
            get { return download; }
        }

        public bool IsComplete
        {
            get
            {
                if (!this.cleaned)
                {
                    if (taskSet == null)
                    {

                        log.Error("Invalid Program State, TaskSet Null");
                        return false;
                    }
                    if (thumbGenerator == null)
                    {

                        log.Error("Invalid Program State, TaskSet Null");
                        return false;
                    }
                }

                return this.flaggedComplete && ((this.cleaned) || (taskSet.IsComplete && thumbGenerator.IsComplete));
            }
        }
        internal bool IsFailed
        {
            get
            {
                return this.failed;
            }
        }
        
        public string JobToken
        {
            get
            {
                return jobToken;
            }
        }
        public int Tasks
        {
            get
            {
                return totalTasks;
            }
        }
        public string Status
        {
            get
            {
                return status;
            }
        }
        public string Message
        {
            get
            {
                return message;
            }
        }
        public bool CanExpire
        {
            get
            {
                if ((DateTime.Now - this.startTime).TotalHours > 24)
                {
                    return true;
                }
                if (this.IsComplete && this.markedTasks == this.totalTasks)
                {
                    return true;
                }
                return false;
            }
        }
        public List<TranscoderResult> Results
        {
            get
            {
                return results;
            }
        }
        public List<string> Thumbnails
        {
            get
            {
                var thumbs = new List<string>();
                if ((thumbGenerator != null && thumbGenerator.IsComplete) || (thumbGenerator == null && this.cleaned))
                {
                    for (var x = 1; x <= 5; x++)
                    {
                        if (FFRest.config["mode"] == "move")
                        {
                            if (File.Exists(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["thumb-destination"] + Path.DirectorySeparatorChar + this.jobToken + Path.DirectorySeparatorChar + "thumbnail-" + this.jobToken + "_000" + x + ".jpg"))
                            {
                                thumbs.Add(FFRest.config["serve-url"] + FFRest.config["thumb-destination"] + "/" + this.jobToken + "/thumbnail-" + this.jobToken + "_000" + x + ".jpg");
                            }
                        }
                        else
                        {
                            if (File.Exists(this.workingDirectory + "thumbnail-" + this.jobToken + "_000" + x + ".jpg"))
                            {
                                thumbs.Add("/thumbs/thumbnail-" + this.jobToken + "_000" + x + ".jpg");
                            }
                        }
                    }
                }
                return thumbs;
            }
        }

        public void AddDownload(FileDownload download)
        {
            try
            {
                log.Debug("Adding download to job: " + this.jobToken);
                this.status = "Downloading";
                this.download = download;

               
                 DirectoryInfo di =   Directory.CreateDirectory(this.WorkingDir);

                 foreach (var file in di.GetFiles())
                 {
                     file.Delete();
                 }
                
                if (File.Exists(FFRest.config["workingdir"] + download.Destination))
                {
                    File.Move(FFRest.config["workingdir"] + download.Destination, this.WorkingDir + Download.Destination);
                }
                
                results = new List<TranscoderResult>();
                if (!this.failed)
                {
                    taskSet.EnqueSimpleTask(download);
                }
            }
            catch (Exception ex)
            {
                log.Error("An error occured AddDownload: " + this.jobToken, ex);
            }
        }
        public void AddFirstPass(Multipass pass)
        {
            try
            {
               
                if (passes.TryAdd(pass.Extension, pass))
                {
                    log.Debug("Adding multipass to job: " + this.jobToken + " " + pass.Extension);
                    Directory.CreateDirectory(this.WorkingDir);
                    if (!this.failed)
                    {
                        taskSet.EnqueSimpleTask(pass);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("An error occured AddFirstPass: " + this.jobToken, ex);
            }
        }
        public bool AddTask(TranscodeTask task,TranscoderResult result)
        {
            try
            {
                if (!this.flaggedComplete && !this.failed)
                {
                   
                    Directory.CreateDirectory(this.workingDirectory);
                    if (task.IsSegmented)
                    {
                        this.adaptive = true;
                        try
                        {
                           
                            Directory.CreateDirectory(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken);
                            if (!File.Exists(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken + Path.DirectorySeparatorChar + "adaptive.m3u8"))
                            {
                                File.Create(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken + Path.DirectorySeparatorChar + "adaptive.m3u8");
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error("Failed to handle adaptive file creation");
                        }
                    }
                    this.status = "Working";
                    log.Debug("Adding task to job: " + this.jobToken);
                    taskSet.EnqueSimpleTask(task);
                    this.results.Add(result);
                    this.totalTasks++;
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                log.Error("An error occured AddTask: " + this.jobToken, ex);
            }
            return false;
        }
        public bool GenerateThumbnails()
        {
            try
            {
                if (!this.failed)
                {

                    Directory.CreateDirectory(this.workingDirectory);
                    log.Debug("Generating thumbnails for job: " + this.jobToken);
                    thumbGenerator = new ThumbnailGenerator(this.JobToken, this.workingDirectory, download);
                    this.status = "Working";
                    taskSet.EnqueSimpleTask(thumbGenerator);
                    return true;
                }
            }
            catch (Exception ex)
            {
                log.Error("An error occured GenerateThumbnails: " + this.jobToken, ex);
            }
            return false;
        }

        public bool Invalid()
        {
            log.Error("Invalid called");
            throw new NotImplementedException();
        }
        public string Adaptive
        {
            get
            {
                if (this.results == null || this.results.Count == 0)
                    return null;
                if (this.adaptive) {
                    if (FFRest.config["mode"] == "move")
                    {
                        return FFRest.config["serve-url"] + FFRest.config["video-destination"] + "/" + this.jobToken + "/adaptive.m3u8";
                    }
                    return FFRest.config["video-destination"] + "/adaptive.m3u8";
                }
                return null;
            }
        }

        public void NotifyCompleted(object data)
        {
            log.Debug("Task completed: " + this.jobToken);
            try
            {
                if (data is TranscoderResult)
                {
                    if (results != null)
                    {
                        if (FFRest.config["mode"] == "move")
                        {
                            var re = data as TranscoderResult;
                            if (!string.IsNullOrEmpty(re.ResultFile))
                            {
                                Directory.CreateDirectory(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken);
                                File.Delete(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken + Path.DirectorySeparatorChar + re.ResultFile);
                                if (File.Exists(this.workingDirectory + re.ResultFile))
                                {
                                    File.Move(this.workingDirectory + re.ResultFile, FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken + Path.DirectorySeparatorChar + re.ResultFile);
                                }
                            }
                            if (re.HasSegments)
                            {
                                
                                Directory.CreateDirectory(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken);
                                File.Delete(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken + Path.DirectorySeparatorChar + re.PlaylistFile);
                                if (File.Exists(this.workingDirectory + re.PlaylistFile))
                                {
                                    File.Move(this.workingDirectory + re.PlaylistFile, FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken + Path.DirectorySeparatorChar + re.PlaylistFile);
                                }
                                foreach (var file in re.SegmentsFiles)
                                {
                                    
                                    File.Delete(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken + Path.DirectorySeparatorChar + file);
                                    if (File.Exists(this.workingDirectory + file))
                                    {
                                        File.Move(this.workingDirectory + file, FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken + Path.DirectorySeparatorChar + file);
                                    }
                                }
                            }
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine("#EXTM3U");
                            foreach (var task in Results.OrderBy(x => x.BitRate))
                            {
                                if (task.ResultFile != null)
                                {
                                    if (task.HasSegments)
                                    {
                                        sb.AppendLine("#EXT-X-STREAM-INF:PROGRAM-ID=1, BANDWIDTH=" + (task.BitRate));
                                        sb.AppendLine(task.Playlist);
                                    }
                                }
                            }
                            using (StreamWriter sw = new StreamWriter(FFRest.config["file-root"] + Path.DirectorySeparatorChar + FFRest.config["video-destination"] + Path.DirectorySeparatorChar + this.jobToken + Path.DirectorySeparatorChar + "adaptive.m3u8"))
                            {
                                sw.Write(sb.ToString());
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(callbackUrl))
                    {
                        try
                        {
                            if (this.callbackUrl == null)
                                return;
                            var request = WebRequest.Create(callbackUrl);
                            request.Method = "POST";
                            var serializer = new FastSerialize.Serializer(typeof(FastSerialize.JsonSerializerGeneric));
                            var postData = "data=" + System.Web.HttpUtility.UrlEncode(serializer.Serialize(data));
                            byte[] rawData = Encoding.UTF8.GetBytes(postData);
                            request.ContentType = "application/x-www-form-urlencoded";
                            request.ContentLength = rawData.Length;
                            Stream dataStream = request.GetRequestStream();
                            dataStream.Write(rawData, 0, rawData.Length);
                            dataStream.Close();
                            var response = request.GetResponse();
                            dataStream = response.GetResponseStream();
                            StreamReader reader = new StreamReader(dataStream);
                            string responseFromServer = reader.ReadToEnd();
                            reader.Close();
                            dataStream.Close();
                            response.Close();
                           
                        }
                        catch (Exception ex)
                        {
                            log.Error("Failed to make http callback request: " + this.callbackUrl, ex);
                        }
                        System.Threading.Interlocked.Increment(ref this.markedTasks);
                    }
                }
                if (taskSet.IsComplete && !this.failed)
                {
                    if (flaggedComplete)
                    {
                        status = "Complete";
                        this.endTime = DateTime.Now;
                        this.jobTime = this.endTime - this.startTime;
                    }
                    else
                        status = "Waiting";
                }
            }
            catch (Exception ex)
            {
                log.Error("An error occured handled completed notification: " + this.jobToken,ex);
            }
        }

        public void NotifyException(Exception ex)
        {
            log.Error("Exeception occured for job: " + this.jobToken, ex);
            try
            {
                if (ex is JobFailureException || (ex.InnerException != null && ex.InnerException is JobFailureException))
                {
                    
                    if (!string.IsNullOrEmpty(callbackUrl))
                    {
                        try
                        {
                            if (this.callbackUrl == null)
                            {
                                return;
                            }
                            var request = WebRequest.Create(callbackUrl);
                            request.Method = "POST";
                            var serializer = new FastSerialize.Serializer(typeof(FastSerialize.JsonSerializerGeneric));
                            var postData = "data=" + System.Web.HttpUtility.UrlEncode(serializer.Serialize(this));
                            byte[] rawData = Encoding.UTF8.GetBytes(postData);
                            request.ContentType = "application/x-www-form-urlencoded";
                            request.ContentLength = rawData.Length;
                            Stream dataStream = request.GetRequestStream();
                            dataStream.Write(rawData, 0, rawData.Length);
                            dataStream.Close();
                            var response = request.GetResponse();
                            dataStream = response.GetResponseStream();
                            StreamReader reader = new StreamReader(dataStream);
                            string responseFromServer = reader.ReadToEnd();
                            reader.Close();
                            dataStream.Close();
                            response.Close();
                            System.Threading.Interlocked.Increment(ref this.markedTasks);
                        }
                        catch (Exception exInner)
                        {
                            log.Error("Failed to make http callback request inner " + this.callbackUrl, exInner);
                        }
                    }
                    this.Fail(ex.Message);
                }
                else
                {
                    this.message = ex.Message;
                    if (taskSet.IsComplete)
                    {
                        if (flaggedComplete)
                            status = "Incomplete";
                        else
                            status = "Waiting";
                    }
                }
            }
            catch (Exception innerEx)
            {
                log.Error("An error occured handled error notification: " + this.jobToken, innerEx);
            }
        }
    }
}
