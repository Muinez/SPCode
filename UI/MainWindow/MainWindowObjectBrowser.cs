﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SPCode.UI.Windows;

namespace SPCode.UI
{
    public partial class MainWindow
    {
        private string CurrentObjectBrowserDirectory = string.Empty;
        private readonly DispatcherTimer SearchCooldownTimer;

        #region Events
        private void TreeViewOBItem_Expanded(object sender, RoutedEventArgs e)
        {
            var source = e.Source;
            if (source is not TreeViewItem)
            {
                return;
            }
            var item = (TreeViewItem)source;
            var itemInfo = (ObjectBrowserTag)item.Tag;
            if (itemInfo.Kind != ObjectBrowserItemKind.Directory || !Directory.Exists(itemInfo.Value))
            {
                return;
            }

            Debug.Assert(Dispatcher != null, nameof(Dispatcher) + " != null");
            using (Dispatcher.DisableProcessing())
            {
                item.Items.Clear();
                var newItems = BuildDirectoryItems(itemInfo.Value);
                foreach (var i in newItems)
                {
                    item.Items.Add(i);
                }
            }
        }

        private void TreeViewOBItem_RightClicked(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);
            var itemTag = treeViewItem.Tag as ObjectBrowserTag;

            if (treeViewItem != null)
            {
                switch (itemTag.Kind)
                {
                    case ObjectBrowserItemKind.Directory:
                        {
                            treeViewItem.Focus();
                            ObjectBrowser.ContextMenu = ObjectBrowser.Resources["TVIContextMenuDir"] as ContextMenu;
                            break;
                        }
                    case ObjectBrowserItemKind.File:
                        {
                            treeViewItem.Focus();
                            ObjectBrowser.ContextMenu = ObjectBrowser.Resources["TVIContextMenu"] as ContextMenu;
                            break;
                        }
                    case ObjectBrowserItemKind.ParentDirectory:
                        {
                            ObjectBrowser.ContextMenu = null;
                            break;
                        }
                }
            }
            e.Handled = true;
        }

