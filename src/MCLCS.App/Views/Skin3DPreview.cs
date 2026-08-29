using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MCLCS.App.Views;

/// <summary>
/// 皮肤 3D 预览（WPF Viewport3D）：6 部位人体模型 + 皮肤纹理 + 鼠标旋转。
/// Viewport3D 构建 MeshGeometry3D 时用三角面片定义立方体并映射 64x64 纹理 UV。
/// </summary>
public class Skin3DPreview : UserControl
{
    private readonly Viewport3D _viewport;
    private readonly PerspectiveCamera _camera;
    private readonly AxisAngleRotation3D _rotY, _rotX;
    private readonly Model3DGroup _modelGroup = new();
    private readonly ImageBrush _skinBrush;
    private Point _lastMouse;
    private bool _dragging;

    public static readonly DependencyProperty SkinBitmapProperty =
        DependencyProperty.Register(nameof(SkinBitmap), typeof(ImageSource), typeof(Skin3DPreview),
            new PropertyMetadata(null, OnSkinChanged));

    public ImageSource? SkinBitmap
    {
        get => (ImageSource?)GetValue(SkinBitmapProperty);
        set => SetValue(SkinBitmapProperty, value);
    }

    public Skin3DPreview()
    {
        // 纹理 UV 用 0–1 相对坐标（ToUv 已除以 64），故用默认 RelativeToBoundingBox，
        // 不能再设 Absolute（否则 UV 像素坐标 0–64 落在默认 1×1 Viewport 之外，纹理错位/不显示）。
        _skinBrush = new ImageBrush();

        _rotY = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 30);
        _rotX = new AxisAngleRotation3D(new Vector3D(1, 0, 0), -15);
        var rotGroup = new Transform3DGroup();
        rotGroup.Children.Add(new RotateTransform3D(_rotY));
        rotGroup.Children.Add(new RotateTransform3D(_rotX));

        var modelVisual = new ModelVisual3D();
        modelVisual.Content = new DirectionalLight(
            Colors.White, new Vector3D(0.5, -1, -0.7));
        var ambient = new ModelVisual3D();
        ambient.Content = new AmbientLight(Color.FromRgb(0x60, 0x60, 0x70));

        _viewport = new Viewport3D { Width = 200, Height = 360, ClipToBounds = true };
        _camera = new PerspectiveCamera(
            new Point3D(0, 0.3, 5), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);
        _viewport.Camera = _camera;

        _viewport.Children.Add(modelVisual);
        _viewport.Children.Add(ambient);

