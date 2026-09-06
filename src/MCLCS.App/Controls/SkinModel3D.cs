using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace MCLCS.App.Controls;

/// <summary>
/// 根据 Minecraft 皮肤位图构建方块角色 3D 模型（规格 3.8 皮肤预览 / 2.3 皮肤编辑器预览）。
/// <para>
/// UV 布局（标准 64×64，Java 版 1.8+）：
/// 头 x0-32/y0-16、帽子 x32-64/y0-16；右腿 x0-16、躯干 x16-40、右臂 x40-56 均在 y16-32；
/// 右裤 x0-16、外套 x16-40、右袖 x40-56 均在 y32-48；左裤 x0-16、左腿 x16-32、左臂 x32-48、左袖 x48-64 均在 y48-64。
/// 64×32 旧皮肤只有上半部分，左臂 / 左腿镜像复用右侧且无第二层。
/// 像素坐标按皮肤实际宽高归一化。classic 与 slim 的区别在于双臂宽度（slim 为 3px）。
/// 模型已平移使垂直中心落于原点，便于绕身体旋转。
/// </para>
/// </summary>
public static class SkinModel3D
{
    private const double CenterY = 16.0; // 身高 32，平移使垂直中心落在原点

    /// <summary>一个身体部位 6 个面的皮肤矩形（像素：X, Y, W, H）。顺序：Front, Back, Left, Right, Top, Bottom。</summary>
    private readonly record struct Rect(double X, double Y, double W, double H);

    private readonly record struct Uv(Rect Front, Rect Back, Rect Left, Rect Right, Rect Top, Rect Bottom);

    /// <summary>
    /// 第一层（实体）UV 表。顺序：头/躯干/右臂/左臂/右腿/左腿。
    /// <para>
    /// 左臂 / 左腿在 64×64 中位于底部带（y52），这是 1.8 新增的独立区域；
    /// 64×32 旧皮肤没有该区域，需镜像复用右臂 / 右腿（见 <see cref="Build"/> 的 legacy 分支）。
    /// </para>
    /// </summary>
    private static readonly (Uv Uv, double W, double H, double D, double Cx, double Cy)[] FirstLayer =
    {
        (HeadUv(),       8, 8,  8,  0, 28),
        (BodyUv(),       8, 12, 4,  0, 18),
        (LimbUv(44, 20), 4, 12, 4,  6, 18),  // 右臂 Right Arm  x40-56 / y16-32
        (LimbUv(36, 52), 4, 12, 4, -6, 18),  // 左臂 Left Arm   x32-48 / y48-64
        (LimbUv(4, 20),  4, 12, 4,  2, 6),   // 右腿 Right Leg  x0-16  / y16-32
        (LimbUv(20, 52), 4, 12, 4, -2, 6),   // 左腿 Left Leg   x16-32 / y48-64
    };

    /// <summary>
    /// 64×32 旧皮肤的第一层左臂 / 左腿 UV：该布局无独立左侧区域，
    /// 由右臂 / 右腿镜像复用（与原版渲染一致）。
    /// </summary>
    private static readonly Uv LegacyLeftArm = LimbUv(44, 20);
    private static readonly Uv LegacyLeftLeg = LimbUv(4, 20);

    /// <summary>第二层（叠加：帽子/外套/袖/裤）UV 表，与第一层一一对应。仅 64×64 使用。</summary>
    private static readonly Uv[] Overlay =
    {
        HatUv(),                 // 0 帽子   Hat        x32-64 / y0-16
        JacketUv(),              // 1 外套   Jacket     x16-40 / y32-48
        LimbUv(44, 36),          // 2 右袖   R Sleeve   x40-56 / y32-48
        LimbUv(52, 52),          // 3 左袖   L Sleeve   x48-64 / y48-64
        LimbUv(4, 36),           // 4 右裤   R Pants    x0-16  / y32-48
        LimbUv(4, 52),           // 5 左裤   L Pants    x0-16  / y48-64
    };

