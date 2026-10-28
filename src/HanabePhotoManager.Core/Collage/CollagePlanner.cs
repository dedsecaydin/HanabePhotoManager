namespace HanabePhotoManager.Core.Collage;

public enum CollageLayout { Grid, Adaptive, Magazine, Long }
public sealed record CollagePhoto(string Id, int Width, int Height);
public sealed record CollageSettings(int Width = 3600, int Height = 2400, CollageLayout Layout = CollageLayout.Grid, int Margin = 48, int Gap = 24, string? HeroId = null, int Generation = 0);
public sealed record CollageSlot(string Id, int X, int Y, int Width, int Height, double FocusX = 0.5, double FocusY = 0.5);
public sealed record CollagePlan(int Width, int Height, IReadOnlyList<CollageSlot> Slots);

/// <summary>Pure geometry; input order is never randomized or changed by candidate generation.</summary>
public static class CollagePlanner
{
    public const long MaximumPixels = 60_000_000;
    public static bool FitsMemoryBudget(long canvasPixels,long largestSourcePixels,bool blurred=false)
        =>canvasPixels>0&&canvasPixels<=MaximumPixels&&largestSourcePixels>0&&largestSourcePixels<=MaximumPixels
          &&(blurred?20L:12L)*canvasPixels+32L*largestSourcePixels<=768L*1024*1024;
    public static IReadOnlyList<CollagePlan> Candidates(IReadOnlyList<CollagePhoto> photos, CollageSettings settings)
        => Enumerable.Range(0, 3).Select(i => Create(photos, settings, i)).ToArray();

    private static CollagePlan Create(IReadOnlyList<CollagePhoto> photos, CollageSettings s, int candidate)
    {
        if (photos.Count == 0 || photos.Count > 200 || s.Width < 64 || s.Height < 64 || (long)s.Width * s.Height > MaximumPixels || s.Margin < 0 || s.Gap < 0 || s.Generation < 0 || s.Margin > s.Width / 2 || s.Margin > s.Height / 2 || s.Gap > Math.Min(s.Width, s.Height))
            throw new ArgumentException("画布应为 64 像素以上、最多 6000 万像素，照片 1–200 张。");
        if (photos.Any(p => p.Width <= 0 || p.Height <= 0) || photos.Select(p => p.Id).Distinct().Count() != photos.Count)
            throw new ArgumentException("照片尺寸或标识无效。");
        s = s with { Margin = s.Margin + candidate * 4 + s.Generation % 17 * 3 };
        int width = s.Width - 2 * s.Margin, height = s.Height - 2 * s.Margin;
        var slots = new List<CollageSlot>();
        var variant = (candidate + s.Generation) % 3;
        if (s.Layout == CollageLayout.Magazine && photos.Count > 1)
        {
            // Reading order stays row-major even when the hero is not the first input.
            int rows = (photos.Count + 1) / 2;
            int baseHeight = (height - s.Gap * (rows - 1)) / (rows + 1);
            int heroRow = Math.Max(0, Array.FindIndex(photos.ToArray(), p => p.Id == (s.HeroId ?? photos[0].Id))) / 2;
            int currentY = s.Margin;
            for (int row = 0; row < rows; row++)
            {
                int cellHeight = baseHeight * (row == heroRow ? 2 : 1);
                var left = photos[row * 2];
                if (row * 2 + 1 >= photos.Count) { slots.Add(new(left.Id, s.Margin, currentY, width, cellHeight)); currentY += cellHeight + s.Gap; continue; }
                var right = photos[row * 2 + 1];
                double fraction = left.Id == (s.HeroId ?? photos[0].Id) ? .68 : right.Id == s.HeroId ? .32 : .5;
                int leftWidth = (int)((width - s.Gap) * fraction);
                slots.Add(new(left.Id, s.Margin, currentY, leftWidth, cellHeight));
                slots.Add(new(right.Id, s.Margin + leftWidth + s.Gap, currentY, width - leftWidth - s.Gap, cellHeight));
                currentY += cellHeight + s.Gap;
            }
        }        else
        {
            int columns = s.Layout == CollageLayout.Long ? 1 : Math.Clamp((int)Math.Ceiling(Math.Sqrt(photos.Count * (double)width / height)) + variant - 1, 1, photos.Count);
            AddRows(photos, s.Margin, s.Margin, width, height, columns, s.Gap, slots, s.Layout == CollageLayout.Adaptive);
        }
        if (slots.Any(slot => slot.Width <= 0 || slot.Height <= 0 || slot.X < 0 || slot.Y < 0 || (long)slot.X + slot.Width > s.Width || (long)slot.Y + slot.Height > s.Height)) throw new ArgumentException("边距或间隔过大，请增加画布或减少照片。");
        return new(s.Width, s.Height, slots);
    }
    private static void AddRows(IReadOnlyList<CollagePhoto> photos, int x, int y, int width, int height, int columns, int gap, List<CollageSlot> slots, bool adaptive)
    {
        int rows = (photos.Count + columns - 1) / columns;
        int rowHeight = (height - gap * (rows - 1)) / rows;
        for (int row = 0; row < rows; row++)
        {
            var members = photos.Skip(row * columns).Take(columns).ToArray();
            double total = members.Sum(p => adaptive ? (double)p.Width / p.Height : 1);
            int available = width - gap * (members.Length - 1), offset = 0;
            for (int col = 0; col < members.Length; col++)
            {
                var p = members[col];
                int cellWidth = col == members.Length - 1 ? width - offset : (int)(available * (adaptive ? (double)p.Width / p.Height : 1) / total);
                slots.Add(new(p.Id, x + offset, y + row * (rowHeight + gap), cellWidth, rowHeight));
                offset += cellWidth + gap;
            }
        }
    }
}





