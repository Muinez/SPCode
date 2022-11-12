﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DiscordRPC;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using SPCode.Interop;
using SPCode.Interop.Updater;
using SPCode.UI.Components;
using SPCode.Utils;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using static SPCode.Interop.TranslationProvider;
using Button = DiscordRPC.Button;

namespace SPCode.UI
{
    public partial class MainWindow
    {
        #region Variables
        private readonly SolidColorBrush BlendOverEffectColorBrush;
        private readonly Storyboard BlendOverEffect;
        private readonly Storyboard DimmMainWindowEffect;
        private readonly Storyboard RestoreMainWindowEffect;
        private readonly Storyboard DisableServerAnim;
        public readonly List<EditorElement> EditorReferences = new();
        public readonly List<DASMElement> DASMReferences = new();
        private readonly Storyboard EnableServerAnim;
        public readonly List<MenuItem> MenuItems;

        private EditorElement EditorToFocus;
        private readonly DispatcherTimer SelectDocumentTimer;

        private bool ClosingBuffer;
        private readonly bool FullyInitialized;

        private ObservableCollection<string> ActionButtonDict = new()
        {
            Translate("Copy"),
            Translate("UploadFTP"),
            Translate("StartServer")
        };

        private ObservableCollection<string> CompileButtonDict = new()
        {
            Translate("CompileAll"),
            Translate("CompileCurrent")
        };

        public MetroDialogSettings ClosingDialogOptions = new()
        {
            AffirmativeButtonText = Translate("Yes"),
            NegativeButtonText = Translate("No"),
            FirstAuxiliaryButtonText = Translate("Cancel"),
            AnimateHide = false,
            AnimateShow = false,
            DefaultButtonFocus = MessageDialogResult.Affirmative
        };
        #endregion

        #region Constructors
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(SplashScreen sc)
        {
            InitializeComponent();
            this.ApplyTheme();

            // Set title
            Title = NamesHelper.ProgramPublicName;

            // Timer to select the newly opened editor 200ms after it has been opened
            SelectDocumentTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(200),
            };

            SelectDocumentTimer.Tick += (s, e) =>
            {
                SelectDocumentTimer.Stop();
                EditorToFocus.editor.Focus();
            };

            // Restore sizes of panels and separators
            ObjectBrowserColumn.Width = new GridLength(Program.OptionsObject.Program_ObjectbrowserWidth, GridUnitType.Pixel);
            var heightDescriptor = DependencyPropertyDescriptor.FromProperty(ColumnDefinition.WidthProperty, typeof(ItemsControl));
            heightDescriptor.AddValueChanged(EditorObjectBrowserGrid.ColumnDefinitions[1], EditorObjectBrowserGridRow_WidthChanged);

            // Fill the configs menu and some toolbar combobox items
            FillConfigMenu();
            CompileButton.ItemsSource = CompileButtonDict;
            CompileButton.SelectedIndex = 0;
            CActionButton.ItemsSource = ActionButtonDict;
            CActionButton.SelectedIndex = 0;

            // Enable/disable toolbar on startup
            if (Program.OptionsObject.UI_ShowToolBar)
            {
                Win_ToolBar.Height = double.NaN;
            }

            // Fill OB scripting directories combobox from the bottom
            OBDirList.ItemsSource = Program.Configs[Program.SelectedConfig].SMDirectories;
            OBDirList.SelectedIndex = 0;

            // Set some visual effects
            BlendOverEffectColorBrush = (SolidColorBrush)FindResource("MahApps.Brushes.Accent4");
            MetroDialogOptions.AnimateHide = MetroDialogOptions.AnimateShow = false;
            BlendOverEffect = (Storyboard)Resources["BlendOverEffect"];
            BlendOverEffect.Completed += delegate
            {
                BlendEffectPlane.Fill = BlendOverEffectColorBrush;
            };
            EnableServerAnim = (Storyboard)Resources["EnableServerAnim"];
            DisableServerAnim = (Storyboard)Resources["DisableServerAnim"];
            DimmMainWindowEffect = (Storyboard)Resources["DimmMainWindow"];
            RestoreMainWindowEffect = (Storyboard)Resources["RestoreMainWindow"];
            RestoreMainWindowEffect.Completed += delegate
            {
                BlendEffectPlane.Fill = BlendOverEffectColorBrush;
            };

            // Start OB
            ChangeObjectBrowserToDirectory(Program.OptionsObject.Program_ObjectBrowserDirectory);

            // Translate
            Language_Translate();

