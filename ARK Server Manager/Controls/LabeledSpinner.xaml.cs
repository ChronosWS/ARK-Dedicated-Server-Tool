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
    /// Interaction logic for LabeledSpinner.xaml
    /// </summary>
    public partial class LabeledSpinner : UserControl
    {
        // Added Dependency Properties
        public static readonly DependencyProperty LabelTextProperty = DependencyProperty.Register(nameof(LabelText), typeof(String), typeof(LabeledSpinner), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty HasLockBoxProperty = DependencyProperty.Register(nameof(HasLockBox), typeof(Boolean), typeof(LabeledSpinner));
        public static readonly DependencyProperty HasCheckBoxProperty = DependencyProperty.Register(nameof(HasCheckBox), typeof(Boolean), typeof(LabeledSpinner));
        public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register(nameof(IsLocked), typeof(Boolean), typeof(LabeledSpinner), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty IsCheckboxCheckedProperty = DependencyProperty.Register(nameof(IsCheckboxChecked), typeof(Boolean), typeof(LabeledSpinner), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(int), typeof(LabeledSpinner), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(LabeledSpinner), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(LabeledSpinner), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty SmallChangeProperty = DependencyProperty.Register(nameof(SmallChange), typeof(int), typeof(LabeledSpinner), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty LargeChangeProperty = DependencyProperty.Register(nameof(LargeChange), typeof(int), typeof(LabeledSpinner), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });

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
        public Boolean HasLockBox
        {
            get { return (Boolean)GetValue(HasLockBoxProperty); }
            set { SetValue(HasLockBoxProperty, value); }

        }
        
        [Category("Custom Properties")]
        public Boolean HasCheckBox
        {
            get { return (Boolean)GetValue(HasCheckBoxProperty); }
            set { SetValue(HasCheckBoxProperty, value); }
        }

        [Category("Custom Properties")]
        public Boolean IsLocked
        {
            get { return (Boolean)GetValue(IsLockedProperty); }
            set { SetValue(IsLockedProperty, value); }

        }

        [Category("Custom Properties")]
        public Boolean IsCheckboxChecked
        {
            get { return (Boolean)GetValue(IsCheckboxCheckedProperty); }
            set { SetValue(IsCheckboxCheckedProperty, value); }
        }
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public LabeledSpinner()
        {
            InitializeComponent();
            IsCheckboxChecked = true; // Default state is checked. This is overridden by XAML attribute if present
            (this.Content as FrameworkElement).DataContext = this;
        }


    }
}