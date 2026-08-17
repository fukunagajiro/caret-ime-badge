using System;
using System.Drawing;
using System.Windows.Automation;
using System.Windows.Automation.Text;

public static class CaretLocator
{
    private static readonly Guid IID_IAccessible =
        new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");

    /// <summary>
    /// UIA が返す矩形がキャレットとして妥当かを判定する。
    /// UIA は行全体や要素全体を返すことがあり(VS Code 1663x19、Edge 548x40)、
    /// そのまま使うとバッジが行頭に飛ぶ。
    /// </summary>
    public static bool IsPlausibleCaret(Rectangle r)
    {
        if (r.Width <= 0 || r.Height <= 0)
        {
            return false;
        }
        return r.Width <= r.Height * 4;
    }

    /// <summary>
    /// 両手段の生の結果から採用する矩形を決める。純粋関数。
    ///
    /// MSAA が「キャレットはあるが面積が無い」と答えた場合、それはテキスト欄に
    /// フォーカスが無いという意味である(Chromium で実測)。この場合 UIA に問い直しては
    /// ならない — Web ページ本体は TextPattern を持つため、別位置にバッジを出してしまう。
    /// </summary>
    public static bool ChooseCaret(bool msaaOk, Rectangle msaa, bool uiaOk, Rectangle uia, out Rectangle rect)
    {
        if (msaaOk)
        {
            if (IsPlausibleCaret(msaa))
            {
                rect = msaa;
                return true;
            }
            rect = Rectangle.Empty;
            return false;
        }
        if (uiaOk && IsPlausibleCaret(uia))
        {
            rect = uia;
            return true;
        }
        rect = Rectangle.Empty;
        return false;
    }

    public static bool TryGetCaret(IntPtr hwndFocus, out Rectangle rect)
    {
        Rectangle msaa;
        bool msaaOk = TryMsaaCaret(hwndFocus, out msaa);
        Rectangle uia = Rectangle.Empty;
        bool uiaOk = false;
        if (!msaaOk)
        {
            uiaOk = TryUiaCaret(out uia);
        }
        return ChooseCaret(msaaOk, msaa, uiaOk, uia, out rect);
    }

    /// <summary>MSAA の生の結果。妥当性判定は行わない。</summary>
    public static bool TryMsaaCaret(IntPtr hwnd, out Rectangle rect)
    {
        rect = Rectangle.Empty;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        try
        {
            Guid iid = IID_IAccessible;
            object acc;
            int hr = NativeMethods.AccessibleObjectFromWindow(hwnd, NativeMethods.OBJID_CARET, ref iid, out acc);
            if (hr != 0 || acc == null)
            {
                return false;
            }
            Accessibility.IAccessible ia = acc as Accessibility.IAccessible;
            if (ia == null)
            {
                return false;
            }
            int x, y, w, h;
            ia.accLocation(out x, out y, out w, out h, (object)0);
            if (x == 0 && y == 0 && w == 0 && h == 0)
            {
                return false;
            }
            rect = new Rectangle(x, y, w, h);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>UIA の生の結果。妥当性判定は行わない。</summary>
    public static bool TryUiaCaret(out Rectangle rect)
    {
        rect = Rectangle.Empty;
        try
        {
            AutomationElement el = AutomationElement.FocusedElement;
            if (el == null)
            {
                return false;
            }
            object pattern;
            if (!el.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
            {
                return false;
            }
            TextPattern tp = pattern as TextPattern;
            if (tp == null)
            {
                return false;
            }
            TextPatternRange[] sel = tp.GetSelection();
            if (sel == null || sel.Length == 0)
            {
                return false;
            }
            System.Windows.Rect[] rects = sel[0].GetBoundingRectangles();
            if (rects.Length < 1)
            {
                // 折りたたまれたキャレットは矩形を返さないので 1 文字分広げる
                TextPatternRange widened = sel[0].Clone();
                widened.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 1);
                rects = widened.GetBoundingRectangles();
            }
            if (rects.Length < 1)
            {
                return false;
            }
            System.Windows.Rect r0 = rects[0];
            rect = new Rectangle(
                (int)r0.X, (int)r0.Y, (int)r0.Width, (int)r0.Height);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
