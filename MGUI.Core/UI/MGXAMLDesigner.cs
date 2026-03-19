using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Text;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using System.Diagnostics;
using System.Threading;

#if OS_WINDOWS
using Microsoft.Win32;
#endif

namespace MGUI.Core.UI
{
    /// <summary>A simple XAML designer that allows you to parse and render XAML markup at runtime.</summary>
    public class MGXAMLDesigner : MGElement
    {
        private MGComponent<MGDockPanel> MainContent { get; }
        public MGButton RefreshButton { get; }
        public MGContentPresenter MarkupPresenter { get; }
        public MGTabControl TabControlComponent { get; }

        /// <summary>True if the input xaml is being read from the file at <see cref="FromFilePath"/>.<br/>
        /// False if it's being read from the <see cref="FromStringTextBoxComponent"/>'s Text.</summary>
        public bool IsReadingInputFromFile => TabControlComponent.SelectedTabIndex == 0 && !string.IsNullOrEmpty(FromFilePath) && File.Exists(FromFilePath);

        public MGTextBox FromStringTextBoxComponent { get; }
        public MGTextBox FromFileTextBoxComponent { get; }

        public MGCheckBox FromFileAutoRefreshCheckBox { get; }
        public bool IsAutoRefreshing => FromFileAutoRefreshCheckBox.IsChecked == true;

        /// <summary>Optional. The default DataContext to apply to the Content immediately after it's been parsed</summary>
        public object ParsedContentDataContext { get; set; }

        public MGXAMLDesigner(MGWindow ParentWindow)
            : base(ParentWindow, MGElementType.XAMLDesigner)
        {
            using (BeginInitializing())
            {
                //  Detect when the selected file is saved, and auto-refresh the parsed content if necessary
                FileWatcher = new();
                FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                FileWatcher.Changed += (sender, e) =>
                {
                    if (IsAutoRefreshing && IsReadingInputFromFile && e.FullPath.Equals(FromFilePath, StringComparison.InvariantCultureIgnoreCase))
                    {
                        RefreshParsedContent();
                    }
                };
                FileWatcher.Filter = "*.xaml";

                //  Create the textbox that the xaml string is read from
                MGScrollViewer MarkupScrollViewer = new(ParentWindow);
                FromStringTextBoxComponent = new(ParentWindow, null);
                string SampleXAML = 
@"<Button Background=""RoyalBlue|DarkBlue"" Padding=""10,8"" VerticalAlignment=""Center"">
    <TextBlock IsBold=""true"" FontSize=""14"" TextAlignment=""Center"" Text=""Hello\nWorld"" />
</Button>";
                FromStringTextBoxComponent.SetText(SampleXAML);
                MarkupScrollViewer.SetContent(FromStringTextBoxComponent);
                MarkupScrollViewer.CanChangeContent = false;

                //  Create the refresh button
                RefreshButton = new(ParentWindow);
                RefreshButton.HorizontalAlignment = HorizontalAlignment.Center;
                RefreshButton.Padding = new(10, 5);
                RefreshButton.Margin = new(0, 0, 15, 0);
                RefreshButton.SetContent("[b]Refresh[/b]");
                RefreshButton.CanChangeContent = false;
                RefreshButton.AddCommandHandler((btn, e) => { RefreshParsedContent(); });

                //  Create the 'From File' browse button and textbox
                MGButton FilePathBrowseButton = new(ParentWindow, btn =>
                {
                    string InitialDirectory;
                    if (!string.IsNullOrEmpty(FromFilePath))
                        InitialDirectory = Path.GetDirectoryName(FromFilePath);
                    else
                    {
                        string AssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
#if DEBUG
                        InitialDirectory = Path.GetFullPath(Path.Combine(AssemblyDirectory, "..", "..", ".."));
#else
                        InitialDirectory = AssemblyDirectory;
#endif
                    }
                    if (TryBrowseFilePath(InitialDirectory, out string SelectedFilePath))
                        FromFileTextBoxComponent.SetText(SelectedFilePath);
                });
                if (FilePathBrowseButton.BackgroundBrush.NormalValue != null && FilePathBrowseButton.BackgroundBrush.NormalValue.TryDarken(0.25f, out IFillBrush Darkened))
                    FilePathBrowseButton.BackgroundBrush.NormalValue = Darkened;
                FilePathBrowseButton.SetContent("Browse");
                FromFileTextBoxComponent = new(ParentWindow, null);
                FromFileTextBoxComponent.WrapText = false;
                FromFileTextBoxComponent.IsReadonly = true;
                FromFileTextBoxComponent.TextChanged += (sender, e) => { FromFilePath = e.NewValue; };

                //  Create the Auto-refresh checkbox
                FromFileAutoRefreshCheckBox = new(ParentWindow, true);
                FromFileAutoRefreshCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
                FromFileAutoRefreshCheckBox.SetContent("Auto-refresh parsed content");
                MGToolTip AutoRefreshToolTip = new MGToolTip(ParentWindow, FromFileAutoRefreshCheckBox, 0, 0);
                AutoRefreshToolTip.DefaultTextForeground.SetAll(Color.White);
                AutoRefreshToolTip.SetContent("If checked, the parsed content will automatically refresh whenever the selected file is saved.");
                AutoRefreshToolTip.ApplySizeToContent(SizeToContent.WidthAndHeight, 50, 50, 350, null, false);
                FromFileAutoRefreshCheckBox.ToolTip = AutoRefreshToolTip;

                //  Populate the 'From File' tab content
                MGDockPanel FilePathDockPanel = new(ParentWindow);
                FilePathDockPanel.TryAddChild(new MGTextBlock(ParentWindow, "File Path:") { IsBold = true, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 5, 0) }, Dock.Left);
                FilePathDockPanel.TryAddChild(FilePathBrowseButton, Dock.Right);
                FilePathDockPanel.TryAddChild(FromFileTextBoxComponent, Dock.Right);
                FilePathDockPanel.CanChangeContent = false;
                MGStackPanel FilePathStackPanel = new(ParentWindow, Orientation.Vertical) { Spacing = 5 };
                FilePathStackPanel.TryAddChild(FilePathDockPanel);
                FilePathStackPanel.TryAddChild(FromFileAutoRefreshCheckBox);
                FilePathStackPanel.CanChangeContent = false;

                //  Create the tab control
                TabControlComponent = new(ParentWindow);
                TabControlComponent.Padding = new(8);
                TabControlComponent.AddTab("From File", FilePathStackPanel);
                TabControlComponent.AddTab("From String", MarkupScrollViewer);
                TabControlComponent.SelectedTabChanged += (sender, e) => { RefreshParsedContent(); };

                MarkupPresenter = new(ParentWindow);
                MarkupPresenter.BackgroundBrush.NormalValue = new MGBorderedFillBrush(new(2), MGUniformBorderBrush.Black, Color.Black.AsFillBrush() * 0.75f, true);

                MGHeaderedContentPresenter Tmp = new(ParentWindow, RefreshButton, TabControlComponent) { HeaderPosition = Dock.Bottom, Spacing = 2 };
                Tmp.CanChangeContent = false;

                MGGrid MainGrid = new(ParentWindow);
                MainGrid.AddRows(ConstrainedGridLength.ParseMultiple("*[150,],15,200[140,]"));
                MainGrid.AddColumn(GridLength.CreateWeightedLength(1.0));
                MainGrid.TryAddChild(0, 0, MarkupPresenter);
                MainGrid.TryAddChild(2, 0, Tmp);
                MainGrid.TryAddChild(1, 0, new MGGridSplitter(ParentWindow) { Margin = new(0, 2) });
                MainGrid.CanChangeContent = false;

                MGDockPanel DockPanel = new(ParentWindow);
                DockPanel.TryAddChild(MainGrid, Dock.Right);
                DockPanel.CanChangeContent = false;
                DockPanel.SetParent(this);
                MainContent = new(DockPanel, false, false, true, true, false, false, true,
                    (AvailableBounds, ComponentSize) => AvailableBounds);
                AddComponent(MainContent);

                SelfOrParentWindow.OnWindowPositionChanged += (sender, e) =>
                {
                    if (MarkupPresenter.Content is MGWindow XAMLWindow)
                    {
                        Rectangle PreviousLayoutBounds = LayoutBounds;
                        Point Offset = new(e.NewValue.Left - e.PreviousValue.Left, e.NewValue.Top - e.PreviousValue.Top);
                        XAMLWindow.TopLeft += Offset;
                    }
                };

                RefreshParsedContent();
            }
        }