            // Load previously opened files
            if (Program.OptionsObject.LastOpenFiles != null)
            {
                foreach (var file in Program.OptionsObject.LastOpenFiles)
                {
                    TryLoadSourceFile(file, out _, false);
                }
            }

            // Take startup commands in consideration
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; ++i)
            {
                if (!args[i].EndsWith("exe"))
                {
                    TryLoadSourceFile(args[i], out _, false, true, i == 0);
                }
                if (args[i].ToLowerInvariant() == "--updateok")
                {
                    this.ShowMessageAsync("Update completed", "SPCode has been updated successfully.");
                }
                if (args[i].ToLowerInvariant() == "--updatefail")
                {
                    this.ShowMessageAsync("Update failed", "SPCode could not be updated properly.");
                }
            }

            // Close SplashScreen
            sc.Close(TimeSpan.FromMilliseconds(500.0));

            // Enclose menuitems in an accesible list to set their InputGestureTexts easier
            MenuItems = new()
            {
                MenuI_File,
                MenuI_Edit,
                MenuI_Build,
                MenuI_Tools,
                MenuI_Folding,
                MenuI_SPAPI,
                MenuI_Reformatter
            };

            LoadInputGestureTexts();

            // Load the commands dictionary in memory
            LoadCommandsDictionary();

            // Load the recent files list
            LoadRecentsList();

            // Disable the Reopen last closed tab button on startup for obvious reasons
            MenuI_ReopenLastClosedTab.IsEnabled = false;

            // Updates the status of the File tab of the OB
            UpdateOBFileButton();

            // Sets up the OB search cooldown timer
            SearchCooldownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            SearchCooldownTimer.Tick += OnSearchCooldownTimerTick;

            // Passes the Logging Box to the LoggingControl class
            LoggingControl.LogBox = LogTextbox;

            // Set error status buttons state
            Status_ErrorButton.IsChecked = true;
            Status_WarningButton.IsChecked = true;

            // Evaluate RTL
            EvaluateRTL();

