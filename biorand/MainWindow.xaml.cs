using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IntelOrca.Biohazard.BioRand.RE1;
using IntelOrca.Biohazard.BioRand.RE2;
using IntelOrca.Biohazard.BioRand.RE3;
using IntelOrca.Biohazard.BioRand.RECV;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static Version CurrentVersion = Assembly.GetEntryAssembly().GetName().Version;

        private BaseRandomiser _randomizer;
        private Rng _random = new Rng();
        private RandoAppSettings _settings = new RandoAppSettings();
        private RandoConfig _config = new RandoConfig();
        private bool _suspendEvents;

        public MainWindow()
        {
            InitializeComponent();
            InitializeEvents();
            LoadSettings();
            UpdateUi();
            UpdateEnabledUi();
            PopulateContributors();
            PopulateChangelog();
#if !DEBUG
            CheckForNewVersion();
#endif

            var version = CurrentVersion;
            Title += $" - v{version.Major}.{version.Minor}.{version.Build}";
            SelectedGame = _settings.LastSelectedGame;
            versionLabel.Text = Program.CurrentVersionInfo;
            txtWhatsNewTitle.Text = txtWhatsNewTitle.Text.Replace("{VERSION}", Program.CurrentVersionNumber);

#if DISABLE_CVX
            if (SelectedGame == 3)
                SelectedGame = -3;
            gameListView.Items.RemoveAt(3);
            gameLocationCV.Visibility = Visibility.Collapsed;
#endif
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MinWidth = 1920 / 4;
            MinHeight = 1080 / 4;
            MaxWidth = Width;
            MaxHeight = Height;
        }

        private void PopulateContributors()
        {
            var text = GetBioRandFileContent("CONTRIBUTORS.md");
            text = Regex.Replace(text, @"#.*\s+", "");
            text = text.Replace("**", "");
            textContributors.Text = text;
        }

        private void PopulateChangelog()
        {
            textChangelog.Text = GetBioRandFileContent("CHANGELOG.md");
        }

        private string GetBioRandFileContent(string fileName)
        {
            try
            {
#if DEBUG
                var installLocation = Path.Combine(Path.GetDirectoryName(Program.CurrentAssembly.Location), "..\\..\\..\\..");
#else
                var installLocation = Path.GetDirectoryName(Program.CurrentAssembly.Location);
#endif
                var path = Path.Combine(installLocation, fileName);
                return File.ReadAllText(path);
            }
            catch
            {
                return "[Failed to load content]";
            }
        }

        private void LoadSettings()
        {
            _settings = RandoAppSettings.Load();
            using (SuspendEvents())
            {
                gameLocation1.Location = _settings.GamePath1;
                gameLocation2.Location = _settings.GamePath2;
                gameLocation3.Location = _settings.GamePath3;
                gameLocationCV.Location = _settings.GamePathCv;

                gameLocation1.IsChecked = _settings.GameEnabled1;
                gameLocation2.IsChecked = _settings.GameEnabled2;
                gameLocation3.IsChecked = _settings.GameEnabled3;
                gameLocationCV.IsChecked = _settings.GameEnabledCv;

                gameLocation1.SetExecutableList(_settings.GamePath1, _settings.GameExecutable1);
                gameLocation2.SetExecutableList(_settings.GamePath2, _settings.GameExecutable2);
                gameLocation3.SetExecutableList(_settings.GamePath3, _settings.GameExecutable3);

                gameLocation1.IsSettingsLoaded = true;
                gameLocation2.IsSettingsLoaded = true;
                gameLocation3.IsSettingsLoaded = true;
                gameLocationCV.IsSettingsLoaded = true;

                chkEnableCustomContent.IsChecked = !_settings.DisableCustomContent;
                chkRandomizeTitleVoice.IsChecked = _settings.RandomizeTitleVoice;
                chkMaxInventorySize.IsChecked = _settings.MaxInventorySize;
                chkDoorSkip.IsChecked = _settings.DoorSkip;
            }

            if (!Version.TryParse(_settings.LastVersion, out var lastVersion) || lastVersion != Program.CurrentVersion)
            {
                _settings.Seed1 = new RandoConfig().WithSeed(_random.Next(0, RandoConfig.MaxSeed)).ToString();
                _settings.Seed2 = new RandoConfig().WithSeed(_random.Next(0, RandoConfig.MaxSeed)).ToString();
                _settings.Seed3 = new RandoConfig().WithSeed(_random.Next(0, RandoConfig.MaxSeed)).ToString();
                _settings.SeedCv = new RandoConfig().WithSeed(_random.Next(0, RandoConfig.MaxSeed)).ToString();
                if (lastVersion != default)
                {
                    SelectedGame = -3;
                }
            }

            var seed = SelectedGame == 0 ? _settings.Seed1 : SelectedGame == 1 ? _settings.Seed2 : SelectedGame == 2 ? _settings.Seed3 : _settings.SeedCv;
            if (seed == null)
            {
                RandomizeSeed();
            }
            else
            {
                _config = RandoConfig.FromString(seed);
            }
        }

        private void SaveSettings()
        {
            _settings.GamePath1 = gameLocation1.Location;
            _settings.GamePath2 = gameLocation2.Location;
            _settings.GamePath3 = gameLocation3.Location;
            _settings.GamePathCv = gameLocationCV.Location;

            _settings.GameEnabled1 = gameLocation1.IsChecked == true;
            _settings.GameEnabled2 = gameLocation2.IsChecked == true;
            _settings.GameEnabled3 = gameLocation3.IsChecked == true;
            _settings.GameEnabledCv = gameLocationCV.IsChecked == true;

            _settings.GameExecutable1 = gameLocation1.SelectedExecutable;
            _settings.GameExecutable2 = gameLocation2.SelectedExecutable;
            _settings.GameExecutable3 = gameLocation3.SelectedExecutable;

            _settings.DisableCustomContent = chkEnableCustomContent.IsChecked != true;
            _settings.RandomizeTitleVoice = chkRandomizeTitleVoice.IsChecked == true;
            _settings.MaxInventorySize = chkMaxInventorySize.IsChecked == true;
            _settings.DoorSkip = chkDoorSkip.IsChecked == true;

            _settings.LastVersion = Program.CurrentVersionNumber;

            switch (SelectedGame)
            {
                case 0:
                    _settings.Seed1 = _config.ToString();
                    break;
                case 1:
                    _settings.Seed2 = _config.ToString();
                    break;
                case 2:
                    _settings.Seed3 = _config.ToString();
                    break;
                case 3:
                    _settings.SeedCv = _config.ToString();
                    break;
            }
            _settings.Save();
        }

        private void InitializeEvents()
        {
            foreach (var control in GetAllControls(this))
            {
                if (IsControlFromPanelConfig(control)) //Avoid subscribing on changes from the GameLocation settings panel. This will prevent the UpdateConfig() call that could crash if the rando configs were not loaded.
                    continue;

                if (control is CheckBox cb)
                {
                    cb.Unchecked += OnCheckBoxChanged;
                    cb.Checked += OnCheckBoxChanged;
                }
                else if (control is CheckGroupBox cgb)
                {
                    cgb.Unchecked += OnCheckBoxChanged;
                    cgb.Checked += OnCheckBoxChanged;
                }
                else if (control is Slider slider)
                {
                    slider.ValueChanged += Slider_ValueChanged;
                }
                else if (control is ComboBox comboBox)
                {
                    comboBox.SelectionChanged += ComboBox_SelectionChanged;
                }
                else if (control is CheckBoxList checkBoxList)
                {
                    checkBoxList.ItemValueChanged += OnCheckBoxChanged;
                }
            }
        }

        private bool IsControlFromPanelConfig(FrameworkElement control)
        {
            if (control is StackPanel && ((StackPanel)control).Name == nameof(panelConfig))
                return true;

            if (control.Parent != null && control.Parent is FrameworkElement)
                return IsControlFromPanelConfig((FrameworkElement)control.Parent);

            return false;
        }

        private void OnCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!_suspendEvents)
            {
                UpdateConfig();
                UpdateUi();
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                slider.Value = (byte)slider.Value;
            }
            else if (sender is RandoSlider randoSlider)
            {
                randoSlider.Value = (byte)randoSlider.Value;
            }

            if (!_suspendEvents)
            {
                UpdateConfig();
                UpdateUi();
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_suspendEvents)
            {
                UpdateConfig();
                UpdateUi();
            }
        }

        private void UpdateUi()
        {
            using (SuspendEvents())
            {
                chkPlayer.IsChecked = _config.ChangePlayer;
                dropdownPlayer0.SelectedIndex = Math.Min(_config.Player0, dropdownPlayer0.Items.Count - 1);
                dropdownPlayer1.SelectedIndex = Math.Min(_config.Player1, dropdownPlayer1.Items.Count - 1);
                chkSwapCharacters.IsChecked = _config.SwapCharacters;

                dropdownPlayer0Container.Child = null;
                dropdownPlayer1Container.Child = null;
                if (chkSwapCharacters.IsChecked == true)
                {
                    dropdownPlayer0Container.Child = dropdownPlayer1;
                    dropdownPlayer1Container.Child = dropdownPlayer0;
                }
                else
                {
                    dropdownPlayer0Container.Child = dropdownPlayer0;
                    dropdownPlayer1Container.Child = dropdownPlayer1;
                }

                if (listEnemies.ItemsSource != null)
                {
                    var index = 0;
                    foreach (SliderListItem item in listEnemies.ItemsSource)
                    {
                        item.Value = _config.EnemyRatios.Length > index ? _config.EnemyRatios[index] : 0;
                        index++;
                    }
                }

                listWeapons.Values = _config.EnabledWeapons;

                chkEnemySkins.IsChecked = _config.RandomEnemySkins;
                listEnemySkins.Values = _config.EnabledEnemySkins;

                chkRngChars.IsChecked = _config.RandomNPCs;
                listNPCs.Values = _config.EnabledNPCs;

                chkRngBgm.IsChecked = _config.RandomBgm;
                listBGMs.Values = _config.EnabledBGMs;

                chkRngDoors.IsChecked = _config.RandomDoors;
                chkPrioritiseCutscenes.IsChecked = _config.PrioritiseCutscenes;
                chkProtectSoftLock.IsChecked = _config.Segmented || _config.RandomDoors;
                chkHiddenKeyItems.IsChecked = _config.HiddenKeyItems;
                chkRngEnemies.IsChecked = _config.RandomEnemies;
                chkRandomEnemyPlacements.IsChecked = _config.RandomEnemyPlacement;
                chkEnemyRestrictedRooms.IsChecked = _config.AllowEnemiesAnyRoom;
                sliderEnemyCount.Value = _config.EnemyQuantity;
                chkRngItems.IsChecked = _config.RandomItems || _config.RandomDoors;
                chkCustomItemPool.IsChecked = !_config.ShuffleItems || _config.RandomDoors;
                chkAlternativeRoute.IsChecked = _config.AlternativeRoutes;
                chkIncludeDocuments.IsChecked = _config.IncludeDocuments;
                chkRandomInventory.IsChecked = _config.RandomInventory;

                dropdownWeapon0.SelectedIndex = Math.Min(_config.Weapon0, dropdownWeapon0.Items.Count - 1);
                dropdownWeapon1.SelectedIndex = Math.Min(_config.Weapon1, dropdownWeapon1.Items.Count - 1);

                sliderEnemyDifficulty.Value = _config.EnemyDifficulty;
                sliderEnemyRooms.Value = _config.EnemyRooms;

                sliderGunpowder.Value = _config.RatioGunpowder;
                sliderAmmo.Value = _config.RatioAmmo;
                sliderHealth.Value = _config.RatioHealth;
                sliderInkRibbons.Value = _config.RatioInkRibbons;
                sliderAmmoQuantity.Value = _config.AmmoQuantity;

                sliderAreaCount.Value = _config.AreaCount;
                sliderAreaSize.Value = _config.AreaSize;

                chkCutscenes.IsChecked = _config.RandomCutscenes;
                chkAllowAnyVoice.IsChecked = _config.AllowAnyVoice;
                chkReduceSilences.IsChecked = _config.ReduceSilences;
                chkRandomEvents.IsChecked = _config.RandomEvents;

                dropdownVariant.SelectedIndex = Math.Min(_config.GameVariant, dropdownVariant.Items.Count - 1);

                txtSeed.Text = _config.ToString();
                seedQrCode.Seed = null;
                seedQrCode.Seed = _config;

                btnStartRE.Content = $"Start RE{_config.Game}";
                var gameLocationPath = GetGameLocation();
                var gameExecutableName = GetGameExecutable();
                btnStartRE.IsEnabled = !string.IsNullOrEmpty(gameLocationPath)
                                        && !string.IsNullOrEmpty(gameExecutableName)
                                        && File.Exists(Path.Combine(gameLocationPath, gameExecutableName));
                btnStartRE.ToolTip = $"Start the '{Path.GetFileName(gameExecutableName)}' executable.";

                UpdateHints();
            }
            UpdateEnabledUi();
        }

        private void UpdateHints()
        {
            UpdateEstimateCompletion();
            UpdateItemPie();
            UpdateEnemyPie();
        }

        private void UpdateEstimateCompletion()
        {
            var colour = Colors.Green;
            if (_config.RandomDoors)
            {
                var lower = (_config.AreaCount + 1) * 60;
                var upper = (_config.AreaSize + 1) * 45;
                var lHours = ((lower + 30) / 60);
                var uHours = ((upper + 30) / 60);
                lHours = Math.Min(lHours, uHours);
                uHours = Math.Max(lHours + 1, uHours);
                string eta;
                if (_config.AreaSize <= 0)
                {
                    eta = "15 to 30 minutes";
                }
                else if (_config.AreaSize <= 1)
                {
                    eta = "30 to 60 minutes";
                    colour = Colors.Brown;
                }
                else
                {
                    eta = $"{(int)lHours} to {(int)uHours} hours";
                    colour = Colors.Red;
                }
                lblEstimateCompletionTime.Text = $"{eta} per scenario";
                lblEstimateCompletionTime.Foreground = new SolidColorBrush(colour);
            }
            else
            {
                lblEstimateCompletionTime.Text = "";
            }
        }

        private void UpdateItemPie()
        {
            var keyItems = 1 / 8.0;
            var totalRest = _config.RatioGunpowder + _config.RatioAmmo + _config.RatioHealth + _config.RatioInkRibbons;
            if (totalRest == 0)
                totalRest = 1;

            var remaining = (1 - keyItems) / totalRest;
            var gunpowder = _config.RatioGunpowder * remaining;
            var ammo = _config.RatioAmmo * remaining;
            var health = _config.RatioHealth * remaining;
            var ink = _config.RatioInkRibbons * remaining;

            pieItemRatios.Records.Clear();
            pieItemRatios.Records.Add(new PieChart.Record()
            {
                Name = "Keys",
                Value = keyItems,
                Color = Colors.LightBlue
            });
            pieItemRatios.Records.Add(new PieChart.Record()
            {
                Name = "Gunp...",
                Value = gunpowder,
                Color = Colors.Gray
            });
            pieItemRatios.Records.Add(new PieChart.Record()
            {
                Name = "Ammo",
                Value = ammo,
                Color = Colors.IndianRed
            });
            pieItemRatios.Records.Add(new PieChart.Record()
            {
                Name = "Health",
                Value = health,
                Color = Colors.SpringGreen
            });
            pieItemRatios.Records.Add(new PieChart.Record()
            {
                Name = "Ink",
                Value = ink,
                Color = Colors.Black
            });
            pieItemRatios.Update();
        }

        private void UpdateEnemyPie()
        {
            pieEnemies.Records.Clear();

            var randomizer = GetRandomizer();
            if (randomizer != null)
            {
                var enemies = randomizer.GetEnemies();
                var index = 0;
                foreach (var enemy in enemies)
                {
                    var colour = (Color)ColorConverter.ConvertFromString(enemy.Colour);
                    var ratio = _config.EnemyRatios.Length > index ?
                        _config.EnemyRatios[index] :
                        0;
                    if (ratio != 0)
                    {
                        pieEnemies.Records.Add(new PieChart.Record()
                        {
                            Name = enemy.Name,
                            Value = ratio,
                            Color = colour
                        });
                    }
                    index++;
                }
            }

            pieEnemies.Update();
        }

        private void UpdateConfig()
        {
            _config.ChangePlayer = chkPlayer.IsChecked == true;
            _config.Player0 = (byte)dropdownPlayer0.SelectedIndex;
            _config.Player1 = (byte)dropdownPlayer1.SelectedIndex;
            _config.SwapCharacters = chkSwapCharacters.IsChecked == true;

            _config.EnabledWeapons = listWeapons.Values;

            _config.EnemyRatios = listEnemies.ItemsSource
                .Cast<SliderListItem>()
                .Select(x => (byte)x.Value)
                .ToArray();

            _config.RandomEnemySkins = chkEnemySkins.IsChecked == true;
            _config.EnabledEnemySkins = listEnemySkins.Values;

            _config.RandomNPCs = chkRngChars.IsChecked == true;
            _config.EnabledNPCs = listNPCs.Values;

            _config.RandomBgm = chkRngBgm.IsChecked == true;
            _config.EnabledBGMs = listBGMs.Values;

            _config.RandomDoors = chkRngDoors.IsChecked == true;
            _config.PrioritiseCutscenes = chkPrioritiseCutscenes.IsChecked == true;
            _config.Segmented = chkProtectSoftLock.IsChecked == true || _config.RandomDoors;
            _config.HiddenKeyItems = chkHiddenKeyItems.IsChecked == true;

            _config.RandomEnemies = chkRngEnemies.IsChecked == true;
            _config.AllowEnemiesAnyRoom = chkEnemyRestrictedRooms.IsChecked == true;
            _config.EnemyQuantity = (byte)sliderEnemyCount.Value;
            _config.EnemyDifficulty = (byte)sliderEnemyDifficulty.Value;
            _config.EnemyRooms = (byte)sliderEnemyRooms.Value;

            _config.RandomItems = chkRngItems.IsChecked == true || _config.RandomDoors;
            _config.RandomEnemyPlacement = chkRandomEnemyPlacements.IsChecked == true;
            _config.ShuffleItems = chkCustomItemPool.IsChecked != true && !_config.RandomDoors;
            _config.AlternativeRoutes = chkAlternativeRoute.IsChecked == true;
            _config.IncludeDocuments = chkIncludeDocuments.IsChecked == true;
            _config.RandomInventory = chkRandomInventory.IsChecked == true;
            _config.Weapon0 = (byte)dropdownWeapon0.SelectedIndex;
            _config.Weapon1 = (byte)dropdownWeapon1.SelectedIndex;

            _config.RatioGunpowder = (byte)sliderGunpowder.Value;
            _config.RatioAmmo = (byte)sliderAmmo.Value;
            _config.RatioHealth = (byte)sliderHealth.Value;
            _config.RatioInkRibbons = (byte)sliderInkRibbons.Value;
            _config.AmmoQuantity = (byte)sliderAmmoQuantity.Value;

            _config.AreaCount = (byte)sliderAreaCount.Value;
            _config.AreaSize = (byte)sliderAreaSize.Value;

            _config.RandomCutscenes = chkCutscenes.IsChecked == true;
            if (SelectedGame == 3)
                _config.RandomCutscenes = false;

            _config.AllowAnyVoice = chkAllowAnyVoice.IsChecked == true;
            _config.ReduceSilences = chkReduceSilences.IsChecked == true;
            _config.RandomEvents = chkRandomEvents.IsChecked == true;
            if (SelectedGame != 1)
                _config.RandomEvents = false;

            _config.GameVariant = (byte)dropdownVariant.SelectedIndex;

            SaveSettings();
        }

        private void UpdateEnabledUi()
        {
            dropdownWeapon0.IsEnabled = chkRandomInventory.IsChecked == true;
            dropdownWeapon1.IsEnabled = chkRandomInventory.IsChecked == true;
            chkAlternativeRoute.IsEnabled = chkRngDoors.IsChecked != true;
            chkProtectSoftLock.IsEnabled = chkRngDoors.IsChecked != true;
            chkEnemyRestrictedRooms.IsEnabled = chkRandomEnemyPlacements.IsChecked == true;
            sliderEnemyCount.IsEnabled = chkRandomEnemyPlacements.IsChecked == true;
        }

        private IEnumerable<FrameworkElement> GetAllControls(object parent)
        {
            if (!(parent is FrameworkElement c))
                yield break;

            yield return c;

            if (parent is ContentControl cc)
            {
                foreach (var child in GetAllControls(cc.Content))
                {
                    yield return child;
                }
            }
            else if (parent is Panel p)
            {
                foreach (var child in p.Children)
                {
                    foreach (var grandChild in GetAllControls(child))
                    {
                        yield return grandChild;
                    }
                }
            }
            else if (parent is Decorator d)
            {
                foreach (var grandChild in GetAllControls(d.Child))
                {
                    yield return grandChild;
                }
            }
        }

        private void RandomizeSeed_Click(object sender, RoutedEventArgs e)
        {
            RandomizeSeed();
            UpdateUi();
        }

        private void RandomizeSeed()
        {
            _config.Version = RandoConfig.LatestVersion;
            _config.Seed = _random.Next(0, RandoConfig.MaxSeed);
            SaveSettings();
        }

        private void RandomizeConfig_Click(object sender, RoutedEventArgs e)
        {
            _config.Version = RandoConfig.LatestVersion;
            _config.Seed = _random.Next(0, RandoConfig.MaxSeed);
            _config.Player0 = (byte)_random.Next(0, dropdownPlayer0.Items.Count);
            _config.Player1 = (byte)_random.Next(0, dropdownPlayer1.Items.Count);
            _config.EnemyDifficulty = (byte)_random.Next(0, 4);
            _config.AmmoQuantity = (byte)_random.Next(0, 8);
            _config.RatioGunpowder = (byte)_random.Next(0, 32);
            _config.RatioAmmo = (byte)_random.Next(0, 32);
            _config.RatioHealth = (byte)_random.Next(0, 32);
            _config.RatioInkRibbons = (byte)_random.Next(0, 32);
            _config.ShuffleItems = false;
            _config.AreaCount = (byte)_random.Next(1, 4);
            SaveSettings();
            UpdateUi();
        }

        private static bool TryExtractSeedFromText(string text, out string seed)
        {
            var m = Regex.Match(text, @"R\d\d\d-[A-Z0-9]{4}-[A-Z0-9]+", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                seed = m.Value;
                return true;
            }
            seed = null;
            return false;
        }

        private async void btnSeedCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(txtSeed.Text);
            if (sender is Button b)
            {
                var guid = Guid.NewGuid();
                lock (b)
                {
                    b.Tag = guid;
                    b.Content = "Copied!";
                }
                await Task.Delay(3000);
                lock (b)
                {
                    if (guid.Equals(b.Tag))
                    {
                        b.Content = "Copy";
                    }
                }
            }
        }

        private void btnSeedPaste_Click(object sender, RoutedEventArgs e)
        {
            var clipboard = Clipboard.GetText();
            if (TryExtractSeedFromText(clipboard, out var seed))
            {
                txtSeed.Text = seed;
            }
            else
            {
                ShowFailedMessage("Failed to paste seed", "No valid seed detected in clipboard. Try copying it again.");
            }
        }

        private void txtSeed_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suspendEvents)
                return;

            var txt = txtSeed.Text;
            if (TryExtractSeedFromText(txt, out var seed))
            {
                txt = seed;
            }

            foreach (var change in e.Changes)
            {
                if (change.AddedLength > 0 && change.RemovedLength > 0)
                {
                }
                else if (change.AddedLength > 0)
                {
                    if (change.Offset + 1 < txt.Length)
                    {
                        txt = txt.Remove(change.Offset + 1, 1);
                    }
                }
                else if (change.RemovedLength > 0)
                {
                    txt = txt.Insert(change.Offset, "0");
                }
            }

            _config = RandoConfig.FromString(txt);
            var caretIndex = txtSeed.CaretIndex;

            if (_config.Game != (SelectedGame + 1))
            {
                if (_config.Game >= 1 && _config.Game <= 4)
                {
                    SelectedGame = _config.Game - 1;
                    _config = RandoConfig.FromString(txt);
                }
                else
                {
                    _config.Game = (byte)(SelectedGame + 1);
                }
            }
            if (_config.Game != 2)
            {
                _config.Scenario = 0;
                if (_config.Game != 1)
                {
                    _config.SwapCharacters = false;
                }
                _config.RandomEvents = false;
            }

            UpdateUi();
            txtSeed.Text = _config.ToString();
            txtSeed.CaretIndex = Math.Min(caretIndex, txtSeed.Text.Length);
        }

        private ReInstallConfig GetInstallConfig(int index)
        {
            SaveSettings();

            var config = new ReInstallConfig();
            config.EnableCustomContent = !_settings.DisableCustomContent;
            config.RandomizeTitleVoice = _settings.RandomizeTitleVoice;
            config.MaxInventorySize = _settings.MaxInventorySize;
            config.DoorSkip = _settings.DoorSkip;
            config.BgmVolume = index switch
            {
                0 => _settings.BgmVolume1,
                1 => _settings.BgmVolume2,
                2 => _settings.BgmVolume3,
                _ => 1.0f
            };
            config.InstallPath = index switch
            {
                0 => _settings.GamePath1,
                1 => _settings.GamePath2,
                2 => _settings.GamePath3,
                3 => _settings.GamePathCv,
                _ => ""
            };
            return config;
        }

        private bool ValidateGameData(BaseRandomiser randomizer, string gamePath, string game)
        {
            if (!Path.IsPathRooted(gamePath) || !Directory.Exists(gamePath))
            {
                var msg = $"You have not specified an {game} game directory that exists.";
                ShowGenerateFailedMessage(msg);
                return false;
            }

            if (!randomizer.ValidateGamePath(gamePath))
            {
                if (!ShowGamePathWarning(gamePath))
                {
                    return false;
                }
            }

            try
            {
                var err = randomizer.DoIntegrityCheck(gamePath);
                if (err == 3)
                {
                    ShowFailedMessage("Integrity Check Failed", "Director's cut camera angles are not supported.\n" +
                        $"Rename your bio1dc folder to something else.");
                }
                else if (err == 2)
                {
                    ShowFailedMessage("Integrity Check Failed", "One or more of your room files are missing.\n" +
                        $"Check that your {game} installation is integral.");
                }
                else if (err == 1)
                {
                    ShowFailedMessage("Integrity Check Failed", "One or more of your room files did not match the original version.\n" +
                        "If you have installed over another mod, issues may occur so proceed with caution!");
                }
            }
            catch (Exception ex)
            {
                ShowFailedMessage("Failed to do integrity check!", ex.Message);
            }
            return true;
        }

        private bool ValidateGamePaths()
        {
            if (_settings.GameEnabled1)
            {
                var r = GetRandomizer(0);
                if (!ValidateGameData(r, _settings.GamePath1, "RE1"))
                {
                    return false;
                }
            }
            if (_settings.GameEnabled2)
            {
                var r = GetRandomizer(1);
                if (!ValidateGameData(r, _settings.GamePath2, "RE2"))
                {
                    return false;
                }
            }
            if (_settings.GameEnabled3)
            {
                var r = GetRandomizer(2);
                if (!ValidateGameData(r, _settings.GamePath3, "RE3"))
                {
                    return false;
                }
            }
            return true;
        }

        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            if (!ValidateGamePaths())
            {
                return;
            }

            var btn = (Button)sender;
            try
            {
                btn.Content = "Generating...";
                IsEnabled = false;
                var randomiser = GetRandomizer();
                await Task.Run(() => randomiser.Generate(_config, new RandoProgress(this)));
                progressLabel.Text = string.Empty;
                RandoAppSettings.LogGeneration(_config.ToString(), GetGameLocation());
                ShowGenerateCompleteMessage();
            }
            catch (Exception ex)
            {
                ShowMessageForException(ex);
            }
            finally
            {
                btn.Content = "Generate";
                IsEnabled = true;
                UpdateLogButtons();
            }
        }

        private void ShowMessageForException(Exception ex)
        {
            var accessDeniedMessage =
                "BioRand does not have permission to write to this location.\n" +
                "If your game is installed in 'Program Files', you may need to run BioRand as administrator.";

            while (ex is AggregateException)
                ex = ex.InnerException;

            switch (ex)
            {
                case BioRandVersionException _:
                    ShowGenerateFailedMessage(ex.Message + "\n" + "Click the seed button to pick a new seed.");
                    break;
                case BioRandUserException _:
                    ShowGenerateFailedMessage(ex.Message);
                    break;
                case UnauthorizedAccessException _:
                    ShowGenerateFailedMessage(accessDeniedMessage);
                    break;
                case Exception _ when IsTypicalException(ex):
                    ShowGenerateFailedMessage("An error occured during generation.\nPlease report this seed and try another.");
                    break;
                default:
                    ShowGenerateFailedMessage(ex.Message + "\n" + "Please report this seed and try another.");
                    break;
            }
        }

        private static bool IsTypicalException(Exception ex)
        {
            if (ex is ArgumentOutOfRangeException) return true;
            if (ex is ArgumentNullException) return true;
            if (ex is ArgumentException) return true;
            if (ex is IndexOutOfRangeException) return true;
            if (ex is InvalidOperationException) return true;
            if (ex is NullReferenceException) return true;
            return false;
        }

        private void ShowGenerateFailedMessage(string message)
        {
            ShowFailedMessage("Failed to Generate", message);
        }

        private void ShowFailedMessage(string title, string message)
        {
            MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private bool ShowGamePathWarning(string game)
        {
            var title = $"Incorrect {game} location";
            var msg = $"This directory was not dectected as a valid {game} game directory. Do you want to continue anyway?";
            return MessageBox.Show(this, msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        private void ShowGenerateCompleteMessage()
        {
            var title = "Randomization Complete!";
            var msg = "The Randomizer mod has successfully been generated. Run the game and choose \"BioRand: A Resident Evil Randomizer\" from the mod selection." +
                "\n\nClick the log buttons to see where items were placed.";
            MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CheckForNewVersion()
        {
            Task.Run(async () =>
            {
                try
                {
                    var version = await GetLatestVersionAsync();
                    if (version > CurrentVersion)
                    {
                        Dispatcher.Invoke(() => versionBox.Visibility = Visibility.Visible);
                    }
                }
                catch
                {
                }
            });
        }

        private async Task<Version> GetLatestVersionAsync()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BioRand", CurrentVersion.ToString()));
            var response = await client.GetAsync("https://api.github.com/repos/biorand/classic/releases/latest");
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var body = JsonSerializer.Deserialize<VersionCheckBody>(jsonResponse);
                var tagName = body.tag_name;
                return Version.Parse(tagName.Substring(1));
            }
            throw new Exception("Unable to get latest version");
        }

        private void UpdateLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/biorand/classic/releases");
        }

        private void ReportIssue_Click(object sender, RoutedEventArgs e)
        {
            var version = UrlEncoder.Default.Encode(string.Format("{0} ({1})", Program.CurrentVersionNumber, Program.GitHash));
            var seed = UrlEncoder.Default.Encode(_config.ToString());
            if (HasCustomContent())
            {
                version += " with custom content";
            }
            Process.Start($"https://github.com/biorand/classic/issues/new?template=bug_report.yml&version={version}&seed={seed}");
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink hyperlink)
            {
                Process.Start(hyperlink.NavigateUri.ToString());
            }
        }

        private void gameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var index = (gameListView.SelectedItem as GameMenuItem)?.Game;
            switch (index)
            {
                case 0:
                    _config = RandoConfig.FromString(_settings.Seed1);
                    break;
                case 1:
                    _config = RandoConfig.FromString(_settings.Seed2);
                    break;
                case 2:
                    _config = RandoConfig.FromString(_settings.Seed3);
                    break;
                case 3:
                    _config = RandoConfig.FromString(_settings.SeedCv);
                    break;
            }

            _randomizer = null;

            using (SuspendEvents())
            {
                if (index == -3)
                {
                    panelNew.Visibility = Visibility.Visible;
                    panelInfo.Visibility = Visibility.Hidden;
                    panelConfig.Visibility = Visibility.Hidden;
                    panelRando.Visibility = Visibility.Hidden;
                }
                else if (index == -2)
                {
                    panelNew.Visibility = Visibility.Hidden;
                    panelInfo.Visibility = Visibility.Visible;
                    panelConfig.Visibility = Visibility.Hidden;
                    panelRando.Visibility = Visibility.Hidden;
                }
                else if (index == -1)
                {
                    panelNew.Visibility = Visibility.Hidden;
                    panelInfo.Visibility = Visibility.Hidden;
                    panelConfig.Visibility = Visibility.Visible;
                    panelRando.Visibility = Visibility.Hidden;
                }
                else
                {
                    panelNew.Visibility = Visibility.Hidden;
                    panelInfo.Visibility = Visibility.Hidden;
                    panelConfig.Visibility = Visibility.Hidden;
                    panelRando.Visibility = Visibility.Visible;

                    dropdownVariant.Visibility = index == 1 ?
                        Visibility.Visible :
                        Visibility.Hidden;
                    UpdatePlayerDropdowns();
                    UpdateItems();
                    UpdateWeaponList();
                    UpdateEnemies();
                    UpdateEnemySkinList();
                    UpdateCutscenes();
                    UpdateBGMList();
                    UpdateNPCList();
                    UpdateLogButtons();
                }
            }

            if (index >= 0 && index <= 3)
                _config.Game = (byte)(index + 1);
            _settings.LastSelectedGame = index;
            _settings.Save();

            UpdateUi();
        }

        private BaseRandomiser GetRandomizer()
        {
            if (SelectedGame == null)
                return null;
            if (_randomizer == null)
                _randomizer = GetRandomizer(SelectedGame.Value);
            return _randomizer;
        }

        private BaseRandomiser GetRandomizer(int index)
        {
            var installConfig = GetInstallConfig(index);
            switch (index)
            {
                case 0:
                    return new Re1Randomiser(installConfig, new BiorandBgCreator());
                case 1:
                    return new Re2Randomiser(installConfig, new BiorandBgCreator());
                case 2:
                    return new Re3Randomiser(installConfig, new BiorandBgCreator());
                case 3:
                    return new ReCvRandomiser(installConfig, new BiorandBgCreator());
                default:
                    return null;
            }
        }

        private string GetGameLocation()
        {
            switch (SelectedGame)
            {
                case 0:
                    return _settings.GamePath1;
                case 1:
                    return _settings.GamePath2;
                case 2:
                    return _settings.GamePath3;
                case 3:
                    return _settings.GamePathCv;
                default:
                    return null;
            }
        }

        private string GetGameExecutable()
        {
            switch (SelectedGame)
            {
                case 0:
                    return _settings.GameExecutable1;
                case 1:
                    return _settings.GameExecutable2;
                case 2:
                    return _settings.GameExecutable3;
                default:
                    return null;
            }
        }

        private void gameLocation_Validate(object sender, PathValidateEventArgs e)
        {
            if (!Directory.Exists(e.Path))
            {
                e.Message = "Directory does not exist!";
                e.IsValid = false;
                return;
            }

            var index = int.Parse((string)((GameLocationBox)sender).Tag);
            var randomizer = GetRandomizer(index);
            if (!randomizer.ValidateGamePath(e.Path))
            {
                e.Message = "Directory not valid for this game!";
                e.IsValid = false;
                return;
            }

            var integrityError = randomizer.DoIntegrityCheck(e.Path);
            if (integrityError == 2)
            {
                e.Message = "One or more of your room files are missing.";
                e.IsValid = false;
                return;
            }
            else if (integrityError == 1)
            {
                e.Message = "One or more of your room files did not match the original version.";
                e.IsValid = false;
                return;
            }

            e.IsValid = true;
            e.Message = "Directory looks good!";
        }

        private void gameLocationCV_Validate(object sender, PathValidateEventArgs e)
        {
            if (!File.Exists(e.Path))
            {
                e.Message = "File does not exist!";
                e.IsValid = false;
                return;
            }

            if (new FileInfo(e.Path).Length != 1883996160)
            {
                e.Message = "Image is not the expected version!";
                e.IsValid = false;
                return;
            }


            e.IsValid = true;
            e.Message = "ISO looks good!";
        }

        private void gameLocation_Changed(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void globalCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_suspendEvents)
            {
                SaveSettings();
            }
        }

        private int? SelectedGame
        {
            get
            {
                var index = (gameListView.SelectedItem as GameMenuItem)?.Game;
                if (index > 3)
                    return null;
                return index;
            }
            set
            {
                value = value ?? -3;
                foreach (GameMenuItem item in gameListView.Items)
                {
                    if (item.Game == value)
                    {
                        gameListView.SelectedItem = item;
                    }
                }
            }
        }

        private void btnLog0_Click(object sender, RoutedEventArgs e)
        {
            ViewLog(0);
        }

        private void btnLog1_Click(object sender, RoutedEventArgs e)
        {
            ViewLog(1);
        }

        private void UpdatePlayerDropdowns()
        {
            var randomizer = GetRandomizer();
            var dropdownLabels = new[] { lblPlayer0, lblPlayer1 };
            var dropdowns = new[] { dropdownPlayer0, dropdownPlayer1 };
            for (int i = 0; i < 2; i++)
            {
                var label = dropdownLabels[i];
                var dropdown = dropdowns[i];
                label.Text = $"{randomizer.GetPlayerName(i)} becomes:";
                var items = new List<string>() { "Random" };
                items.AddRange(randomizer.GetPlayerCharacters(i));
                dropdown.ItemsSource = items;
                if (dropdown.SelectedIndex == -1)
                    dropdown.SelectedIndex = 0;
            }

            if (SelectedGame != 2)
            {
                lblPlayer1.Visibility = Visibility.Visible;
                dropdownPlayer1.Visibility = Visibility.Visible;
            }
            else
            {
                lblPlayer1.Visibility = Visibility.Collapsed;
                dropdownPlayer1.Visibility = Visibility.Collapsed;
            }
            chkSwapCharacters.Visibility = SelectedGame == 0 || SelectedGame == 1 ?
                Visibility.Visible :
                Visibility.Hidden;
            chkPlayer.Visibility = Visibility.Visible;
        }

        private void UpdateItems()
        {
            if (SelectedGame == 2 || SelectedGame == 3)
            {
                sliderGunpowder.Visibility = Visibility.Visible;
            }
            else
            {
                _config.RatioGunpowder = 0;
                sliderGunpowder.Visibility = Visibility.Hidden;
            }
        }

        private void UpdateEnemies()
        {
            var randomizer = GetRandomizer();
            var items = new List<SliderListItem>();
            foreach (var enemy in randomizer.GetEnemies())
            {
                items.Add(new SliderListItem(enemy.Name, 4, 7));
            }
            listEnemies.ItemsSource = items;
            chkRandomEnemyPlacements.Visibility = Visibility.Visible;
        }

        private void UpdateEnemySkinList()
        {
            if (SelectedGame == 3)
            {
                chkEnemySkins.Visibility = Visibility.Hidden;
            }
            else
            {
                var randomizer = GetRandomizer();
                var enemySkins = randomizer.GetEnemySkins();
                listEnemySkins.Names = enemySkins
                    .Select(x => x.Name)
                    .ToArray();
                listEnemySkins.ToolTips = enemySkins
                    .Select(x => x.ToolTip)
                    .ToArray();
                chkEnemySkins.Visibility = Visibility.Visible;
            }
        }

        private void UpdateCutscenes()
        {
            if (SelectedGame == 3)
            {
                chkCutscenes.Visibility = Visibility.Hidden;
            }
            else
            {
                chkCutscenes.Visibility = Visibility.Visible;
                if (SelectedGame == 1)
                {
                    chkRandomEvents.Visibility = Visibility.Visible;
                }
                else
                {
                    chkRandomEvents.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UpdateWeaponList()
        {
            var randomizer = GetRandomizer();
            listWeapons.Names = randomizer
                .GetWeaponNames()
                .ToArray();
        }

        private void UpdateNPCList()
        {
            var randomizer = GetRandomizer();
            listNPCs.Names = randomizer
                .GetNPCs()
                .Select(x => x.ToActorString())
                .ToArray();
        }

        private void UpdateBGMList()
        {
            if (SelectedGame == 3)
            {
                chkRngBgm.Visibility = Visibility.Collapsed;
            }
            else
            {
                var randomizer = GetRandomizer();
                listBGMs.Names = randomizer.GetMusicAlbums();
                chkRngBgm.Visibility = Visibility.Visible;
            }
        }

        private void UpdateLogButtons()
        {
            var randomizer = GetRandomizer();
            var buttons = new[] { btnLog0, btnLog1 };
            for (int i = 0; i < 2; i++)
            {
                var btn = buttons[i];
                var path = GetLogPath(i);
                btn.IsEnabled = File.Exists(path);
                var hyperlink = btn.Inlines.FirstInline as Hyperlink;
                hyperlink.Inlines.Clear();
                hyperlink.Inlines.Add($"{randomizer.GetPlayerName(i)} Log");
                btn.Visibility = i == 1 && SelectedGame >= 2 ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void ViewLog(int player)
        {
            var path = GetLogPath(player);
            if (File.Exists(path))
            {
                Process.Start(path);
            }
            else
            {
                ShowFailedMessage("View Log", $"Unable to find log file: '{path}'");
            }
        }

        private string GetLogPath(int player)
        {
            var location = GetGameLocation();
            if (location == null)
                return null;

            if (SelectedGame == 3)
                location = Path.GetDirectoryName(location);

            var path = Path.Combine(location, "mod_biorand", $"log_pl{player}.txt");
            return path;
        }

        private void enemyContextAllNone_Click(object sender, RoutedEventArgs e)
        {
            using (SuspendEvents())
            {
                foreach (SliderListItem item in listEnemies.ItemsSource)
                {
                    item.Value = 0;
                }
            }
            UpdateConfig();
            UpdateUi();
        }

        private void enemyContextAllMax_Click(object sender, RoutedEventArgs e)
        {
            using (SuspendEvents())
            {
                foreach (SliderListItem item in listEnemies.ItemsSource)
                {
                    item.Value = item.Maximum;
                }
            }
            UpdateConfig();
            UpdateUi();
        }

        private void enemyContextRandom_Click(object sender, RoutedEventArgs e)
        {
            using (SuspendEvents())
            {
                var items = listEnemies.ItemsSource.Cast<SliderListItem>();
                var numItems = items.Count();
                var numChecked = _random.Next(0, numItems);
                var checkedItems = items.Shuffle(_random).Take(numChecked).ToArray();
                foreach (SliderListItem item in listEnemies.ItemsSource)
                {
                    item.Value = _random.Next(0, (int)item.Maximum);
                }
            }
            UpdateConfig();
            UpdateUi();
        }

        private SuspendEventsScope SuspendEvents()
        {
            return new SuspendEventsScope(this);
        }

        private readonly struct SuspendEventsScope : IDisposable
        {
            private readonly MainWindow _mainWindow;

            public SuspendEventsScope(MainWindow mainWindow)
            {
                _mainWindow = mainWindow;
                _mainWindow._suspendEvents = true;
            }

            public void Dispose()
            {
                _mainWindow._suspendEvents = false;
            }
        }

        private class RandoProgress : IRandoProgress
        {
            private readonly MainWindow _mainWindow;
            private readonly LinkedList<(int eventId, string message)> _events = new LinkedList<(int eventId, string message)>();
            private readonly object _sync = new object();
            private int _nextEventId;

            public RandoProgress(MainWindow mainWindow)
            {
                _mainWindow = mainWindow;
            }

            private void SetLabel(string message)
            {
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.progressLabel.Text = message;
                });
            }

            public IDisposable BeginTask(int? player, string message)
            {
                message += "...";

                int eventId;
                lock (_sync)
                {
                    SetLabel(message);
                    _events.AddLast((_nextEventId, message));
                    eventId = _nextEventId++;
                }
                return new RandoProgressTask(this, eventId);
            }

            private void EndTask(int eventId)
            {
                lock (_sync)
                {
                    var node = _events.First;
                    while (node != null)
                    {
                        if (node.Value.eventId == eventId)
                        {
                            if (node == _events.Last)
                            {
                                if (node.Previous == null)
                                    SetLabel("");
                                else
                                    SetLabel(node.Previous.Value.message);
                            }
                            _events.Remove(node);
                        }
                        node = node.Next;
                    }
                }
            }

            private readonly struct RandoProgressTask : IDisposable
            {
                private readonly RandoProgress _parent;
                private readonly int _eventId;

                public RandoProgressTask(RandoProgress parent, int eventId)
                {
                    _parent = parent;
                    _eventId = eventId;
                }

                public void Dispose()
                {
                    _parent.EndTask(_eventId);
                }
            }
        }



        private void btnStartRE_Click(object sender, RoutedEventArgs e)
        {
            var executableDirectory = GetGameLocation();
            var executablePath = Path.Combine(GetGameLocation(), GetGameExecutable());

            if (!File.Exists(executablePath))
            {
                MessageBox.Show($"Unable to start the game.{Environment.NewLine}{Environment.NewLine}The path '{executablePath}' does not exists.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // This helps prevent "The operation was cancelled by the user" messages by unblocking
            // the exe file that Windows marks when extracting the zip file
            Kernel32.DeleteFile(executablePath + ":Zone.Identifier");

            try
            {
                var processStartInfo = new ProcessStartInfo() { WorkingDirectory = executableDirectory, FileName = Path.GetFileName(executablePath) };
                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to start the game.{Environment.NewLine}{Environment.NewLine}{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        private void BrowseCustomContentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var directory = new DirectoryInfo(RandoAppSettings.GetCustomContentDirectory());
                if (!directory.Exists)
                {
                    directory.Create();
                }
                Process.Start(directory.FullName);
            }
            catch
            {
                ShowFailedMessage("Biorand", "Unable to create or open custom content directory.");
            }
        }

        private bool HasCustomContent()
        {
            if (chkEnableCustomContent.IsChecked != true)
            {
                return false;
            }
            try
            {
                var directory = new DirectoryInfo(RandoAppSettings.GetCustomContentDirectory());
                return directory.GetDirectories().Any();
            }
            catch
            {
                return false;
            }
        }
    }

    public class VersionCheckBody
    {
        public string tag_name { get; set; }
    }

    public class GameMenuItem
    {
        public ImageSource Image { get; set; }
        public int Game { get; set; }
    }

    public class SliderListItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _text;
        private double _value;
        private double _maximum;

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        public double Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        public double Maximum
        {
            get => _maximum;
            set
            {
                if (_maximum != value)
                {
                    _maximum = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Maximum)));
                }
            }
        }

        public SliderListItem(string text, double value, int max)
        {
            Text = text;
            Value = value;
            Maximum = max;
        }
    }
}
