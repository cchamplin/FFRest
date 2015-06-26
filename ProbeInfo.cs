using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TranscodeProcessor
{
    // Wrapper classes for deserialized probe information
    public class ProbeInfo
    {
        public List<StreamInfo> streams { get; set; }
        public FormatInfo format { get; set; }
    }
    public class StreamInfo
    {
        public object index;
        public object codec_name;
        public object profile;
        public object codec_type;
        public object width;
        public object height;
        public object display_aspect_ratio;
        public object sample_aspect_ratio;
        public object bit_rate;
        public object avg_frame_rate;
    }
    public class FormatInfo
    {
        public object duration;
        public object bit_rate;
    }
}
