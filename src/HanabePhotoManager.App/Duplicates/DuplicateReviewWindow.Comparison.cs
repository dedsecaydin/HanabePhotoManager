using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.App;

public partial class DuplicateReviewWindow
{
    private CancellationTokenSource? _comparisonCancellation;
    private VisualSimilarityEvidence? _evidence;
    private string? _comparedPath;

    private async Task ShowComparisonAsync(DuplicateCandidateGroup group, string path)
    {
        _comparisonCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _comparisonCancellation = cancellation;
        var reference = group.Paths[0];
        if (string.Equals(reference, path, StringComparison.OrdinalIgnoreCase)) path = group.Paths[1];
        _comparedPath = path;
        _evidence = null;
        LeftImageHost.Children.Clear();
        RightImageHost.Children.Clear();
        ComparisonTitle.Text = group.IsSuspected ? "视觉相似 · 需要人工复核" : "SHA-256 完全相同";
        EvidenceText.Text = "正在加载图片与比对依据…";
        LeftCaption.Text = reference;
        RightCaption.Text = path;
        try
        {
            var left = await Task.Run(() => LoadPreview(reference), cancellation.Token);
            var right = await Task.Run(() => LoadPreview(path), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            SetImage(LeftImageHost, left);
            SetImage(RightImageHost, right);
            LeftCaption.Text = $"{Path.GetFileName(reference)} · {left.PixelWidth}×{left.PixelHeight}（预览）\n{reference}";
            RightCaption.Text = $"{Path.GetFileName(path)} · {right.PixelWidth}×{right.PixelHeight}（预览）\n{path}";
            if (group.IsSuspected)
            {
                var evidence = await _scanner.CompareVisualAsync(reference, path, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                _evidence = evidence;
                EvidenceText.Text = $"亮暗布局一致率 {evidence.AgreementPercent:0.0}%：64 格中 {evidence.MatchingCells} 格一致，{evidence.DifferentCells} 格不同。不是同图概率。";
                DrawOverlays();
            }
            else
                EvidenceText.Text = "扫描时文件内容 SHA-256 一致；删除前仍会重新核对。视觉网格不用于精确重复判断。";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!cancellation.IsCancellationRequested)
                EvidenceText.Text = "无法生成图片对照（视频或不支持的格式可打开原文件）：" + ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_comparisonCancellation, cancellation)) _comparisonCancellation = null;
            cancellation.Dispose();
        }
    }

    private static BitmapImage LoadPreview(string path)
    {
        using var stream = File.OpenRead(path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 640;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static void SetImage(Grid host, BitmapImage bitmap)
    {
        host.Width = bitmap.PixelWidth;
        host.Height = bitmap.PixelHeight;
        host.Children.Add(new System.Windows.Controls.Image { Source = bitmap, Stretch = Stretch.Fill });
    }

    private void OverlayChanged(object sender, RoutedEventArgs e) => DrawOverlays();

    private void DrawOverlays()
    {
        if (LeftImageHost is null || RightImageHost is null) return;
        foreach (var host in new[] { LeftImageHost, RightImageHost })
        {
            while (host.Children.Count > 1) host.Children.RemoveAt(1);
            if (_evidence is null || ShowDifferenceGrid.IsChecked != true || host.Children.Count == 0) continue;
            var grid = new UniformGrid { Rows = 8, Columns = 8, IsHitTestVisible = false };
            for (var index = 0; index < 64; index++)
            {
                var different = (_evidence.DifferenceMask & (1UL << index)) != 0;
                var cell = new Border { BorderThickness = new Thickness(different ? 3 : 0) };
                cell.SetResourceReference(Border.BorderBrushProperty, "Brush.Primary");
                grid.Children.Add(cell);
            }
            host.Children.Add(grid);
        }
    }

    private void OpenCompared_Click(object sender, RoutedEventArgs e)
    {
        if (_comparedPath is null) return;
        try { Process.Start(new ProcessStartInfo(_comparedPath) { UseShellExecute = true }); }
        catch (Exception ex) { EvidenceText.Text = "无法打开文件：" + ex.Message; }
    }
}