            FullyInitialized = true;
        }
        #endregion

        #region Events
        private void DockingManager_ActiveContentChanged(object sender, EventArgs e)
        {
            if (OBTabFile.IsSelected && !SearchMode)
            {
                ListViewOBItem_SelectFile(OBTabFile, null);
                OBTabFile.IsSelected = true;
            }

            UpdateWindowTitle();
            UpdateOBFileButton();
        }

        private void DockingManager_DocumentClosed(object sender, DocumentClosedEventArgs e)
        {
            (e.Document.Content as EditorElement)?.Close();
            (e.Document.Content as DASMElement)?.Close();
            UpdateWindowTitle();
            UpdateOBFileButton();
        }

        private void DockingPaneGroup_ChildrenTreeChanged(object sender, ChildrenTreeChangedEventArgs e)
        {
            // Luqs: Code taken from VisualPawn Editor (Not published yet)

            // if the active LayoutDocumentPane gets closed 
            // 1. it will not be in the LayoutDocumentPaneGroup.
            // 2. editor that get added to it will not be shown in the client.
            // Solution: Set the active LayoutDocumentPane to the first LayoutDocumentPaneGroup avilable child.

            if (e.Change == ChildrenTreeChange.DirectChildrenChanged
                && !DockingPaneGroup.Children.Contains(DockingPane)
                && DockingPaneGroup.Children.Count > 0
                && DockingPaneGroup.Children[0] is LayoutDocumentPane pane)
            {
                DockingPane = pane;
            }
        }

        private async void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!ClosingBuffer)
            {
                // Close directly if no files need to be saved

                if (!EditorReferences.Any() || !EditorReferences.Any(x => x.NeedsSave) || Program.OptionsObject.ActionOnClose != ActionOnClose.Prompt)
                {
                    ClosingBuffer = true;
                    CloseProgram(true);
                }
                else
                {
                    // Cancel closing to handle it manually
                    e.Cancel = true;

                    // Build list of unsaved files to show
                    var sb = new StringBuilder();

                    foreach (var editor in EditorReferences.Where(x => x.NeedsSave))
                    {
                        sb.AppendLine($"  - {editor.Parent.Title.Substring(1)}");
                    }

                    var result = await this.ShowMessageAsync("Save all files?", $"Unsaved files:\n{sb}",
                        MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary, ClosingDialogOptions);

                    switch (result)
                    {
                        case MessageDialogResult.Affirmative:
                            ClosingBuffer = true;
                            CloseProgram(true);
                            Close();
                            break;

                        case MessageDialogResult.Negative:
                            ClosingBuffer = true;
                            CloseProgram(false);
                            Close();
                            break;

                        case MessageDialogResult.FirstAuxiliary:
                            return;
                    }
                }
            }
        }

        private void MetroWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                Activate();
                Focus();
                Debug.Assert(files != null, nameof(files) + " != null");
                for (var i = 0; i < files.Length; ++i)
                {
                    TryLoadSourceFile(files[i], out _, i == 0, true, i == 0);
                }
            }
        }

        private void EditorObjectBrowserGridRow_WidthChanged(object sender, EventArgs e)
        {
            if (FullyInitialized)
            {
                Program.OptionsObject.Program_ObjectbrowserWidth = ObjectBrowserColumn.Width.Value;
            }

        }
        #endregion

        #region Methods
        /// <summary>
        /// Loads a file into the editor.
        /// </summary>
        /// <param name="filePath">The path of the file to load</param>
        /// <param name="UseBlendoverEffect">Whether to execute the blendover effect</param>
        /// <param name="outEditor">The editor that has been loaded</param>
        /// <param name="TryOpenIncludes">Whether to open the includes associated with that file</param>
        /// <param name="SelectMe">Whether to focus the editor element once the file gets opened</param>
        /// <returns>If the file opening was successful or not</returns>
        public bool TryLoadSourceFile(string filePath, out EditorElement outEditor, bool UseBlendoverEffect = true, bool TryOpenIncludes = true, bool SelectMe = false)
        {
            outEditor = null;
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists)
            {
                return false;
            }

            if (DirHelper.HasValidTextExtension(fileInfo))
            {
                var finalPath = fileInfo.FullName;
                if (!DirHelper.CanAccess(finalPath))
                {
                    return false;
                }

                if (EditorReferences.Any())
                {
                    foreach (var editor in EditorReferences)
                    {
                        if (editor.FullFilePath == finalPath)
                        {
                            if (SelectMe)
                            {
                                editor.Parent.IsSelected = true;
                                editor.editor.TextArea.Caret.Show();
                                EditorToFocus = editor;
                                SelectDocumentTimer.Start();
                            }

                            outEditor = editor;
                            return true;
                        }
                    }
                }

                AddEditorElement(fileInfo, fileInfo.Name, SelectMe, out outEditor);
                if (TryOpenIncludes && Program.OptionsObject.Program_OpenCustomIncludes)
                {
                    using var textReader = fileInfo.OpenText();
                    var source = Regex.Replace(textReader.ReadToEnd(), @"/\*.*?\*/", string.Empty,
                        RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.Singleline);
                    var regex = new Regex(@"^\s*\#include\s+((\<|"")(?<name>.+?)(\>|""))",
                        RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.Multiline);
                    var mc = regex.Matches(source);
                    for (var i = 0; i < mc.Count; ++i)
                    {
                        try
                        {
                            var fileName = mc[i].Groups["name"].Value;
                            if (!(fileName.EndsWith(".inc", StringComparison.InvariantCultureIgnoreCase) ||
                                  fileName.EndsWith(".sp", StringComparison.InvariantCultureIgnoreCase)))
                            {
                                fileName += ".inc";
                            }

                            fileName = Path.Combine(
                                fileInfo.DirectoryName ?? throw new NullReferenceException(), fileName);
                            TryLoadSourceFile(fileName, out _, false,
                                Program.OptionsObject.Program_OpenIncludesRecursively);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                }
            }
            else if (DirHelper.IsBinary(fileInfo))
            {
                if (DASMReferences.Any())
                {
                    foreach (var dasmviewer in DASMReferences)
                    {
                        if (dasmviewer.FilePath == fileInfo.FullName)
                        {
                            DockingManager.ActiveContent = dasmviewer;
                            return true;
                        }
                    }
                }
                AddDASMElement(fileInfo);
            }
            else
            {
                return false;
            }

            if (UseBlendoverEffect)
            {
                BlendOverEffect.Begin();
            }

            return true;
        }

        /// <summary>
        /// Adds a new editor element associated with the file to the Docking Manager.
        /// </summary>
        /// <param name="filePath">The path of the file</param>
        /// <param name="editorTitle">The title of the tab</param>
        /// <param name="SelectMe">Whether to focus this editor element once created.</param>
        private void AddEditorElement(FileInfo fInfo, string editorTitle, bool SelectMe, out EditorElement editor)
        {
            var layoutDocument = new LayoutDocument
            {
                Title = editorTitle,
                ToolTip = fInfo.FullName
            };
            
            editor = new EditorElement(fInfo.FullName) { Parent = layoutDocument };
            layoutDocument.Content = editor;
            EditorReferences.Add(editor);
            DockingPane.Children.Add(layoutDocument);
            if (FullyInitialized)
            {
                AddNewRecentFile(fInfo);
            }
            if (SelectMe)
            {
                layoutDocument.IsSelected = true;
                editor.editor.TextArea.Caret.Show();
                EditorToFocus = editor;
                SelectDocumentTimer.Start();
            }
            layoutDocument.Closing += editor.Editor_TabClosed;
        }

        /// <summary>
        /// Adds a new DASM element associated with the file to the Docking Manager.
        /// </summary>
        private void AddDASMElement(FileInfo fileInfo)
        {
            var layoutDocument = new LayoutDocument { Title = "DASM: " + fileInfo.Name };
            var dasmElement = new DASMElement(fileInfo) { Parent = layoutDocument };
            DASMReferences.Add(dasmElement);
            layoutDocument.Content = dasmElement;
            DockingPane.Children.Add(layoutDocument);
            DockingPane.SelectedContentIndex = DockingPane.ChildrenCount - 1;
            if (FullyInitialized)
            {
                AddNewRecentFile(fileInfo);
            }
        }

        /// <summary>
        /// Performs a visual refresh on the editor.
        /// </summary>
        public static void ProcessUITasks()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(
                delegate
                {
                    frame.Continue = false;
                    return null;
                }), null);
            Dispatcher.PushFrame(frame);
        }

        /// <summary>
        /// Updates the editor's title and the Discord RPC status with the currently opened file.
        /// </summary>
        public void UpdateWindowTitle()
        {
            var ee = GetCurrentEditorElement();
            var de = GetCurrentDASMElement();
            var someEditorIsOpen = ee != null || de != null;
            var outString = "Idle";
            if (someEditorIsOpen)
            {
                outString = ee?.FullFilePath ?? de.FilePath;
            }

            outString += $" - {NamesHelper.ProgramPublicName}";

            if (Program.DiscordClient.IsInitialized)
            {
                var action = ee == null ? "Viewing" : "Editing";
                Program.DiscordClient.SetPresence(new RichPresence
                {
                    Timestamps = Program.OptionsObject.Program_DiscordPresenceTime ? Program.DiscordTime : null,
                    State = Program.OptionsObject.Program_DiscordPresenceFile ? someEditorIsOpen ? $"{action} {Path.GetFileName(ee?.FullFilePath ?? de.FilePath)}" : "Idle" : null,
                    Assets = new Assets
                    {
                        LargeImageKey = "immagine",
                    },
                    Buttons = new Button[]
                    {
                        new Button() { Label = Constants.GetSPCodeText, Url = Constants.GitHubLatestRelease }
                    }
                });
            }

            if (ServerIsRunning)
            {
                outString = $"{outString} | {Translate("ServerRunning")}";
            }

            Title = outString;
        }

        [Obsolete("Obsolete")]
        private void CloseProgram(bool saveAll)
        {
            // Save all the last open files
            var lastOpenFiles = new List<string>();

            EditorReferences.ForEach(x =>
            {
                if (File.Exists(x.FullFilePath))
                {
                    lastOpenFiles.Add(x.FullFilePath);
                }
            });
            Program.OptionsObject.LastOpenFiles = lastOpenFiles.ToArray();

            if (saveAll)
            {
                EditorReferences.ToList().ForEach(x => x.Close(true));
            }
            else
            {
                EditorReferences.ToList().ForEach(x => x.Close(false, false));
            }

            // Kill children process from "Server Start" feature
            if (ServerIsRunning)
            {
                ServerCheckThread.Abort();
                ServerProcess.Kill();
            }

            // Kill Discord RPC
            Program.DiscordClient.Dispose();

            // Check for updates in production
            if (!Debugger.IsAttached && Program.UpdateStatus.IsAvailable)
            {
                var updateWin = new UpdateWindow(Program.UpdateStatus) { Owner = this };
                updateWin.ShowDialog();
            }
        }

        public void DimmMainWindow()
        {
            BlendEffectPlane.Fill = new SolidColorBrush(Colors.Black);
            DimmMainWindowEffect.Begin();
        }

        public void RestoreMainWindow()
        {
            RestoreMainWindowEffect.Begin();
        }

        public void EvaluateRTL()
        {
            FlowDirection = Program.IsRTL ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            EditorReferences.ForEach(x => x.EvaluateRTL());
        }
        #endregion
    }
}