    /// <summary>构建角色模型（头/躯干/双臂/双腿；64×64 额外含第二层叠加）。</summary>
    public static Model3DGroup Build(BitmapImage skin, bool slim)
    {
        var group = new Model3DGroup();
        var material = MakeMaterial(skin);

        bool legacy = skin.PixelHeight is <= 32; // 64×32 旧皮肤：无第二层
        double tw = Math.Max(1, skin.PixelWidth);
        double th = Math.Max(1, skin.PixelHeight);

        // 第一层
        for (int i = 0; i < FirstLayer.Length; i++)
        {
            var p = FirstLayer[i];
            bool isArm = i is 2 or 3;
            double w = (isArm && slim) ? 3 : p.W;   // slim：双臂均收窄为 3px
            // 原版正面约定：角色右手在观察者左侧(-X)，故取 -Cx（头/躯干 Cx=0 不受影）。
            // slim 手臂宽度减少的 1px 全部从外侧收回，向身体中心(0)移 0.5。
            double cx = -p.Cx;
            if (isArm && slim) cx -= Math.Sign(cx) * 0.5;

            Uv uv = p.Uv;
            if (legacy)
            {
                // 64×32：无左臂 / 左腿独立区域，镜像复用右侧
                if (i == 3) uv = LegacyLeftArm;
                else if (i == 5) uv = LegacyLeftLeg;
            }
            if (isArm && slim) uv = SlimUv(uv);
            // 位于 -X 侧的肢体，其内(朝身体)/外(背离身体)侧面与默认(+X 侧)相反，
            // 须交换 Left/Right 纹理，否则左臂/左腿内外贴反。
            if (cx < 0) uv = uv with { Left = uv.Right, Right = uv.Left };

            AddBox(group, material, cx, p.Cy - CenterY, 0, w, p.H, p.D, uv, tw, th);
        }

        // 第二层叠加（仅 64×64）：按权威尺寸做“向外偏移”
        //   帽子层：外扩 0.5px/边（盒 ±1，8→9）
        //   躯干/臂/腿（衣裤层）：外扩 0.25px/边（盒 ±0.5）
        // 叠加盒与实体盒共中心，故偏移对称包覆；几何外扩 + 纹理齐平可避免 z-fighting 与“漏肉”。
        if (!legacy)
        {
            for (int i = 0; i < Overlay.Length; i++)
            {
                var p = FirstLayer[i];
                bool isHead = i == 0;
                bool isArm = i is 2 or 3;
                double expand = isHead ? 1.0 : 0.5;              // 帽子 0.5/边；衣裤 0.25/边
                double w = (isArm && slim) ? 3 + expand : p.W + expand;
                double cx = -p.Cx;
                if (isArm && slim) cx -= Math.Sign(cx) * 0.5;
                Uv uv = (isArm && slim) ? SlimUv(Overlay[i]) : Overlay[i];
                if (cx < 0) uv = uv with { Left = uv.Right, Right = uv.Left };
                AddBox(group, material, cx, p.Cy - CenterY, 0, w, p.H + expand, p.D + expand, uv, tw, th);
            }
        }

        return group;
    }

    // —— UV 构造助手（坐标均来自标准 64×64 布局，第一层与 64×32 共用） ——

    private static Uv HeadUv() => new(
        new Rect(8, 8, 8, 8), new Rect(24, 8, 8, 8), new Rect(16, 8, 8, 8), new Rect(0, 8, 8, 8),
        new Rect(8, 0, 8, 8), new Rect(16, 0, 8, 8));

    private static Uv BodyUv() => new(
        new Rect(20, 20, 8, 12), new Rect(32, 20, 8, 12), new Rect(16, 20, 4, 12), new Rect(28, 20, 4, 12),
        new Rect(20, 16, 8, 4), new Rect(28, 16, 8, 4));

    // 四肢：front=fx,fy；inner(Left)=fx-4；outer(Right)=fx+4；back=fx+8；top/bottom 在 front 正上方 4px。
    private static Uv LimbUv(int fx, int fy) => new(
        new Rect(fx, fy, 4, 12), new Rect(fx + 8, fy, 4, 12), new Rect(fx - 4, fy, 4, 12), new Rect(fx + 4, fy, 4, 12),
        new Rect(fx, fy - 4, 4, 4), new Rect(fx + 4, fy - 4, 4, 4));

