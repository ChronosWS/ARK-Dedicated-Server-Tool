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

namespace ARK_Server_Manager.Controls
{
    /// <summary>
    /// Interaction logic for LabeledTextBlock.xaml
    /// </summary>
    public partial class LabeledTextBlock : UserControl
    {



        // Added Dependency Properties
        public static readonly DependencyProperty LabelTextProperty = DependencyProperty.Register(nameof(LabelText), typeof(String), typeof(LabeledTextBlock), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });
        public static readonly DependencyProperty BodyTextProperty = DependencyProperty.Register(nameof(BodyText), typeof(String), typeof(LabeledTextBlock), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true });

        #region Public Dependency Properties
        [Category("Custom Properties")]
        public String LabelText
        {
            get { return (String)GetValue(LabelTextProperty); }
            set { SetValue(LabelTextProperty, value); }
        }

        [Category("Custom Properties")]
        public String BodyText
        {
            get { return (String)GetValue(BodyTextProperty); }
            set { SetValue(BodyTextProperty, value); }
        }
        #endregion



        public LabeledTextBlock()
        {
            InitializeComponent();
            (this.Content as FrameworkElement).DataContext = this;
        }
    }
}