        var root3D = new ModelVisual3D();
        root3D.Transform = rotGroup;
        root3D.Content = _modelGroup;
        _viewport.Children.Add(root3D);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x40)),
            Child = _viewport
        };

        _viewport.MouseLeftButtonDown += (_, e) => { _dragging = true; _lastMouse = e.GetPosition(_viewport); CaptureMouse(); };
        _viewport.MouseMove += OnMouseMove;
        _viewport.MouseLeftButtonUp += (_, _) => { _dragging = false; ReleaseMouseCapture(); };
        _viewport.MouseWheel += (_, e) =>
        {
            _camera.Position = new Point3D(_camera.Position.X, _camera.Position.Y, _camera.Position.Z - e.Delta * 0.005);
        };

        BuildModel();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var pos = e.GetPosition(_viewport);
        var dx = pos.X - _lastMouse.X;
        var dy = pos.Y - _lastMouse.Y;
        _rotY.Angle = (_rotY.Angle + dx * 0.5) % 360;
        _rotX.Angle = Math.Clamp(_rotX.Angle - dy * 0.5, -80, 80);
        _lastMouse = pos;
    }

    private static void OnSkinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Skin3DPreview preview && e.NewValue is ImageSource)
        {
            preview._skinBrush.ImageSource = (ImageSource)e.NewValue;
        }
    }

    private void BuildModel()
    {
        // 头部 (8x8x8 px in texture → 1.0 unit)
        AddCuboid(0, 1.45, 0, 1.0, 1.0, 1.0,
            (8, 8, 8, 16),  // front
            (24,8, 32,16),  // back
            (0, 8, 8, 16),  // right
            (16,8, 24,16),  // left
            (8, 0, 16, 8),  // top
            (16,0, 24, 8)); // bottom

        // 身体
        AddCuboid(0, -0.15, 0, 1.0, 1.5, 0.5,
            (20,16,28,28), (32,16,40,28),
            (16,16,20,28), (28,16,32,28),
            (20,28,28,32), (28,28,36,32));

        // 右臂
        AddCuboid(-0.75, -0.15, 0, 0.5, 1.5, 0.5,
            (44,16,48,28), (52,16,56,28),
            (40,16,44,28), (48,16,52,28),
            (44,28,48,32), (48,28,52,32));

        // 左臂
        AddCuboid(0.75, -0.15, 0, 0.5, 1.5, 0.5,
            (36,48,40,60), (44,48,48,60),
            (40,48,44,60), (32,48,36,60),
            (36,60,40,64), (40,60,44,64));

        // 右腿
        AddCuboid(-0.25, -1.65, 0, 0.5, 1.5, 0.5,
            (4, 16,8, 28), (12,16,16,28),
            (0, 16,4, 28), (8, 16,12,28),
            (4, 28,8, 32), (8, 28,12,32));

        // 左腿
        AddCuboid(0.25, -1.65, 0, 0.5, 1.5, 0.5,
            (20,48,24,60), (28,48,32,60),
            (16,48,20,60), (24,48,28,60),
            (20,60,24,64), (24,60,28,64));
    }

    private void AddCuboid(double cx, double cy, double cz,
        double w, double h, double d,
        (int x1, int y1, int x2, int y2) front,
        (int x1, int y1, int x2, int y2) back,
        (int x1, int y1, int x2, int y2) right,
        (int x1, int y1, int x2, int y2) left,
        (int x1, int y1, int x2, int y2) top,
        (int x1, int y1, int x2, int y2) bottom)
    {
        var hw = w / 2; var hh = h / 2; var hd = d / 2;

        // 8 corners
        var p000 = new Point3D(cx - hw, cy - hh, cz + hd);
        var p100 = new Point3D(cx + hw, cy - hh, cz + hd);
        var p010 = new Point3D(cx - hw, cy + hh, cz + hd);
        var p110 = new Point3D(cx + hw, cy + hh, cz + hd);
        var p001 = new Point3D(cx - hw, cy - hh, cz - hd);
        var p101 = new Point3D(cx + hw, cy - hh, cz - hd);
        var p011 = new Point3D(cx - hw, cy + hh, cz - hd);
        var p111 = new Point3D(cx + hw, cy + hh, cz - hd);

        // Add each face
        AddQuad(p000, p100, p110, p010, ToUv(front.x1, front.y1, front.x2, front.y2));    // front
        AddQuad(p001, p101, p111, p011, ToUv(back.x1,  back.y1,  back.x2,  back.y2));      // back
        AddQuad(p100, p101, p111, p110, ToUv(right.x1, right.y1, right.x2, right.y2));     // right
        AddQuad(p000, p001, p011, p010, ToUv(left.x1,  left.y1,  left.x2,  left.y2));      // left
        AddQuad(p010, p110, p111, p011, ToUv(top.x1,   top.y1,   top.x2,   top.y2));       // top
        AddQuad(p000, p100, p101, p001, ToUv(bottom.x1,bottom.y1,bottom.x2,bottom.y2));    // bottom
    }

    private static Point[] ToUv(int x1, int y1, int x2, int y2)
    {
        // UV 转 0–1 相对坐标（纹理为 64×64，ImageBrush 用默认 RelativeToBoundingBox）。
        // Quad vertex order: tl, tr, br, bl
        const double s = 1.0 / 64.0;
        return new[]
        {
            new Point(x1 * s, y1 * s),   // tl → top-left
            new Point(x2 * s, y1 * s),   // tr → top-right
            new Point(x2 * s, y2 * s),   // br → bottom-right
            new Point(x1 * s, y2 * s)    // bl → bottom-left
        };
    }

    private void AddQuad(Point3D tl, Point3D tr, Point3D br, Point3D bl, Point[] uv)
    {
        var mesh = new MeshGeometry3D();
        mesh.Positions = new Point3DCollection { tl, tr, br, bl };
        mesh.TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 };
        mesh.TextureCoordinates = new PointCollection { uv[0], uv[1], uv[2], uv[3] };

        var material = new DiffuseMaterial(_skinBrush);
        var model = new GeometryModel3D(mesh, material);
        model.BackMaterial = material; // 双面渲染
        _modelGroup.Children.Add(model);
    }
}
