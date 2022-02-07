﻿using Lively.Common.Helpers.Pinvoke;
using Lively.Grpc.Client;
using Lively.UI.WinUI.ViewModels;
using Lively.UI.WinUI.Views;
using Lively.UI.WinUI.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using SettingsUI.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Lively.UI.WinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly SettingsViewModel settingsVm;
        private readonly IDesktopCoreClient desktopCore;
        private readonly IUserSettingsClient userSettings;

        public MainWindow(IDesktopCoreClient desktopCore, IUserSettingsClient userSettings, SettingsViewModel settingsVm)
        {
            this.settingsVm = settingsVm;
            this.desktopCore = desktopCore;
            this.userSettings = userSettings;

            this.InitializeComponent();
            this.audioSlider.Value = settingsVm.GlobalWallpaperVolume;
            UpdateAudioSliderIcon(settingsVm.GlobalWallpaperVolume);
            this.controlPanelLabel.Label = $"{desktopCore.Wallpapers.Count} active wallpaper(s)";
            desktopCore.WallpaperChanged += DesktopCore_WallpaperChanged;
            desktopCore.WallpaperError += DesktopCore_WallpaperError;
            //App startup is slower if done in NavView_Loaded..
            CreateMainMenu();
            NavViewNavigate(NavPages.library);

            //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/6070
            //ExtendsContentIntoTitleBar = true;
            //SetTitleBar(TitleBar);

            //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/4056
            this.Title = "Lively Wallpaper (WinUI)";
            this.SetIconEx("appicon.ico");

            _ = StdInListener();
        }

        private void DesktopCore_WallpaperError(object sender, Exception e)
        {
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                infoBar.IsOpen = true;
                infoBar.ActionButton = new HyperlinkButton
                {
                    Content = "More information",
                    NavigateUri = new Uri("https://github.com/rocksdanister/lively/wiki/Common-Problems"),
                };
                infoBar.Title = "Error";
                infoBar.Message = e.Message;
                infoBar.Severity = InfoBarSeverity.Error;
            });
        }

        private void DesktopCore_WallpaperChanged(object sender, EventArgs e)
        {
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                //wallpaper focus steal fix.
                if (this.Visible)
                {
                    this.Activate();
                }
                controlPanelLabel.Label = $"{desktopCore.Wallpapers.Count} active wallpaper(s)";
            });
        }

        public void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                CreateSettingsMenu();
                NavViewNavigate(NavPages.settingsGeneral);
            }
            else if (args.InvokedItemContainer != null)
            {
                var navItemTag = args.InvokedItemContainer.Tag.ToString();
                NavigatePage(navItemTag);
            }
        }

        public void NavViewNavigate(NavPages item)
        {
            string tag = item switch
            {
                NavPages.library => "library",
                NavPages.gallery => "gallery",
                NavPages.settingsGeneral => "general",
                NavPages.settingsPerformance => "performance",
                NavPages.settingsWallpaper => "wallpaper",
                NavPages.settingsSystem => "system",
                _ => "library"
            };
            navView.SelectedItem = navView.MenuItems.First(x => ((NavigationViewItem)x).Tag.ToString() == tag);
            NavigatePage(tag);
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            CreateMainMenu();
            NavViewNavigate(NavPages.library);

            //If audio changed in settings page..
            this.audioSlider.Value = settingsVm.GlobalWallpaperVolume;
        }

        private void NavigatePage(string tag)
        {
            switch (tag)
            {
                case "library":
                    contentFrame.Navigate(typeof(LibraryView), null, new DrillInNavigationTransitionInfo());
                    break;
                case "gallery":
                    contentFrame.Navigate(typeof(GalleryView), null, new DrillInNavigationTransitionInfo());
                    break;
                case "general":
                    contentFrame.Navigate(typeof(SettingsGeneralView), null, new DrillInNavigationTransitionInfo());
                    break;
                case "performance":
                    contentFrame.Navigate(typeof(SettingsPerformanceView), null, new DrillInNavigationTransitionInfo());
                    break;
                case "wallpaper":
                    contentFrame.Navigate(typeof(SettingsWallpaperView), null, new DrillInNavigationTransitionInfo());
                    break;
                case "system":
                    contentFrame.Navigate(typeof(SettingsSystemView), null, new DrillInNavigationTransitionInfo());
                    break;
                default:
                    //TODO
                    break;
            }
        }

        private void AddWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog()
            {
                Title = "Add wallpaper",
                Content = new AddWallpaperView(),
                PrimaryButtonText = "Close",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
            }.ShowAsyncQueue();
        }

        private void ControlPanelButton_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog()
            {
                Title = "Choose display",
                Content = new ScreenLayoutView(),
                PrimaryButtonText = "OK",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
            }.ShowAsyncQueue();
        }

        private void AppBarHelpButton_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog()
            {
                Title = "Help",
                Content = new HelpView(),
                PrimaryButtonText = "OK",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
            }.ShowAsyncQueue();
        }

        private void AppBarAboutButton_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog()
            {
                Title = "About",
                Content = new AboutView(),
                PrimaryButtonText = "OK",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
            }.ShowAsyncQueue();
        }

        private void SliderAudio_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            settingsVm.GlobalWallpaperVolume = (int)e.NewValue;
            UpdateAudioSliderIcon(settingsVm.GlobalWallpaperVolume);
        }

        private void CreateMainMenu()
        {
            navView.MenuItems.Clear();
            navView.FooterMenuItems.Clear();
            navView.IsSettingsVisible = true;
            navCommandBar.Visibility = Visibility.Visible;
            navView.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
            navView.MenuItems.Add(CreateMenu("Library", "library", "\uE8A9"));
            navView.MenuItems.Add(CreateMenu("Gallery", "gallery", "\uE719"));
        }

        private void CreateSettingsMenu()
        {
            navView.MenuItems.Clear();
            navView.FooterMenuItems.Clear();
            navView.IsSettingsVisible = false;
            navCommandBar.Visibility = Visibility.Collapsed;
            navView.IsBackButtonVisible = NavigationViewBackButtonVisible.Visible;
            navView.MenuItems.Add(CreateMenu("General", "general"));
            navView.MenuItems.Add(CreateMenu("Performance", "performance"));
            navView.MenuItems.Add(CreateMenu("Wallpaper", "wallpaper"));
            navView.MenuItems.Add(CreateMenu("System", "system"));
        }

        private void UpdateAudioSliderIcon(double volume) => audioBtn.Icon = audioIcons[(int)Math.Ceiling((audioIcons.Length - 1) * volume / 100)];

        public enum NavPages
        {
            library,
            gallery,
            help,
            settingsGeneral,
            settingsPerformance,
            settingsWallpaper,
            settingsAudio,
            settingsSystem,
            settingsMisc
        }

        //Actually called before window closed!
        //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/5454
        private void Window_Closed(object sender, WindowEventArgs args)
        {
            if (userSettings.Settings.KeepAwakeUI)
            {
                args.Handled = true;
                NativeMethods.ShowWindow(this.GetWindowHandleEx(), (uint)NativeMethods.SHOWWINDOW.SW_HIDE);
            }
            else
            {
                App.ShutDown();
            }
        }

        /// <summary>
        /// std I/O redirect.
        /// </summary>
        private async Task StdInListener()
        {
            try
            {
                await Task.Run(async () =>
                {
                    while (true)
                    {
                        var msg = await Console.In.ReadLineAsync();
                        if (string.IsNullOrEmpty(msg))
                        {
                            //When the redirected stream is closed, a null line is sent to the event handler. 
                            break;
                        }
                        var args = msg.Split(' ');
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (args[0].Equals("WM", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args[1].Equals("SHOW", StringComparison.OrdinalIgnoreCase))
                                {
                                    //this.Show();
                                    NativeMethods.ShowWindow(this.GetWindowHandleEx(), (uint)NativeMethods.SHOWWINDOW.SW_SHOW);
                                }
                            }
                            else if (args[0].Equals("LM", StringComparison.OrdinalIgnoreCase))
                            {

                                /*
                                if (args[1].Equals("SHOWCONTROLPANEL", StringComparison.OrdinalIgnoreCase))
                                {
                                    //this.ShowControlPanelDialog();
                                }
                                else if (args[1].Equals("SHOWCUSTOMISEPANEL", StringComparison.OrdinalIgnoreCase))
                                {
                                    var items = desktopCore.Wallpapers.Where(x => x.LivelyPropertyCopyPath != null);
                                    if (items.Count() != 0)
                                    {
                                        //Usually this msg is sent when span/duplicate layout mode.
                                        var model = libraryVm.LibraryItems.FirstOrDefault(x => items.First().LivelyInfoFolderPath == x.LivelyInfoFolderPath);
                                        if (model != null)
                                        {
                                            var settingsWidget = new LivelyPropertiesTrayWidget(model);
                                            settingsWidget.Show();
                                        }
                                    }
                                }
                                */
                            }
                        });
                    }
                });
            }
            catch { }
        }

        #region helpers

        private readonly FontIcon[] audioIcons =
{
            new FontIcon(){ Glyph = "\uE74F" },
            new FontIcon(){ Glyph = "\uE992" },
            new FontIcon(){ Glyph = "\uE993" },
            new FontIcon(){ Glyph = "\uE994" },
            new FontIcon(){ Glyph = "\uE995" },
        };

        private NavigationViewItem CreateMenu(string menuName, string tag, string glyph = "")
        {
            var item = new NavigationViewItem
            {
                Name = menuName,
                Content = menuName,
                Tag = tag,
            };
            if (!string.IsNullOrEmpty(glyph))
            {
                item.Icon = new FontIcon()
                {
                    Glyph = glyph
                };
            }
            return item;
        }

        #endregion //helpers
    }
}
