using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Logging;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
namespace TranscodeProcessor
{
    /// <summary>
    /// Handles the actual transcoding of files
    /// </summary>
    public class TranscodeTask : Sprite.ISimpleRunnable
    {

        private ILog log = LogManager.GetCurrentClassLogger();
        
        private string taskOptions = null;
        private string taskId = "";
        private string taskOutput = null;
        private string workingDir = null;
        private string outputFile = "";
        private string segment = null;
        private DateTime startTime;
        private string segmentFile = null;
        private TranscodeJob job = null;
        private TranscoderResult result = null;
        //private ThumbGenerator.ThumbnailGenerator taskThumbnails = null;
        private string taskCommand;
        private string extension;
        private bool multiPass;
        public TranscodeTask(TranscodeJob job, TranscoderResult result, string taskId, string segment, string taskOptions, string extension, bool multiPass)
        {
            try
            {

                FFRest.stats.AddTask();
                this.workingDir = job.WorkingDir;
                this.startTime = DateTime.Now;
                this.extension = extension;
                this.result = result;
                this.result.Status = "Pending";
                this.result.StartTime = this.startTime;
                this.multiPass = multiPass;
                this.taskOptions = taskOptions;
                this.taskId = taskId;
                this.outputFile = job.JobToken + "_" + taskId + "." + extension;
                this.segment = segment;
                if (this.segment == "hls")
                {
                    this.segmentFile = job.JobToken + "_" + taskId + "." + extension + ".m3u8";
                }
               
                this.job = job;
              
            }
            catch (Exception ex)
            {
                log.Error("Exception occured in task constructor", ex);
            }
        }
        public bool IsSegmented
        {
            get
            {
                return !string.IsNullOrEmpty(this.segment);
            }
        }

       
        public string TaskID
        {
            get
            {
                return this.taskId;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="workingdir"></param>
        /// <param name="filename"></param>
        /// <param name="args"></param>
        /// <param name="outFile"></param>
        /// <returns></returns>
        private string buildTranscodingArguments(string workingdir, string filename, string args, string outFile)
        {
            args = args.Trim();
            string outputFile = workingdir + outFile;
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }
            this.taskOutput =  outputFile;
            if (multiPass)
            {
                args = args.Trim() + " -pass 2 -passlogfile \"" + this.job.FirstPasses[this.extension].MultiPassFile + "\"";
            }
            
            string command = "-i " + workingdir + filename + " -y " + args + " " + outputFile;

           
            return command;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="workingdir"></param>
        /// <param name="filename"></param>
        /// <param name="args"></param>
        /// <param name="outFile"></param>
        /// <returns></returns>
        private string buildTranscodingSegmentArguments(string workingdir, string filename, string args, string outFile)
        {
            args = args.Trim();
            string outputFile = workingdir + outFile;
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }
            this.taskOutput = outputFile;
            args = "-vcodec copy -acodec copy";
            var segmentArgs = " -bsf:v h264_mp4toannexb -hls_list_size 0 -hls_segment_filename " + workingdir + this.job.JobToken + "_" + this.taskId + "_%03d.ts";
            if (multiPass)
            {
                args = args.Trim() + segmentArgs + " -pass 2 -passlogfile \"" + this.job.FirstPasses[this.extension].MultiPassFile + "\"";
            }
            else
            {
                args = args.Trim() + segmentArgs;
            }

            string command = "-i " + workingdir + filename + " -y " + args + " " + outputFile;


            return command;
        }
       

        public object Handle()
        {
            try
            {
                while (!job.DownloadComplete)
                    System.Threading.Thread.Sleep(100);
                this.startTime = DateTime.Now;
                this.result.StartTime = this.startTime;
                if (multiPass)
                {
                    this.result.Status = "Waiting on First Pass";
                    while (!job.FirstPasses[this.extension].IsComplete)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    if (job.FirstPasses[this.extension].IsFailed)
                    {
                        log.Error("Multipass first pass failed JobID: " + this.job.JobToken + " TaskID: " + this.TaskID);
                        FFRest.stats.CompleteTask();
                        this.result.FinishTime = DateTime.Now;
                        this.result.Status = "Failed";
                        this.result.PercentComplete = 0;
                        return this.result;
                    }
                }

                this.result.Status = "Processing";
                //Get Video Info
                log.Debug("Beginning transcoding task processing JobID: " + this.job.JobToken + " TaskID: " + this.taskId);

                if (job.Download.ProbeInfo == null)
                {
                    FFRest.stats.CompleteTask();
                    //return new TranscoderResult(job.JobToken,taskId,this.startTime);
                    this.result.FinishTime = DateTime.Now;
                    this.result.Status = "Failed/Skipped";
                    this.result.PercentComplete = 0;
                    return this.result;
                }
                if (job.Download.IsFailed)
                {
                    FFRest.stats.CompleteTask();
                    //return new TranscoderResult(job.JobToken,taskId,this.startTime);
                    this.result.FinishTime = DateTime.Now;
                    this.result.Status = "Failed/Skipped";
                    this.result.PercentComplete = 0;
                    return this.result;
                }

                

               
                bool can1080 = false;
                bool can720 = false;
                string fpsString = "";
                int frameRate = 0;
                foreach (var stream in job.Download.ProbeInfo.streams)
                {
                    log.Debug(stream.codec_type);
                    log.Debug(stream.width);
                    if (stream.codec_type.ToString() == "video")
                    {
                        int width;
                        fpsString = stream.avg_frame_rate.ToString();
                        var d = float.Parse(fpsString.Substring(fpsString.IndexOf("/")+1));
                        var r = float.Parse(fpsString.Substring(0, fpsString.IndexOf("/")));
                        frameRate = (int)Math.Round(r / d);

                        if (int.TryParse(stream.width.ToString(), out width))
                        {
                            if (width >= 1920)
                            {
                                can1080 = true;
                                can720 = true;
                            }
                            if (width >= 1280)
                            {
                                can720 = true;
                            }
                        }

                    }
                }
                log.Debug("Format Supports: 1080p" + can1080 + " 720p:" + can720);
                var doProcessing = true;
                if ((taskOptions.Contains("hd1080") || taskOptions.Contains("-s 1920x1080") || taskOptions.Contains("-s '1920x1080'") || taskOptions.Contains("-s \"1920x1080\"")) && !can1080)
                {
                    doProcessing = false;
                }
                if ((taskOptions.Contains("hd720") || taskOptions.Contains("-s 1280x720") || taskOptions.Contains("-s '1280x720'") || taskOptions.Contains("-s \"1280x720\"")) && !can720)
                {
                    doProcessing = false;
                }

                if (doProcessing)
                {

                    string resolution = "";
                    if (taskOptions.ToLower().Contains("hd720"))
                        resolution = "1280x720";
                    else if (taskOptions.ToLower().Contains("hd1080"))
                        resolution = "1920x1080";
                    else if (taskOptions.ToLower().Contains("hd480"))
                        resolution = "720x480";
                    else if (taskOptions.ToLower().Contains("sd480"))
                        resolution = "720x480";
                    else if (Regex.IsMatch(taskOptions, "-s [\"']?([0-9]+x[0-9]+)[\"']?"))
                        resolution = Regex.Match(taskOptions, "-s [\"']?([0-9]+x[0-9]+)[\"']?").Groups[1].Value;

                    string bitrate = "";
                    if (taskOptions.ToLower().Contains("-minrate"))
                    {
                        bitrate = taskOptions.Substring(taskOptions.IndexOf("-minrate") + 9);
                        bitrate = bitrate.Substring(0, bitrate.IndexOf(" "));
                    }
                    else if (taskOptions.ToLower().Contains("-maxrate"))
                    {
                        bitrate = taskOptions.Substring(taskOptions.IndexOf("-maxrate") + 9);
                        bitrate = bitrate.Substring(0, bitrate.IndexOf(" "));
                    }
                    else if (taskOptions.ToLower().Contains("-b:v"))
                    {
                        bitrate = taskOptions.Substring(taskOptions.IndexOf("-b:v") + 5);
                        bitrate = bitrate.Substring(0, bitrate.IndexOf(" "));
                    }
                    this.taskOptions = this.taskOptions.Replace("'{{bitrate}}'", bitrate);
                    this.taskOptions = this.taskOptions.Replace("'{{fps}}'", frameRate.ToString());
                    this.taskOptions = this.taskOptions.Replace("'{{fps2x}}'", (frameRate * 2).ToString());
                    this.taskOptions = this.taskOptions.Replace("'{{resolution}}'", resolution);
                    this.taskOptions = this.taskOptions.Replace("{{bitrate}}", bitrate);
                    this.taskOptions = this.taskOptions.Replace("{{fps}}", frameRate.ToString());
                    this.taskOptions = this.taskOptions.Replace("{{fps2x}}", (frameRate*2).ToString());
                    this.taskOptions = this.taskOptions.Replace("{{resolution}}", resolution);
                    this.taskCommand = this.buildTranscodingArguments(this.workingDir, this.job.Download.Destination, this.taskOptions, this.outputFile);



                    log.Debug("Executing Task:" + this.taskId);
                    StringBuilder results = new StringBuilder();
                    int exitCode = 0;
                    if (job.Download.ProbeInfo != null && job.Download.ProbeInfo.format != null && job.Download.ProbeInfo.format.duration != null)
                    {
                        var duration = double.Parse(job.Download.ProbeInfo.format.duration as string);
                        StringBuilder buffer = new StringBuilder();
                        int idx = 0;
                        results.Append(FFmpeg.Exec(this.taskCommand, out exitCode, (x) =>
                           {
                               buffer.Append(x);
                               int length = buffer.Length-idx;
                               if (idx >= buffer.Length - 1)
                                   return;
                               string data = buffer.ToString(idx, length);
                               int rIdx = 0;
                               data = data.Replace("\r\n","\n");
                               data = data.Replace("\r","\n");
                               int pos = 0;
                               while (rIdx < data.Length && (pos = data.Substring(rIdx).IndexOf("\n")) > -1)
                               {
                                   //log.Trace("Result Data: " +x);
                                   idx += pos + 1;
                                   var line = data.Substring(rIdx, pos + 1).Trim();
                                   rIdx += pos + 1;
                                   if (line.StartsWith("frame="))
                                   {
                                       line = line.Substring(line.IndexOf("time=") + 5);
                                       line = line.Substring(0, line.IndexOf(" "));
                                       var timeSpan = TimeSpan.Parse(line.Trim());
                                       var activePercent = timeSpan.TotalSeconds / duration;
                                       activePercent = activePercent * 100;
                                       if (activePercent > 100)
                                           activePercent = 100;
                                       if (this.segmentFile != null)
                                       {
                                           activePercent -= 10;
                                       }
                                       if (this.result.PercentComplete < (int)activePercent)
                                       {
                                           this.result.PercentComplete = (int)activePercent;
                                           
                                       }
                                   }

                               }
                           }));
                    }
                    else
                    {
                        results.Append(FFmpeg.Exec(this.taskCommand, out exitCode));
                    }
                    if (exitCode != 0)
                    {
                        log.Error("Failed to transcode file " + results.ToString());
                        this.result.FinishTime = DateTime.Now;
                        this.result.Status = "Failed";
                        this.result.PercentComplete = 0;
                        return this.result;
                    }

                    List<string> segments = null;
                    if (this.segmentFile != null)
                    {
                        var cmd = this.buildTranscodingSegmentArguments(this.workingDir, this.outputFile, this.taskOptions, this.segmentFile);
                        results.Append(" ");
                        results.Append(FFmpeg.Exec(cmd, out exitCode));

                        if (File.Exists(this.workingDir + this.segmentFile))
                        {
                            segments = new List<string>();
                            string line = null;
                            using (StreamReader sw = new StreamReader(this.workingDir + this.segmentFile)) {
                                  while ((line = sw.ReadLine()) != null) {
                                      if (line.Trim().EndsWith(".ts"))
                                      {
                                          segments.Add(line.Trim());
                                      }
                                  }
                            }
                        }
                    }

                    log.Debug("Finished transcoding JobID: " + this.job.JobToken + "  TaskID: " + taskId);
                    if (this.segmentFile == null)
                    {
                        this.result.FinishTime = DateTime.Now;
                        this.result.Status = "Complete";
                        this.result.PercentComplete = 100;
                        this.result.ResultVideo = outputFile;
                    }
                    else
                    {
                        this.result.FinishTime = DateTime.Now;
                        this.result.Playlist = this.segmentFile;
                        this.result.Status = "Complete";
                        this.result.PercentComplete = 100;
                        this.result.Segments = segments;
                        this.result.ResultVideo = outputFile;
                    }
                    FFRest.stats.CompleteTask();
                    return this.result;
                    
                }
                else
                {
                    log.Debug("Skipping task JobID: " + this.job.JobToken + " TaskID:" + this.taskId);
                    FFRest.stats.CompleteTask();
                    this.result.FinishTime = DateTime.Now;
                    this.result.Status = "Skipped";
                    this.result.PercentComplete = 0;
                    return this.result;
                    
                }

            }
            catch (Exception e)
            {
                log.Error("Exception occured in task run", e);
                this.result.FinishTime = DateTime.Now;
                this.result.Status = "Failed";
                this.result.PercentComplete = 0;
            }
            FFRest.stats.CompleteTask();
            return this.result ;
        }

        public object Handle(object resource)
        {
            throw new NotImplementedException();
        }
    }
}
