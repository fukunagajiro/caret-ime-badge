using System.Drawing;

public struct Sample
{
    public bool HasCaret;
    public Rectangle Caret;
    public ImeMode Mode;
    /// <summary>直前のティック以降に EVENT_OBJECT_FOCUS が発生したか</summary>
    public bool FocusChanged;
    /// <summary>
    /// キャレット位置が要素の矩形から組み立てられたものか（空の入力欄の場合）。
    /// true のとき、位置の変化はユーザーの操作ではなくレイアウトの揺れを意味する。
    /// </summary>
    public bool CaretIsSynthesized;
}
