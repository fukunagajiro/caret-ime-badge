using System;
using System.Drawing;

public enum BadgeAction
{
    None,
    Show,
    Move,
    /// <summary>フェードアウトを開始する</summary>
    Fade,
    /// <summary>フェードせず即座に消す</summary>
    HideNow
}

/// <summary>
/// 仕様 §5.1 の表示規則。Win32 に依存せず、時刻も引数で受け取るため単体テストできる。
/// 表示時間の管理をここに置いているのは、BadgeWindow 側に持たせるとフェード完了が
/// 状態機械に伝わらず IsShown が嘘になり、以後の表示判定が全て狂うため。
/// </summary>
public class BadgeStateMachine
{
    private readonly int _moveThresholdPx;
    private readonly int _showDurationMs;
    private bool _hasPrev;
    private Sample _prev;
    private bool _shown;
    private Rectangle _anchor;
    private long _shownAtMs;

    public BadgeStateMachine(int moveThresholdPx, int showDurationMs)
    {
        _moveThresholdPx = moveThresholdPx;
        _showDurationMs = showDurationMs;
        _hasPrev = false;
        _shown = false;
        _shownAtMs = 0;
    }

    public bool IsShown { get { return _shown; } }

    public BadgeAction Next(Sample s, long nowMs)
    {
        BadgeAction action = Decide(s, nowMs);
        if (action == BadgeAction.Show)
        {
            _shown = true;
            _anchor = s.Caret;
            _shownAtMs = nowMs;
        }
        else if (action == BadgeAction.Fade || action == BadgeAction.HideNow)
        {
            _shown = false;
        }
        _prev = s;
        _hasPrev = true;
        return action;
    }

    private BadgeAction Decide(Sample s, long nowMs)
    {
        if (!s.HasCaret)
        {
            return _shown ? BadgeAction.HideNow : BadgeAction.None;
        }
        // 入力可能になった
        if (!_hasPrev || !_prev.HasCaret)
        {
            return BadgeAction.Show;
        }
        // モード変化とフォーカス移動は移動判定より先に評価する。
        // これが仕様 §5.1 の競合解決（未確定文字の変換中に切り替えた場合など）。
        if (s.Mode != _prev.Mode)
        {
            return BadgeAction.Show;
        }
        if (s.FocusChanged)
        {
            return BadgeAction.Show;
        }
        if (!_shown)
        {
            return BadgeAction.None;
        }
        if (s.CaretIsSynthesized)
        {
            // 組み立てた位置は要素のレイアウトに追従するだけで、ユーザーの操作の証拠ではない。
            // 実測: メモ帳の検索バーは要素矩形が 120ms ごとに最大 95px 揺れるため、
            // 移動判定を適用するとバッジが即座に消える。位置も固定する(Move を返さない)。
            if (nowMs - _shownAtMs >= _showDurationMs)
            {
                return BadgeAction.Fade;
            }
            return BadgeAction.None;
        }
        int dx = Math.Abs(s.Caret.X - _anchor.X);
        int dy = Math.Abs(s.Caret.Y - _anchor.Y);
        if (dx >= _moveThresholdPx || dy >= _moveThresholdPx)
        {
            return BadgeAction.Fade;
        }
        if (nowMs - _shownAtMs >= _showDurationMs)
        {
            return BadgeAction.Fade;
        }
        return BadgeAction.Move;
    }
}
