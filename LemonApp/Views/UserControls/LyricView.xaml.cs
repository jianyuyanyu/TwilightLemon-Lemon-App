﻿using EleCho.WpfSuite;
using LemonApp.Common.Funcs;
using LemonApp.MusicLib.Lyric;
using LemonApp.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using LemonApp.MusicLib.Abstraction.Entities;
using LemonApp.Common.Configs;
using CommunityToolkit.Mvvm.Input;

//TODO: 提供效果选择
namespace LemonApp.Views.UserControls
{
    public class LrcItem(TextBlock container, TextBlock main,BoxPanel box)
    {
        public double Time { get; set; }
        public string Lyric { get; set; } = string.Empty;
        /// <summary>
        /// Container TextBlock
        /// </summary>
        public TextBlock LrcTb { get; set; }=container;
        /// <summary>
        /// Main Lyric Tb
        /// </summary>
        public TextBlock LrcMain { get; set; } = main;
        public TextBlock? LrcTrans { get; set; }
        public TextBlock? Romaji { get; set; }
        public BoxPanel Box { get; set; } = box;
    }

    /// <summary>
    /// LyricView.xaml 的交互逻辑
    /// </summary>
    public partial class LyricView : UserControl
    {
        private readonly HttpClient? _hc;
        private readonly SettingsMgr<LyricOption> _settings;
        private readonly UIResourceService _uiResourceService;
        private readonly List<LrcItem> LrcItems = [];
        private LrcItem? _currentLrc = null;

        public LyricView(IHttpClientFactory httpClientFactory,
            UIResourceService uiResourceService,
            AppSettingService appSettingsService)
        {
            InitializeComponent();

            _settings = appSettingsService.GetConfigMgr<LyricOption>();
            _settings.OnDataChanged += Settings_OnDataChanged;
            _hc = httpClientFactory.CreateClient(App.PublicClientFlag);
            _uiResourceService = uiResourceService;
            _uiResourceService.OnColorModeChanged += UiResourceService_OnColorModeChanged;

            SizeChanged += LyricView_SizeChanged;
            IsVisibleChanged += LyricView_IsVisibleChanged;
            Loaded += LyricView_Loaded;
        }

        #region Self-adaption
        /// <summary>
        /// respond to LyricOption changed
        /// </summary>
        private async void Settings_OnDataChanged()
        {
            await _settings.LoadAsync();
            ApplySettings();
        }
        private void LyricView_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            UpdateColorMode();
        }
        private void ApplySettings()
        {
            this.Dispatcher.Invoke(() =>
            {
                IsShowTranslation = _settings?.Data?.ShowTranslation is true;
                IsShowRomaji = _settings?.Data?.ShowRomaji is true;
                SetFontSize(_settings?.Data?.FontSize ?? (int)LyricFontSize);
                this.FontFamily = new FontFamily(_settings?.Data?.FontFamily ?? "Segou UI");
            });
        }

        private void UpdateColorMode()
        {
            if (Foreground is SolidColorBrush color)
            {
                Hightlighter = new DropShadowEffect() { 
                    BlurRadius = 24, 
                    Color = color.Color, 
                    Opacity = 1, 
                    ShadowDepth = 0, 
                    Direction = 0
                };
            }
        }
        private void UiResourceService_OnColorModeChanged() => UpdateColorMode();

