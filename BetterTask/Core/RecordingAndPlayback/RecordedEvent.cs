using SharpHook.Data;

namespace BetterTask.Core.RecordingAndPlayback
{
    public record RecordedEvent
    {
        public long Time { get; set; }
        public EventType Type { get; set; }
        public short? MouseX { get; set; }
        public short? MouseY { get; set; }
        public KeyCode? KeyCode { get; set; }
        public MouseButton? MouseButton { get; set; }
        public ushort? WheelDelta { get; set; }
        public short? Rotation { get; set; }
        public MouseWheelScrollDirection? ScrollDirection { get; set; }
        public MouseWheelScrollType? ScrollType { get; set; }

    }
}
