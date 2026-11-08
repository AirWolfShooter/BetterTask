using System;
using System.Collections.Generic;
using System.Text;

namespace BetterTask.Utils
{
    public class RecordingFile
    {
        public string Name { get; set; } = "Untitled Recording";
        public string Created { get; set; } = DateTime.Now.ToString();
        public string Version { get; set; } = "1.0";

        public List<RecordedEvent> Events { get; set; } = new();
    }
}
