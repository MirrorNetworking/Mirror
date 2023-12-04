namespace Mirror
{
    public interface Capture
    {
        // server timestamp at time of capture.
        double timestamp { get; set; }

        // optional gizmo drawing for visual debugging.
        // history is only known on the server, which usually doesn't render.
        // showing Gizmos in the Editor is enough.
        void DrawGizmo();
    }
}
