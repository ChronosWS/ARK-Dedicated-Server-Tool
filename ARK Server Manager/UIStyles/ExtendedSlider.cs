using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ARK_Server_Manager
{
	public class ExtendedSlider : Slider
	{
		// Added Dependency Properties
        public static readonly DependencyProperty LeftSideBrushProperty = DependencyProperty.Register("LeftSideBrush", typeof(Brush), typeof(ExtendedSlider));
		public Brush LeftSideBrush
        {
            get { return (Brush)GetValue(LeftSideBrushProperty); }
            set { SetValue(LeftSideBrushProperty, value); }
        }
		
		public static readonly DependencyProperty GlowBrushProperty = DependencyProperty.Register("GlowBrush", typeof(Brush), typeof(ExtendedSlider));
		public Brush GlowBrush
        {
            get { return (Brush)GetValue(GlowBrushProperty); }
            set { SetValue(GlowBrushProperty, value); }
        }
		
        public static readonly DependencyProperty RightSideBrushProperty = DependencyProperty.Register("RightSideBrush", typeof(Brush), typeof(ExtendedSlider));
		public Brush RightSideBrush
        {
            get { return (Brush)GetValue(RightSideBrushProperty); }
            set { SetValue(RightSideBrushProperty, value); }
        }
		
		
        public static readonly DependencyProperty ThumbBrushProperty = DependencyProperty.Register("ThumbBrush", typeof(Brush), typeof(ExtendedSlider));
		public Brush ThumbBrush
        {
            get { return (Brush)GetValue(ThumbBrushProperty); }
            set { SetValue(ThumbBrushProperty, value); }
        }
		
        public static readonly DependencyProperty ThumbBorderBrushProperty = DependencyProperty.Register("ThumbBorderBrush", typeof(Brush), typeof(ExtendedSlider));
		public Brush ThumbBorderBrush
        {
            get { return (Brush)GetValue(ThumbBorderBrushProperty); }
            set { SetValue(ThumbBorderBrushProperty, value); }
        }
		
		static ExtendedSlider()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(ExtendedSlider), new FrameworkPropertyMetadata(typeof(ExtendedSlider)));
		}
	}
}