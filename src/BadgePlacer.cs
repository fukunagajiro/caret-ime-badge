using System.Drawing;

public static class BadgePlacer
{
    /// <summary>
    /// キャレット矩形の右上を既定位置とし、作業領域からはみ出す場合は反転・クランプする。
    /// workArea は呼び出し側が Screen.FromPoint で選んだモニタの作業領域を渡すこと。
    /// </summary>
    public static Point Place(Rectangle caret, Size badge, Rectangle workArea, int offsetX, int offsetY)
    {
        int x = caret.X + offsetX;
        int y = caret.Y - badge.Height + offsetY;

        if (x + badge.Width > workArea.Right)
        {
            x = caret.X - badge.Width - offsetX;
        }
        if (y < workArea.Top)
        {
            y = caret.Bottom - offsetY;
        }

        if (x < workArea.Left) { x = workArea.Left; }
        if (y < workArea.Top) { y = workArea.Top; }
        if (x + badge.Width > workArea.Right) { x = workArea.Right - badge.Width; }
        if (y + badge.Height > workArea.Bottom) { y = workArea.Bottom - badge.Height; }

        return new Point(x, y);
    }
}
