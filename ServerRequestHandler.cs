using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;

namespace TranscodeProcessor
{
    /// <summary>
    ///  Request handler for HTTP Request
    /// </summary>
    public class ServerRequestHandler : Sprite.ISimpleRunnable
    {
        private static ILog log = LogManager.GetCurrentClassLogger();
        protected HttpListenerContext context;
        protected IHttpRequestHandler handler;
        public ServerRequestHandler(HttpListenerContext context, IHttpRequestHandler handler)
        {
            this.context = context;
            this.handler = handler;
        }
        public object Handle()
        {
            try
            {
                if (handler == null)
                {
                    log.Error("HTTP Request Handler is null");
                }
                switch (context.Request.HttpMethod.ToLower())
                {
                    case "get":

                        handler.HandleGet(context.Request, context.Response);
                        break;
                    case "head":
                        if (handler is IHttpExtendedRequestHandler)
                        {
                            ((IHttpExtendedRequestHandler)handler).HandleHead(context.Request, context.Response);
                        }
                        else
                        {
                            context.Response.StatusCode = 404;
                            //context.Response.Close();
                        }
                        break;
                    case "delete":
                        if (handler is IHttpExtendedRequestHandler)
                        {
                            ((IHttpExtendedRequestHandler)handler).HandleDelete(context.Request, context.Response);
                        }
                        else
                        {
                            context.Response.StatusCode = 404;
                            //context.Response.Close();
                        }
                        break;
                    case "post":
                        Server.RequestData data = null;
                        // Handle multipart requests with file data
                        if (context.Request.ContentType != null && context.Request.ContentType.StartsWith("multipart/form-data;"))
                        {
                            var boundary = "--" + context.Request.ContentType.Substring(30);

                            byte[] buffer = new byte[4096];
                            int read = 0;
                            MemoryStream mstream = new MemoryStream();
                            while ((read = context.Request.InputStream.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                mstream.Write(buffer, 0, read);
                            }
                            try
                            {
                                data = processMultipartData(mstream, boundary);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Failed to process http post request", ex);
                                context.Response.WriteResponse(500, ex.Message);
                                return null;
                            }

                        }
                        else
                        {
                            var post_string = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                            var parts = Server.ParseQueryString(post_string);
                            data = new Server.RequestData();
                            data.PostParameters = parts;
                        }

                        handler.HandlePost(context.Request, context.Response, data);
                        break;
                }

            }
            catch (Exception ex)
            {
                log.Error("HTTP Processing Exception", ex);
                if (context != null)
                {
                    if (context.Response != null)
                    {
                        if (context.Response.OutputStream.CanWrite)
                        {
                            try
                            {
                                context.Response.WriteResponse(500, ex.ToString());
                            }
                            catch (Exception innerException)
                            {
                                log.Error("Failed to write response", innerException);
                                //Console.WriteLine(innerException);
                            }
                        }
                    }
                }
            }

            try
            {
                if (context.Response.OutputStream.CanWrite)
                {
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                log.Error("Error closing stream", ex);
            }
            return null;
        }

        public object Handle(object resource)
        {
            throw new NotImplementedException();
        }

        private Server.RequestData processMultipartData(MemoryStream data, string boundary)
        {
            string endBoundary = boundary + "--";
            byte[] boundaryBytes = Encoding.UTF8.GetBytes(boundary);
            byte[] endBoundaryBytes = Encoding.UTF8.GetBytes(endBoundary);
            var requestData = new Server.RequestData();
            using (data)
            {
                data.Position = 0;


                StreamReader sr = new StreamReader(data);

                while (true)
                {
                    string line = sr.ReadLine();

                    if (line == boundary)
                    {
                        break;
                    }
                    if (line == null)
                    {


                        log.Error("Parse error occurred when processing file data");

                    }
                }
                bool endBoundaryFound = false;
                BinaryWriter currentWriter;
                while (!endBoundaryFound)
                {
                    var parameters = new Dictionary<string, string>();
                    string line = sr.ReadLine();

                    while (line != string.Empty)
                    {
                        if (line == null)
                        {

                            throw new Exception("Segment parse Error");


                        }
                        if (line == boundary || line == endBoundary)
                        {
                            throw new Exception("Unexpected segment end");
                        }
                        Dictionary<string, string> values = SplitBySemicolonIgnoringSemicolonsInQuotes(line)
                     .Select(x => x.Split(new[] { ':', '=' }, 2))
                            // Limit split to 2 splits so we don't accidently split characters in file paths.
                     .ToDictionary(
                         x => x[0].Trim().Replace("\"", string.Empty).ToLower(),
                         x => x[1].Trim().Replace("\"", string.Empty));
                        // parameters dictionary.
                        try
                        {
                            foreach (var pair in values)
                            {
                                parameters.Add(pair.Key, pair.Value);
                            }
                        }
                        catch (ArgumentException)
                        {
                            throw new Exception("Duplicate field in section");
                        }

                        line = sr.ReadLine();

                    }
                    if (parameters.ContainsKey("filename"))
                    {

                        string name = parameters["name"];
                        string filename = Server.random.Next() + ".vod";//parameters["filename"];
                        File.Delete(filename);
                        string contentType = parameters.ContainsKey("content-type") ? parameters["content-type"] : "text/plain";
                        string contentDisposition = parameters.ContainsKey("content-disposition")
                                                        ? parameters["content-disposition"]
                                                        : "form-data";

                        string filePath = FFRest.config["workingdir"];
                        currentWriter = new BinaryWriter(File.Open(filePath + Path.DirectorySeparatorChar + filename, FileMode.Append, FileAccess.Write, FileShare.Read));
                        bool writerOpen = true;
                        // We want to create a stream and fill it with the data from the
                        // file.
                        var curBuffer = new byte[4096];
                        var prevBuffer = new byte[4096];
                        var fullBuffer = new byte[4096 * 2];
                        int curLength = 0;
                        int prevLength = 0;
                        int fullLength = 0;
                        int fileSize = 0;
                        data.Position = GetActualPosition(sr);
                        prevLength = data.Read(prevBuffer, 0, prevBuffer.Length);
                        do
                        {
                            curLength = data.Read(curBuffer, 0, curBuffer.Length);


                            Buffer.BlockCopy(prevBuffer, 0, fullBuffer, 0, prevLength);
                            Buffer.BlockCopy(curBuffer, 0, fullBuffer, prevLength, curLength);
                            fullLength = prevLength + curLength;

                            // Now we want to check for a substring within the current buffer.
                            // We need to find the closest substring greedily. That is find the
                            // closest boundary and don't miss the end --'s if it's an end boundary.
                            int endBoundaryPos = ByteSearch(fullBuffer, endBoundaryBytes, fullLength);
                            int endBoundaryLength = endBoundaryBytes.Length;
                            int boundaryPos = ByteSearch(fullBuffer, boundaryBytes, fullLength);
                            int boundaryLength = boundaryBytes.Length;

                            // We need to select the appropriate position and length
                            // based on the smallest non-negative position.
                            int endPos = -1;
                            int endPosLength = 0;

                            if (endBoundaryPos >= 0 && boundaryPos >= 0)
                            {
                                if (boundaryPos < endBoundaryPos)
                                {
                                    // Select boundary
                                    endPos = boundaryPos;
                                    endPosLength = boundaryLength;
                                }
                                else
                                {
                                    // Select end boundary
                                    endPos = endBoundaryPos;
                                    endPosLength = endBoundaryLength;
                                    endBoundaryFound = true;
                                }
                            }
                            else if (boundaryPos >= 0 && endBoundaryPos < 0)
                            {
                                // Select boundary    
                                endPos = boundaryPos;
                                endPosLength = boundaryLength;
                            }
                            else if (boundaryPos < 0 && endBoundaryPos >= 0)
                            {
                                // Select end boundary
                                endPos = endBoundaryPos;
                                endPosLength = endBoundaryLength;
                                endBoundaryFound = true;
                            }

                            if (endPos != -1)
                            {
                                // Now we need to check if the endPos is followed by \r\n or just \n. HTTP
                                // specifies \r\n but some clients might encode with \n. Or we might get 0 if
                                // we are at the end of the file.



                                // We also need to check if the last n characters of the buffer to write
                                // are a newline and if they are ignore them.
                                int maxNewlineBytes = Encoding.UTF8.GetMaxByteCount(2);
                                int bufferNewlineOffset = -1;
                                byte[] dataRef = fullBuffer;
                                int tmpOffset = Math.Max(0, endPos - maxNewlineBytes);
                                if (tmpOffset != 0)
                                {
                                    dataRef = fullBuffer.Skip(tmpOffset).ToArray();
                                }


                                int position = ByteSearch(dataRef, Encoding.UTF8.GetBytes("\r\n"), maxNewlineBytes);
                                if (position != -1)
                                {
                                    bufferNewlineOffset = position + tmpOffset;
                                }


                                int bufferNewlineLength = 0;
                                if (fullBuffer[bufferNewlineOffset] == '\r' && fullBuffer[bufferNewlineOffset + 1] == '\n')
                                {
                                    bufferNewlineLength = 2;
                                }

                                // We've found an end. We need to consume all the binary up to it 
                                // and then write the remainder back to the original stream. Then we
                                // need to modify the original streams position to take into account
                                // the new data.
                                // We also want to chop off the newline that is inserted by the protocl.
                                // We can do this by reducing endPos by the length of newline in this environment
                                // and encoding
                                //FileHandler(name, filename, contentType, contentDisposition, fullBuffer,
                                //            endPos - bufferNewlineLength);
                                fileSize += endPos - bufferNewlineLength;
                                currentWriter.Write(fullBuffer, 0, endPos - bufferNewlineLength);
                                currentWriter.Flush();
                                currentWriter.Close();
                                writerOpen = false;
                                sr.DiscardBufferedData();

                                //int writeBackOffset = endPos + endPosLength + boundaryNewlineOffset;
                                //int writeBackAmount = (prevLength + curLength) - writeBackOffset;
                                //var writeBackBuffer = new byte[writeBackAmount];
                                //Buffer.BlockCopy(fullBuffer, writeBackOffset, writeBackBuffer, 0, writeBackAmount);
                                //reader.Buffer(writeBackBuffer);

                                break;
                            }


                            // No end, consume the entire previous buffer  
                            fileSize += prevLength;
                            currentWriter.Write(prevBuffer, 0, prevLength);
                            sr.DiscardBufferedData();
                            //FileHandler(name, filename, contentType, contentDisposition, prevBuffer, prevLength);

                            // Now we want to swap the two buffers, we don't care
                            // what happens to the data from prevBuffer so we set
                            // curBuffer to it so it gets overwrited.

                            byte[] tempBuffer = curBuffer;
                            curBuffer = prevBuffer;
                            prevBuffer = tempBuffer;

                            // We don't need to swap the lengths because
                            // curLength will be overwritten in the next
                            // iteration of the loop.
                            prevLength = curLength;

                        } while (prevLength != 0);
                        if (writerOpen)
                        {
                            currentWriter.Flush();
                            currentWriter.Close();
                        }
                        requestData.Files.Add(new Server.RequestData.UploadFile()
                        {
                            FileSize = fileSize,
                            Name = filename,
                            Path = filePath + Path.DirectorySeparatorChar + filename
                        });

                    }
                    else
                    {
                        var paramData = new StringBuilder();
                        bool firstTime = true;
                        line = sr.ReadLine();
                        while (line != boundary && line != endBoundary)
                        {
                            if (line == null)
                            {


                                throw new Exception("Unexpected end of section");

                            }

                            if (firstTime)
                            {
                                paramData.Append(line);
                                firstTime = false;
                            }
                            else
                            {
                                paramData.Append(Environment.NewLine);
                                paramData.Append(line);
                            }
                            line = sr.ReadLine();
                        }

                        if (line == endBoundary)
                        {
                            endBoundaryFound = true;
                        }

                        // If we're here we've hit the boundary and have the data!
                        requestData.PostParameters.Add(parameters["name"], paramData.ToString());
                    }
                }
            }
            return requestData;
        }

        public long GetActualPosition(StreamReader reader)
        {
            // The current buffer of decoded characters
            char[] charBuffer = (char[])reader.GetType().InvokeMember("charBuffer"
                , System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.GetField
                , null, reader, null);

            // The current position in the buffer of decoded characters
            int charPos = (int)reader.GetType().InvokeMember("charPos"
                , System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.GetField
                , null, reader, null);

            // The number of bytes that the already-read characters need when encoded.
            int numReadBytes = reader.CurrentEncoding.GetByteCount(charBuffer, 0, charPos);

            // The number of encoded bytes that are in the current buffer
            int byteLen = (int)reader.GetType().InvokeMember("byteLen"
                , System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.GetField
                , null, reader, null);

            return reader.BaseStream.Position - byteLen + numReadBytes;
        }

        private IEnumerable<string> SplitBySemicolonIgnoringSemicolonsInQuotes(string line)
        {
            // Loop over the line looking for a semicolon. Keep track of if we're currently inside quotes
            // and if we are don't treat a semicolon as a splitting character.
            bool inQuotes = false;
            string workingString = "";
            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }

                if (c == ';' && !inQuotes)
                {
                    yield return workingString;
                    workingString = "";
                }
                else
                {
                    workingString += c;
                }
            }

            yield return workingString;
        }
        private int ByteSearch(byte[] haystack, byte[] needle, int haystackLength)
        {
            var charactersInNeedle = new HashSet<byte>(needle);

            var length = needle.Length;
            var index = 0;
            while (index + length <= haystackLength)
            {
                // Worst case scenario: Go back to character-by-character parsing until we find a non-match
                // or we find the needle.
                if (charactersInNeedle.Contains(haystack[index + length - 1]))
                {
                    var needleIndex = 0;
                    while (haystack[index + needleIndex] == needle[needleIndex])
                    {
                        if (needleIndex == needle.Length - 1)
                        {
                            // Found our match!
                            return index;
                        }

                        needleIndex += 1;
                    }

                    index += 1;
                    index += needleIndex;
                    continue;
                }

                index += length;
            }

            return -1;
        }
    }
}
