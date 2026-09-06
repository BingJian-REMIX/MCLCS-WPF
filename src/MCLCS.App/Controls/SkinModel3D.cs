using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace MCLCS.App.Controls;

/// <summary>
/// 根据 Minecraft 皮肤位图构建方块角色 3D 模型。
/// <para>
/// 实现策略：把皮肤 PNG 按面裁切为独立小图，每面一个 DiffuseMaterial + Quad，
/// 避免整张皮肤走 UV 图时出现的采样漂移、左右面混淆、slim 手臂面宽不一致等问题。
/// 支持标准 64×64（Java 1.8+）与 64×32 旧皮肤，以及 slim/classic 手臂。
/// </para>
/// </summary>
public static class SkinModel3D
{
    private const double CenterY = 16.0; // 身高 32，平移使垂直中心落在原点

    /// <summary>一个皮肤矩形（像素：X, Y, W, H）。</summary>
    private readonly record struct Rect(int X, int Y, int W, int H);

    /// <summary>一个身体部位 6 个面的皮肤矩形。顺序：Front, Back, Left, Right, Top, Bottom。</summary>
    private readonly record struct Uv(Rect Front, Rect Back, Rect Left, Rect Right, Rect Top, Rect Bottom);

    /// <summary>一个身体部位的定义（尺寸、中心、6 面矩形、第二层矩形）。</summary>
    private readonly record struct PartDef(Uv First, Uv Overlay, double W, double H, double D, double Cx, double Cy, bool IsArm = false);

    /// <summary>第一层（实体）部位：头/躯干/右臂/左臂/右腿/左腿。</summary>
    private static readonly PartDef[] FirstLayer =
    {
        new(HeadUv(),       HatUv(),       8, 8,  8,  0, 28),            // 头
        new(BodyUv(),       JacketUv(),    8, 12, 4,  0, 18),            // 躯干
        new(LimbUv(44, 20), LimbUv(44, 36), 4, 12, 4,  6, 18, true),     // 右臂
        new(LimbUv(36, 52), LimbUv(52, 52), 4, 12, 4, -6, 18, true),     // 左臂
        new(LimbUv(4, 20),  LimbUv(4, 36),  4, 12, 4,  2, 6),            // 右腿
        new(LimbUv(20, 52), LimbUv(4, 52),  4, 12, 4, -2, 6),            // 左腿
    };

    /// <summary>构建角色模型。</summary>
    public static Model3DGroup Build(BitmapImage skin, bool slim)
    {
        var group = new Model3DGroup();

        bool legacy = skin.PixelHeight is <= 32;
        int tw = Math.Max(1, skin.PixelWidth);
        int th = Math.Max(1, skin.PixelHeight);

        foreach (var def in FirstLayer)
        {
            BuildPart(group, skin, tw, th, def, slim, legacy, useOverlay: false);
            if (!legacy)
                BuildPart(group, skin, tw, th, def, slim, legacy, useOverlay: true);
        }

        return group;
    }

    private static void BuildPart(Model3DGroup group, BitmapImage skin, int tw, int th, PartDef def, bool slim, bool legacy, bool useOverlay)
    {
        double w = def.IsArm && slim ? 3 : def.W;
        double h = def.H;
        double d = def.D;

        // 原版正面约定：角色右手在观察者左侧(-X)，左手在观察者右侧(+X)。
        // 原表中右臂 Cx=6（错在右侧），这里取反纠正。
        double cx = -def.Cx;
        if (def.IsArm && slim) cx -= Math.Sign(cx) * 0.5; // slim 手臂向身体中心收 0.5

        double cy = def.Cy - CenterY;

        Uv uv = useOverlay ? def.Overlay : def.First;

        // 64×32 旧皮肤无独立左侧区域：左臂/左腿镜像复用右侧纹理。
        if (legacy && !useOverlay)
        {
            int idx = Array.IndexOf(FirstLayer, def);
            if (idx is 3) uv = LimbUv(44, 20); // 左臂用右臂区域
            else if (idx is 5) uv = LimbUv(4, 20); // 左腿用右腿区域
        }

        if (def.IsArm && slim) uv = SlimUv(uv);

        // 位于 +X 侧的肢体（左臂/左腿），其内外侧面与默认(-X 侧)相反，须交换 Left/Right 纹理。
        if (cx > 0) uv = uv with { Left = uv.Right, Right = uv.Left };

        double expand = 0.0;
        if (useOverlay)
        {
            expand = def.W is 8 && def.H is 8 ? 1.0 : 0.5; // 帽子 0.5/边；衣裤 0.25/边
            w += expand;
            h += expand;
            d += expand;
        }

        AddBox(group, skin, tw, th, cx, cy, 0, w, h, d, uv);
    }

    // —— UV 构造助手（坐标均来自标准 64×64 布局） ——

    private static Uv HeadUv() => new(
        new Rect(8, 8, 8, 8), new Rect(24, 8, 8, 8), new Rect(16, 8, 8, 8), new Rect(0, 8, 8, 8),
        new Rect(8, 0, 8, 8), new Rect(16, 0, 8, 8));