    // 帽子层位于头部右侧（x32-64 / y0-16），即头部区域整体右移 32，而非下移。
    private static Uv HatUv() => new(
        new Rect(40, 8, 8, 8), new Rect(56, 8, 8, 8), new Rect(48, 8, 8, 8), new Rect(32, 8, 8, 8),
        new Rect(40, 0, 8, 8), new Rect(48, 0, 8, 8));

    // 外套层位于躯干正下方（x16-40 / y32-48），即躯干区域下移 16。
    private static Uv JacketUv() => new(
        new Rect(20, 36, 8, 12), new Rect(32, 36, 8, 12), new Rect(16, 36, 4, 12), new Rect(28, 36, 4, 12),
        new Rect(20, 32, 8, 4), new Rect(28, 32, 8, 4));

    // slim：左臂仅 front/back 宽度由 4 收窄为 3（取插槽最左 3px），其余面维持不变。
    private static Uv SlimUv(Uv uv) => uv with
    {
        Front = uv.Front with { W = 3 },
        Back = uv.Back with { W = 3 },
    };

    private static Material MakeMaterial(BitmapImage skin)
    {
        RenderOptions.SetBitmapScalingMode(skin, BitmapScalingMode.NearestNeighbor);
        return new DiffuseMaterial(new ImageBrush(skin));
    }

    private static void AddBox(Model3DGroup group, Material material,
        double cx, double cy, double cz, double w, double h, double d,
        Uv uv, double tw, double th)
    {
        double hx = w / 2, hy = h / 2, hz = d / 2;

        Point3D P(double x, double y, double z) => new Point3D(cx + x, cy + y, cz + z);

        // 各面四角（外视 CCW：左上、右上、右下、左下）
        AddQuad(group, material,
            P(-hx, +hy, +hz), P(+hx, +hy, +hz), P(+hx, -hy, +hz), P(-hx, -hy, +hz), uv.Front, tw, th);   // 正面 +Z
        AddQuad(group, material,
            P(+hx, +hy, -hz), P(-hx, +hy, -hz), P(-hx, -hy, -hz), P(+hx, -hy, -hz), uv.Back, tw, th);    // 背面 -Z
        AddQuad(group, material,
            P(-hx, +hy, -hz), P(-hx, +hy, +hz), P(-hx, -hy, +hz), P(-hx, -hy, -hz), uv.Left, tw, th);    // 左面 -X
        AddQuad(group, material,
            P(+hx, +hy, +hz), P(+hx, +hy, -hz), P(+hx, -hy, -hz), P(+hx, -hy, +hz), uv.Right, tw, th);   // 右面 +X
        AddQuad(group, material,
            P(-hx, +hy, -hz), P(+hx, +hy, -hz), P(+hx, +hy, +hz), P(-hx, +hy, +hz), uv.Top, tw, th);      // 顶面 +Y
        AddQuad(group, material,
            P(-hx, -hy, +hz), P(+hx, -hy, +hz), P(+hx, -hy, -hz), P(-hx, -hy, -hz), uv.Bottom, tw, th);   // 底面 -Y
    }

    private static void AddQuad(Model3DGroup group, Material material,
        Point3D tl, Point3D tr, Point3D br, Point3D bl, Rect r, double tw, double th)
    {
        double u0 = r.X / tw, u1 = (r.X + r.W) / tw;
        double v0 = r.Y / th, v1 = (r.Y + r.H) / th;

        var mesh = new MeshGeometry3D();
        mesh.Positions.Add(tl);
        mesh.Positions.Add(tr);
        mesh.Positions.Add(br);
        mesh.Positions.Add(bl);
        mesh.TextureCoordinates.Add(new Point(u0, v0));
        mesh.TextureCoordinates.Add(new Point(u1, v0));
        mesh.TextureCoordinates.Add(new Point(u1, v1));
        mesh.TextureCoordinates.Add(new Point(u0, v1));
        mesh.TriangleIndices.Add(0);
        mesh.TriangleIndices.Add(1);
        mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(0);
        mesh.TriangleIndices.Add(2);
        mesh.TriangleIndices.Add(3);

        var model = new GeometryModel3D(mesh, material)
        {
            BackMaterial = material // 双面渲染，旋转时背面不缺失
        };
        group.Children.Add(model);
    }
}
