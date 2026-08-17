using System;
using System.Drawing;
using System.Windows.Automation;
using System.Windows.Automation.Text;

public static class CaretLocator
{
    private static readonly Guid IID_IAccessible =
        new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");

    /// <summary>
    /// MSAA / UIA いずれの矩形もキャレットとして妥当かを判定する(ChooseCaret から両方に適用される)。
    /// UIA は行全体や要素全体を返すことがあり(VS Code 1663x19、Edge 548x40)、そのまま使うと
    /// バッジが行頭に飛ぶ。MSAA は位置はあるが面積の無い矩形を返すことがあり(Chromium で
    /// テキスト欄以外にフォーカスがある場合)、そのまま使うと直前の位置にバッジが残り続ける。
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

    /// <summary>
    /// キャレット矩形に加えて、それが実測値ではなく要素の矩形から組み立てられたもの
    /// (<see cref="TryEmptyFieldCaret"/> 経由)かどうかを返す。MSAA 経路は決して
    /// 組み立てを行わないため、`!msaaOk` の条件で十分である。
    /// </summary>
    public static bool TryGetCaret(IntPtr hwndFocus, out Rectangle rect, out bool isSynthesized)
    {
        isSynthesized = false;
        Rectangle msaa;
        bool msaaOk = TryMsaaCaret(hwndFocus, out msaa);
        Rectangle uia = Rectangle.Empty;
        bool uiaOk = false;
        bool uiaSynth = false;
        if (!msaaOk)
        {
            uiaOk = TryUiaCaret(out uia, out uiaSynth);
        }
        bool ok = ChooseCaret(msaaOk, msaa, uiaOk, uia, out rect);
        isSynthesized = ok && !msaaOk && uiaSynth;
        return ok;
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

    /// <summary>
    /// TextPattern が矩形を返さなかったとき、要素自身の矩形からキャレット位置を組み立てる。
    ///
    /// 空の入力欄では UIA が矩形を返せないが、そこは「入力前に IME を確認したい」瞬間そのもの
    /// である。単一行の入力欄なら、空のときのキャレットは要素の左端にある。
    ///
    /// isEditControl が false のときは必ず false を返すこと。Web ページ本体は
    /// ControlType.Document かつ TextPattern を持つが編集できず、ここで受け入れると
    /// ページ全体の矩形にバッジを出してしまう(実測済み)。
    /// </summary>
    public static bool TryEmptyFieldCaret(bool isEditControl, Rectangle elementRect, out Rectangle rect)
    {
        rect = Rectangle.Empty;
        if (!isEditControl)
        {
            return false;
        }
        if (elementRect.Width <= 0 || elementRect.Height <= 0)
        {
            return false;
        }
        rect = new Rectangle(elementRect.X, elementRect.Y, 1, elementRect.Height);
        return true;
    }

    /// <summary>
    /// UIA の生の結果。妥当性判定は行わない。
    /// <paramref name="isSynthesized"/> は、TextPattern が矩形を返せず
    /// <see cref="TryEmptyFieldCaret"/> の組み立てた矩形を採用した場合にのみ true になる。
    /// </summary>
    public static bool TryUiaCaret(out Rectangle rect, out bool isSynthesized)
    {
        rect = Rectangle.Empty;
        isSynthesized = false;
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
                // TextPattern は矩形を返せなかった。空の入力欄(Edit)に限り、要素自身の
                // 矩形の左端をキャレット位置とみなす。Document(Web ページ本体)などは除外する。
                bool isEditControl = el.Current.ControlType == ControlType.Edit;
                System.Windows.Rect er = el.Current.BoundingRectangle;
                Rectangle elementRect = new Rectangle(
                    (int)er.X, (int)er.Y, (int)er.Width, (int)er.Height);
                bool synthOk = TryEmptyFieldCaret(isEditControl, elementRect, out rect);
                isSynthesized = synthOk;
                return synthOk;
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
