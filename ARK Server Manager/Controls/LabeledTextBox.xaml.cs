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
    /// Interaction logic for LabeledTextBox.xaml
    /// </summary>
    public partial class LabeledTextBox : UserControl
    {
        #region Public Dependency Properties
        public static readonly DependencyProperty LabelTextProperty = DependencyProperty.Register(nameof(LabelText), typeof(String), typeof(LabeledTextBox), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty InputTextProperty = DependencyProperty.Register(nameof(InputText), typeof(String), typeof(LabeledTextBox), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty HasLockBoxProperty = DependencyProperty.Register(nameof(HasLockBox), typeof(Boolean), typeof(LabeledTextBox));
        public static readonly DependencyProperty HasCheckBoxProperty = DependencyProperty.Register(nameof(HasCheckBox), typeof(Boolean), typeof(LabeledTextBox));
        public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register(nameof(IsLocked), typeof(Boolean), typeof(LabeledTextBox), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty IsCheckboxCheckedProperty = DependencyProperty.Register(nameof(IsCheckboxChecked), typeof(Boolean), typeof(LabeledTextBox), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        
        [Category("Custom Properties")]
        public String LabelText
        {
            get { return (String)GetValue(LabelTextProperty); }
            set { SetValue(LabelTextProperty, value); }
        }

        [Category("Custom Properties")]
        public String InputText
        {
            get { return (String)GetValue(InputTextProperty); }
            set { SetValue(InputTextProperty, value); }
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
        public LabeledTextBox()
        {
            InitializeComponent();
            IsCheckboxChecked = true; // Default state is checked. This is overridden by XAML attribute if present
            (this.Content as FrameworkElement).DataContext = this;
        }


    }
}