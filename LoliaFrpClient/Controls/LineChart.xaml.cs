using LoliaFrpClient.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace LoliaFrpClient.Controls
{
    public sealed partial class LineChart : UserControl
    {
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(LineChart), new PropertyMetadata(string.Empty));

        public List<DailyTrafficViewModel> Data
        {
            get => (List<DailyTrafficViewModel>)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(List<DailyTrafficViewModel>), typeof(LineChart), new PropertyMetadata(null, OnDataChanged));

        private Popup? _tooltipPopup;
        private Border? _tooltipBorder;
        private TextBlock? _tooltipText;

        public LineChart()
        {
            this.InitializeComponent();
            this.SizeChanged += OnSizeChanged;
            CreateTooltip();
        }

        private void CreateTooltip()
        {
            _tooltipText = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                TextWrapping = TextWrapping.Wrap
            };

            _tooltipBorder = new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Child = _tooltipText
            };

            _tooltipPopup = new Popup
            {
                Child = _tooltipBorder
            };
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LineChart chart)
            {
                chart.DrawChart();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawChart();
        }

        private void DrawChart()
        {
            if (Data == null || Data.Count == 0 || ChartCanvas == null)
            {
                return;
            }

            ChartCanvas.Children.Clear();

            double canvasWidth = ChartCanvas.ActualWidth;
            double canvasHeight = ChartCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0)
            {
                return;
            }

            // 计算最大值用于缩放
            long maxValue = Data.Max(d => Math.Max(d.InboundBytes, d.OutboundBytes));
            if (maxValue == 0)
            {
                maxValue = 1;
            }

            // 绘制网格线
            DrawGridLines(canvasWidth, canvasHeight);

            // 绘制入站流量折线
            DrawLine(Data.Select(d => d.InboundBytes).ToList(), 
                     maxValue, 
                     canvasWidth, 
                     canvasHeight, 
                     new SolidColorBrush(Microsoft.UI.Colors.Blue), 
                     "入站");

            // 绘制出站流量折线
            DrawLine(Data.Select(d => d.OutboundBytes).ToList(), 
                     maxValue, 
                     canvasWidth, 
                     canvasHeight, 
                     new SolidColorBrush(Microsoft.UI.Colors.Green), 
                     "出站");

            // 绘制图例
            DrawLegend(canvasWidth);

            // 绘制可交互的数据点区域
            DrawInteractivePoints(maxValue, canvasWidth, canvasHeight);
        }

        private void DrawGridLines(double width, double height)
        {
            var gridColor = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200));
            
            // 绘制水平网格线
            for (int i = 0; i <= 4; i++)
            {
                double y = height - (height * i / 4);
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = gridColor,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                };
                ChartCanvas.Children.Add(line);
            }

            // 绘制垂直网格线
            int dataCount = Data.Count;
            if (dataCount > 1)
            {
                for (int i = 0; i < dataCount; i++)
                {
                    double x = width * i / (dataCount - 1);
                    var line = new Line
                    {
                        X1 = x,
                        Y1 = 0,
                        X2 = x,
                        Y2 = height,
                        Stroke = gridColor,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 4, 4 }
                    };
                    ChartCanvas.Children.Add(line);

                    // 添加日期标签
                    var textBlock = new TextBlock
                    {
                        Text = Data[i].Date,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 120, 120))
                    };
                    
                    Canvas.SetLeft(textBlock, x - 20);
                    Canvas.SetTop(textBlock, height + 5);
                    ChartCanvas.Children.Add(textBlock);
                }
            }
        }

        private void DrawLine(List<long> values, long maxValue, double width, double height, Brush strokeBrush, string label)
        {
            if (values.Count < 2)
            {
                return;
            }

            var polyline = new Polyline
            {
                Stroke = strokeBrush,
                StrokeThickness = 2,
                Points = new PointCollection()
            };

            for (int i = 0; i < values.Count; i++)
            {
                double x = width * i / (values.Count - 1);
                double y = height - (height * values[i] / maxValue);
                polyline.Points.Add(new Windows.Foundation.Point(x, y));

                // 绘制数据点
                var ellipse = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = strokeBrush,
                    Stroke = (SolidColorBrush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(ellipse, x - 3);
                Canvas.SetTop(ellipse, y - 3);
                ChartCanvas.Children.Add(ellipse);
            }

            ChartCanvas.Children.Add(polyline);
        }

        private void DrawInteractivePoints(long maxValue, double width, double height)
        {
            int dataCount = Data.Count;
            if (dataCount < 1) return;

            for (int i = 0; i < dataCount; i++)
            {
                double x = width * i / (Math.Max(1, dataCount - 1));
                var data = Data[i];

                // 创建一个透明的可点击区域
                var hitArea = new Rectangle
                {
                    Width = width / Math.Max(1, dataCount - 1),
                    Height = height + 30, // 包含日期标签区域
                    Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Tag = i // 存储数据索引
                };

                Canvas.SetLeft(hitArea, x - (width / Math.Max(1, dataCount - 1)) / 2);
                Canvas.SetTop(hitArea, 0);

                hitArea.PointerEntered += OnPointPointerEntered;
                hitArea.PointerExited += OnPointPointerExited;

                ChartCanvas.Children.Add(hitArea);
            }
        }

        private void OnPointPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Rectangle rect && rect.Tag is int index && index >= 0 && index < Data.Count)
            {
                var data = Data[index];
                var position = e.GetCurrentPoint(ChartCanvas).Position;

                _tooltipText!.Text = $"{data.Date}\n入站: {data.FormattedInbound}\n出站: {data.FormattedOutbound}";
                
                // 确保 Popup 有 XamlRoot
                if (_tooltipPopup!.XamlRoot == null)
                {
                    _tooltipPopup.XamlRoot = this.XamlRoot;
                }
                
                // 计算 Tooltip 位置
                double popupX = position.X + 10;
                double popupY = position.Y - 60;

                // 确保 Tooltip 不超出画布边界
                _tooltipBorder!.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                double tooltipWidth = _tooltipBorder.DesiredSize.Width;
                double tooltipHeight = _tooltipBorder.DesiredSize.Height;

                if (popupX + tooltipWidth > ChartCanvas.ActualWidth)
                {
                    popupX = position.X - tooltipWidth - 10;
                }
                if (popupY < 0)
                {
                    popupY = position.Y + 20;
                }

                // 转换为屏幕坐标
                var transform = ChartCanvas.TransformToVisual(this.XamlRoot.Content);
                var screenPoint = transform.TransformPoint(new Windows.Foundation.Point(popupX, popupY));

                _tooltipPopup.HorizontalOffset = screenPoint.X;
                _tooltipPopup.VerticalOffset = screenPoint.Y;
                _tooltipPopup.IsOpen = true;
            }
        }

        private void OnPointPointerExited(object sender, PointerRoutedEventArgs e)
        {
            _tooltipPopup!.IsOpen = false;
        }

        private void DrawLegend(double width)
        {
            // 入站图例
            var inboundLegend = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };
            
            var inboundEllipse = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.Blue)
            };
            
            var inboundText = new TextBlock
            {
                Text = "入站",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            inboundLegend.Children.Add(inboundEllipse);
            inboundLegend.Children.Add(inboundText);
            
            Canvas.SetLeft(inboundLegend, 20);
            Canvas.SetTop(inboundLegend, 10);
            ChartCanvas.Children.Add(inboundLegend);

            // 出站图例
            var outboundLegend = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };
            
            var outboundEllipse = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.Green)
            };
            
            var outboundText = new TextBlock
            {
                Text = "出站",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            outboundLegend.Children.Add(outboundEllipse);
            outboundLegend.Children.Add(outboundText);
            
            Canvas.SetLeft(outboundLegend, 80);
            Canvas.SetTop(outboundLegend, 10);
            ChartCanvas.Children.Add(outboundLegend);
        }
    }
}
