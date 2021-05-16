﻿using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Interaction logic for SoundReplaceOptionsDialog.xaml
    /// </summary>
    public partial class SoundReplaceOptionsDialog : NotifyPropertyChangedWindowBase
    {
        public ObservableCollectionExtended<int> SampleRates { get; } = new ObservableCollectionExtended<int>();
        private static readonly int[] AcceptedSampleRates = {24000, 32000}; //may add more later
        public WwiseConversionSettingsPackage ChosenSettings; 

        public SoundReplaceOptionsDialog() : base()
        {
            DataContext = this;
            SampleRates.AddRange(AcceptedSampleRates);
            LoadCommands();
            InitializeComponent();
            SampleRate_Combobox.SelectedIndex = 0;
        }

        public SoundReplaceOptionsDialog(Window w) : this()
        {
            Owner = w;
        }


        public ICommand ConvertAudioCommand { get; private set; }

        void LoadCommands()
        {
            ConvertAudioCommand = new GenericCommand(ReturnSettings, CanReturnSettings);
        }

        private void ReturnSettings()
        {
            ChosenSettings = new WwiseConversionSettingsPackage
            {
                TargetSamplerate = (int)SampleRate_Combobox.SelectedItem,
                UpdateReferencedEvents = (bool)UpdateEvents_CheckBox.IsChecked
            };
            DialogResult = true;
            Close();
        }

        private bool CanReturnSettings() => SampleRate_Combobox.SelectedIndex >= 0;

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
    public class WwiseConversionSettingsPackage
    {
        public int TargetSamplerate = 0;
        public bool UpdateReferencedEvents = true;
    }
}