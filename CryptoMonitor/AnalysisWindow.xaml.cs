using CryptoMonitor.Models;
using Microsoft.Win32;
using ScottPlot;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CryptoMonitor
{
    public partial class AnalysisWindow : Window
    {
        private List<PricePoint> _history;

        public AnalysisWindow(List<PricePoint> history)
        {
            InitializeComponent();
            _history = history;
            AnalysisTypeCombo.SelectionChanged += OnAnalysisChanged;
            
            // Ручной вызов первого обрисовывания
            DrawChart();
        }

        private void ApplyDarkStyle(ScottPlot.Plot plt)
        {
            var panelDark = ScottPlot.Color.FromHex("#1C2541");
            var bgDark = ScottPlot.Color.FromHex("#0B132B");
            var textMuted = ScottPlot.Color.FromHex("#8D99AE");
            var borderColor = ScottPlot.Color.FromHex("#2B3655");

            plt.FigureBackground.Color = panelDark;
            plt.DataBackground.Color = bgDark;
            plt.Axes.Color(textMuted);
            plt.Grid.MajorLineColor = borderColor;
        }

        private void OnAnalysisChanged(object sender, SelectionChangedEventArgs e)
        {
            DrawChart();
        }

        private void DrawChart()
        {
            if (_history == null || _history.Count == 0 || MainPlot == null) return;

            var plt = MainPlot.Plot;
            plt.Clear();

            double[] prices = _history.Select(x => x.Price).ToArray();
            double[] times = _history.Select(x => x.Time.ToOADate()).ToArray();

            int selectedIndex = AnalysisTypeCombo.SelectedIndex;

            if (selectedIndex == 0) // Цена + EMA
            {
                var pLine = plt.Add.Scatter(times, prices);
                pLine.Color = ScottPlot.Color.FromHex("#5BC0BE");
                pLine.LegendText = "Цена";

                var ema = ExponentialMovingAverage(prices, 14);
                var emaLine = plt.Add.Scatter(times, ema);
                emaLine.Color = ScottPlot.Color.FromHex("#FF9800");
                emaLine.LegendText = "EMA 14";

                plt.YLabel("Цена (USD)");
                plt.Axes.AutoScale();
            }
            else if (selectedIndex == 1) // Полосы Боллинджера
            {
                var (sma, upper, lower) = CalculateBollingerBands(prices, 20, 2);
                
                var fill = plt.Add.FillY(times, lower, upper);
                fill.FillColor = ScottPlot.Color.FromHex("#33FF9800");
                fill.LineWidth = 0;
                
                var smaLine = plt.Add.Scatter(times, sma);
                smaLine.Color = ScottPlot.Color.FromHex("#FF9800");
                smaLine.LinePattern = LinePattern.Dashed;
                smaLine.LegendText = "SMA 20";

                var pLine = plt.Add.Scatter(times, prices);
                pLine.Color = ScottPlot.Color.FromHex("#5BC0BE");
                pLine.LegendText = "Цена";

                plt.YLabel("Цена (USD)");
                plt.Axes.AutoScale();
            }
            else if (selectedIndex == 2) // RSI
            {
                var rsi = CalculateRSI(prices, 14);
                var rsiLine = plt.Add.Scatter(times, rsi);
                rsiLine.Color = ScottPlot.Color.FromHex("#9C27B0");
                rsiLine.LegendText = "RSI 14";
                
                var topZone = plt.Add.HorizontalLine(70);
                topZone.Color = ScottPlot.Color.FromHex("#F44336");
                topZone.LinePattern = LinePattern.Dashed;
                
                var botZone = plt.Add.HorizontalLine(30);
                botZone.Color = ScottPlot.Color.FromHex("#4CAF50");
                botZone.LinePattern = LinePattern.Dashed;

                plt.Axes.SetLimitsY(0, 100);
                plt.YLabel("Индекс RSI");
            }
            else if (selectedIndex == 3) // MACD
            {
                var (macd, signal, hist) = CalculateMACD(prices, 12, 26, 9);
                
                double[] zeros = new double[times.Length];
                var histFill = plt.Add.FillY(times, zeros, hist);
                histFill.FillColor = ScottPlot.Color.FromHex("#558D99AE");
                histFill.LineWidth = 0;
                
                var macdLine = plt.Add.Scatter(times, macd);
                macdLine.Color = ScottPlot.Color.FromHex("#2196F3");
                macdLine.LegendText = "MACD";

                var sigLine = plt.Add.Scatter(times, signal);
                sigLine.Color = ScottPlot.Color.FromHex("#FF9800");
                sigLine.LegendText = "Сигнал";

                plt.YLabel("MACD");
                plt.Axes.AutoScale();
            }

            if (AnalysisTypeCombo.SelectedItem is ComboBoxItem item)
            {
                plt.Title(item.Content?.ToString() ?? "Анализ");
            }

            plt.Axes.DateTimeTicksBottom();
            
            plt.ShowLegend();

            ApplyDarkStyle(plt);
            MainPlot.Refresh();
        }

        private void BtnExportPng_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "PNG Image|*.png", FileName = "ChartReport.png" };
            if (dlg.ShowDialog() == true)
            {
                MainPlot.Plot.SavePng(dlg.FileName, 1200, 800);
                MessageBox.Show("График успешно сохранен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "CSV File|*.csv", FileName = "CryptoAnalysisReport.csv" };
            if (dlg.ShowDialog() == true)
            {
                double[] prices = _history.Select(x => x.Price).ToArray();
                var ema = ExponentialMovingAverage(prices, 14);
                var rsi = CalculateRSI(prices, 14);
                var (_, upper, lower) = CalculateBollingerBands(prices, 20, 2);
                var (macd, signal, hist) = CalculateMACD(prices, 12, 26, 9);

                var sb = new StringBuilder();
                sb.AppendLine("Date;Price_USD;EMA_14;RSI_14;Bollinger_Up;Bollinger_Down;MACD;MACD_Signal;MACD_Hist");

                for (int i = 0; i < _history.Count; i++)
                {
                    // Делаем явное приведение культур, чтобы точки с запятой в числах не мешали, либо используем InvariantCulture
                    string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0:yyyy-MM-dd HH:mm};{1:F4};{2:F4};{3:F4};{4:F4};{5:F4};{6:F4};{7:F4};{8:F4}",
                        _history[i].Time, prices[i], ema[i], rsi[i], upper[i], lower[i], macd[i], signal[i], hist[i]);
                    sb.AppendLine(line);
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Данные (CSV) успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // --- Индикаторы ---
        private double[] ExponentialMovingAverage(double[] prices, int period)
        {
            double[] ema = new double[prices.Length];
            if (prices.Length == 0) return ema;

            double multiplier = 2.0 / (period + 1);
            ema[0] = prices[0];

            for (int i = 1; i < prices.Length; i++)
            {
                ema[i] = ((prices[i] - ema[i - 1]) * multiplier) + ema[i - 1];
            }
            return ema;
        }

        private double[] CalculateRSI(double[] prices, int period)
        {
            double[] rsi = new double[prices.Length];
            if (prices.Length <= period) return rsi;
            
            double gain = 0, loss = 0;

            for (int i = 1; i <= period; i++)
            {
                double change = prices[i] - prices[i - 1];
                if (change > 0) gain += change;
                else loss -= change;
            }

            gain /= period;
            loss /= period;

            for (int i = period + 1; i < prices.Length; i++)
            {
                double change = prices[i] - prices[i - 1];
                if (change > 0)
                {
                    gain = (gain * (period - 1) + change) / period;
                    loss = (loss * (period - 1)) / period;
                }
                else
                {
                    gain = (gain * (period - 1)) / period;
                    loss = (loss * (period - 1) - change) / period;
                }
                double rs = loss == 0 ? 0 : gain / loss;
                rsi[i] = 100 - (100 / (1 + rs));
            }
            return rsi;
        }

        private (double[] sma, double[] upper, double[] lower) CalculateBollingerBands(double[] prices, int period, double multiplier)
        {
            double[] sma = new double[prices.Length];
            double[] upper = new double[prices.Length];
            double[] lower = new double[prices.Length];

            for (int i = 0; i < prices.Length; i++)
            {
                if (i < period - 1)
                {
                    sma[i] = prices[i];
                    upper[i] = prices[i];
                    lower[i] = prices[i];
                    continue;
                }

                double sum = 0;
                for (int j = 0; j < period; j++)
                    sum += prices[i - j];
                
                double avg = sum / period;
                sma[i] = avg;

                double varianceSum = 0;
                for (int j = 0; j < period; j++)
                    varianceSum += Math.Pow(prices[i - j] - avg, 2);
                
                double stdDev = Math.Sqrt(varianceSum / period);
                
                upper[i] = avg + (multiplier * stdDev);
                lower[i] = avg - (multiplier * stdDev);
            }

            return (sma, upper, lower);
        }

        private (double[] macd, double[] signal, double[] hist) CalculateMACD(double[] prices, int shortPeriod, int longPeriod, int signalPeriod)
        {
            var shortEma = ExponentialMovingAverage(prices, shortPeriod);
            var longEma = ExponentialMovingAverage(prices, longPeriod);
            
            double[] macd = new double[prices.Length];
            for (int i = 0; i < prices.Length; i++)
                macd[i] = shortEma[i] - longEma[i];

            var signal = ExponentialMovingAverage(macd, signalPeriod);
            
            double[] hist = new double[prices.Length];
            for (int i = 0; i < prices.Length; i++)
                hist[i] = macd[i] - signal[i];

            return (macd, signal, hist);
        }
    }
}
