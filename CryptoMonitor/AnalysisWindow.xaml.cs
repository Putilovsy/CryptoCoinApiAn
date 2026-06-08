using CryptoMonitor.Models;
using Microsoft.Win32;
using ScottPlot;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CryptoMonitor
{
    public partial class AnalysisWindow : Window
    {
        private List<PricePoint> _history;

        public AnalysisWindow(List<PricePoint> history)
        {
            // Установка лицензии QuestPDF (бесплатная версия для сообщества)
            QuestPDF.Settings.License = LicenseType.Community;

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

            if (selectedIndex == 0) // Объем торгов
            {
                double[] volumes = _history.Select(x => x.Volume).ToArray();
                double[] zeros = new double[times.Length];

                var fill = plt.Add.FillY(times, zeros, volumes);
                fill.FillColor = ScottPlot.Color.FromHex("#552196F3");
                fill.LineWidth = 0;
                
                var vLine = plt.Add.Scatter(times, volumes);
                vLine.Color = ScottPlot.Color.FromHex("#2196F3");
                vLine.LegendText = "Объем торгов";

                plt.YLabel("Объем (USD)");
                plt.Axes.AutoScale();
            }
            else if (selectedIndex == 1) // Цена + EMA + SMA
            {
                var pLine = plt.Add.Scatter(times, prices);
                pLine.Color = ScottPlot.Color.FromHex("#FFFFFF"); // Белый для контраста
                pLine.LegendText = "Цена";

                var ema = ExponentialMovingAverage(prices, 14);
                var emaLine = plt.Add.Scatter(times, ema);
                emaLine.Color = ScottPlot.Color.FromHex("#FF9800");
                emaLine.LegendText = "EMA 14";

                var sma = SimpleMovingAverage(prices, 14);
                var smaLine = plt.Add.Scatter(times, sma);
                smaLine.Color = ScottPlot.Color.FromHex("#9C27B0");
                smaLine.LegendText = "SMA 14";

                plt.YLabel("Цена (USD)");
                plt.Axes.AutoScale();
            }
            else if (selectedIndex == 2) // Полосы Боллинджера
            {
                var (sma, upper, lower) = CalculateBollingerBands(prices, 20, 2);
                
                var fill = plt.Add.FillY(times, lower, upper);
                fill.FillColor = ScottPlot.Color.FromHex("#339E9E9E"); // Прозрачный серый
                fill.LineWidth = 0;

                var upperLine = plt.Add.Scatter(times, upper);
                upperLine.Color = ScottPlot.Color.FromHex("#9E9E9E"); // Серый
                upperLine.LegendText = "Верхняя полоса";

                var lowerLine = plt.Add.Scatter(times, lower);
                lowerLine.Color = ScottPlot.Color.FromHex("#9E9E9E"); // Серый
                lowerLine.LegendText = "Нижняя полоса";
                
                var smaLine = plt.Add.Scatter(times, sma);
                smaLine.Color = ScottPlot.Color.FromHex("#FF9800");
                smaLine.LinePattern = LinePattern.Dashed;
                smaLine.LegendText = "SMA 20";

                var pLine = plt.Add.Scatter(times, prices);
                pLine.Color = ScottPlot.Color.FromHex("#FFFFFF"); // Белый для контраста
                pLine.LegendText = "Цена";

                plt.YLabel("Цена (USD)");
                plt.Axes.AutoScale();
            }
            else if (selectedIndex == 3) // RSI
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
            else if (selectedIndex == 4) // MACD
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
            else if (selectedIndex == 5) // Parabolic SAR
            {
                var sar = CalculateParabolicSAR(prices);

                var pLine = plt.Add.Scatter(times, prices);
                pLine.Color = ScottPlot.Color.FromHex("#FFFFFF"); // Белый для контраста
                pLine.LegendText = "Цена";

                // Разделяем SAR на восходящий (ниже цены) и нисходящий (выше цены) тренды
                var upTimes = new List<double>();
                var upSar = new List<double>();
                var downTimes = new List<double>();
                var downSar = new List<double>();

                for (int i = 0; i < prices.Length; i++)
                {
                    if (sar[i] <= prices[i]) { upTimes.Add(times[i]); upSar.Add(sar[i]); }
                    else { downTimes.Add(times[i]); downSar.Add(sar[i]); }
                }

                if (upTimes.Count > 0)
                {
                    var upDots = plt.Add.Scatter(upTimes.ToArray(), upSar.ToArray());
                    upDots.LineWidth = 0;
                    upDots.MarkerSize = 5;
                    upDots.Color = ScottPlot.Color.FromHex("#4CAF50");
                    upDots.LegendText = "SAR (↑ тренд)";
                }
                if (downTimes.Count > 0)
                {
                    var downDots = plt.Add.Scatter(downTimes.ToArray(), downSar.ToArray());
                    downDots.LineWidth = 0;
                    downDots.MarkerSize = 5;
                    downDots.Color = ScottPlot.Color.FromHex("#F44336");
                    downDots.LegendText = "SAR (↓ тренд)";
                }

                plt.YLabel("Цена (USD)");
                plt.Axes.AutoScale();
            }
            else if (selectedIndex == 6) // Стохастический осциллятор
            {
                var (stochK, stochD) = CalculateStochastic(prices, 14, 3);

                var kLine = plt.Add.Scatter(times, stochK);
                kLine.Color = ScottPlot.Color.FromHex("#2196F3");
                kLine.LegendText = "%K (14)";

                var dLine = plt.Add.Scatter(times, stochD);
                dLine.Color = ScottPlot.Color.FromHex("#FF9800");
                dLine.LinePattern = LinePattern.Dashed;
                dLine.LegendText = "%D (3)";

                var overbought = plt.Add.HorizontalLine(80);
                overbought.Color = ScottPlot.Color.FromHex("#F44336");
                overbought.LinePattern = LinePattern.Dashed;

                var oversold = plt.Add.HorizontalLine(20);
                oversold.Color = ScottPlot.Color.FromHex("#4CAF50");
                oversold.LinePattern = LinePattern.Dashed;

                plt.Axes.SetLimitsY(0, 100);
                plt.YLabel("Стохастик (%)");
            }
            else if (selectedIndex == 7) // Ichimoku Cloud
            {
                var (tenkan, kijun, spanA, spanB, chikou) = CalculateIchimoku(prices);

                // Отрисовка облака (заливка между Span A и Span B с разделением на цвета)
                double[] bullTop = new double[times.Length];
                double[] bullBot = new double[times.Length];
                double[] bearTop = new double[times.Length];
                double[] bearBot = new double[times.Length];

                for (int i = 0; i < times.Length; i++)
                {
                    if (spanA[i] >= spanB[i])
                    {
                        bullTop[i] = spanA[i];
                        bullBot[i] = spanB[i];
                        bearTop[i] = spanB[i];
                        bearBot[i] = spanB[i];
                    }
                    else
                    {
                        bullTop[i] = spanA[i];
                        bullBot[i] = spanA[i];
                        bearTop[i] = spanB[i];
                        bearBot[i] = spanA[i];
                    }
                }

                var bullCloud = plt.Add.FillY(times, bullBot, bullTop);
                bullCloud.FillColor = new ScottPlot.Color(76, 175, 80, 85); // Явно заданный Зеленый (R, G, B, Alpha)
                bullCloud.LineWidth = 0;

                var bearCloud = plt.Add.FillY(times, bearBot, bearTop);
                bearCloud.FillColor = new ScottPlot.Color(255, 51, 51, 85); // Явно заданный Красный (R, G, B, Alpha)
                bearCloud.LineWidth = 0;

                var tenkanLine = plt.Add.Scatter(times, tenkan);
                tenkanLine.Color = ScottPlot.Color.FromHex("#2196F3");
                tenkanLine.LegendText = "Tenkan (9)";

                var kijunLine = plt.Add.Scatter(times, kijun);
                kijunLine.Color = ScottPlot.Color.FromHex("#F44336");
                kijunLine.LegendText = "Kijun (26)";

                var chikouLine = plt.Add.Scatter(times, chikou);
                chikouLine.Color = ScottPlot.Color.FromHex("#66BB6A");
                chikouLine.LinePattern = LinePattern.Dashed;
                chikouLine.LegendText = "Chikou (26)";

                var pLine = plt.Add.Scatter(times, prices);
                pLine.Color = ScottPlot.Color.FromHex("#FFFFFF"); // Белый для контраста
                pLine.LegendText = "Цена";

                plt.YLabel("Цена (USD)");
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
                var sar = CalculateParabolicSAR(prices);
                var (stochK, stochD) = CalculateStochastic(prices, 14, 3);
                var (tenkan, kijun, spanA, spanB, chikou) = CalculateIchimoku(prices);

                var sb = new StringBuilder();
                sb.AppendLine("sep=,"); 
                sb.AppendLine("Date,Price_USD,EMA_14,RSI_14,Bollinger_Up,Bollinger_Down,MACD,MACD_Signal,MACD_Hist,Parabolic_SAR,Stoch_K,Stoch_D,Tenkan,Kijun,SpanA,SpanB");

                for (int i = 0; i < _history.Count; i++)
                {
                    string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0:yyyy-MM-dd HH:mm},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4},{10:F4},{11:F4},{12:F4},{13:F4},{14:F4},{15:F4}",
                        _history[i].Time, prices[i], ema[i], rsi[i], upper[i], lower[i], macd[i], signal[i], hist[i], sar[i], stochK[i], stochD[i], tenkan[i], kijun[i], spanA[i], spanB[i]);
                    sb.AppendLine(line);
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show("Данные (CSV) успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "PDF Document|*.pdf", FileName = "CryptoAnalysisReport.pdf" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // 1. Сохраняем текущий график во временный файл
                    string tempImagePath = Path.Combine(Path.GetTempPath(), $"chart_{Guid.NewGuid()}.png");
                    MainPlot.Plot.SavePng(tempImagePath, 1200, 800);

                    // 2. Создаем PDF документ
                    Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(1, Unit.Centimetre);
                            page.PageColor(QuestPDF.Helpers.Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(11).FontFamily(QuestPDF.Helpers.Fonts.Verdana));

                            // Заголовок
                            page.Header().Text("Отчёт по техническому анализу").FontSize(20).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Medium);

                            page.Content().PaddingVertical(10).Column(col =>
                            {
                                col.Spacing(10);
                                
                                // Инфо об индикаторе
                                if (AnalysisTypeCombo.SelectedItem is ComboBoxItem item)
                                {
                                    col.Item().Text($"Индикатор: {item.Content}").FontSize(14).Bold();
                                }

                                col.Item().Text($"Дата формирования: {DateTime.Now:yyyy-MM-dd HH:mm}");
                                
                                // График
                                col.Item().Image(tempImagePath);

                                col.Item().PaddingTop(10).Text("Краткое описание индикатора:").Bold();
                                col.Item().Text(GetIndicatorDescription());

                                // Таблица цен (последние 10 записей для примера)
                                col.Item().PaddingTop(20).Text("Последние данные:").Bold();
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(150);
                                        columns.RelativeColumn();
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().BorderBottom(1).Padding(5).Text("Дата и время");
                                        header.Cell().BorderBottom(1).Padding(5).Text("Цена (USD)");
                                    });

                                    foreach (var point in _history.Skip(Math.Max(0, _history.Count - 20)))
                                    {
                                        table.Cell().Padding(5).Text(point.Time.ToString("yyyy-MM-dd HH:mm"));
                                        table.Cell().Padding(5).Text(point.Price.ToString("F4"));
                                    }
                                });
                            });

                            page.Footer().AlignCenter().Text(x =>
                            {
                                x.Span("Стр. ");
                                x.CurrentPageNumber();
                            });
                        });
                    }).GeneratePdf(dlg.FileName);

                    // 3. Удаляем временный файл
                    if (File.Exists(tempImagePath)) File.Delete(tempImagePath);

                    MessageBox.Show("PDF-отчёт успешно сформирован!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при создании PDF: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string GetIndicatorDescription()
        {
            int idx = AnalysisTypeCombo.SelectedIndex;
            return idx switch
            {
                0 => "Объем торгов (Trading Volume) — показывает количество монет, перешедших из рук в руки за период.\n" +
                     "Высокий объем подтверждает силу текущего тренда.\n" +
                     "Падающий объем может сигнализировать об ослаблении интереса.",
                1 => "EMA и SMA (Exponential / Simple Moving Average) — скользящие средние.\n" +
                     "SMA показывает обычное среднее за период, а EMA придает больший вес последним данным.\n" +
                     "Разница между ними помогает оценить импульс: если EMA выше SMA, импульс растет.",
                2 => "Полосы Боллинджера — индикатор волатильности рынка.\n" +
                     "Сужение полос означает спокойный рынок, расширение — рост волатильности.\n" +
                     "Пробитие верхней полосы может сигнализировать о перекупленности,\nнижней — о перепроданности актива.",
                3 => "RSI (Relative Strength Index) — индекс относительной силы.\n" +
                     "Значение выше 70 — актив перекуплен (возможен разворот вниз).\n" +
                     "Значение ниже 30 — актив перепродан (возможен разворот вверх).\n" +
                     "Диапазон 30–70 считается нейтральной зоной.",
                4 => "MACD (Moving Average Convergence/Divergence) — схождение/расхождение скользящих средних.\n" +
                     "Пересечение линии MACD и сигнальной линии снизу вверх — сигнал к покупке.\n" +
                     "Пересечение сверху вниз — сигнал к продаже.\n" +
                     "Гистограмма показывает силу текущего тренда.",
                5 => "Parabolic SAR (Stop and Reverse) — параболическая система на разворот.\n" +
                     "Зелёные точки ниже цены — восходящий тренд (рекомендация: держать/покупать).\n" +
                     "Красные точки выше цены — нисходящий тренд (рекомендация: продавать).\n" +
                     "При пересечении ценой точек SAR происходит смена тренда.",
                6 => "Стохастический осциллятор — индикатор перекупленности/перепроданности.\n" +
                     "%K (синяя) — основная линия, %D (оранжевая) — её сигнальная SMA.\n" +
                     "Зона выше 80 — перекупленность (сигнал к продаже).\n" +
                     "Зона ниже 20 — перепроданность (сигнал к покупке).\n" +
                     "Пересечение %K и %D в этих зонах усиливает сигнал.",
                7 => "Облако Ишимоку (Ichimoku Cloud) — комплексный индикатор тренда.\n" +
                     "Tenkan-sen (синяя) — линия переворота.\n" +
                     "Kijun-sen (красная) — основная линия.\n" +
                     "Облако (Kumo) — пространство между Senkou Span A и B.\n" +
                     "Зеленое облако указывает на бычий тренд, красное — на медвежий.",
                _ => "Выберите индикатор для отображения подсказки."
            };
        }


        private void BtnIndicatorHelp_Click(object sender, RoutedEventArgs e)
        {
            if (HelpPopup != null && HelpPopupText != null)
            {
                HelpPopupText.Text = GetIndicatorDescription();
                HelpPopup.PlacementTarget = BtnIndicatorHelp;
                HelpPopup.IsOpen = !HelpPopup.IsOpen;
            }
        }

        // --- Индикаторы ---
        private double[] SimpleMovingAverage(double[] prices, int period)
        {
            double[] sma = new double[prices.Length];
            for (int i = 0; i < prices.Length; i++)
            {
                if (i < period - 1)
                {
                    sma[i] = prices[i];
                    continue;
                }
                double sum = 0;
                for (int j = 0; j < period; j++)
                    sum += prices[i - j];
                sma[i] = sum / period;
            }
            return sma;
        }

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

        private double[] CalculateParabolicSAR(double[] prices, double afStep = 0.02, double afMax = 0.2)
        {
            int n = prices.Length;
            double[] sar = new double[n];
            if (n < 2) return sar;

            // Начальное направление тренда по первым двум точкам
            bool isUpTrend = prices[1] >= prices[0];
            double af = afStep;
            double ep = isUpTrend ? prices[0] : prices[0]; // Extreme Point
            sar[0] = prices[0];
            sar[1] = prices[0];

            for (int i = 2; i < n; i++)
            {
                double prevSar = sar[i - 1];
                double newSar = prevSar + af * (ep - prevSar);

                if (isUpTrend)
                {
                    // SAR не должен быть выше двух предыдущих минимумов
                    newSar = Math.Min(newSar, prices[i - 2]);
                    newSar = Math.Min(newSar, prices[i - 1]);

                    if (prices[i] < newSar) // Разворот вниз
                    {
                        isUpTrend = false;
                        newSar = ep;
                        ep = prices[i];
                        af = afStep;
                    }
                    else
                    {
                        if (prices[i] > ep) // Новый максимум — ускоряем AF
                        {
                            ep = prices[i];
                            af = Math.Min(af + afStep, afMax);
                        }
                    }
                }
                else
                {
                    // SAR не должен быть ниже двух предыдущих максимумов
                    newSar = Math.Max(newSar, prices[i - 2]);
                    newSar = Math.Max(newSar, prices[i - 1]);

                    if (prices[i] > newSar) // Разворот вверх
                    {
                        isUpTrend = true;
                        newSar = ep;
                        ep = prices[i];
                        af = afStep;
                    }
                    else
                    {
                        if (prices[i] < ep) // Новый минимум — ускоряем AF
                        {
                            ep = prices[i];
                            af = Math.Min(af + afStep, afMax);
                        }
                    }
                }

                sar[i] = newSar;
            }

            return sar;
        }

        private (double[] k, double[] d) CalculateStochastic(double[] prices, int kPeriod = 14, int dPeriod = 3)
        {
            int n = prices.Length;
            double[] k = new double[n];
            double[] d = new double[n];

            for (int i = kPeriod - 1; i < n; i++)
            {
                double highest = double.MinValue;
                double lowest = double.MaxValue;

                for (int j = i - kPeriod + 1; j <= i; j++)
                {
                    if (prices[j] > highest) highest = prices[j];
                    if (prices[j] < lowest) lowest = prices[j];
                }

                double range = highest - lowest;
                k[i] = range == 0 ? 50.0 : (prices[i] - lowest) / range * 100.0;
            }

            // %D = dPeriod-периодная SMA от %K
            for (int i = kPeriod + dPeriod - 2; i < n; i++)
            {
                double sum = 0;
                for (int j = 0; j < dPeriod; j++)
                    sum += k[i - j];
                d[i] = sum / dPeriod;
            }

            return (k, d);
        }

        private (double[] tenkan, double[] kijun, double[] spanA, double[] spanB, double[] chikou) CalculateIchimoku(double[] prices)
        {
            int n = prices.Length;
            double[] tenkan = new double[n];
            double[] kijun = new double[n];
            double[] spanA = new double[n];
            double[] spanB = new double[n];
            double[] chikou = new double[n];

            for (int i = 0; i < n; i++)
            {
                tenkan[i] = CalculateMidPrice(prices, i, 9);
                kijun[i] = CalculateMidPrice(prices, i, 26);
                
                // Senkou Span A = (Tenkan + Kijun) / 2
                spanA[i] = (tenkan[i] + kijun[i]) / 2;
                
                // Senkou Span B = (52-period high + 52-period low) / 2
                spanB[i] = CalculateMidPrice(prices, i, 52);
                
                // Chikou Span = Close shifted back 26 periods
                if (i + 26 < n) chikou[i] = prices[i + 26];
                else chikou[i] = prices[i];
            }

            return (tenkan, kijun, spanA, spanB, chikou);
        }

        private double CalculateMidPrice(double[] prices, int index, int period)
        {
            if (index < period - 1) return prices[index];
            double high = double.MinValue;
            double low = double.MaxValue;
            for (int j = index - period + 1; j <= index; j++)
            {
                if (prices[j] > high) high = prices[j];
                if (prices[j] < low) low = prices[j];
            }
            return (high + low) / 2;
        }
    }
}
