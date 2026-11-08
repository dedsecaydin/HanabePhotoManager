using System.Numerics;
using System.IO;

namespace HanabePhotoManager.App.Services;

internal readonly record struct FaceAlignmentTransform(double A, double B, double OffsetX, double OffsetY);

internal static class FaceAlignmentGeometry
{
    private static readonly Vector2[] Template =
    [
        new(38.2946f, 51.6963f), new(73.5318f, 51.5014f), new(56.0252f, 71.7366f),
        new(41.5493f, 92.3655f), new(70.7299f, 92.2041f)
    ];

    internal static FaceAlignmentTransform Estimate(IReadOnlyList<Vector2> landmarks)
    {
        if (landmarks.Count != 5) throw new InvalidDataException("检测器必须输出五个人脸关键点。");
        double sourceX = 0, sourceY = 0, targetX = 0, targetY = 0;
        for (var index = 0; index < 5; index++)
        {
            sourceX += landmarks[index].X; sourceY += landmarks[index].Y;
            targetX += Template[index].X; targetY += Template[index].Y;
        }
        sourceX /= 5; sourceY /= 5; targetX /= 5; targetY /= 5;
        double denominator = 0, a = 0, b = 0;
        for (var index = 0; index < 5; index++)
        {
            var sx = landmarks[index].X - sourceX;
            var sy = landmarks[index].Y - sourceY;
            var tx = Template[index].X - targetX;
            var ty = Template[index].Y - targetY;
            denominator += sx * sx + sy * sy;
            a += sx * tx + sy * ty;
            b += sx * ty - sy * tx;
        }
        if (denominator <= 1e-8) throw new InvalidDataException("人脸关键点无法对齐。");
        a /= denominator; b /= denominator;
        return new(a, b, targetX - a * sourceX + b * sourceY, targetY - b * sourceX - a * sourceY);
    }
}
