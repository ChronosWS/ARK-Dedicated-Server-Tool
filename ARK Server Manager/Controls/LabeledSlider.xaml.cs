using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ARK_Server_Manager
{
    /// <summary>
    /// Interaction logic for LabeledSlider.xaml
    /// </summary>
    public partial class LabeledSlider : UserControl
    {
        // Added Dependency Properties
        public static readonly DependencyProperty LabelTextProperty = DependencyProperty.Register(nameof(LabelText), typeof(String), typeof(LabeledSlider), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty HasCheckBoxProperty = DependencyProperty.Register(nameof(HasCheckBox), typeof(Boolean), typeof(LabeledSlider));
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(Boolean), typeof(LabeledSlider), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(int), typeof(LabeledSlider), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(LabeledSlider), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(LabeledSlider), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty SmallChangeProperty = DependencyProperty.Register(nameof(SmallChange), typeof(int), typeof(LabeledSlider), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty LargeChangeProperty = DependencyProperty.Register(nameof(LargeChange), typeof(int), typeof(LabeledSlider), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });

        #region Public Dependency Properties
        [Category("Custom Properties")]
        public int SmallChange
        {
            get { return (int)GetValue(SmallChangeProperty); }
            set { SetValue(SmallChangeProperty, value); }
        }

        [Category("Custom Properties")]
        public int LargeChange
        {
            get { return (int)GetValue(LargeChangeProperty); }
            set { SetValue(LargeChangeProperty, value); }
        }

        [Category("Custom Properties")]
        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        [Category("Custom Properties")]
        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        [Category("Custom Properties")]
        public int Minimum
        {
            get { return (int)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        [Category("Custom Properties")]
        public String LabelText
        {
            get { return (String)GetValue(LabelTextProperty); }
            set { SetValue(LabelTextProperty, value); }
        }


        [Category("Custom Properties")]
        public Boolean HasCheckBox
        {
            get { return (Boolean)GetValue(HasCheckBoxProperty); }
            set { SetValue(HasCheckBoxProperty, value); }
        }

        [Category("Custom Properties")]
        public Boolean IsChecked
        {
            get { return (Boolean)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public LabeledSlider()
        {
            InitializeComponent();
            IsChecked = true; // Default state is checked. This is overridden by XAML attribute if present
            (this.Content as FrameworkElement).DataContext = this;
        }


    }
}