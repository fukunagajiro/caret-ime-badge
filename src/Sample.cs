using System.Drawing;

public struct Sample
{
    public bool HasCaret;
    public Rectangle Caret;
    public ImeMode Mode;
    /// <summary>直前のティック以降に EVENT_OBJECT_FOCUS が発生したか</summary>
    public bool FocusChanged;
}