        private async void LyricView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                await Task.Delay(300);//?? 为什么这里不等待会导致TransformToVisual抛出异常
                RefreshCurrentLrcStyle();
            }
        }

        private void LyricView_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshCurrentLrcStyle();
        #endregion
        #region Apperance

        public Thickness LyricMargin = new Thickness(20, 12, 0, 12);
        /// <summary>
        /// 非高亮歌词的透明度
        /// </summary>
        public double LyricOpacity = 0.5;
        /// <summary>
        /// 高亮歌词效果
        /// </summary>
        public Effect? Hightlighter;
        public Effect? NomalTextEffect =  new BlurEffect() { Radius = 8 };
        public Effect? AroundTextEffect =  new BlurEffect() { Radius = 5 };
        /// <summary>
        /// 歌词的文本对齐方式
        /// </summary>
        public TextAlignment TextAlignment = TextAlignment.Left;

        public double LyricFontSize = 24;
        public const double LyricFontSizeDelta = 6;
        public FontWeight NormalTextFontWeight = FontWeights.Normal;
        #endregion


        public event Action<LrcLine,LrcLine?>? OnNextLrcReached;
        public static LrcLine? ToLrcLine(LrcItem? item) =>item==null?null: new()
        {
            Lyric = item?.Lyric ?? "",
            Trans = item?.LrcTrans?.Text ?? "",
            Romaji = item?.Romaji?.Text ?? "",
            Time = double.NaN
        };

        [RelayCommand]
        public void FontSizeUp() => SetFontSize((int)LyricFontSize +2);
        [RelayCommand]
        public void FontSizeDown() => SetFontSize((int)LyricFontSize - 2);
        public void SetFontSize(int size)
        {
            LyricFontSize = size;
            _settings.Data.FontSize = size;
            foreach (var item in LrcItems)
            {
                item.LrcTb!.FontSize = size;
                if (item.Romaji != null)
                    item.Romaji.FontSize = size - LyricFontSizeDelta;
                if (item.LrcTrans != null)
                    item.LrcTrans.FontSize = size - LyricFontSizeDelta;
            }
        }


        public bool IsRomajiAvailable
        {
            get { return (bool)GetValue(IsRomajiAvailableProperty); }
            private set { SetValue(IsRomajiAvailableProperty, value); }
        }

        public static readonly DependencyProperty IsRomajiAvailableProperty =
            DependencyProperty.Register("IsRomajiAvailable", typeof(bool), typeof(LyricView), new PropertyMetadata(false));


        public bool IsTranslationAvailable
        {
            get { return (bool)GetValue(IsTranslationAvailableProperty); }
            private set { SetValue(IsTranslationAvailableProperty, value); }
        }

        public static readonly DependencyProperty IsTranslationAvailableProperty =
            DependencyProperty.Register("IsTranslationAvailable", typeof(bool), typeof(LyricView), new PropertyMetadata(false));

        public bool IsShowTranslation
        {
            get => (bool)GetValue(IsShowTranslationProperty);
            set => SetValue(IsShowTranslationProperty, value);
        }

        public static readonly DependencyProperty IsShowTranslationProperty =
            DependencyProperty.Register("IsShowTranslation", typeof(bool), typeof(LyricView), new PropertyMetadata(true, OnIsShowTranslationChanged));

        private static void OnIsShowTranslationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LyricView view)
            {
                view.SetShowTranslation(e.NewValue is true);
            }
        }

        public bool IsShowRomaji
        {
            get => (bool)GetValue(IsShowRomajiProperty);
            set => SetValue(IsShowRomajiProperty, value);
        }

        public static readonly DependencyProperty IsShowRomajiProperty =
            DependencyProperty.Register("IsShowRomaji", typeof(bool), typeof(LyricView), new PropertyMetadata(true, OnIsShowRomajiChanged));

        private static void OnIsShowRomajiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LyricView view)
            {
                view.SetShowRomaji(e.NewValue is true);
            }
        }

        public void SetShowTranslation(bool show)
        {
            _settings.Data.ShowTranslation = show;
            foreach (var item in LrcItems)
            {
                if (item.LrcTrans != null)
                {
                    item.LrcTrans.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
        public void SetShowRomaji(bool show)
        {
            _settings.Data.ShowRomaji = show;
            foreach (var item in LrcItems)
            {
                if (item.Romaji != null)
                {
                    item.Romaji.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private string? _handlingMusic = null;
        public async Task LoadFromMusic(Music m)
        {
            Reset();
            _handlingMusic = m.MusicID;
            var path = CacheManager.GetCachePath(CacheManager.CacheType.Lyric);
            path = System.IO.Path.Combine(path, m.MusicID + ".json");
            if (await Settings.LoadFromJsonAsync<LocalLyricData>(path, false) is { } local)
            {
                LoadLrc(local);
            }
            else
            {
                if (_hc == null) return;
                var ly = await GatitoGetLyric.GetTencLyricAsync(_hc, m);
                await Settings.SaveAsJsonAsync(ly, path, false);
                if (_handlingMusic == m.MusicID)//防止异步加载时已经切换歌曲
                    LoadLrc(ly);
            }
        }
        private void Reset()
        {
            scrollviewer.BeginAnimation(ScrollViewerUtils.VerticalOffsetProperty, null);
            scrollviewer.ScrollToTop();
            LyricPanel.Children.Clear();
            LrcItems.Clear();
            GC.Collect();
        }
        private void LoadLrc(LocalLyricData data)
        {
            IsTranslationAvailable = data.HasTrans;
            IsRomajiAvailable = data.HasRomaji;

            //占位
            LyricPanel.Children.Add(new Border() { Height = 200, Background = Brushes.Transparent });
            foreach (var line in data.LyricData)
            {
                //Container
                TextBlock tb = new()
                {
                    FontSize = LyricFontSize,
                    //Foreground = NormalLrcColor,
                    TextAlignment = TextAlignment,
                    Opacity = LyricOpacity,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.None,
                    Effect = NomalTextEffect,
                    FontWeight = NormalTextFontWeight
                };
                //Romaji
                TextBlock? romaji = null;
                if (line.Romaji != null)
                {
                    romaji = new()
                    {
                        Text = line.Romaji,
                        Opacity = 0.5,
                        FontSize = LyricFontSize - LyricFontSizeDelta,
                        FontWeight = FontWeights.Regular,
                        //Foreground = NormalLrcColor,
                        TextWrapping = TextWrapping.Wrap,
                        TextTrimming = TextTrimming.None
                    };
                    if (!IsShowRomaji) romaji.Visibility = Visibility.Collapsed;
                    tb.Inlines.Add(romaji);
                    tb.Inlines.Add(new LineBreak());
                }
                //Main Lyric
                TextBlock lyric = new()
                {
                    Text = line.Lyric,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.None
                };
                var box = new BoxPanel() {
                    Margin = LyricMargin
                };
                box.Children.Add(tb);
                LrcItem item = new(tb,lyric,box);
                item.Romaji = romaji;
                item.Time = line.Time;
                item.LrcMain = lyric;
                item.Lyric = line.Lyric;
                tb.Inlines.Add(lyric);
                //Translation
                if (line.Trans != null)
                {
                    TextBlock trans = new()
                    {
                        FontWeight = FontWeights.Regular,
                        Text = line.Trans,
                        Opacity = 0.5,
                        FontSize = LyricFontSize - LyricFontSizeDelta,
                        //Foreground = NormalLrcColor,
                        TextWrapping = TextWrapping.Wrap,
                        TextTrimming = TextTrimming.None
                    };
                    if (!IsShowTranslation) trans.Visibility = Visibility.Collapsed;
                    item.LrcTrans = trans;
                    tb.Inlines.Add(new LineBreak());
                    tb.Inlines.Add(trans);
                }
                LrcItems.Add(item);
                LyricPanel.Children.Add(box);
            }
            LyricPanel.Children.Add(new Border() { Height = 200, Background = Brushes.Transparent });
        }


        private static double? pixelsPerDip;
        private static string InsertLineBreaks(TextBlock tb, string preText, double fontSize, double maxWidth,FontWeight fontWeight)
        {
            var typeface = new Typeface(tb.FontFamily, tb.FontStyle, fontWeight, tb.FontStretch);
            pixelsPerDip ??= VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip;
            double GetWidth(string text)
            {
                var formattedLine = new FormattedText(text, CultureInfo.CurrentCulture,
                                                                                    FlowDirection.LeftToRight, typeface, fontSize, Brushes.Black, pixelsPerDip!.Value);
                return formattedLine.WidthIncludingTrailingWhitespace;
            }
            StringBuilder temp = new();
            string[] blocks;
            bool spaceSplit = preText.Contains(' ');
            if (spaceSplit)
                blocks = preText.Split(' ');
            else blocks = [.. preText.Select(c => c.ToString())];


            foreach (var block in blocks)
            {
                if (string.IsNullOrWhiteSpace(block)) continue;
                temp.Append(block);
                if (spaceSplit) temp.Append(' ');
                if (GetWidth(temp.ToString()) > maxWidth)
                {
                    int undoLength = block.Length + (spaceSplit ? 1 : 0);
                    temp.Remove(temp.Length - undoLength, undoLength);
                    temp.AppendLine();
                    temp.Append(block);
                    if (spaceSplit) temp.Append(' ');
                }
            }
            string result = temp.ToString();
            return result;
        }

        public void UpdateTime(double ms)
        {
            void reset(LrcItem? item)
            {
                if (item == null || item.LrcTb == null) return;
                item.LrcTb.FontWeight = NormalTextFontWeight;
                item.LrcTb.Effect = NomalTextEffect;
                item.LrcMain.Background=null;
                if(item.Box.Children.Count>1)
                item.Box.Children.RemoveAt(0);

                item.LrcTb.BeginAnimation(FontSizeProperty, null);
                item.LrcTb.BeginAnimation(OpacityProperty, null);
                item.LrcMain.TextWrapping = TextWrapping.Wrap;
                item.LrcMain.Text = item.Lyric;

                if (IsVisible)
                    RefreshCurrentLrcStyle();
            }
            var temp = LrcItems.LastOrDefault(p => p.Time <= ms);
            if (temp == null || temp == _currentLrc) return;

            var last = _currentLrc;
            _currentLrc = temp;
            reset(last);
            var next = LrcItems.FirstOrDefault(p => p.Time > temp.Time);
            OnNextLrcReached?.Invoke(ToLrcLine(_currentLrc)!,ToLrcLine(next));
        }

        private void RefreshCurrentLrcStyle()
        {
            if (_currentLrc == null) return;
            var container = _currentLrc.LrcTb!;
            //container.Foreground = HighlightLrcColor;
            container.FontWeight = FontWeights.Bold;
            container.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.2)));

            container.Effect = null;
            container.Background = Brushes.Transparent;
            var box = _currentLrc.Box;
            if(box.Children.Count==1)
            {
                box.Children.Insert(0, new Border()
                {
                    Background = new VisualBrush()
                    {
                        Visual = container,
                        Stretch = Stretch.None,
                        AlignmentX = AlignmentX.Left,
                        AlignmentY = AlignmentY.Top,
                        Opacity = 0.7
                    },
                    Effect = Hightlighter
                });
            }

            double targetFontsize = LyricFontSize + 8;
            var mainLine = _currentLrc.LrcMain!;
            mainLine.TextWrapping = TextWrapping.NoWrap;
            mainLine.Text = InsertLineBreaks(mainLine, _currentLrc.Lyric, targetFontsize, ActualWidth - LyricMargin.Left - LyricMargin.Right - 1, FontWeights.Bold);
            var da = new DoubleAnimation(targetFontsize, TimeSpan.FromSeconds(0.3))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            //Timeline.SetDesiredFrameRate(da, 60);
            ResetLrcviewScroll();
            container.BeginAnimation(FontSizeProperty, da);

            int index = LrcItems.IndexOf(_currentLrc);
            if (index < LrcItems.Count - 1)
            {
                var next = LrcItems[index + 1];
                next.LrcTb!.Effect = AroundTextEffect;
            }
        }

        private void ResetLrcviewScroll()
        {
            try
            {
                if (_currentLrc == null || _currentLrc.LrcTb == null) return;
                GeneralTransform gf = _currentLrc.LrcTb.TransformToVisual(LyricPanel);
                Point p = gf.Transform(new Point(0, 0));
                double os = p.Y - (scrollviewer.ActualHeight / 2) + 120;
                var da = new DoubleAnimation(os, TimeSpan.FromMilliseconds(500));
                da.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                //Timeline.SetDesiredFrameRate(da, 60);
                scrollviewer.BeginAnimation(ScrollViewerUtils.VerticalOffsetProperty, da);
            }
            catch { }
        }

        private void scrollviewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            e.Handled = true;
        }
    }
}