        private void TreeViewOBItemParentDir_DoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }
            var currentInfo = new DirectoryInfo(CurrentObjectBrowserDirectory);
            var parentInfo = currentInfo.Parent;
            if (parentInfo != null)
            {
                if (parentInfo.Exists)
                {
                    ChangeObjectBrowserToDirectory(parentInfo.FullName);
                    return;
                }
            }
            ChangeObjectBrowserToDrives();
        }

        private void TreeViewOBItemFile_DoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || sender is not TreeViewItem item)
            {
                return;
            }
            var itemInfo = (ObjectBrowserTag)item.Tag;
            if (itemInfo.Kind == ObjectBrowserItemKind.File)
            {
                TryLoadSourceFile(itemInfo.Value, true, false, true);
            }
        }

        private void OBItemOpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            var selectedItemFile = ((ObjectBrowser.SelectedItem as TreeViewItem).Tag as ObjectBrowserTag).Value;
            Process.Start("explorer.exe", $"/select, \"{selectedItemFile}\"");
        }

        private void OBItemRename_Click(object sender, RoutedEventArgs e)
        {
            var file = ((ObjectBrowser.SelectedItem as TreeViewItem).Tag as ObjectBrowserTag).Value;
            var renameWindow = new RenameWindow(file);
            renameWindow.ShowDialog();
            if (!string.IsNullOrEmpty(renameWindow.NewName))
            {
                File.Move(file, Path.GetDirectoryName(file) + $@"\{renameWindow.NewName}");
                OBDirList_SelectionChanged(null, null);
            }
        }

        private void OBItemDelete_Click(object sender, RoutedEventArgs e)
        {
            var file = ((ObjectBrowser.SelectedItem as TreeViewItem).Tag as ObjectBrowserTag).Value;
            File.Delete(file);
            OBDirList_SelectionChanged(null, null);
        }

        private void ListViewOBItem_SelectFile(object sender, RoutedEventArgs e)
        {
            if (sender is not ListViewItem item)
            {
                return;
            }
            var ee = GetCurrentEditorElement();
            if (ee != null)
            {
                var fInfo = new FileInfo(ee.FullFilePath);
                ChangeObjectBrowserToDirectory(fInfo.DirectoryName);
            }
            item.IsSelected = true;
            OBButtonHolder.SelectedIndex = -1;
        }

        private void ListViewOBItem_SelectConfig(object sender, RoutedEventArgs e)
        {
            if (sender is not ListViewItem item)
            {
                return;
            }
            var cc = Program.Configs[Program.SelectedConfig];
            if (cc.SMDirectories.Count > 0)
            {
                ChangeObjectBrowserToDirectory((string)OBDirList.SelectedItem);
            }
            item.IsSelected = true;
            OBButtonHolder.SelectedIndex = -1;
        }

        private void OBDirList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ChangeObjectBrowserToDirectory((string)OBDirList.SelectedItem);
            OBButtonHolder.SelectedIndex = 1;
        }

        private void OBSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchCooldownTimer.Stop();
            SearchCooldownTimer.Start();
        }

        private void OnSearchCooldownTimerTick(object sender, EventArgs e)
        {
            SearchCooldownTimer.Stop();

            // frenar carpetas duplicadas
            // que vuelva todo el tree a la normalidad si el filtro está vacío
            // checkear recursividad (testing caja negra/blanca)

            foreach (TreeViewItem tvi in ObjectBrowser.Items)
            {
                if ((tvi.Tag as ObjectBrowserTag).Kind != ObjectBrowserItemKind.ParentDirectory)
                {
                    TraverseTree(OBSearch.Text, tvi);
                }
            }
        }

        #endregion

        #region Methods
        private void ChangeObjectBrowserToDirectory(string dir, string filter = "")
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                var cc = Program.Configs[Program.SelectedConfig];
                if (cc.SMDirectories.Count > 0)
                {
                    dir = cc.SMDirectories[0];
                }
            }
            else if (dir == "0:")
            {
                ChangeObjectBrowserToDrives();
                return;
            }
            if (!Directory.Exists(dir))
            {
                dir = Environment.CurrentDirectory;
            }
            try
            {
                Directory.GetAccessControl(dir);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            CurrentObjectBrowserDirectory = dir;
            Program.OptionsObject.Program_ObjectBrowserDirectory = CurrentObjectBrowserDirectory;

            Debug.Assert(Dispatcher != null, nameof(Dispatcher) + " != null");
            using (Dispatcher.DisableProcessing())
            {
                ObjectBrowser.Items.Clear();
                var parentDirItem = new TreeViewItem()
                {
                    Header = "..",
                    Tag = new ObjectBrowserTag() { Kind = ObjectBrowserItemKind.ParentDirectory }
                };
                parentDirItem.MouseDoubleClick += TreeViewOBItemParentDir_DoubleClicked;
                parentDirItem.PreviewMouseRightButtonDown += TreeViewOBItem_RightClicked;
                ObjectBrowser.Items.Add(parentDirItem);
                var newItems = BuildDirectoryItems(dir, filter);
                foreach (var item in newItems)
                {
                    ObjectBrowser.Items.Add(item);
                }
            }
        }

        private void ChangeObjectBrowserToDrives()
        {
            Program.OptionsObject.Program_ObjectBrowserDirectory = "0:";
            var drives = DriveInfo.GetDrives();
            Debug.Assert(Dispatcher != null, nameof(Dispatcher) + " != null");
            using (Dispatcher.DisableProcessing())
            {
                ObjectBrowser.Items.Clear();
                foreach (var dInfo in drives)
                {
                    if (dInfo.IsReady && (dInfo.DriveType == DriveType.Fixed || dInfo.DriveType == DriveType.Removable))
                    {
                        var tvi = new TreeViewItem()
                        {
                            Header = BuildTreeViewItemContent(dInfo.Name, "iconmonstr-folder-13-16.png"),
                            Tag = new ObjectBrowserTag() { Kind = ObjectBrowserItemKind.Directory, Value = dInfo.RootDirectory.FullName }
                        };
                        tvi.Items.Add("...");
                        ObjectBrowser.Items.Add(tvi);
                    }
                }
            }
        }

        private List<TreeViewItem> BuildDirectoryItems(string dir, string filter = "")
        {
            var itemList = new List<TreeViewItem>();
            var spFiles = Directory.GetFiles(dir, "*.sp", SearchOption.TopDirectoryOnly);
            var incFiles = Directory.GetFiles(dir, "*.inc", SearchOption.TopDirectoryOnly);
            var directories = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly);
            foreach (var d in directories)
            {
                var dInfo = new DirectoryInfo(d);
                if (!dInfo.Exists)
                {
                    continue;
                }
                try
                {
                    dInfo.GetAccessControl();
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                var tvi = new TreeViewItem()
                {
                    Header = BuildTreeViewItemContent(dInfo.Name, "iconmonstr-folder-13-16.png"),
                    Tag = new ObjectBrowserTag() { Kind = ObjectBrowserItemKind.Directory, Value = dInfo.FullName }
                };
                tvi.Items.Add("...");
                itemList.Add(tvi);
            }
            foreach (var f in spFiles)
            {
                var fInfo = new FileInfo(f);
                if (!fInfo.Exists || (!string.IsNullOrWhiteSpace(filter) && !(fInfo.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    continue;
                }
                var tvi = new TreeViewItem()
                {
                    Header = BuildTreeViewItemContent(fInfo.Name, "iconmonstr-file-5-16.png"),
                    Tag = new ObjectBrowserTag() { Kind = ObjectBrowserItemKind.File, Value = fInfo.FullName }
                };
                tvi.MouseDoubleClick += TreeViewOBItemFile_DoubleClicked;
                tvi.MouseDown += TreeViewOBItem_RightClicked;
                itemList.Add(tvi);
            }
            foreach (var f in incFiles)
            {
                var fInfo = new FileInfo(f);
                if (!fInfo.Exists || (!string.IsNullOrWhiteSpace(filter) && !(fInfo.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    continue;
                }
                var tvi = new TreeViewItem()
                {
                    Header = BuildTreeViewItemContent(fInfo.Name, "iconmonstr-file-8-16.png"),
                    Tag = new ObjectBrowserTag() { Kind = ObjectBrowserItemKind.File, Value = fInfo.FullName }
                };
                tvi.MouseDoubleClick += TreeViewOBItemFile_DoubleClicked;
                tvi.MouseRightButtonDown += TreeViewOBItem_RightClicked;
                itemList.Add(tvi);
            }
            return itemList;
        }

        private object BuildTreeViewItemContent(string headerString, string iconFile)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            var image = new Image();
            var uriPath = $"/SPCode;component/Resources/Icons/{iconFile}";
            image.Source = new BitmapImage(new Uri(uriPath, UriKind.Relative));
            image.Width = 16;
            image.Height = 16;
            var lbl = new TextBlock { Text = headerString, Margin = new Thickness(2.0, 0.0, 0.0, 0.0) };
            stack.Children.Add(image);
            stack.Children.Add(lbl);
            return stack;
        }

        private static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            // Snippet that allows me to select items while right-clicking them to enable Context Menu capabilities
            while (source != null && !(source is TreeViewItem))
            {
                source = VisualTreeHelper.GetParent(source);
            }
            return source as TreeViewItem;
        }

        public void UpdateOBFileButton()
        {
            if (GetAllEditorElements() == null)
            {
                OBTabFile.IsEnabled = false;
                OBTabFile.IsSelected = false;
                OBTabConfig.IsSelected = true;
            }
            else
            {
                OBTabFile.IsEnabled = true;
            }
        }

        public void FilterDirectory(TreeViewItem tvi, string filter)
        {
            var omittedDirs = new List<TreeViewItem>();
            tvi.IsExpanded = true;
            foreach (TreeViewItem item in tvi.Items)
            {
                if ((item.Tag as ObjectBrowserTag).Kind != ObjectBrowserItemKind.Directory)
                {
                    item.Visibility = Visibility.Collapsed;
                }
                else
                {
                    omittedDirs.Add(item);
                }
            }

            var newItems = BuildDirectoryItems((tvi.Tag as ObjectBrowserTag).Value, filter);
            foreach (var item in omittedDirs)
            {
                newItems.Remove(newItems.FirstOrDefault(x => (x.Tag as ObjectBrowserTag).Value == (item.Tag as ObjectBrowserTag).Value));
            }
            foreach (var item in newItems)
            {
                tvi.Items.Add(item);
            }
        }

        public void TraverseTree(string filter, TreeViewItem tvi)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return;
            }

            tvi.ExpandSubtree();
            foreach (TreeViewItem item in tvi.Items)
            {
                var tag = item.Tag as ObjectBrowserTag;
                if (tag.Kind == ObjectBrowserItemKind.Directory)
                {
                    FilterDirectory(item, filter);
                }
                else if (tag.Kind == ObjectBrowserItemKind.File && 
                    new FileInfo(tag.Value).Name.IndexOf(OBSearch.Text, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    item.Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        private class ObjectBrowserTag
        {
            public ObjectBrowserItemKind Kind;
            public string Value;
        }

        private enum ObjectBrowserItemKind
        {
            ParentDirectory,
            Directory,
            File
        }
    }
}