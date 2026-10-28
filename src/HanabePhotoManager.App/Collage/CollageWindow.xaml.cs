using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HanabePhotoManager.App.Compression;
using HanabePhotoManager.Core.Collage;
using Microsoft.Win32;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
namespace HanabePhotoManager.App.Collage;
public partial class CollageWindow : Window
{
    private readonly CollageViewModel model = new();
    private CancellationTokenSource? cancellation;
    private Point dragStart;
    private int previewVersion;
    private CancellationTokenSource previewCancellation=new();
    public CollageWindow()
    {
        InitializeComponent(); DataContext=model;
        var p=model.LoadPreferences(); WidthBox.Text=p.Width.ToString();HeightBox.Text=p.Height.ToString();LayoutBox.SelectedIndex=Math.Clamp(p.Layout,0,3);MarginBox.Text=p.Margin.ToString();GapBox.Text=p.Gap.ToString();BackgroundBox.Text=p.Background;CropBox.IsChecked=p.Crop;FaceBox.IsChecked=p.FaceHints;FormatBox.SelectedIndex=Math.Clamp(p.Format,0,2);
        Closed+=(_,_)=>{cancellation?.Cancel();previewCancellation.Cancel();previewVersion++;};
    }
    public CollageWindow(IEnumerable<string> paths) : this() { foreach(var path in paths.Distinct())model.Photos.Add(path); }
    private void Add(object sender,RoutedEventArgs e)
    {
        if(cancellation is not null)return;
        var dialog=new Microsoft.Win32.OpenFileDialog {Multiselect=true,Filter="图片|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.webp"};
        if(dialog.ShowDialog(this)==true)foreach(var path in dialog.FileNames)if(!model.Photos.Contains(path))model.Photos.Add(path);
        Invalidate();
    }
    private void Invalidate(){model.Candidates=[];previewCancellation.Cancel();previewVersion++;Preview.Children.Clear();}
    private void SettingsChanged(object sender,RoutedEventArgs e){if(!IsLoaded)return;Invalidate();model.Status="参数已更改，请重新生成三套候选。";}
    private void AspectChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!IsLoaded || !int.TryParse(WidthBox.Text,out int width))return;
        double? ratio=AspectBox.SelectedIndex switch{1=>1.5,2=>4d/3,3=>1,4=>9d/16,_=>null};
        if(ratio.HasValue)HeightBox.Text=Math.Max(64,(int)Math.Round(width/ratio.Value)).ToString();
    }
    private void NameSort(object sender,RoutedEventArgs e){model.Sort(false);Invalidate();}
    private async void TimeSort(object sender,RoutedEventArgs e)
    {
        if(cancellation is not null)return;
        ControlsPanel.IsEnabled=false; cancellation=new();
        try { await model.SortByCaptureTimeAsync(cancellation.Token);Invalidate(); }
        catch(Exception ex){model.Status=ex.Message;}
        finally{cancellation?.Dispose();cancellation=null;ControlsPanel.IsEnabled=true;}
    }
    private void Up(object sender,RoutedEventArgs e){model.Move(PhotoList.SelectedIndex,PhotoList.SelectedIndex-1);Invalidate();}
    private void Down(object sender,RoutedEventArgs e){model.Move(PhotoList.SelectedIndex,PhotoList.SelectedIndex+1);Invalidate();}
    private void Swap(object sender,RoutedEventArgs e){var selected=PhotoList.SelectedItems.OfType<string>().ToArray();if(selected.Length!=2){model.Status="按 Ctrl 选择两张照片后交换。";return;}model.Swap(model.Photos.IndexOf(selected[0]),model.Photos.IndexOf(selected[1]));Invalidate();}
    private void Hero(object sender,RoutedEventArgs e){model.SetManualHero(PhotoList.SelectedItem as string);model.Status="主图："+Path.GetFileName(model.Hero);Invalidate();}
    private void ClearHero(object sender,RoutedEventArgs e){model.SetManualHero(null);Invalidate();}
    private async void SuggestHero(object sender,RoutedEventArgs e)
    {
        if(cancellation is not null)return;
        try{cancellation=new();ControlsPanel.IsEnabled=false;PhotoList.IsEnabled=false;await model.SuggestHeroAsync(cancellation.Token);Invalidate();}
        catch(Exception ex){model.Status=ex is OperationCanceledException?"已取消":ex.Message;}
        finally{cancellation?.Dispose();cancellation=null;ControlsPanel.IsEnabled=true;PhotoList.IsEnabled=true;}
    }
    private void Remove(object sender,RoutedEventArgs e){if(PhotoList.SelectedItem is string path&&!model.OrderLocked)model.Photos.Remove(path);Invalidate();}
    private void DragStart(object sender,MouseButtonEventArgs e)=>dragStart=e.GetPosition(PhotoList);
    private void DragMove(object sender,MouseEventArgs e)
    {
        if(cancellation is null&&!model.OrderLocked&&e.LeftButton==MouseButtonState.Pressed&&PhotoList.SelectedItem is string path&&(e.GetPosition(PhotoList)-dragStart).Length>8)DragDrop.DoDragDrop(PhotoList,path,DragDropEffects.Move);
    }
    private void PhotoDrop(object sender,DragEventArgs e)
    {
        if(cancellation is null&&e.Data.GetData(typeof(string))is string path&&ItemsControl.ContainerFromElement(PhotoList,e.OriginalSource as DependencyObject)is ListBoxItem item&&item.DataContext is string target){model.Move(model.Photos.IndexOf(path),model.Photos.IndexOf(target));Invalidate();}
    }
    private CollagePreferences Preferences()=>new(int.Parse(WidthBox.Text),int.Parse(HeightBox.Text),LayoutBox.SelectedIndex,int.Parse(MarginBox.Text),int.Parse(GapBox.Text),BackgroundBox.Text,CropBox.IsChecked==true,FaceBox.IsChecked==true,FormatBox.SelectedIndex);
    private async void Generate(object sender,RoutedEventArgs e)
    {
        if(cancellation is not null)return;
        try
        {
            var p=Preferences(); _=SixLabors.ImageSharp.Color.ParseHex(p.Background.TrimStart('#'));
            if(p.FaceHints&&!p.Crop){model.Status="人脸建议仅用于裁切，请先启用居中裁切。";return;}
            cancellation=new();ControlsPanel.IsEnabled=false;PhotoList.IsEnabled=false;
            await model.GenerateAsync(new(p.Width,p.Height,(CollageLayout)p.Layout,p.Margin,p.Gap),p.FaceHints,cancellation.Token);
            model.SavePreferences(p);Render();
        }
        catch(Exception ex){model.Status=ex is OperationCanceledException?"已取消":ex.Message;}
        finally{cancellation?.Dispose();cancellation=null;ControlsPanel.IsEnabled=true;PhotoList.IsEnabled=true;}
    }
    private void CandidateChanged(object sender,SelectionChangedEventArgs e){if(IsLoaded)Render();}
    private async void Render()
    {
        if(model.Candidates.Count==0||CandidateBox.SelectedIndex<0)return;
        previewCancellation.Cancel();previewCancellation.Dispose();previewCancellation=new();var previewToken=previewCancellation.Token;
        int version=++previewVersion;var plan=model.Candidates[CandidateBox.SelectedIndex];bool crop=CropBox.IsChecked==true;
        Preview.Children.Clear();Preview.Width=plan.Width;Preview.Height=plan.Height;
        try
        {
            Preview.Background=(Brush)new BrushConverter().ConvertFromString(BackgroundBox.Text)!;
            foreach(var slot in plan.Slots)
            {
                double factor=240d/Math.Max(slot.Width,slot.Height);int width=Math.Max(1,(int)(slot.Width*factor)),height=Math.Max(1,(int)(slot.Height*factor));
                var bitmap=await Task.Run(async ()=>
                {
                    using var gate=await CollageWorkGate.EnterAsync(previewToken);
                    previewToken.ThrowIfCancellationRequested();
                    await ImageCollageService.ProbeAsync([slot.Id],previewToken);
                    using var source=SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(new SixLabors.ImageSharp.Formats.DecoderOptions {MaxFrames=1,TargetSize=new SixLabors.ImageSharp.Size(480,480)},slot.Id);
                    using var image=CollageColorConverter.ConvertToSrgb(source,Path.Combine(HanabePhotoManager.App.Services.AppDataPaths.ProcessingRoot,"collage-preview"));
                    CollageImagePlacement.Resize(image,width,height,crop,slot);
                    using var bgra=image.CloneAs<SixLabors.ImageSharp.PixelFormats.Bgra32>();var bytes=new byte[bgra.Width*bgra.Height*4];bgra.CopyPixelDataTo(bytes);
                    var result=BitmapSource.Create(bgra.Width,bgra.Height,96,96,System.Windows.Media.PixelFormats.Bgra32,null,bytes,bgra.Width*4);result.Freeze();return result;
                });
                if(version!=previewVersion)return;
                var control=new System.Windows.Controls.Image{Source=bitmap,Width=slot.Width,Height=slot.Height,Stretch=Stretch.Uniform};Canvas.SetLeft(control,slot.X);Canvas.SetTop(control,slot.Y);Preview.Children.Add(control);
            }
        }
        catch(OperationCanceledException) {}
        catch(Exception ex){if(version==previewVersion)model.Status="预览失败："+ex.Message;}
    }
    private async void Export(object sender,RoutedEventArgs e)
    {
        if(cancellation is not null)return;
        if(model.Candidates.Count==0){model.Status="请先生成排版。";return;}
        var dialog=new OpenFolderDialog{Title="选择拼图导出文件夹",InitialDirectory=@"D:\HanabePhoto"};if(dialog.ShowDialog(this)!=true)return;
        try
        {
            cancellation=new();ControlsPanel.IsEnabled=false;PhotoList.IsEnabled=false;CandidateBox.IsEnabled=false;
            await model.ExportAsync(CandidateBox.SelectedIndex,new(dialog.FolderName,(CollageFileFormat)FormatBox.SelectedIndex,BackgroundBox.Text,CropBox.IsChecked==true),cancellation.Token);
        }
        catch(Exception ex){model.Status=ex is OperationCanceledException?"已取消，未发布输出。":ex.Message;}
        finally{cancellation?.Dispose();cancellation=null;ControlsPanel.IsEnabled=true;PhotoList.IsEnabled=true;CandidateBox.IsEnabled=true;}
    }
    private void Cancel(object sender,RoutedEventArgs e){cancellation?.Cancel();previewCancellation.Cancel();}
}







