using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using ClearView.Data;

namespace ClearView.UI
{
    public partial class SettingsWindow : Window
    {
        public List<string> SelectedDrives { get; private set; }
        public IndexingSettings IndexingSettings { get; private set; }
        public ExclusionSettings ExclusionSettings { get; private set; }
        public GeneralSettings GeneralSettings { get; private set; }
        public bool ShouldRebuildIndex { get; private set; } = false;
        public bool HotkeyChanged { get; private set; } = false;

        private List<SelectableItem> _driveItems = new List<SelectableItem>();
        private List<SelectableItem> _fileTypeItems = new List<SelectableItem>();

        public SettingsWindow(List<string> selectedDrives, IndexingSettings indexingSettings, ExclusionSettings exclusionSettings, GeneralSettings generalSettings)
        {
            InitializeComponent();
            SelectedDrives = selectedDrives;
            IndexingSettings = indexingSettings;
            ExclusionSettings = exclusionSettings;
            GeneralSettings = generalSettings;

            LoadDrives();
            LoadIndexingSettings();
            LoadExclusionSettings();
            LoadGeneralSettings();
        }

        private void LoadGeneralSettings()
        {
            RunOnStartupCheckBox.IsChecked = GeneralSettings.RunOnStartup;
            UpdateHotkeyText();
        }

        private void LoadDrives()
        {
            var allDrives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
            _driveItems = allDrives.Select(d => new SelectableItem
            {
                Name = d.Name,
                IsSelected = SelectedDrives.Contains(d.Name)
            }).ToList();
            DrivesItemsControl.ItemsSource = _driveItems;
        }
        
        private void LoadIndexingSettings()
        {
            switch (IndexingSettings.Schedule)
            {
                case IndexingSchedule.OnStartup:
                    StartupRadio.IsChecked = true;
                    break;
                case IndexingSchedule.Interval:
                    IntervalRadio.IsChecked = true;
                    break;
                case IndexingSchedule.Manual:
                    ManualRadio.IsChecked = true;
                    break;
            }

            IntervalValueTextBox.Text = IndexingSettings.IntervalValue.ToString();
            IntervalUnitComboBox.Text = IndexingSettings.IntervalUnit;
            UpdateIntervalControls();
        }
        
        private void LoadExclusionSettings()
        {
            ExcludedFoldersListBox.ItemsSource = ExclusionSettings.ExcludedFolders;
            
            var commonFileTypes = new List<string> { ".log", ".tmp", ".bak", ".temp", ".db", ".dll", ".ini", ".sys", ".cache", ".pkg" };
            _fileTypeItems = commonFileTypes.Select(ft => new SelectableItem
            {
                Name = ft,
                IsSelected = ExclusionSettings.ExcludedFileTypes.Contains(ft, StringComparer.OrdinalIgnoreCase)
            }).ToList();
            ExcludedFileTypesItemsControl.ItemsSource = _fileTypeItems;
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ExclusionSettings.ExcludedFolders.Add(dialog.SelectedPath);
                    ExcludedFoldersListBox.ItemsSource = null;
                    ExcludedFoldersListBox.ItemsSource = ExclusionSettings.ExcludedFolders;
                }
            }
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (ExcludedFoldersListBox.SelectedItem is string selectedFolder)
            {
                ExclusionSettings.ExcludedFolders.Remove(selectedFolder);
                ExcludedFoldersListBox.ItemsSource = null;
                ExcludedFoldersListBox.ItemsSource = ExclusionSettings.ExcludedFolders;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SelectedDrives = _driveItems.Where(item => item.IsSelected).Select(item => item.Name).ToList();

            if (StartupRadio.IsChecked == true) IndexingSettings.Schedule = IndexingSchedule.OnStartup;
            else if (IntervalRadio.IsChecked == true) IndexingSettings.Schedule = IndexingSchedule.Interval;
            else IndexingSettings.Schedule = IndexingSchedule.Manual;

            if (int.TryParse(IntervalValueTextBox.Text, out int intervalValue))
            {
                IndexingSettings.IntervalValue = intervalValue;
            }
            if (IntervalUnitComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedUnit)
            {
                IndexingSettings.IntervalUnit = selectedUnit.Content?.ToString() ?? "Hours";
            }
            
            ExclusionSettings.ExcludedFileTypes = _fileTypeItems
                .Where(item => item.IsSelected)
                .Select(item => item.Name)
                .ToList();

            GeneralSettings.RunOnStartup = RunOnStartupCheckBox.IsChecked ?? false;
            
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void RebuildIndex_Click(object sender, RoutedEventArgs e)
        {
            ShouldRebuildIndex = true;
            var messageWindow = new MessageWindow("Rebuild Started", "The index will be rebuilt in the background.");
            messageWindow.Owner = this;
            messageWindow.ShowDialog();
            DialogResult = true;
        }
        
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateIntervalControls();
        }

        private void UpdateIntervalControls()
        {
            if (IntervalValueTextBox != null)
            {
                bool isInterval = IntervalRadio.IsChecked == true;
                IntervalValueTextBox.IsEnabled = isInterval;
                IntervalUnitComboBox.IsEnabled = isInterval;
            }
        }
        
        private void SetHotkey_Click(object sender, RoutedEventArgs e)
        {
            HotkeyTextBox.Text = "Press a key combination...";
            this.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;

            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt || key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            GeneralSettings.HotkeyKey = key;
            GeneralSettings.HotkeyModifiers = Keyboard.Modifiers;
            HotkeyChanged = true;

            UpdateHotkeyText();
            this.PreviewKeyDown -= HotkeyTextBox_PreviewKeyDown;
        }

        private void UpdateHotkeyText()
        {
            string modifiers = GeneralSettings.HotkeyModifiers.ToString();
            string key = GeneralSettings.HotkeyKey.ToString();
            HotkeyTextBox.Text = $"{modifiers} + {key}";
        }
    }
}