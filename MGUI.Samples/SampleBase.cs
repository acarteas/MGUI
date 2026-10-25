#nullable disable

using System;
using System.Diagnostics;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples
{
    public abstract class SampleBase : ViewModelBase
    {
        public ContentManager Content { get; }
        public MGDesktop Desktop { get; }
        public MGResources Resources { get; }
        public MGWindow Window { get; }

        private bool _IsVisible;
        public bool IsVisible
        {
            get => _IsVisible;
            set
            {
                if (_IsVisible != value)
                {
                    _IsVisible = value;
                    NPC(nameof(IsVisible));

                    if (IsVisible)
                        Desktop.Windows.Add(Window);
                    else
                        Desktop.Windows.Remove(Window);
                    VisibilityChanged?.Invoke(this, IsVisible);
                }
            }
        }

        public event EventHandler<bool> VisibilityChanged;

        public void Show() => IsVisible = true;
        public void Hide() => IsVisible = false;

        /// <param name="Initialize">Optional. This delegate is invoked before the XAML content is parsed,
        /// so you may wish to use this delegate to add resources to <see cref="MGDesktop.Resources"/> that may be required in order to parse the XAML.</param>
        protected SampleBase(ContentManager Content, MGDesktop Desktop, string ProjectFolderName, string XAMLFilename, Action Initialize = null)
        {
            this.Content = Content;
            this.Desktop = Desktop;
            Resources = Desktop.Resources;
            string ResourceName = $"{nameof(MGUI)}.{nameof(Samples)}.{(ProjectFolderName == null ? "" : ProjectFolderName + ".")}{XAMLFilename}";
            string XAML = GeneralUtils.ReadEmbeddedResourceAsString(Assembly.GetExecutingAssembly(), ResourceName);
            Initialize?.Invoke();
            try
            {
                Window = XAMLParser.LoadRootWindow(Desktop, XAML, false, true);
                Window.WindowClosed += (sender, e) => IsVisible = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing XAML content for {ResourceName}: {ex}");
#if DEBUG
                throw;
#endif
            }
        }

        protected static void OpenURL(string URL)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = URL,
                UseShellExecute = true
            });
        }
    }

}