    private static Uv BodyUv() => new(
        new Rect(20, 20, 8, 12), new Rect(32, 20, 8, 12), new Rect(16, 20, 4, 12), new Rect(28, 20, 4, 12),
        new Rect(20, 16, 8, 4), new Rect(28, 16, 8, 4));

    private static Uv LimbUv(int fx, int fy) => new(
        new Rect(fx, fy, 4, 12), new Rect(fx + 8, fy, 4, 12), new Rect(fx - 4, fy, 4, 12), new Rect(fx + 4, fy, 4, 12),
        new Rect(fx, fy - 4, 4, 4), new Rect(fx + 4, fy - 4, 4, 4));

    private static Uv HatUv() => new(
        new Rect(40, 8, 8, 8), new Rect(56, 8, 8, 8), new Rect(48, 8, 8, 8), new Rect(32, 8, 8, 8),
        new Rect(40, 0, 8, 8), new Rect(48, 0, 8, 8));

    private static Uv JacketUv() => new(
        new Rect(20, 36, 8, 12), new Rect(32, 36, 8, 12), new Rect(16, 36, 4, 12), new Rect(28, 36, 4, 12),
        new Rect(20, 32, 8, 4), new Rect(28, 32, 8, 4));

    /// <summary>slim 手臂：front/back/top/bottom 宽度由 4 收窄为 3，左右侧面保持 4（深度面）。</summary>
    private static Uv SlimUv(Uv uv) => uv with
    {
        Front = uv.Front with { W = 3 },
        Back = uv.Back with { W = 3 },
        Top = uv.Top with { W = 3 },
        Bottom = uv.Bottom with { W = 3 },
    };

    private static void AddBox(Model3DGroup group, BitmapImage skin, int tw, int th,
        double cx, double cy, double cz, double w, double h, double d, Uv uv)
    {
        double hx = w / 2, hy = h / 2, hz = d / 2;
        Point3D P(double x, double y, double z) => new Point3D(cx + x, cy + y, cz + z);

        // 各面四角（外视 CCW：左上、右上、右下、左下）
        AddFace(group, skin, tw, th,
            P(-hx, +hy, +hz), P(+hx, +hy, +hz), P(+hx, -hy, +hz), P(-hx, -hy, +hz), uv.Front);   // 正面 +Z
        AddFace(group, skin, tw, th,
            P(+hx, +hy, -hz), P(-hx, +hy, -hz), P(-hx, -hy, -hz), P(+hx, -hy, -hz), uv.Back);    // 背面 -Z
        AddFace(group, skin, tw, th,
            P(-hx, +hy, -hz), P(-hx, +hy, +hz), P(-hx, -hy, +hz), P(-hx, -hy, -hz), uv.Left);    // 左面 -X
        AddFace(group, skin, tw, th,
            P(+hx, +hy, +hz), P(+hx, +hy, -hz), P(+hx, -hy, -hz), P(+hx, -hy, +hz), uv.Right);   // 右面 +X
        AddFace(group, skin, tw, th,
            P(-hx, +hy, -hz), P(+hx, +hy, -hz), P(+hx, +hy, +hz), P(-hx, +hy, +hz), uv.Top);      // 顶面 +Y
        AddFace(group, skin, tw, th,
            P(-hx, -hy, +hz), P(+hx, -hy, +hz), P(+hx, -hy, -hz), P(-hx, -hy, -hz), uv.Bottom);   // 底面 -Y
    }

    private static void AddFace(Model3DGroup group, BitmapImage skin, int tw, int th,
        Point3D tl, Point3D tr, Point3D br, Point3D bl, Rect r)
    {
        // 把矩形裁到贴图边界内，防止 slim/legacy 算出的区域越界。
        int x = Math.Max(0, Math.Min(tw - 1, r.X));
        int y = Math.Max(0, Math.Min(th - 1, r.Y));
        int w = Math.Max(0, Math.Min(tw - x, r.W));
        int h = Math.Max(0, Math.Min(th - y, r.H));
        if (w <= 0 || h <= 0) return;

        var cropped = new CroppedBitmap(skin, new Int32Rect(x, y, w, h));
        RenderOptions.SetBitmapScalingMode(cropped, BitmapScalingMode.NearestNeighbor);

        var brush = new ImageBrush(cropped)
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.None,
        };
        var material = new DiffuseMaterial(brush);

        var mesh = new MeshGeometry3D();
        mesh.Positions.Add(tl);
        mesh.Positions.Add(tr);
        mesh.Positions.Add(br);
        mesh.Positions.Add(bl);
        mesh.TextureCoordinates.Add(new Point(0, 0));
        mesh.TextureCoordinates.Add(new Point(1, 0));
        mesh.TextureCoordinates.Add(new Point(1, 1));
        mesh.TextureCoordinates.Add(new Point(0, 1));
        mesh.TriangleIndices.Add(0);
        mesh.TriangleIndices.Add(1);
        mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(0);
        mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(3);

        group.Children.Add(new GeometryModel3D(mesh, material)
        {
            BackMaterial = material // 双面渲染，旋转时背面不缺失
        });
    }
}
