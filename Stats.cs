using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace TranscodeProcessor
{

    /// <summary>
    /// Keep track of transcoding job stats
    /// TODO Expand this to keep more information like total hours transcoded
    /// average bit rates, etc
    /// </summary>
    public class Stats : IHttpRequestHandler
    {
        private ILog log = LogManager.GetCurrentClassLogger();

        public int totalJobs;
        public int totalFilesTranscoded;
        public int runningJobs;
        public int failedJobs;

        public bool saving;
        CancellationTokenSource cancelToken;
        public Stats()
        {
            log.Debug("Initializing stats service");
            StreamReader sr = null;
            try
            {
               sr = new StreamReader(FFRest.config["statsfile"]);
                if (!int.TryParse(sr.ReadLine(), out totalJobs))
                {
                    totalJobs = 0;
                }
                if (!int.TryParse(sr.ReadLine(), out totalFilesTranscoded))
                {
                    totalFilesTranscoded = 0;
                }
            }
            catch (Exception e)
            {
                log.Error("Exception in file loading occured", e);
                totalJobs = 0;
                totalFilesTranscoded = 0;
            }
            finally
            {
                if (sr != null)
                  sr.Close();
            }
            
            runningJobs = 0;
            cancelToken = new CancellationTokenSource();
            TaskExecutorFactory.Begin(SaveFile, 100000, 100, Timeout.Infinite, -1, cancelToken.Token);

        }
        private void SaveFile()
        {

            try
            {

                saving = true;
                log.Trace("Running Stats Save");
                if (File.Exists(FFRest.config["statsfile"]))
                {
                    File.Delete(FFRest.config["statsfile"]);
                }
                StreamWriter sw = new StreamWriter(FFRest.config["statsfile"]);
                sw.WriteLine(totalJobs);
                sw.WriteLine(totalFilesTranscoded);
                sw.Close();
                saving = false;

            }
            catch (OperationCanceledException)
            {
                log.Debug("Shutting down stats");
            }
            catch (ThreadInterruptedException)
            {
                log.Debug("Shutting down stats");
            }
            catch (ThreadAbortException)
            {
                log.Debug("Shutting down stats");
            }
            catch (Exception e)
            {
                log.Error("Exception in file saving occured", e);
            }
            finally
            {
                saving = false;
            }
        }
        public void HandleGet(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                StringBuilder bld = new StringBuilder();
                bld.Append("Total Jobs: " + totalJobs + "\n");
                bld.Append("Total Files Transcoded: " + totalFilesTranscoded + "\n");
                bld.Append("Active: " + runningJobs + "\n");
                response.StatusCode = 200;
                response.WriteResponse(bld.ToString());
            }
            catch (Exception ex)
            {
                log.Error("Stats Request Exception Occured", ex);
            }
           // return new Httpd.HttpResponse(200,bld.ToString());
        }

        public void HandlePost(HttpListenerRequest request, HttpListenerResponse response, Server.RequestData data)
        {
            response.StatusCode = 400;
            response.WriteResponse("Bad Request");
            //return new Httpd.HttpResponse(400, "Bad Request");
        }
        public void AddJob()
        {
            Interlocked.Increment(ref totalJobs);
        }
        public void AddTask()
        {
            Interlocked.Increment(ref totalFilesTranscoded);
            Interlocked.Increment(ref runningJobs);
        }
        public void CompleteTask()
        {
            Interlocked.Decrement(ref runningJobs);
        }
        public void Shutdown()
        {
            try
            {
                cancelToken.Cancel(true);
                int cnt = 0;
                while (saving == true && cnt < 20)
                {
                    Thread.Sleep(15);
                    cnt++;
                }
            }
            catch (ThreadInterruptedException)
            {
                log.Debug("Shutting down stats");
            }
            catch (ThreadAbortException)
            {
                log.Debug("Shutting down starts");
            }
            catch (Exception e)
            {
                log.Error("Exception in data stream occured", e);
            }
            finally
            {
            }
            
        }
    }
}
