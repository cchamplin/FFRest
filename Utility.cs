using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TranscodeProcessor
{
    public static class Utility
    {
        private static FastSerialize.Serializer serializer = new FastSerialize.Serializer(typeof(FastSerialize.JsonSerializerGeneric));
        public static string GetFirstValue(this NameValueCollection collection, string key)
        {
            string[] values = collection.GetValues(key);
            if (values == null) return null;
            if (values.Length > 0)
            {
                return values[0];
            }
            return null;
        }
        public static void WriteResponse(this HttpListenerResponse response, string output)
        {
            if (response.OutputStream == null || !response.OutputStream.CanWrite)
            {
                return;
            }
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(output);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream outputStream = response.OutputStream;
            outputStream.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            outputStream.Close();
        }
        public static void WriteResponse(this HttpListenerResponse response, object output)
        {
            if (response.OutputStream == null || !response.OutputStream.CanWrite)
            {
                return;
            }
            var data = serializer.Serialize(output);
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(data);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream outputStream = response.OutputStream;
            outputStream.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            outputStream.Close();
        }
        public static void WriteResponse(this HttpListenerResponse response, int statusCode, string output)
        {
            if (response.OutputStream == null || !response.OutputStream.CanWrite)
            {
                return;
            }

            response.StatusCode = statusCode;
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(output);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream outputStream = response.OutputStream;
            outputStream.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            outputStream.Close();
        }
        internal static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[32768];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
            }
        }
        internal static string GetMime(string fileType)
        {
            if (fileType[0] == '.')
                fileType = fileType.Substring(1);
            switch (fileType.ToLower())
            {
                case "txt":
                    return "text/plain";
                case "html":
                    return "text/html";
                case "htm":
                    return "text/html";
                case "css":
                    return "text/css";
                case "gif":
                    return "image/gif";
                case "jpeg":
                case "jpg":
                    return "image/jpeg";
                case "js":
                    return "application/x-javascript";
                case "png":
                    return "image/png";
                case "mp3":
                    return "audio/mpeg";
                case "mp4":
                    return "video/mp4";
                case "mov":
                    return "video/quicktime";
                case "mpeg":
                case "mpg":
                    return "video/mpeg";
                case "3gp":
                case "3gpp":
                    return "video/3gpp";
                case "flv":
                    return "video/x-flv";
                case "ts":
                case "avi":
                    return "video/x-msvideo";
                case "wmv":
                    return "video/x-ms-wmv";
                case "m4v":
                    return "video/mp4";
                case "webm":
                    return "video/webm";
                case "ogg":
                    return "audio/ogg";
                case "ogv":
                    return "video/ogg";
            }
            return "application/octet-stream";
        }
        public static void WriteResponse(this HttpListenerResponse response, int statusCode, object output)
        {
            if (response.OutputStream == null || !response.OutputStream.CanWrite)
            {
                return;
            }
            response.StatusCode = statusCode;
            var data = serializer.Serialize(output);
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(data);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream outputStream = response.OutputStream;
            outputStream.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            outputStream.Close();
        }
        
        // Todo: Handle permission errors?
        public static void DirectoryCreate(string path)
        {
            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                }
            }
        }

        // Todo: Handle permission errors?
        public static void FileDelete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
