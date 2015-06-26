using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace TranscodeProcessor
{

    /// <summary>
    /// Multithreaded HTTPD for handling restful requests
    /// </summary>
    public class Server
    {
        private ILog log = LogManager.GetCurrentClassLogger();
        protected HttpListener listener;
        protected List<Thread> threads;
        protected bool isActive;
        protected Sprite.ThreadPool pool;
        protected Sprite.TaskSet taskSet;
        protected Dictionary<string, IHttpRequestHandler> handlers;
        internal static Random random = new Random();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="host">Host to bind to</param>
        /// <param name="port">Port to bind to</param>
        /// <param name="handlers">List of request handlers</param>
        public Server(string host, int port, Dictionary<string,IHttpRequestHandler> handlers)
        {

            this.handlers = handlers;

            // Initialize our thread pool
            threads = new List<Thread>();
            // Todo the pool size should be based on the number of cores available
            // or a configuration option
            pool = new Sprite.ThreadPool(7,Sprite.WaitStrategy.MODERATE);
            taskSet = new Sprite.TaskSet(pool,Sprite.WaitStrategy.MODERATE);


            // We may need to handle a permission issue here with a prompt on windows machines
            this.listener = new HttpListener();
            this.listener.IgnoreWriteExceptions = true;
            string prefix = "http://" + host + ":" + port + "/";
            this.listener.Prefixes.Add(prefix);
            
            Thread thread = new Thread(new ThreadStart(this.Process));
            
            threads.Add(thread);
        }

        /// <summary>
        /// Starts threadpool and begins listening for requests
        /// </summary>
        public void Listen()
        {
            isActive = true;
            pool.StartPool();
            listener.Start();
            threads[0].Start();
        }

        private void Process()
        {
            while (isActive)
            {
                //Console.WriteLine("Waiting for Connection");
                var context = listener.GetContext();
                //Console.WriteLine("Received Request");
                ExecuteHandler(context);
            }
        }

        /// <summary>
        /// Handles incoming requests from the HttpListener
        /// </summary>
        /// <param name="context">Listener context</param>
        private void ExecuteHandler(HttpListenerContext context)
        {
            // Find Handler
            // We will work up the passed in path until we can find a handler
            // We move from more specific to more generic
            // eg Check first for /jobs/specifichandler/morespecifichandler
            // Then /jobs/specifichandler
            // Then /jobs
            var partialPath = context.Request.Url.AbsolutePath;
            if (!string.IsNullOrEmpty(partialPath))
            {
                while (true)
                {

                    if (handlers.ContainsKey(partialPath))
                    {
                        var handler = handlers[partialPath];
                        taskSet.EnqueSimpleTask(new ServerRequestHandler(context, handler));
                        return;
                    }
                    else
                    {
                        partialPath = partialPath.Substring(0, partialPath.LastIndexOf("/"));
                        if (partialPath.LastIndexOf("/") <= 0)
                        {
                            break;
                        }
                    }
                }
            }

            if (handlers.ContainsKey(partialPath))
            {
                // We located a handler send the request to the task queue
                var handler = handlers[partialPath];
                taskSet.EnqueSimpleTask(new ServerRequestHandler(context, handler));
                return;
            }

            // Return a 404 if we cannot find a handler
            log.Debug("Could not find handler for path: " + context.Request.Url.AbsolutePath);
            var errorHandler = _404Handler.Instance();
            taskSet.EnqueSimpleTask(new ServerRequestHandler(context, errorHandler));
            return;
        }

        public static NameValueCollection ParseQueryString(string s)
        {
            NameValueCollection nvc = new NameValueCollection();

            // remove anything other than query string from url
            if (s.Contains("?"))
            {
                s = s.Substring(s.IndexOf('?') + 1);
            }

            foreach (string vp in Regex.Split(s, "&"))
            {
                string[] singlePair = Regex.Split(vp, "=");
                if (singlePair.Length == 2)
                {
                    nvc.Add(singlePair[0], singlePair[1]);
                }
                else
                {
                    // only one key with no value specified in query string
                    nvc.Add(singlePair[0], string.Empty);
                }
            }

            return nvc;
        }

        public void Shutdown()
        {
            try
            {
                isActive = false;
                listener.Stop();
                pool.StopPool();
                while (threads.Count > 0)
                {
                    try
                    {
                        Thread t = threads[0];
                        if (t.IsAlive)
                            t.Abort();
                    }
                    catch (ThreadInterruptedException)
                    {
                        log.Debug("Shutting down connection");
                    }
                    catch (ThreadAbortException)
                    {
                        log.Debug("Shutting down connection");
                    }
                    catch (Exception e)
                    {
                        log.Error("Exception in data stream occured", e);
                    }
                    finally
                    {
                        threads.RemoveAt(0);
                    }
                }
            }
            catch (ThreadInterruptedException)
            {
                log.Debug("Shutting down connection");
            }
            catch (ThreadAbortException)
            {
                log.Debug("Shutting down connection");
            }
            catch (Exception e)
            {
                log.Error("Exception in data stream occured", e);
            }
            finally
            {
            }
        }
        public class RequestData
        {
            public NameValueCollection PostParameters;
            public List<UploadFile> Files;
            public class UploadFile
            {
                public string Name;
                public string Path;
                public int FileSize;
            }
            public RequestData()
            {
                PostParameters = new NameValueCollection();
                Files = new List<UploadFile>();
            }
        }
    }

}
