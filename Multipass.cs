using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Common.Logging;
namespace TranscodeProcessor
{
    /// <summary>
    /// Handle first pass run for multipass transcoding jobs
    /// </summary>
    public class Multipass : Sprite.ISimpleRunnable
    {
        private static ILog log = LogManager.GetCurrentClassLogger();


        protected TranscodeJob job;
        protected string token;
        protected bool complete;
        protected bool failed;
        protected string workingDir;
        protected string dest;

        protected string multiPassOptions;
        protected string multiPassExtension;
        protected string outputFileMultipass;

        public Multipass(TranscodeJob job,string token, string workingDir, string multiPassExtension, string multiPassOptions)
        {
            this.token = token;
            this.job = job;
            this.workingDir = workingDir;
            this.complete = false;
            this.dest = this.job.Download.Destination;

            this.multiPassOptions = multiPassOptions;
            this.multiPassExtension = multiPassExtension;
        }
       
       

        public void RunPass()
        {
            try
            {
                // TODO configure this sleep time
                // Make sure we have a file available
                while (!job.DownloadComplete)
                    System.Threading.Thread.Sleep(100);

                // If we were not able to probe the file we cannot proceed
                if (job.Download.ProbeInfo != null)
                {
                    // Multipass options must be provided
                    if (string.IsNullOrEmpty(multiPassOptions))
                    {
                        this.complete = true;
                        this.failed = true;
                        log.Debug("No multi pass options provided");
                        throw new JobFailureException("No multi pass options provided");
                    }

                    string fpsString;
                    int frameRate = 0;
                    // Get the current frame rate for variable replacement
                    // Todo possible look into other variables that could be replaced in
                    foreach (var stream in job.Download.ProbeInfo.streams)
                    {
                        if (stream.codec_type.ToString() == "video")
                        {

                            fpsString = stream.avg_frame_rate.ToString();
                            var d = float.Parse(fpsString.Substring(fpsString.IndexOf("/") + 1));
                            var r = float.Parse(fpsString.Substring(0, fpsString.IndexOf("/")));
                            frameRate = (int)Math.Round(r / d);
                        }
                    }
                    this.multiPassOptions = this.multiPassOptions.Replace("'{{fps}}'", frameRate.ToString());
                    this.multiPassOptions = this.multiPassOptions.Replace("'{{fps2x}}'", (frameRate * 2).ToString());
                    this.multiPassOptions = this.multiPassOptions.Replace("{{fps}}", frameRate.ToString());
                    this.multiPassOptions = this.multiPassOptions.Replace("{{fps2x}}", (frameRate * 2).ToString());
                    var args = buildTranscodingArguments(this.workingDir, this.dest, this.multiPassOptions, "fastpass_" + this.dest + "." + this.multiPassExtension);
                    int status = 0;
                    var fastPassData = FFmpeg.Exec(args, out status);
                    if (status != 0)
                    {
                        this.complete = true;
                        this.failed = true;
                        log.Debug("Failed to perform fast pass of data");
                        throw new JobFailureException("Failed to perform fast pass of data");
                    }
                    this.complete = true;
                }
                else
                {
                    this.complete = true;
                    this.failed = true;
                    log.Debug("Failed to perform fast pass of data missing probe info");
                    throw new JobFailureException("Failed to perform fast pass of data missing probe info");
                }
                
            }
            catch (Exception ex)
            {
                log.Error("An error occured Download()", ex);
            }
        }

        public string WorkingDirectory
        {
            get { return this.workingDir; }
        }

        public bool IsComplete
        {
            get { return this.complete; }
        }
        internal bool IsFailed
        {
            get { return this.failed; }
        }
        public string Token
        {
            get
            {
                return this.token;
            }
        }
        public string Extension
        {
            get
            {
                return this.multiPassExtension;
            }
        }
        public string MultiPassFile
        {
            get
            {
                return this.outputFileMultipass;
            }
        }

        /// <summary>
        /// Build arguements for transcoder
        /// </summary>
        /// <param name="workingdir">Working directory where input file is located</param>
        /// <param name="filename">Name of file to be transcoded</param>
        /// <param name="args">Transcoding arguments</param>
        /// <param name="outFile">Name of transcoded destination file. This will be placed into workingDir</param>
        /// <returns>Returns a command string for FFMpeg</returns>
        private string buildTranscodingArguments(string workingdir, string filename, string args, string outFile)
        {
            args = args.Trim();
            string outputFile = workingdir + outFile;
            string taskOutput = outputFile;
            this.outputFileMultipass = outputFile + "-multipass";
            args = args.Trim() + " -pass 1"+ " -passlogfile \"" + outputFileMultipass + "\"";

            string command = "-i " + workingdir + filename + " -y " + args + " " + outputFile;

           
            return command;
        }


        public override string ToString()
        {
            return "Complete: " + complete;
        }


        /// <summary>
        /// Invoked method from Sprite
        /// </summary>
        /// <returns>Return invoked instance of Multipass class</returns>
        public object Handle()
        {
            RunPass();
            return this;
        }

        public object Handle(object resource)
        {
            throw new NotImplementedException();
        }
    }
}
