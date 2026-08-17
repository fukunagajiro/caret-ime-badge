using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

/// <summary>
/// タスクトレイ用のアイコンを実行時に描画する。
/// TrayApp は配線・メニュー・寿命管理を担うので、描画ロジックはここに分離する。
/// </summary>
public static class TrayIcon
{
    private const int CanvasSize = 32;

    // 角丸四角（バッジ本体）。トレイでの実表示は 16px 相当なので、
    // キャンバスいっぱいに広げてグリフに使える面積を最大化する。
    private const float SquareX = 1f;
    private const float SquareY = 1f;
    private const float SquareSize = 30f;
    private const float CornerRadius = 8f;

    private const string GlyphText = "あ";
    private const float GlyphFontSize = 26f;

    /// <summary>
    /// トレイ用のアイコンを生成する。呼び出し側が Dispose する。
    /// </summary>
    public static Icon Create()
    {
        using (Bitmap bmp = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                // バッジ本体（角丸四角）。16px ではキャレットの縦棒は判別できず
                // 幅を食うだけのノイズになるため描かない。
                using (SolidBrush kanaBrush = new SolidBrush(BadgeStyles.Kana))
                {
                    RectangleF squareBounds = new RectangleF(SquareX, SquareY, SquareSize, SquareSize);
                    using (GraphicsPath path = RoundedRect(squareBounds, CornerRadius))
                    {
                        g.FillPath(kanaBrush, path);
                    }
                }

                using (Font font = new Font("Yu Gothic UI", GlyphFontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush glyphBrush = new SolidBrush(BadgeStyles.Background))
                {
                    SizeF glyphSize = g.MeasureString(GlyphText, font);
                    float x = SquareX + (SquareSize - glyphSize.Width) / 2f;
                    float y = SquareY + (SquareSize - glyphSize.Height) / 2f;
                    g.DrawString(GlyphText, font, glyphBrush, x, y);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                // Icon.FromHandle はハンドルを所有しないラッパーを返すだけなので、
                // Clone() で管理下のコピーを作ってから元の HICON を DestroyIcon で破棄する。
                // こうすれば呼び出し側は返された Icon を Dispose するだけでよい。
                using (Icon temp = Icon.FromHandle(hIcon))
                {
                    return (Icon)temp.Clone();
                }
            }
            finally
            {
                NativeMethods.DestroyIcon(hIcon);
            }
        }
    }

    private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        float d = radius * 2f;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
