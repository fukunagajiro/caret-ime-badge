using System.Drawing;

public static class BadgePlacerTests
{
    public static void Run()
    {
        Rectangle work = new Rectangle(0, 0, 1920, 1040);
        Size badge = new Size(64, 22);

        // 通常: キャレットの右上
        Point p = BadgePlacer.Place(new Rectangle(500, 300, 1, 20), badge, work, 6, -4);
        TestRunner.AssertEqual(506, p.X, "place/normal-x");
        TestRunner.AssertEqual(274, p.Y, "place/normal-y");

        // 右端: 左へ反転
        Point r = BadgePlacer.Place(new Rectangle(1900, 300, 1, 20), badge, work, 6, -4);
        TestRunner.AssertEqual(1830, r.X, "place/flip-left-x");

        // 上端: 下へ反転
        Point t = BadgePlacer.Place(new Rectangle(500, 2, 1, 20), badge, work, 6, -4);
        TestRunner.AssertEqual(26, t.Y, "place/flip-down-y");

        // 反転してもなお収まらない場合は作業領域内にクランプする
        Point c = BadgePlacer.Place(new Rectangle(0, 0, 1, 20), badge, work, 6, -4);
        TestRunner.AssertTrue(c.X >= work.Left, "place/clamp-left");
        TestRunner.AssertTrue(c.Y >= work.Top, "place/clamp-top");
        TestRunner.AssertTrue(c.X + badge.Width <= work.Right, "place/clamp-right");
        TestRunner.AssertTrue(c.Y + badge.Height <= work.Bottom, "place/clamp-bottom");

        // 原点が (0,0) でないモニタ（マルチモニタの副画面）でも作業領域を尊重する
        Rectangle work2 = new Rectangle(1920, 0, 1280, 1024);
        Point m = BadgePlacer.Place(new Rectangle(3190, 500, 1, 20), badge, work2, 6, -4);
        TestRunner.AssertTrue(m.X >= work2.Left, "place/second-monitor-left");
        TestRunner.AssertTrue(m.X + badge.Width <= work2.Right, "place/second-monitor-right");
    }
}
