using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Samples.Controls;
using MGUI.Samples.Dialogs;
using MGUI.Samples.Dialogs.Debugging;
using MGUI.Samples.Dialogs.FF7;
using MGUI.Samples.Dialogs.Stardew_Valley;
using MGUI.Samples.Features;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Samples
{
    public class DataContextTest : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void NotifyPropertyChanged(string PropertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        /// <summary>Notify Property Changed for the given <paramref name="PropertyName"/></summary>
        public void NPC(string PropertyName) => NotifyPropertyChanged(PropertyName);

        private string _TestString;
        public string TestString
        {
            get => _TestString;
            set
            {
                if (_TestString != value)
                {
                    _TestString = value;
                    NPC(nameof(TestString));
                }
            }
        }

        public DataContextTest()
        {
            TestString = "Hello World";
        }
    }

    public class Compendium : SampleBase
    {
        #region Controls
        public ButtonSamples ButtonSamples { get; }
        public CheckBoxSamples CheckBoxSamples { get; }
        public ComboBoxSamples ComboBoxSamples { get; }
        public ContextMenuSamples ContextMenuSamples { get; }
        public MenuBarSamples MenuBarSamples { get; }
        public ContextualContentPresenterSamples ContextualContentPresenterSamples { get; }
        public DockPanelSamples DockPanelSamples { get; }
        public ExpanderSamples ExpanderSamples { get; }
        public GridSamples GridSamples { get; }
        public GridColorPickerSamples GridColorPickerSamples { get; }
        public GroupBoxSamples GroupBoxSamples { get; }
        public ImageSamples ImageSamples { get; }
        public ListBoxSamples ListBoxSamples { get; }
        public ListViewSamples ListViewSamples { get; }
        public OverlaySamples OverlaySamples { get; }
        public PasswordBoxSamples PasswordBoxSamples { get; }
        public ProgressBarSamples ProgressBarSamples { get; }
        public ProgressButtonSamples ProgressButtonSamples { get; }
        public RadioButtonSamples RadioButtonSamples { get; }
        public ScrollViewerSamples ScrollViewerSamples { get; }
        public SliderSamples SliderSamples { get; }
        public StackPanelSamples StackPanelSamples { get; }
        public TabControlSamples TabControlSamples { get; }
        public TextBlockSamples TextBlockSamples { get; }
        public TextBoxSamples TextBoxSamples { get; }
        public ToolTipSamples ToolTipSamples { get; }
        public TreeViewSamples TreeViewSamples { get; }
        public UniformGridSamples UniformGridSamples { get; }
        public WindowSamples WindowSamples { get; }
        #endregion Controls

        #region Features
        public StylesSamples StylesSamples { get; }
        public DataBindingSamples DataBindingSamples { get; }
        public UIScalingSamples UIScalingSamples { get; }
        public IFillBrushSamples IFillBrushSamples { get; }
        public IBorderBrushSamples IBorderBrushSamples { get; }
        public ShaderEffectsSamples ShaderEffectsSamples { get; }
        public RenderCachingSamples RenderCachingSamples { get; }
        #endregion Features

        #region Dialogs
        public FF7Inventory FF7Inventory { get; }
        public Registration Registration { get; }
        public SDVInventory SDVInventory { get; }
        public SampleHUD HUD { get; }
        public XAMLDesignerWindow XAMLDesignerWindow { get; }
        public Debug1 Debug1 { get; }
        public Debug2 Debug2 { get; }
        #endregion Dialogs

        public Compendium(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, null, $"{nameof(Compendium)}.xaml")
        {
            Window.IsCloseButtonVisible = false;

            #region Controls
            ButtonSamples = new(Content, Desktop);
            CheckBoxSamples = new(Content, Desktop);
            ComboBoxSamples = new(Content, Desktop);
            ContextMenuSamples = new(Content, Desktop);
            MenuBarSamples = new(Content, Desktop);
            ContextualContentPresenterSamples = new(Content, Desktop);
            DockPanelSamples = new(Content, Desktop);
            ExpanderSamples = new(Content, Desktop);
            GridSamples = new(Content, Desktop);
            GridColorPickerSamples = new(Content, Desktop);
            GroupBoxSamples = new(Content, Desktop);
            ImageSamples = new(Content, Desktop);
            ListBoxSamples = new(Content, Desktop);
            ListViewSamples = new(Content, Desktop);
            OverlaySamples = new(Content, Desktop);
            PasswordBoxSamples = new(Content, Desktop);
            ProgressBarSamples = new(Content, Desktop);
            ProgressButtonSamples = new(Content, Desktop);
            RadioButtonSamples = new(Content, Desktop);
            ScrollViewerSamples = new(Content, Desktop);
            SliderSamples = new(Content, Desktop);
            StackPanelSamples = new(Content, Desktop);
            TabControlSamples = new(Content, Desktop);
            TextBlockSamples = new(Content, Desktop);
            TextBoxSamples = new(Content, Desktop);
            ToolTipSamples = new(Content, Desktop);
            TreeViewSamples = new(Content, Desktop);
            UniformGridSamples = new(Content, Desktop);
            WindowSamples = new(Content, Desktop);
            #endregion Controls

            #region Features
            StylesSamples = new(Content, Desktop);
            DataBindingSamples = new(Content, Desktop);
            UIScalingSamples = new(Content, Desktop);
            IFillBrushSamples = new(Content, Desktop);
            IBorderBrushSamples = new(Content, Desktop, this);
            ShaderEffectsSamples = new(Content, Desktop);
            RenderCachingSamples = new(Content, Desktop);
            #endregion Features

            #region Dialogs
            FF7Inventory = new(Content, Desktop) { IsVisible = true };
            SDVInventory = new(Content, Desktop);
            Registration = new(Content, Desktop);
            HUD = new(Content, Desktop);
            XAMLDesignerWindow = new(Content, Desktop);
            Debug1 = new(Content, Desktop);
            Debug2 = new(Content, Desktop);
            #endregion Dialogs

#if DEBUG
            //HUD.Show();
#endif

            Window.WindowDataContext = this;
        }
    }
}
