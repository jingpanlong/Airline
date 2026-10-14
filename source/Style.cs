using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Airline {
    internal static class Ui {
        public static readonly Brush Paper=B("#F7F5EF"), Ink=B("#303A35"), Muted=B("#7B837B"), Green=B("#557260"), Line=B("#D8DACE");
        public static Brush B(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
        public static TextBlock Text(string text,double size,Brush color) {
            return new TextBlock { Text=text, FontSize=size, Foreground=color, FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI"), VerticalAlignment=VerticalAlignment.Center };
        }
        public static Button Button(string text, Action action) {
            var b=new Button { Content=text, Foreground=Ink, Background=Brushes.Transparent, BorderThickness=new Thickness(0), Padding=new Thickness(13,7,13,7), Cursor=System.Windows.Input.Cursors.Hand, FontSize=12, FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI") };
            var factory=new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));
            factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));
            factory.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
            var content=new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center); factory.AppendChild(content);
            b.Template=new ControlTemplate(typeof(Button)) { VisualTree=factory };
            b.MouseEnter+=(s,e)=>b.Background=B("#E5E9DF"); b.MouseLeave+=(s,e)=>b.Background=Brushes.Transparent;
            b.Click+=(s,e)=>action(); return b;
        }
        internal static CheckBox Switch(string title,bool value) {
            var box=new CheckBox { Content=title,IsChecked=value,FontSize=14,Foreground=Ink,Margin=new Thickness(0,8,0,5),Cursor=System.Windows.Input.Cursors.Hand };
            var root=new FrameworkElementFactory(typeof(DockPanel));
            var track=new FrameworkElementFactory(typeof(Border),"Track"); track.SetValue(DockPanel.DockProperty,Dock.Right); track.SetValue(Border.WidthProperty,38.0); track.SetValue(Border.HeightProperty,22.0); track.SetValue(Border.CornerRadiusProperty,new CornerRadius(11)); track.SetValue(Border.BackgroundProperty,Line);
            var thumb=new FrameworkElementFactory(typeof(Border),"Thumb"); thumb.SetValue(Border.WidthProperty,16.0); thumb.SetValue(Border.HeightProperty,16.0); thumb.SetValue(Border.CornerRadiusProperty,new CornerRadius(8)); thumb.SetValue(Border.MarginProperty,new Thickness(3)); thumb.SetValue(Border.BackgroundProperty,Brushes.White); thumb.SetValue(Border.HorizontalAlignmentProperty,HorizontalAlignment.Left); track.AppendChild(thumb); root.AppendChild(track);
            var content=new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(ContentControl.ContentProperty)); content.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center); root.AppendChild(content);
            var template=new ControlTemplate(typeof(CheckBox)) { VisualTree=root };
            var on=new Trigger { Property=System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,Value=true }; on.Setters.Add(new Setter(Border.BackgroundProperty,Green,"Track")); on.Setters.Add(new Setter(Border.HorizontalAlignmentProperty,HorizontalAlignment.Right,"Thumb")); template.Triggers.Add(on);
            var disabled=new Trigger { Property=UIElement.IsEnabledProperty,Value=false }; disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.45)); template.Triggers.Add(disabled);
            box.Template=template; return box;
        }
    }
}
