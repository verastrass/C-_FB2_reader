using System;
using System.Collections.Generic;
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
using System.Xml.Linq;
using System.IO;
using IOPath = System.IO.Path;
using Microsoft.Win32;

namespace FB2Viewer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Текущий загруженный документ
        XDocument d = null;

        // Текущий загруженный документ
        string dname = null;

        // Пространство имён элементов загруженного документа
        XNamespace ns = null;

        // Сохранение информации о состоянии программы
        string regKeyName = "Software\\WPFExamples\\IMGViewer";

        // Расширения отображаемых файлов
        string[] exts = { ".fb2" };

        public MainWindow()
        {
            InitializeComponent();

            // Работа с деревом каталогов
            TreeViewItem item = new TreeViewItem();
            dirList1.Tag = item;
            item.Tag = null;
            item.Header = "Компьютер";
            item.Items.Add("*");
            dirList1.Items.Add(item);
            item.IsSelected = true;
            dirList1.Focus();
            dirList1.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(TreeViewItem_Expanded));

            // Автоматический переход к каталогу, сохранённому в реестре
            string s = "";
            int i = 0;
            RegistryKey rk = null;
            try
            {
                rk = Registry.CurrentUser.OpenSubKey(regKeyName);
                if (rk != null)
                {
                    Width = (int)rk.GetValue("Width", (int)Width);
                    Height = (int)rk.GetValue("Height", (int)Height);
                    grid1.ColumnDefinitions[0].Width = new GridLength((int)rk.GetValue("DirList", (int)grid1.ColumnDefinitions[0].Width.Value));
                    grid1.ColumnDefinitions[2].Width = new GridLength((int)rk.GetValue("FileList", (int)grid1.ColumnDefinitions[2].Width.Value));
                    s = (string)rk.GetValue("Path", "");
                    i = (int)rk.GetValue("File", 0);
                }
            }
            finally
            {
                if (rk != null)
                    rk.Close();
            }

            if (!Directory.Exists(s))
                s = Directory.GetCurrentDirectory();

            item = InitialExpanding(s);
            if (item != null)
                item.IsSelected = true;

            if (fileList1.Items.Count == 0)
                i = -1;
            else if (i >= fileList1.Items.Count || i == -1)
                i = 0;

            fileList1.SelectedIndex = i;
        }


        // Развёртывание списка
        void ExpandItem(TreeViewItem item)
        {
            item.Items.Clear();
            if (item.Tag == null)
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady)
                        continue;

                    TreeViewItem newItem = new TreeViewItem();
                    newItem.Tag = drive.RootDirectory;
                    newItem.Header = drive.Name;

                    if (drive.VolumeLabel != "")
                        newItem.Header += " [" + drive.VolumeLabel + "]";

                    if (drive.RootDirectory.GetDirectories().Length > 0)
                        newItem.Items.Add("*");

                    item.Items.Add(newItem);
                }
            else
            {
                try
                {
                    foreach (var subDir in (item.Tag as DirectoryInfo).GetDirectories())
                    {
                        try
                        {
                            TreeViewItem newItem = new TreeViewItem();
                            newItem.Tag = subDir;
                            newItem.Header = subDir.Name;

                            if (subDir.GetDirectories().Length > 0)
                                newItem.Items.Add("*");

                            item.Items.Add(newItem);
                        }
                        catch
                        { }
                    }
                }
                catch
                { }
            }
            item.IsExpanded = true;
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            ExpandItem(e.Source as TreeViewItem);
        }


        // Развёртывание списка с помощью клавиш со стрелками
        private void dirList1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Return)
            {
                var tv = e.Source as TreeViewItem;
                tv.IsExpanded = !tv.IsExpanded;
            }
        }


        // Автоматический переход к каталогу
        TreeViewItem InitialExpanding(string fullPath)
        {
            if (!Directory.Exists(fullPath))
                return null;

            var paths = fullPath.Split('\\');
            paths[0] += "\\";
            TreeViewItem rootItem = dirList1.Items[0] as TreeViewItem;
            ExpandItem(rootItem);
            TreeViewItem item = rootItem;

            foreach (var e in paths)
            {
                item = item.Items.Cast<TreeViewItem>().FirstOrDefault(e1 => (e1.Tag as DirectoryInfo).Name.ToUpper() == e.ToUpper());

                if (item == null)
                    return null;

                ExpandItem(item);
            }

            return item;
        }


        // Отображение файлов с заданным расширением в конкретном каталоге
        private void dirList1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (button1.IsEnabled)
                button1_Click(null, null);

            var dirInfo = (dirList1.SelectedItem as TreeViewItem).Tag as DirectoryInfo;
            if (dirInfo == null)
                fileList1.ItemsSource = null;
            else
            {
                try
                {
                    var src = dirInfo.GetFiles().Select(e1 => e1.Name)
                        .Where(e1 => exts.Contains(IOPath.GetExtension(e1).ToLower()));
                    fileList1.ItemsSource = src;
                    if (src.Count() > 0)
                        fileList1.SelectedIndex = 0;
                }
                catch
                {
                    fileList1.ItemsSource = null;
                }
            }
        }


        // Отображение информации в правой колонке
        private void fileList1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (button1.IsEnabled)
                button1_Click(null, null);

            tabControl1.IsEnabled = false;
            tabControl1.SelectedItem = titleInfo1;
            titleTextBox.Text = "";

            var name = dname = ((dirList1.SelectedItem as TreeViewItem).Tag as DirectoryInfo).FullName + "\\" + (string)fileList1.SelectedValue;
            d = XDocument.Load(name);
            ns = d.Root.Name.Namespace;
            
            if (name != null)
            {
                Title = "Fb2 Viewer - " + name;
                Mouse.OverrideCursor = Cursors.Wait;

                try
                {
                    tabControl1.IsEnabled = true;
                    updateTitleInfoTab();
                    updateEditTab();
                    updateStatisticsTab();
                    updateSectionsTab();
                    button1.IsEnabled = false;
                }
                catch
                {
                    Title += " (WRONG FORMAT)";
                    tabControl1.IsEnabled = false;
                }

                Mouse.OverrideCursor = null;
            }
            else
            {
                Title = "Fb2 Viewer";
                tabControl1.IsEnabled = false;
            }
        }


        // Обновление вкладки title-info
        private void updateTitleInfoTab()
        {
            titleTextBox.Text = d.Root.Descendants(ns + "title-info").First().Elements()
                        .Select(x => new
                        {
                            name = x.Name.LocalName,
                            val = (x.HasElements ? x.Elements().Select(y => y.Value).Combine(" ") : x.Value != "" ? x.Value : x.HasAttributes ? x.Attributes().OrderBy(y => y.Name.LocalName).Select(y => y.Name.LocalName + "=" + y.Value).Combine(" ") : "")})
                        .Where(x => x.val.Length > 0)
                        .Select(x => x.name + ": " + x.val)
                        .Combine("\n");
        }

        
        // Обновление вкладки edit
        private void updateEditTab()
        {
            editTextBox1.Text = d.Root.Descendants(ns + "title-info").First().Element(ns + "book-title").Value;
            var seq = d.Root.Descendants(ns + "title-info").First().Element(ns + "sequence");
            if (seq != null)
                editTextBox2.Text = seq.Attributes().Select(x => x.Value).Combine(" ");
            else
                editTextBox2.Text = "";
        }

                
        // Обновление вкладки statistics
        private void updateStatisticsTab()
        {
            statisticsTextBox.Text = "Elements: \n" + d.Root.DescendantsAndSelf().Select(x => x.Name.LocalName).GroupBy(x => x, (x, xs) => "    " + x + " - " + xs.Count()).Combine("\n");
        }


        // Обновление вкладки sections
        private void updateSectionsTab()
        {
            if (d.Root.Element(ns + "body").Elements(ns + "section") != null)
                sectionsTextBox.Text = d.Root.Element(ns + "body").Elements(ns + "section").Select(x => getSectionsRecursively(x, 0)).Combine("");
        }

        private string getSectionsRecursively(XElement el, int level)
        {
            string title = "";
            if (el.Element(ns + "title") != null)
            {
                title = level == 0 ? "" : Enumerable.Repeat("    ", level).Combine("");
                title += el.Element(ns + "title").Elements().Select(x => x.Value).Combine(" ") + "\n";
            }
                

            if (el.Elements(ns + "section") != null)
                title += el.Elements(ns + "section").Select(x => getSectionsRecursively(x, level + 1)).Combine("");

            return title;
        }


        // Сохранение изменений при нажатии кнопки Save на вкладке Edit
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите сохранить изменения?", "Сохранение изменений", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                string str = editTextBox1.Text.SkipWhile(x => x == ' ').Reverse().SkipWhile(x => x == ' ').Reverse().Combine("");
                if (str != "")
                    d.Root.Descendants(ns + "title-info").First().Element(ns + "book-title").Value = str;

                if (d.Root.Descendants(ns + "title-info").First().Element(ns + "sequence") == null)
                    d.Root.Descendants(ns + "title-info").First().Add(new XElement(ns + "sequence"));

                str = editTextBox2.Text.SkipWhile(x => x == ' ').Reverse().SkipWhile(x => x == ' ').Reverse().DefaultIfEmpty().Combine("");
                if (str == "" || !char.IsLetter(str[0]))
                    d.Root.Descendants(ns + "title-info").First().Element(ns + "sequence").Remove();
                else
                {
                    var attrs = str.Split(' ');
                    if (attrs.Length == 1)
                        d.Root.Descendants(ns + "title-info").First().Element(ns + "sequence").ReplaceAttributes(new XAttribute("name", attrs[0]), new XAttribute("number", "0"));
                    else
                        d.Root.Descendants(ns + "title-info").First().Element(ns + "sequence").ReplaceAttributes(new XAttribute("name", attrs.Take(attrs.Length - 1).Combine(" ")), new XAttribute("number", attrs[attrs.Length - 1]));
                }
                                                    
                d.Save(dname);
                updateEditTab();
                updateTitleInfoTab();
                updateStatisticsTab();
                button1.IsEnabled = false;
            }
            else
            {
                updateEditTab();
                button1.IsEnabled = false;
            }
        }


        // Активация кнопки Save
        private void textBox1_2_textChanged(object sender, EventArgs e)
        {
            button1.IsEnabled = true;
        }


        // Отмена закрытия окна при несохранённых данных
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (button1.IsEnabled)
                button1_Click(null, null);
        }


        // Сохранение информации о состоянии программы
        private void Window_Closed(object sender, EventArgs e)
        {
            RegistryKey rk = null;
            try
            {
                rk = Registry.CurrentUser.CreateSubKey(regKeyName);
                if (rk == null)
                    return;

                rk.SetValue("Width", (int)ActualWidth);
                rk.SetValue("Height", (int)ActualHeight);
                rk.SetValue("DirList", (int)grid1.ColumnDefinitions[0].ActualWidth);
                rk.SetValue("FileList", (int)grid1.ColumnDefinitions[2].ActualWidth);
                var dirInfo = (dirList1.SelectedItem as TreeViewItem).Tag as DirectoryInfo;
                rk.SetValue("Path", dirInfo == null ? "" : dirInfo.FullName);
                rk.SetValue("File", fileList1.SelectedIndex);
            }
            finally
            {
                if (rk != null)
                    rk.Close();
            }
        }

    }


    // Метод расширения для объединения строкового представления элементов последовательности через символ-разделитель
    public static class ExtCombine
    {
        public static string Combine<T>(this IEnumerable<T> src, string separator)
        {
            if (src == null || !src.Any())
                return null;

            return src.Aggregate("", (seed, s) => seed + s.ToString() + separator,
                s => separator.Length > 0 ? s.Remove(s.Length - separator.Length) : s);
        }
    }
}
