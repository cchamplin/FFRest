using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TranscodeProcessor
{

    /// <summary>
    /// Handler for transcoding presets
    /// Used for adding and retrieve existing presets
    /// </summary>
    public class PresetHandler : IHttpRequestHandler
    {
        protected Presets presets;

        public PresetHandler(Presets presets)
        {
            this.presets = presets;
        }
        
        public void HandleGet(HttpListenerRequest request, HttpListenerResponse response)
        {
            string queryString = request.Url.Query;
            var queryParts = Server.ParseQueryString(queryString);

            string presetName = queryParts.GetFirstValue("preset");

            if (string.IsNullOrEmpty(presetName))
            {
                response.StatusCode = 200;
                response.WriteResponse(presets.GetAll());
            }
            else
            {
                string result = presets.Get(presetName);
                if (result == null)
                {
                    response.StatusCode = 404;
                    response.WriteResponse("No such preset has been registered");
                }
                else
                {
                    response.StatusCode = 200;
                    response.WriteResponse(result);
                }
            }    
        }

        public void HandlePost(HttpListenerRequest request, HttpListenerResponse response, Server.RequestData data)
        {
            var presetName = data.PostParameters.GetFirstValue("preset");
            var value = data.PostParameters.GetFirstValue("value");

            if (string.IsNullOrEmpty(presetName))
            {
                response.StatusCode = 400;
                response.WriteResponse("Missing preset name");
                return;
            }
            if (string.IsNullOrEmpty(value))
            {
                response.StatusCode = 400;
                response.WriteResponse("Missing preset value");
                return;
            }

            // TODO do we need to do filtering on this value?
            presets.Add(presetName, value);
            response.WriteResponse(200, "Preset added");
        }
    }
}
