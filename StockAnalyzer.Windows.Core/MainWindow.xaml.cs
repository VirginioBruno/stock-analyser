using StockAnalyzer.Core;
using StockAnalyzer.Core.Domain;
using StockAnalyzer.Core.Services;
using StockAnalyzer.Windows.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace StockAnalyzer.Windows
{
    public partial class MainWindow : Window
    {
        private static string API_URL = "https://ps-async.fekberg.com/api/stocks";
        private Stopwatch stopwatch = new Stopwatch();
        CancellationTokenSource cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource = null;
                }

                BeforeLoadingStockData();

                var progress = new Progress<IEnumerable<StockPrice>>();
                progress.ProgressChanged += (_, stocks) =>
                {
                    StockProgress.Value += 1;
                    Notes.Text += $"Loaded {stocks.Count()} stocks for {stocks.First().Identifier}\n";
                };

                await SearchStocksByService(progress);
            }
            catch (Exception ex)
            {
                Notes.Text = ex.Message;
            }
            finally 
            {
                AfterLoadingStockData();
            }
        }

        private async Task SearchStocksByService(IProgress<IEnumerable<StockPrice>> progress)
        {
            var service = new StockService();
            var loadingTasks = new List<Task<IEnumerable<StockPrice>>>();

            foreach (var id in StockIdentifier.Text.Split(','))
            {
                var task = service.GetStockPricesFor(id.Replace(" ", ""), cancellationTokenSource.Token);

                task = task.ContinueWith(t => {

                    var result = t.Result;
                    progress?.Report(result);

                    Dispatcher.Invoke(() => {
                        Stocks.ItemsSource = result;
                    });

                    return result;
                });

                loadingTasks.Add(task);
            }

            await Task.WhenAll(loadingTasks);
        }

        private async Task<ObservableCollection<StockPrice>> GetStocksAsyncStream()
        {
            var service = new DiskStockStreamService();
            var data = new ObservableCollection<StockPrice>();

            var ids = StockIdentifier.Text.Split(',', ' ');

            var enumerator = service.GetAllStockPrices(cancellationTokenSource.Token);

            await foreach (var stock in enumerator
                .WithCancellation(cancellationTokenSource.Token))
            {
                if (ids.Contains(stock.Identifier, StringComparer.InvariantCultureIgnoreCase))
                    data.Add(stock);
            }

            return data;
        }

        private async Task<IEnumerable<StockPrice>>
            GetStocksFor(string identifier)
        {
            var service = new StockService();
            var data = await service.GetStockPricesFor(identifier,
                CancellationToken.None).ConfigureAwait(false);

            return data.Take(5);
        }

        private static Task<List<string>> SearchForStocks(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                using (var stream = new StreamReader(File.OpenRead("StockPrices_Small.csv")))
                {
                    var lines = new List<string>();

                    string line;
                    while ((line = await stream.ReadLineAsync()) != null)
                    {
                        if(cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        lines.Add(line);
                    }

                    return lines;
                }
            }, cancellationToken);
        }

        private async Task GetStocks()
        {
            try
            {
                var store = new DataStore();

                var responseTask = store.GetStockPrices(StockIdentifier.Text);

                Stocks.ItemsSource = await responseTask;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void BeforeLoadingStockData()
        {
            stopwatch.Restart();
            StockProgress.Visibility = Visibility.Visible;
            StockProgress.Value = 0;
            StockProgress.Maximum = StockIdentifier.Text.Split(' ', ',').Length;
            StockProgress.IsIndeterminate = false;
            Stocks.ItemsSource = null;
            Search.Content = "Cancel";
            cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Token.Register(() => Notes.Text = "Cancellation Requested");
            Notes.Text = "";
        }

        private void AfterLoadingStockData()
        {
            StocksStatus.Text = $"Loaded stocks for {StockIdentifier.Text} in {stopwatch.ElapsedMilliseconds}ms";
            StockProgress.Visibility = Visibility.Hidden;
            Search.Content = "Search";
            cancellationTokenSource = null;
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));

            e.Handled = true;
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