        private FileSystemWatcher FileWatcher { get; }

        private string _FromFilePath;
        public string FromFilePath
        {
            get => _FromFilePath;
            set
            {
                if (_FromFilePath != value)
                {
                    _FromFilePath = value;
                    FileWatcher.EnableRaisingEvents = false;
                    FileWatcher.Path = string.IsNullOrEmpty(FromFilePath) ? null : Path.GetDirectoryName(FromFilePath);
                    if (!string.IsNullOrEmpty(FileWatcher.Path) && Directory.Exists(Path.GetDirectoryName(FileWatcher.Path)))
                        FileWatcher.EnableRaisingEvents = true;
                    RefreshParsedContent();
                    NPC(nameof(FromFilePath));
                }
            }
        }

        private static bool TryBrowseFilePath(string InitialDirectory, out string FilePath)
        {
#if OS_WINDOWS
            string Browse()
            {
                OpenFileDialog FileBrowser = new();
                FileBrowser.Filter = "Xaml Files|*.xaml";
                if (!string.IsNullOrEmpty(InitialDirectory) && Directory.Exists(InitialDirectory))
                    FileBrowser.InitialDirectory = InitialDirectory;

                if (FileBrowser.ShowDialog() == true)
                    return FileBrowser.FileName;
                else
                    return null;
            }

            //  Microsoft.Win32.OpenFileDialog.ShowDialog() requires STA apartment state
            ApartmentState State = Thread.CurrentThread.GetApartmentState();
            FilePath = State == ApartmentState.STA ? Browse() : StartSTATask(Browse).Result;
            return !string.IsNullOrEmpty(FilePath);
#elif OS_LINUX
            return TryBrowseFilePathLinux(InitialDirectory, out FilePath);
#elif OS_MAC
            return TryBrowseFilePathMacOS(out FilePath);
#else
            //TODO: Implement this
            Debug.WriteLine($"This feature is not implemented on non-Windows platforms: {nameof(MGXAMLDesigner)}.{nameof(TryBrowseFilePath)}");
            FilePath = null;
            return false;
#endif
        }

#if OS_LINUX
        private static bool TryBrowseFilePathLinux(string InitialDirectory, out string FilePath)
        {
            if (TryRunFilePickerProcess("zenity", StartInfo =>
            {
                StartInfo.ArgumentList.Add("--file-selection");
                StartInfo.ArgumentList.Add("--title=Select XAML File");
                StartInfo.ArgumentList.Add("--file-filter=*.xaml");

                if (!string.IsNullOrEmpty(InitialDirectory) && Directory.Exists(InitialDirectory))
                {
                    StartInfo.ArgumentList.Add($"--filename={EnsureTrailingDirectorySeparator(InitialDirectory)}");
                }
            }, out FilePath))
            {
                return true;
            }

            if (TryRunFilePickerProcess("kdialog", StartInfo =>
            {
                StartInfo.ArgumentList.Add("--getopenfilename");
                StartInfo.ArgumentList.Add(!string.IsNullOrEmpty(InitialDirectory) && Directory.Exists(InitialDirectory) ? InitialDirectory : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                StartInfo.ArgumentList.Add("*.xaml|XAML Files");
            }, out FilePath))
            {
                return true;
            }

            Debug.WriteLine("Unable to open a Linux file picker for MGXAMLDesigner. Install zenity or kdialog to enable Browse.");
            FilePath = null;
            return false;
        }
#endif

#if OS_MAC
        private static bool TryBrowseFilePathMacOS(out string FilePath)
        {
            string Script = @"set selectedFile to choose file with prompt ""Select XAML file""
POSIX path of selectedFile";
            if (TryRunFilePickerProcess("/usr/bin/osascript", StartInfo =>
            {
                StartInfo.ArgumentList.Add("-e");
                StartInfo.ArgumentList.Add(Script);
            }, out FilePath))
            {
                return true;
            }

            Debug.WriteLine("Unable to open the macOS file picker for MGXAMLDesigner.");
            FilePath = null;
            return false;
        }
#endif

        private static bool TryRunFilePickerProcess(string FileName, Action<ProcessStartInfo> ConfigureStartInfo, out string FilePath)
        {
            try
            {
                ProcessStartInfo StartInfo = new()
                {
                    FileName = FileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                ConfigureStartInfo(StartInfo);

                using Process Process = Process.Start(StartInfo);
                if (Process == null)
                {
                    FilePath = null;
                    return false;
                }

                string StandardOutput = Process.StandardOutput.ReadToEnd().Trim();
                string StandardError = Process.StandardError.ReadToEnd().Trim();
                Process.WaitForExit();

                if (Process.ExitCode == 0 && !string.IsNullOrWhiteSpace(StandardOutput))
                {
                    FilePath = StandardOutput;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(StandardError))
                {
                    Debug.WriteLine($"{nameof(MGXAMLDesigner)} file picker '{FileName}' failed: {StandardError}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(MGXAMLDesigner)} file picker '{FileName}' failed: {ex}");
            }

            FilePath = null;
            return false;
        }

        private static string EnsureTrailingDirectorySeparator(string DirectoryPath)
        {
            if (string.IsNullOrEmpty(DirectoryPath))
            {
                return DirectoryPath;
            }

            if (Path.EndsInDirectorySeparator(DirectoryPath))
            {
                return DirectoryPath;
            }

            return DirectoryPath + Path.DirectorySeparatorChar;
        }

        //Taken from: https://stackoverflow.com/a/16722767/11689514
#if OS_WINDOWS
        private static Task<T> StartSTATask<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            Thread thread = new(() =>
            {
                try { tcs.SetResult(func()); }
                catch (Exception e) { tcs.SetException(e); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }
#endif

        private void RefreshParsedContent()
        {
            MGElement Result;
            try
            {
                string Markup;
                if (IsReadingInputFromFile)
                    Markup = File.ReadAllText(FromFilePath);
                else
                    Markup = FromStringTextBoxComponent.Text;
                Result = XAMLParser.Load<MGElement>(SelfOrParentWindow, Markup, !IsReadingInputFromFile, true);
                Result.DataContextOverride = ParsedContentDataContext;
            }
            catch (Exception ex)
            {
                MGScrollViewer SV = new(SelfOrParentWindow);
                SV.Padding = new(5);
                string Message = FTTokenizer.EscapeMarkdown($"{ex.Message}\n\n{ex}");
                SV.SetContent(Message);
                SV.CanChangeContent = false;
                Result = SV;
            }

            using (MarkupPresenter.AllowChangingContentTemporarily())
            {
                MarkupPresenter.Content?.RemoveDataBindings(true);
                MarkupPresenter.SetContent(Result);
            }
        }
    }
}
