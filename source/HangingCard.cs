using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Airline {
    internal sealed class HangingCard : Grid {
        readonly Border paper;
        readonly RotateTransform rotation;
        readonly ScaleTransform scale;
        readonly TranslateTransform offset;
        readonly DropShadowEffect shadow;
        readonly TextBlock badge;
        readonly Func<bool> animate;
        readonly double angle,period;
        internal bool Selected { get; private set; }
        internal bool SwayActive { get; private set; }
        internal HangingCard(Shot shot,int index,Func<bool> shouldAnimate) {
            Name="HangingScreenshot"; Width=170; Height=190; Cursor=System.Windows.Input.Cursors.Hand; animate=shouldAnimate;
            angle=new double[]{-3,2,-1.5,3,-2,1}[index%6]; period=2.7+(index%6)*.23;
            rotation=new RotateTransform(angle); scale=new ScaleTransform(1,1); offset=new TranslateTransform();
            var transforms=new TransformGroup(); transforms.Children.Add(rotation); transforms.Children.Add(scale); transforms.Children.Add(offset);
            shadow=new DropShadowEffect { BlurRadius=12,ShadowDepth=4,Opacity=.12,Color=Colors.Black };
            paper=new Border { Name="ShotPaper",Background=Brushes.White,BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(2),Padding=new Thickness(5,6,5,5),Margin=new Thickness(0,9,0,0),VerticalAlignment=VerticalAlignment.Top,RenderTransform=transforms,RenderTransformOrigin=new Point(.5,0),Effect=shadow };
            var content=new StackPanel();
            try {
                var image=Library.Load(shot.Path,420); content.Children.Add(new Image { Source=image,Height=118,Stretch=Stretch.Uniform });
                string dimensions; using(var stream=File.OpenRead(shot.Path)) { var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None); dimensions=decoder.Frames[0].PixelWidth+" × "+decoder.Frames[0].PixelHeight; }
                var caption=Ui.Text(shot.Created.ToString("HH:mm")+"   ·   "+dimensions,10,Ui.Muted); caption.Margin=new Thickness(3,10,0,0); content.Children.Add(caption);
            } catch { content.Children.Add(Ui.Text("无法读取这张图",12,Ui.Muted)); }
            paper.Child=content; Children.Add(paper);
            var peg=new Border { Width=8,Height=22,Background=Ui.B("#B5BFA5"),BorderBrush=Ui.B("#96A48A"),BorderThickness=new Thickness(.5),CornerRadius=new CornerRadius(2),VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Center,RenderTransform=new RotateTransform(-5),IsHitTestVisible=false };
            peg.Child=new Border { Width=1,Height=13,Background=Ui.B("#909D82") }; Children.Add(peg);
            badge=Ui.Text("已选中 · 单击复制",10,Ui.Green); badge.HorizontalAlignment=HorizontalAlignment.Center; badge.VerticalAlignment=VerticalAlignment.Bottom; badge.Opacity=0; badge.IsHitTestVisible=false; Children.Add(badge);
            Loaded+=(s,e)=>StartSway(); Unloaded+=(s,e)=>StopSway();
            IsVisibleChanged+=(s,e)=> { if(IsVisible) StartSway(); else StopSway(); };
            MouseEnter+=(s,e)=>SetSelected(true); MouseLeave+=(s,e)=>SetSelected(false);
        }
        void StopSway() { SwayActive=false; rotation.BeginAnimation(RotateTransform.AngleProperty,null); offset.BeginAnimation(TranslateTransform.XProperty,null); offset.BeginAnimation(TranslateTransform.YProperty,null); }
        void StartSway() {
            if(Selected || !IsVisible || !animate()) return;
            SwayActive=true;
            var swing=new DoubleAnimation(angle-1.6,angle+1.6,TimeSpan.FromSeconds(period)) { AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase { EasingMode=EasingMode.EaseInOut } };
            rotation.BeginAnimation(RotateTransform.AngleProperty,swing);
            offset.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(-1,1,TimeSpan.FromSeconds(period+.4)) { AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase { EasingMode=EasingMode.EaseInOut } });
        }
        internal void SetSelected(bool selected) {
            Selected=selected; StopSway(); Panel.SetZIndex(this,selected?10:0);
            paper.BorderBrush=selected?Ui.Green:Brushes.Transparent; shadow.Color=selected?Color.FromRgb(85,114,96):Colors.Black; shadow.Opacity=selected?.26:.12; shadow.BlurRadius=selected?18:12;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(selected?1.05:1,TimeSpan.FromMilliseconds(140)));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(selected?1.05:1,TimeSpan.FromMilliseconds(140)));
            badge.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(selected?1:0,TimeSpan.FromMilliseconds(140)));
            offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(selected?-3:0,TimeSpan.FromMilliseconds(140)));
            if(selected) rotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(140)));
            else { rotation.Angle=angle; StartSway(); }
        }
    }
}
